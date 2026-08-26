using KaguERP.BuildingBlocks.Application.Security;
using Npgsql;

namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

public sealed record PostedJournalReversalLinkResult(
    Guid OriginalJournalId,
    Guid ReversalJournalId,
    bool Created,
    DateTimeOffset LinkedAt);

public static class PostgresPostedJournalReversalLinkWriter
{
    public static async ValueTask<PostedJournalReversalLinkResult> PersistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        Guid originalJournalId,
        Guid reversalJournalId,
        DateTimeOffset linkedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (companyId == Guid.Empty || originalJournalId == Guid.Empty || reversalJournalId == Guid.Empty)
        {
            throw new ArgumentException("Company, original journal and reversal journal IDs are required.");
        }
        if (linkedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Reversal link timestamp must use the UTC offset.", nameof(linkedAt));
        }
        scope.EnsureAllowed(scope.TenantId, companyId);

        const string insertSql = """
            INSERT INTO accounting.posted_journal_reversal
                (tenant_id, company_id, original_journal_id, reversal_journal_id, linked_at, linked_by)
            VALUES ($1, $2, $3, $4, $5, $6)
            ON CONFLICT (tenant_id, company_id, original_journal_id) DO NOTHING
            RETURNING linked_at
            """;
        await using (var command = new NpgsqlCommand(insertSql, connection, transaction))
        {
            command.Parameters.AddWithValue(scope.TenantId);
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(originalJournalId);
            command.Parameters.AddWithValue(reversalJournalId);
            command.Parameters.AddWithValue(linkedAt);
            command.Parameters.AddWithValue(scope.ActorId);
            await using NpgsqlDataReader insertedReader = await command.ExecuteReaderAsync(cancellationToken);
            if (await insertedReader.ReadAsync(cancellationToken))
            {
                return new PostedJournalReversalLinkResult(
                    originalJournalId, reversalJournalId, true,
                    insertedReader.GetFieldValue<DateTimeOffset>(0));
            }
        }

        const string existingSql = """
            SELECT reversal_journal_id, linked_at
            FROM accounting.posted_journal_reversal
            WHERE tenant_id = $1 AND company_id = $2 AND original_journal_id = $3
            """;
        await using var existing = new NpgsqlCommand(existingSql, connection, transaction);
        existing.Parameters.AddWithValue(scope.TenantId);
        existing.Parameters.AddWithValue(companyId);
        existing.Parameters.AddWithValue(originalJournalId);
        await using NpgsqlDataReader reader = await existing.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Posted journal reversal link is not visible after its uniqueness conflict.");
        }
        Guid persistedReversalId = reader.GetGuid(0);
        if (persistedReversalId != reversalJournalId)
        {
            throw new PostedJournalReversalConflictException(originalJournalId, persistedReversalId);
        }

        return new PostedJournalReversalLinkResult(
            originalJournalId, persistedReversalId, false, reader.GetFieldValue<DateTimeOffset>(1));
    }
}

public sealed class PostedJournalReversalConflictException(
    Guid originalJournalId,
    Guid existingReversalJournalId)
    : InvalidOperationException("The original journal already has a different posted reversal.")
{
    public string Code { get; } = "POSTED_JOURNAL_ALREADY_REVERSED";

    public Guid OriginalJournalId { get; } = originalJournalId;

    public Guid ExistingReversalJournalId { get; } = existingReversalJournalId;
}
