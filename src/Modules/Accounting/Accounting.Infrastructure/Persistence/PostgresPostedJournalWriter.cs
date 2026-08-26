using KaguERP.BuildingBlocks.Application.Approvals;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Accounting.Application.Posting;
using KaguERP.Modules.Accounting.Domain.Journals;
using KaguERP.Modules.Accounting.Domain.Periods;
using Npgsql;

namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

public static class PostgresPostedJournalWriter
{
    public static async ValueTask<PostedJournalPersistenceResult> PersistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid journalId,
        ValidatedJournalDraftPersistenceResult persistedDraft,
        ValidatedJournalDraft draft,
        long sourceVersion,
        ApprovalCompletionEvidence approval,
        ValidatedPeriodLockSet periodLocks,
        DateTimeOffset postedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(persistedDraft);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(periodLocks);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (journalId == Guid.Empty || persistedDraft.JournalDraftId == Guid.Empty)
        {
            throw new ArgumentException("Posted journal and persisted draft IDs are required.");
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceVersion);
        if (postedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Posted timestamp must use the UTC offset.", nameof(postedAt));
        }

        scope.EnsureAllowed(draft.TenantId, draft.CompanyId);
        if (!scope.HasPermission(draft.CompanyId, AuthorizedJournalPostingCandidate.RequiredPermission))
        {
            throw new JournalPostingCandidateException(
                "JOURNAL_POST_PERMISSION_REQUIRED",
                "The active actor does not have permission to post journals for this company.");
        }
        if (!string.Equals(persistedDraft.DraftHash, JournalDraftFingerprintV1.Compute(draft), StringComparison.Ordinal))
        {
            throw new PostedJournalPersistenceException(
                "POSTED_JOURNAL_DRAFT_MISMATCH",
                "The persisted draft fingerprint does not match the validated journal draft.");
        }
        approval.EnsureSubject(
            draft.TenantId, draft.CompanyId, draft.SourceType, draft.SourceEventId, sourceVersion);
        if (periodLocks.TenantId != draft.TenantId || periodLocks.CompanyId != draft.CompanyId)
        {
            throw new PostedJournalPersistenceException(
                "POSTED_JOURNAL_PERIOD_SCOPE_MISMATCH",
                "The period evidence does not match the journal scope.");
        }
        periodLocks.EnsureStandardPostingAllowed();

        const string insertHeaderSql = """
            INSERT INTO accounting.posted_journal
                (journal_id, tenant_id, company_id, journal_draft_id, period_id, approval_instance_id,
                 source_type, source_event_id, source_version, posting_purpose, posting_rule_version_id,
                 effective_date, recorded_at, posted_at, posted_by, functional_currency, draft_hash,
                 total_debit, total_credit, line_count)
            SELECT $1, d.tenant_id, d.company_id, d.journal_draft_id, $2, $3,
                   r.source_type, r.source_event_id, $4, r.posting_purpose, d.posting_rule_version_id,
                   d.effective_date, d.recorded_at, $5, $6, d.functional_currency, d.draft_hash,
                   d.total_debit, d.total_credit, d.line_count
            FROM accounting.validated_journal_draft d
            JOIN accounting.journal_source_reservation r
              ON r.tenant_id = d.tenant_id AND r.company_id = d.company_id AND r.reservation_id = d.reservation_id
            WHERE d.tenant_id = $7 AND d.company_id = $8 AND d.journal_draft_id = $9
              AND d.draft_hash = $10 AND r.source_type = $11 AND r.source_event_id = $12
              AND r.posting_purpose = $13
            ON CONFLICT (tenant_id, company_id, journal_draft_id) DO NOTHING
            RETURNING journal_id, posted_at
            """;
        Guid? insertedJournalId = null;
        DateTimeOffset insertedPostedAt = default;
        await using (var command = new NpgsqlCommand(insertHeaderSql, connection, transaction))
        {
            command.Parameters.AddWithValue(journalId);
            command.Parameters.AddWithValue(periodLocks.PeriodId);
            command.Parameters.AddWithValue(approval.ApprovalInstanceId);
            command.Parameters.AddWithValue(sourceVersion);
            command.Parameters.AddWithValue(postedAt);
            command.Parameters.AddWithValue(scope.ActorId);
            command.Parameters.AddWithValue(draft.TenantId);
            command.Parameters.AddWithValue(draft.CompanyId);
            command.Parameters.AddWithValue(persistedDraft.JournalDraftId);
            command.Parameters.AddWithValue(persistedDraft.DraftHash);
            command.Parameters.AddWithValue(draft.SourceType);
            command.Parameters.AddWithValue(draft.SourceEventId);
            command.Parameters.AddWithValue(draft.PostingPurpose);
            await using NpgsqlDataReader insertedReader = await command.ExecuteReaderAsync(cancellationToken);
            if (await insertedReader.ReadAsync(cancellationToken))
            {
                insertedJournalId = insertedReader.GetGuid(0);
                insertedPostedAt = insertedReader.GetFieldValue<DateTimeOffset>(1);
            }
        }
        if (insertedJournalId is Guid insertedId)
        {
            await CopyLinesAsync(connection, transaction, insertedId, persistedDraft.JournalDraftId, draft, cancellationToken);
            return new PostedJournalPersistenceResult(insertedId, true, insertedPostedAt);
        }

