using System.Text.Json;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Accounting.Domain.Currencies;
using KaguERP.Modules.Accounting.Domain.Journals;
using Npgsql;
using NpgsqlTypes;

namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

public static class PostgresValidatedJournalDraftWriter
{
    private const decimal Numeric20Scale4Maximum = 9999999999999999.9999m;

    public static async ValueTask<ValidatedJournalDraftPersistenceResult> PersistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid journalDraftId,
        JournalSourceReservationResult reservation,
        ValidatedJournalDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(draft);

        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }

        if (journalDraftId == Guid.Empty)
        {
            throw new ArgumentException("Journal draft ID cannot be empty.", nameof(journalDraftId));
        }

        if (reservation.ReservationId == Guid.Empty || string.IsNullOrWhiteSpace(reservation.DraftHash))
        {
            throw new ArgumentException("A valid journal source reservation result is required.", nameof(reservation));
        }

        scope.EnsureAllowed(draft.TenantId, draft.CompanyId);
        string computedHash = JournalDraftFingerprintV1.Compute(draft);
        if (!string.Equals(reservation.DraftHash, computedHash, StringComparison.Ordinal))
        {
            throw new ArgumentException("The reservation fingerprint does not match the validated journal draft.", nameof(reservation));
        }

        ValidateAmounts(draft);

        const string insertHeaderSql = """
            INSERT INTO accounting.validated_journal_draft
                (journal_draft_id, reservation_id, tenant_id, company_id, posting_rule_version_id,
                 effective_date, recorded_at, functional_currency, draft_hash, total_debit,
                 total_credit, line_count, persisted_by)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13)
            ON CONFLICT (reservation_id) DO NOTHING
            RETURNING journal_draft_id
            """;
        await using (var command = new NpgsqlCommand(insertHeaderSql, connection, transaction))
        {
            command.Parameters.AddWithValue(journalDraftId);
            command.Parameters.AddWithValue(reservation.ReservationId);
            command.Parameters.AddWithValue(draft.TenantId);
            command.Parameters.AddWithValue(draft.CompanyId);
            command.Parameters.AddWithValue(draft.PostingRuleVersionId);
            command.Parameters.AddWithValue(draft.EffectiveDate);
            command.Parameters.AddWithValue(draft.RecordedAt);
            command.Parameters.AddWithValue(draft.FunctionalCurrency.Value);
            command.Parameters.AddWithValue(computedHash);
            command.Parameters.AddWithValue(draft.TotalDebit);
            command.Parameters.AddWithValue(draft.TotalCredit);
            command.Parameters.AddWithValue(draft.Lines.Count);
            command.Parameters.AddWithValue(scope.ActorId);
            object? inserted = await command.ExecuteScalarAsync(cancellationToken);
            if (inserted is Guid insertedId)
            {
                await InsertLinesAsync(connection, transaction, insertedId, draft, cancellationToken);
                return new ValidatedJournalDraftPersistenceResult(insertedId, true, computedHash);
            }
        }

        const string existingSql = """
            SELECT journal_draft_id, draft_hash, line_count
            FROM accounting.validated_journal_draft
            WHERE reservation_id = $1
              AND tenant_id = $2
              AND company_id = $3
            """;
        await using var existingCommand = new NpgsqlCommand(existingSql, connection, transaction);
        existingCommand.Parameters.AddWithValue(reservation.ReservationId);
        existingCommand.Parameters.AddWithValue(draft.TenantId);
        existingCommand.Parameters.AddWithValue(draft.CompanyId);
        await using NpgsqlDataReader reader = await existingCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("The conflicting validated journal draft is not visible in the active scope.");
        }

        Guid existingId = reader.GetGuid(0);
        string existingHash = reader.GetString(1);
        int existingLineCount = reader.GetInt32(2);
        if (!string.Equals(existingHash, computedHash, StringComparison.Ordinal) || existingLineCount != draft.Lines.Count)
        {
            throw new ValidatedJournalDraftPersistenceConflictException(existingId);
        }

        return new ValidatedJournalDraftPersistenceResult(existingId, false, existingHash);
    }

    private static async Task InsertLinesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid journalDraftId,
        ValidatedJournalDraft draft,
        CancellationToken cancellationToken)
    {
        const string insertLineSql = """
            INSERT INTO accounting.validated_journal_line
                (journal_draft_id, tenant_id, company_id, line_number, account_id, source_line_id,
                 debit, credit, dimensions, currency_snapshot)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
            """;
        for (var index = 0; index < draft.Lines.Count; index++)
        {
            JournalLineDraft line = draft.Lines[index];
            await using var command = new NpgsqlCommand(insertLineSql, connection, transaction);
            command.Parameters.AddWithValue(journalDraftId);
            command.Parameters.AddWithValue(draft.TenantId);
            command.Parameters.AddWithValue(draft.CompanyId);
            command.Parameters.AddWithValue(index + 1);
            command.Parameters.AddWithValue(line.AccountId);
            command.Parameters.AddWithValue((object?)line.SourceLineId ?? DBNull.Value);
            command.Parameters.AddWithValue(line.Amount.Debit);
            command.Parameters.AddWithValue(line.Amount.Credit);
            command.Parameters.AddWithValue(NpgsqlDbType.Jsonb, SerializeDimensions(line));
            command.Parameters.AddWithValue(
                NpgsqlDbType.Jsonb,
                line.CurrencyAmount is null ? DBNull.Value : SerializeCurrencySnapshot(line.CurrencyAmount));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static string SerializeDimensions(JournalLineDraft line) => JsonSerializer.Serialize(
        line.Dimensions.Select(item => new { dimensionId = item.DimensionId, dimensionValueId = item.DimensionValueId }));

    private static string SerializeCurrencySnapshot(JournalCurrencyAmountSnapshot snapshot) => JsonSerializer.Serialize(new
    {
        exchangeRate = new
        {
            snapshot.ExchangeRate.TenantId,
            snapshot.ExchangeRate.CompanyId,
            snapshot.ExchangeRate.RateSnapshotId,
            snapshot.ExchangeRate.Version,
            transactionCurrency = snapshot.ExchangeRate.TransactionCurrency.Value,
            functionalCurrency = snapshot.ExchangeRate.FunctionalCurrency.Value,
            snapshot.ExchangeRate.RateType,
            snapshot.ExchangeRate.Source,
            snapshot.ExchangeRate.RateDate,
            snapshot.ExchangeRate.FunctionalUnitsNumerator,
            snapshot.ExchangeRate.TransactionUnitsDenominator,
        },
        roundingPolicy = new
        {
            snapshot.RoundingPolicy.TenantId,
            snapshot.RoundingPolicy.CompanyId,
            snapshot.RoundingPolicy.PolicyId,
            snapshot.RoundingPolicy.Version,
            snapshot.RoundingPolicy.Scale,
            mode = snapshot.RoundingPolicy.Mode.ToString(),
        },
        snapshot.TransactionAmount,
        snapshot.FunctionalAmount,
        snapshot.UnroundedFunctionalAmount,
        snapshot.RoundingDifference,
    });

    private static void ValidateAmounts(ValidatedJournalDraft draft)
    {
        ValidateAmount(draft.TotalDebit, nameof(draft.TotalDebit));
        ValidateAmount(draft.TotalCredit, nameof(draft.TotalCredit));
        foreach (JournalLineDraft line in draft.Lines)
        {
            ValidateAmount(line.Amount.Debit, "line debit");
            ValidateAmount(line.Amount.Credit, "line credit");
        }
    }

    private static void ValidateAmount(decimal value, string field)
    {
        if (decimal.Abs(value) > Numeric20Scale4Maximum || decimal.Round(value, 4) != value)
        {
            throw new ArgumentOutOfRangeException(
                field,
                value,
                "Journal amounts must fit PostgreSQL numeric(20,4) exactly without rounding.");
        }
    }
}
