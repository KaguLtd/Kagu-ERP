using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Accounting.Domain.Currencies;
using KaguERP.Modules.Accounting.Domain.Journals;
using Npgsql;

namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

public static class PostgresJournalSourceReservationWriter
{
    public static async ValueTask<JournalSourceReservationResult> ReserveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid reservationId,
        ValidatedJournalDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(draft);

        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }

        if (reservationId == Guid.Empty)
        {
            throw new ArgumentException("Reservation ID cannot be empty.", nameof(reservationId));
        }

        scope.EnsureAllowed(draft.TenantId, draft.CompanyId);
        string draftHash = JournalDraftFingerprintV1.Compute(draft);

        const string insertSql = """
            INSERT INTO accounting.journal_source_reservation
                (reservation_id, tenant_id, company_id, source_type, source_event_id,
                 posting_purpose, journal_draft_hash, reserved_by)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
            ON CONFLICT (tenant_id, company_id, source_type, source_event_id, posting_purpose)
            DO NOTHING
            RETURNING reservation_id
            """;
        await using (var command = new NpgsqlCommand(insertSql, connection, transaction))
        {
            command.Parameters.AddWithValue(reservationId);
            command.Parameters.AddWithValue(draft.TenantId);
            command.Parameters.AddWithValue(draft.CompanyId);
            command.Parameters.AddWithValue(draft.SourceType);
            command.Parameters.AddWithValue(draft.SourceEventId);
            command.Parameters.AddWithValue(draft.PostingPurpose);
            command.Parameters.AddWithValue(draftHash);
            command.Parameters.AddWithValue(scope.ActorId);
            object? inserted = await command.ExecuteScalarAsync(cancellationToken);
            if (inserted is Guid insertedId)
            {
                return new JournalSourceReservationResult(insertedId, true, draftHash);
            }
        }

        const string existingSql = """
            SELECT reservation_id, journal_draft_hash
            FROM accounting.journal_source_reservation
            WHERE tenant_id = $1
              AND company_id = $2
              AND source_type = $3
              AND source_event_id = $4
              AND posting_purpose = $5
            """;
        Guid existingReservationId;
        string existingHash;
        await using (var command = new NpgsqlCommand(existingSql, connection, transaction))
        {
            command.Parameters.AddWithValue(draft.TenantId);
            command.Parameters.AddWithValue(draft.CompanyId);
            command.Parameters.AddWithValue(draft.SourceType);
            command.Parameters.AddWithValue(draft.SourceEventId);
            command.Parameters.AddWithValue(draft.PostingPurpose);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("The conflicting journal source reservation is not visible in the active scope.");
            }

            existingReservationId = reader.GetGuid(0);
            existingHash = reader.GetString(1);
        }

        if (!string.Equals(existingHash, draftHash, StringComparison.Ordinal))
        {
            throw new JournalSourceReservationConflictException(existingReservationId);
        }

        return new JournalSourceReservationResult(existingReservationId, false, existingHash);
    }
}

internal static class JournalDraftFingerprintV1
{
    public static string Compute(ValidatedJournalDraft draft)
    {
        var canonical = new StringBuilder();
        Append(canonical, "KAGU-JOURNAL-DRAFT-V1");
        Append(canonical, draft.TenantId);
        Append(canonical, draft.CompanyId);
        Append(canonical, draft.SourceType);
        Append(canonical, draft.SourceEventId);
        Append(canonical, draft.PostingPurpose);
        Append(canonical, draft.PostingRuleVersionId);
        Append(canonical, draft.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Append(canonical, draft.RecordedAt.ToString("O", CultureInfo.InvariantCulture));
        Append(canonical, draft.FunctionalCurrency.Value);

        string[] lines = draft.Lines.Select(CreateLineFingerprint).Order(StringComparer.Ordinal).ToArray();
        Append(canonical, lines.Length.ToString(CultureInfo.InvariantCulture));
        foreach (string line in lines)
        {
            Append(canonical, line);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string CreateLineFingerprint(JournalLineDraft line)
    {
        var canonical = new StringBuilder();
        Append(canonical, line.AccountId);
        Append(canonical, line.SourceLineId?.ToString("D") ?? "null");
        Append(canonical, Decimal(line.Amount.Debit));
        Append(canonical, Decimal(line.Amount.Credit));
        Append(canonical, line.Dimensions.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var dimension in line.Dimensions.OrderBy(item => item.DimensionId))
        {
            Append(canonical, dimension.DimensionId);
            Append(canonical, dimension.DimensionValueId);
        }

        if (line.CurrencyAmount is null)
        {
            Append(canonical, "no-currency-snapshot");
            return canonical.ToString();
        }

        Append(canonical, "currency-snapshot-v1");
        AppendCurrency(canonical, line.CurrencyAmount);
        return canonical.ToString();
    }

    private static void AppendCurrency(StringBuilder canonical, JournalCurrencyAmountSnapshot snapshot)
    {
        var rate = snapshot.ExchangeRate;
        Append(canonical, rate.TenantId);
        Append(canonical, rate.CompanyId);
        Append(canonical, rate.RateSnapshotId);
        Append(canonical, rate.Version.ToString(CultureInfo.InvariantCulture));
        Append(canonical, rate.TransactionCurrency.Value);
        Append(canonical, rate.FunctionalCurrency.Value);
        Append(canonical, rate.RateType);
        Append(canonical, rate.Source);
        Append(canonical, rate.RateDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Append(canonical, Decimal(rate.FunctionalUnitsNumerator));
        Append(canonical, Decimal(rate.TransactionUnitsDenominator));

        var rounding = snapshot.RoundingPolicy;
        Append(canonical, rounding.TenantId);
        Append(canonical, rounding.CompanyId);
        Append(canonical, rounding.PolicyId);
        Append(canonical, rounding.Version.ToString(CultureInfo.InvariantCulture));
        Append(canonical, rounding.Scale.ToString(CultureInfo.InvariantCulture));
        Append(canonical, ((int)rounding.Mode).ToString(CultureInfo.InvariantCulture));
        Append(canonical, Decimal(snapshot.TransactionAmount.Debit));
        Append(canonical, Decimal(snapshot.TransactionAmount.Credit));
        Append(canonical, Decimal(snapshot.FunctionalAmount.Debit));
        Append(canonical, Decimal(snapshot.FunctionalAmount.Credit));
        Append(canonical, Decimal(snapshot.UnroundedFunctionalAmount));
        Append(canonical, Decimal(snapshot.RoundingDifference));
    }

    private static string Decimal(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);

    private static void Append(StringBuilder target, Guid value) => Append(target, value.ToString("D"));

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
        target.Append(';');
    }
}
