using KaguERP.BuildingBlocks.Application.Security;
using Npgsql;

namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

public sealed record PostedSourceEvidence(
    Guid JournalId,
    string SourceType,
    Guid SourceEventId,
    long SourceVersion,
    string PostingPurpose,
    DateOnly EffectiveDate,
    DateTimeOffset RecordedAt,
    DateTimeOffset PostedAt,
    Guid PostedBy);

public enum PostedSourceLifecycleState
{
    NotPosted = 0,
    Active = 1,
    Reversed = 2,
}

public sealed record PostedSourceReversalEvidence(
    Guid OriginalJournalId,
    Guid ReversalJournalId,
    DateOnly EffectiveDate,
    DateTimeOffset RecordedAt,
    DateTimeOffset PostedAt,
    Guid PostedBy,
    DateTimeOffset LinkedAt,
    Guid LinkedBy);

public sealed record PostedSourceLifecycleEvidence(
    PostedSourceLifecycleState State,
    PostedSourceEvidence? Posting,
    PostedSourceReversalEvidence? Reversal);

public static class PostgresPostedSourceEvidenceLoader
{
    public static async ValueTask<PostedSourceEvidence?> LoadActiveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        string sourceType,
        Guid sourceEventId,
        long sourceVersion,
        string postingPurpose,
        DateOnly effectiveAsOf,
        DateTimeOffset recordedCutoff,
        CancellationToken cancellationToken = default)
    {
        PostedSourceLifecycleEvidence lifecycle = await LoadLifecycleAsync(
            connection,
            transaction,
            scope,
            companyId,
            sourceType,
            sourceEventId,
            sourceVersion,
            postingPurpose,
            effectiveAsOf,
            recordedCutoff,
            cancellationToken);
        return lifecycle.State == PostedSourceLifecycleState.Active ? lifecycle.Posting : null;
    }

    public static async ValueTask<PostedSourceLifecycleEvidence> LoadLifecycleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        string sourceType,
        Guid sourceEventId,
        long sourceVersion,
        string postingPurpose,
        DateOnly effectiveAsOf,
        DateTimeOffset recordedCutoff,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (companyId == Guid.Empty || sourceEventId == Guid.Empty)
        {
            throw new ArgumentException("Company and source-event IDs are required.");
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceVersion);
        string normalizedSourceType = RequireText(sourceType, nameof(sourceType));
        string normalizedPurpose = RequireText(postingPurpose, nameof(postingPurpose));
        if (effectiveAsOf == default)
        {
            throw new ArgumentException("Effective as-of date is required.", nameof(effectiveAsOf));
        }
        if (recordedCutoff.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Recorded cutoff must use the UTC offset.", nameof(recordedCutoff));
        }

        scope.EnsureAllowed(scope.TenantId, companyId);
        const string sql = """
            SELECT journal.journal_id, journal.source_type, journal.source_event_id,
                   journal.source_version, journal.posting_purpose,
                   journal.effective_date, journal.recorded_at, journal.posted_at, journal.posted_by,
                   reversal_journal.journal_id, reversal_journal.effective_date,
                   reversal_journal.recorded_at, reversal_journal.posted_at, reversal_journal.posted_by,
                   reversal_link.linked_at, reversal_link.linked_by
            FROM accounting.posted_journal journal
            LEFT JOIN accounting.posted_journal_reversal reversal_link
              ON reversal_link.tenant_id = journal.tenant_id
             AND reversal_link.company_id = journal.company_id
             AND reversal_link.original_journal_id = journal.journal_id
             AND reversal_link.linked_at <= $8
            LEFT JOIN accounting.posted_journal reversal_journal
              ON reversal_journal.tenant_id = reversal_link.tenant_id
             AND reversal_journal.company_id = reversal_link.company_id
             AND reversal_journal.journal_id = reversal_link.reversal_journal_id
             AND reversal_journal.effective_date <= $7
             AND reversal_journal.recorded_at <= $8
             AND reversal_journal.posted_at <= $8
            WHERE journal.tenant_id=$1 AND journal.company_id=$2 AND journal.source_type=$3
              AND journal.source_event_id=$4 AND journal.source_version=$5
              AND journal.posting_purpose=$6
              AND journal.effective_date <= $7 AND journal.recorded_at <= $8 AND journal.posted_at <= $8
            ORDER BY journal.posted_at, journal.journal_id
            LIMIT 2
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(scope.TenantId);
        command.Parameters.AddWithValue(companyId);
        command.Parameters.AddWithValue(normalizedSourceType);
        command.Parameters.AddWithValue(sourceEventId);
        command.Parameters.AddWithValue(sourceVersion);
        command.Parameters.AddWithValue(normalizedPurpose);
        command.Parameters.AddWithValue(effectiveAsOf);
        command.Parameters.AddWithValue(recordedCutoff);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new PostedSourceLifecycleEvidence(
                PostedSourceLifecycleState.NotPosted,
                null,
                null);
        }

        var posting = new PostedSourceEvidence(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetGuid(2),
            reader.GetInt64(3),
            reader.GetString(4),
            reader.GetFieldValue<DateOnly>(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.GetGuid(8));
        PostedSourceReversalEvidence? reversal = reader.IsDBNull(9)
            ? null
            : new PostedSourceReversalEvidence(
                posting.JournalId,
                reader.GetGuid(9),
                reader.GetFieldValue<DateOnly>(10),
                reader.GetFieldValue<DateTimeOffset>(11),
                reader.GetFieldValue<DateTimeOffset>(12),
                reader.GetGuid(13),
                reader.GetFieldValue<DateTimeOffset>(14),
                reader.GetGuid(15));
        if (await reader.ReadAsync(cancellationToken))
        {
            throw new PostedSourceEvidenceConflictException(
                normalizedSourceType,
                sourceEventId,
                sourceVersion,
                normalizedPurpose);
        }
        return new PostedSourceLifecycleEvidence(
            reversal is null ? PostedSourceLifecycleState.Active : PostedSourceLifecycleState.Reversed,
            posting,
            reversal);
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Source type and posting purpose are required.", parameterName);
        }
        string normalized = value.Trim();
        if (normalized.Length > 120)
        {
            throw new ArgumentException("Source type and posting purpose cannot exceed 120 characters.", parameterName);
        }
        return normalized;
    }
}

public sealed class PostedSourceEvidenceConflictException(
    string sourceType,
    Guid sourceEventId,
    long sourceVersion,
    string postingPurpose)
    : InvalidOperationException("More than one active posted journal matched the exact source identity.")
{
    public string Code { get; } = "POSTED_SOURCE_EVIDENCE_CONFLICT";
    public string SourceType { get; } = sourceType;
    public Guid SourceEventId { get; } = sourceEventId;
    public long SourceVersion { get; } = sourceVersion;
    public string PostingPurpose { get; } = postingPurpose;
}
