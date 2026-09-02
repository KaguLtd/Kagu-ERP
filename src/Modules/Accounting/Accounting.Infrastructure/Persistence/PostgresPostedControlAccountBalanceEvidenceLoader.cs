using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KaguERP.BuildingBlocks.Application.Security;
using Npgsql;

namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

public sealed record PostedControlAccountLineageReference(
    Guid JournalId,
    string SourceType,
    Guid SourceEventId,
    long SourceVersion,
    string PostingPurpose,
    DateOnly EffectiveDate,
    DateTimeOffset RecordedAt,
    DateTimeOffset PostedAt);

public sealed record PostedControlAccountBalanceEvidence(
    Guid TenantId,
    Guid CompanyId,
    Guid ControlAccountId,
    string Currency,
    DateOnly EffectiveAsOf,
    DateTimeOffset RecordedCutoff,
    decimal OpeningBalance,
    decimal Debits,
    decimal Credits,
    decimal ClosingBalance,
    long RowCount,
    string SourceChecksumSha256);

public static class PostgresPostedControlAccountBalanceEvidenceLoader
{
    public static async ValueTask<PostedControlAccountBalanceEvidence> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        Guid controlAccountId,
        string currency,
        DateOnly effectiveAsOf,
        DateTimeOffset recordedCutoff,
        IEnumerable<PostedControlAccountLineageReference?>? postingLineage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (companyId == Guid.Empty || controlAccountId == Guid.Empty)
        {
            throw new ArgumentException("Company and control-account IDs are required.");
        }
        if (currency is null || currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z'))
        {
            throw new ArgumentException("Currency must contain three uppercase ASCII letters.", nameof(currency));
        }
        if (effectiveAsOf == default)
        {
            throw new ArgumentException("Effective as-of date is required.", nameof(effectiveAsOf));
        }
        if (recordedCutoff.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Recorded cutoff must use the UTC offset.", nameof(recordedCutoff));
        }
        ArgumentNullException.ThrowIfNull(postingLineage);
        PostedControlAccountLineageReference?[] copied = postingLineage.ToArray();
        if (copied.Any(item => item is null))
        {
            throw new ArgumentException("Posting lineage cannot contain null.", nameof(postingLineage));
        }
        PostedControlAccountLineageReference[] expected = copied
            .Cast<PostedControlAccountLineageReference>()
            .ToArray();
        if (expected.Select(item => item.JournalId).Distinct().Count() != expected.Length)
        {
            throw new ArgumentException("Posting journal IDs must be unique.", nameof(postingLineage));
        }

        scope.EnsureAllowed(scope.TenantId, companyId);
        var rows = new List<ControlAccountRow>();
        if (expected.Length > 0)
        {
            const string sql = """
                SELECT journal.journal_id, journal.source_type, journal.source_event_id,
                       journal.source_version, journal.posting_purpose, journal.effective_date,
                       journal.recorded_at, journal.posted_at, journal.functional_currency,
                       line.line_number, line.source_line_id,
                       CASE
                           WHEN journal.functional_currency = $7 THEN line.debit
                           WHEN line.currency_snapshot #>> '{exchangeRate,transactionCurrency}' = $7
                               THEN (line.currency_snapshot #>> '{TransactionAmount,Debit}')::numeric
                           ELSE NULL
                       END,
                       CASE
                           WHEN journal.functional_currency = $7 THEN line.credit
                           WHEN line.currency_snapshot #>> '{exchangeRate,transactionCurrency}' = $7
                               THEN (line.currency_snapshot #>> '{TransactionAmount,Credit}')::numeric
                           ELSE NULL
                       END,
                       line.dimensions::text, line.currency_snapshot::text
                FROM accounting.posted_journal journal
                JOIN accounting.posted_journal_line line
                  ON line.tenant_id = journal.tenant_id
                 AND line.company_id = journal.company_id
                 AND line.journal_id = journal.journal_id
                 AND line.account_id = $4
                WHERE journal.tenant_id = $1 AND journal.company_id = $2
                  AND journal.journal_id = ANY($3)
                  AND journal.effective_date <= $5
                  AND journal.recorded_at <= $6
                  AND journal.posted_at <= $6
                  AND NOT EXISTS (
                      SELECT 1
                      FROM accounting.posted_journal_reversal reversal_link
                      JOIN accounting.posted_journal reversal_journal
                        ON reversal_journal.tenant_id = reversal_link.tenant_id
                       AND reversal_journal.company_id = reversal_link.company_id
                       AND reversal_journal.journal_id = reversal_link.reversal_journal_id
                      WHERE reversal_link.tenant_id = journal.tenant_id
                        AND reversal_link.company_id = journal.company_id
                        AND reversal_link.original_journal_id = journal.journal_id
                        AND reversal_link.linked_at <= $6
                        AND reversal_journal.effective_date <= $5
                        AND reversal_journal.recorded_at <= $6
                        AND reversal_journal.posted_at <= $6)
                ORDER BY journal.journal_id, line.line_number
                """;
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue(scope.TenantId);
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(expected.Select(item => item.JournalId).ToArray());
            command.Parameters.AddWithValue(controlAccountId);
            command.Parameters.AddWithValue(effectiveAsOf);
            command.Parameters.AddWithValue(recordedCutoff);
            command.Parameters.AddWithValue(currency);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(11) || reader.IsDBNull(12))
                {
                    throw new PostedControlAccountEvidenceException(
                        "POSTED_CONTROL_ACCOUNT_CURRENCY_EVIDENCE_MISMATCH",
                        "A GL control-account line cannot be reproduced in the Party report currency.");
                }
                rows.Add(new ControlAccountRow(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetGuid(2),
                    reader.GetInt64(3),
                    reader.GetString(4),
                    reader.GetFieldValue<DateOnly>(5),
                    reader.GetFieldValue<DateTimeOffset>(6),
                    reader.GetFieldValue<DateTimeOffset>(7),
                    reader.GetString(8),
                    reader.GetInt32(9),
                    reader.IsDBNull(10) ? null : reader.GetGuid(10),
                    reader.GetDecimal(11),
                    reader.GetDecimal(12),
                    reader.GetString(13),
                    reader.IsDBNull(14) ? null : reader.GetString(14)));
            }
        }

        EnsureExactLineage(expected, rows);
        decimal debits = decimal.Zero;
        decimal credits = decimal.Zero;
        foreach (ControlAccountRow row in rows)
        {
            checked
            {
                debits += row.Debit;
                credits += row.Credit;
            }
        }
        decimal closing;
        checked
        {
            closing = debits - credits;
        }
        return new PostedControlAccountBalanceEvidence(
            scope.TenantId,
            companyId,
            controlAccountId,
            currency,
            effectiveAsOf,
            recordedCutoff,
            decimal.Zero,
            debits,
            credits,
            closing,
            rows.Count,
            ComputeChecksum(rows));
    }

    private static void EnsureExactLineage(
        IReadOnlyCollection<PostedControlAccountLineageReference> expected,
        IReadOnlyCollection<ControlAccountRow> rows)
    {
        var expectedByJournal = expected.ToDictionary(item => item.JournalId);
        if (rows.Select(item => item.JournalId).Distinct().Count() != expectedByJournal.Count)
        {
            throw new PostedControlAccountEvidenceException(
                "POSTED_CONTROL_ACCOUNT_LINEAGE_INCOMPLETE",
                "Every active Party posting must contribute a line to the selected GL control account.");
        }
        foreach (IGrouping<Guid, ControlAccountRow> journalRows in rows.GroupBy(item => item.JournalId))
        {
            if (!expectedByJournal.TryGetValue(journalRows.Key, out PostedControlAccountLineageReference? lineage))
            {
                throw new PostedControlAccountEvidenceException(
                    "POSTED_CONTROL_ACCOUNT_LINEAGE_UNEXPECTED",
                    "The GL control-account query returned an unexpected journal.");
            }
            foreach (ControlAccountRow row in journalRows)
            {
                if (!string.Equals(row.SourceType, lineage.SourceType, StringComparison.Ordinal) ||
                    row.SourceEventId != lineage.SourceEventId || row.SourceVersion != lineage.SourceVersion ||
                    !string.Equals(row.PostingPurpose, lineage.PostingPurpose, StringComparison.Ordinal) ||
                    row.EffectiveDate != lineage.EffectiveDate || row.RecordedAt != lineage.RecordedAt ||
                    row.PostedAt != lineage.PostedAt)
                {
                    throw new PostedControlAccountEvidenceException(
                        "POSTED_CONTROL_ACCOUNT_LINEAGE_MISMATCH",
                        "GL control-account evidence does not match the authoritative Party posting lineage.");
                }
            }
        }
    }

    private static string ComputeChecksum(IEnumerable<ControlAccountRow> rows)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AddFramed(hash, "posted-control-account-balance-v1");
        foreach (ControlAccountRow row in rows.OrderBy(item => item.JournalId).ThenBy(item => item.LineNumber))
        {
            AddFramed(hash, row.JournalId.ToString("N"));
            AddFramed(hash, row.LineNumber.ToString(CultureInfo.InvariantCulture));
            AddFramed(hash, row.SourceLineId?.ToString("N") ?? string.Empty);
            AddFramed(hash, row.FunctionalCurrency);
            AddFramed(hash, row.Debit.ToString("G29", CultureInfo.InvariantCulture));
            AddFramed(hash, row.Credit.ToString("G29", CultureInfo.InvariantCulture));
            AddFramed(hash, row.Dimensions);
            AddFramed(hash, row.CurrencySnapshot ?? string.Empty);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AddFramed(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(Encoding.ASCII.GetBytes(bytes.Length.ToString(CultureInfo.InvariantCulture)));
        hash.AppendData(":"u8);
        hash.AppendData(bytes);
    }

    private sealed record ControlAccountRow(
        Guid JournalId,
        string SourceType,
        Guid SourceEventId,
        long SourceVersion,
        string PostingPurpose,
        DateOnly EffectiveDate,
        DateTimeOffset RecordedAt,
        DateTimeOffset PostedAt,
        string FunctionalCurrency,
        int LineNumber,
        Guid? SourceLineId,
        decimal Debit,
        decimal Credit,
        string Dimensions,
        string? CurrencySnapshot);
}

public sealed class PostedControlAccountEvidenceException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