        const string existingSql = """
            SELECT journal_id, period_id, approval_instance_id, source_version, posted_at, draft_hash
            FROM accounting.posted_journal
            WHERE tenant_id = $1 AND company_id = $2 AND journal_draft_id = $3
            """;
        await using var existingCommand = new NpgsqlCommand(existingSql, connection, transaction);
        existingCommand.Parameters.AddWithValue(draft.TenantId);
        existingCommand.Parameters.AddWithValue(draft.CompanyId);
        existingCommand.Parameters.AddWithValue(persistedDraft.JournalDraftId);
        await using NpgsqlDataReader reader = await existingCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new PostedJournalPersistenceException(
                "POSTED_JOURNAL_DRAFT_NOT_FOUND",
                "The persisted draft is not visible or does not match its canonical source.");
        }

        Guid existingId = reader.GetGuid(0);
        if (reader.GetGuid(1) != periodLocks.PeriodId || reader.GetGuid(2) != approval.ApprovalInstanceId ||
            reader.GetInt64(3) != sourceVersion || !string.Equals(reader.GetString(5), persistedDraft.DraftHash, StringComparison.Ordinal))
        {
            throw new PostedJournalPersistenceConflictException(existingId);
        }

        return new PostedJournalPersistenceResult(existingId, false, reader.GetFieldValue<DateTimeOffset>(4));
    }

    private static async Task CopyLinesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid journalId,
        Guid journalDraftId,
        ValidatedJournalDraft draft,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO accounting.posted_journal_line
                (journal_id, tenant_id, company_id, line_number, account_id, source_line_id,
                 debit, credit, dimensions, currency_snapshot)
            SELECT $1, tenant_id, company_id, line_number, account_id, source_line_id,
                   debit, credit, dimensions, currency_snapshot
            FROM accounting.validated_journal_line
            WHERE tenant_id = $2 AND company_id = $3 AND journal_draft_id = $4
            ORDER BY line_number
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(journalId);
        command.Parameters.AddWithValue(draft.TenantId);
        command.Parameters.AddWithValue(draft.CompanyId);
        command.Parameters.AddWithValue(journalDraftId);
        int copied = await command.ExecuteNonQueryAsync(cancellationToken);
        if (copied != draft.Lines.Count)
        {
            throw new PostedJournalPersistenceException(
                "POSTED_JOURNAL_LINE_COUNT_MISMATCH",
                "The posted journal did not copy every validated draft line.");
        }
    }
}

public sealed record PostedJournalPersistenceResult(Guid JournalId, bool Created, DateTimeOffset PostedAt);

public sealed class PostedJournalPersistenceException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class PostedJournalPersistenceConflictException(Guid existingJournalId)
    : InvalidOperationException($"The draft is already posted as journal {existingJournalId:D} with different evidence.")
{
    public Guid ExistingJournalId { get; } = existingJournalId;
}
