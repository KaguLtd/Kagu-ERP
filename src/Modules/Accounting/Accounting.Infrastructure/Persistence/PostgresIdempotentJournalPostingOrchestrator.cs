using System.Text.Json;
using KaguERP.BuildingBlocks.Application.Idempotency;
using Npgsql;

namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

public sealed record IdempotentJournalPostingResult(
    JournalPostingResult Posting,
    bool Replayed);

public static class PostgresIdempotentJournalPostingOrchestrator
{
    private const string CommandName = "accounting.journal.post";

    public static async ValueTask<IdempotentJournalPostingResult> PostAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        JournalPostingCommand command,
        Guid idempotencyRecordId,
        string idempotencyKey,
        JournalPreparationIdempotencyAcquirer acquireIdempotency,
        JournalPreparationIdempotencyCompleter completeIdempotency,
        JournalPreparationSourceLoader loadSource,
        JournalPreparationAuditAppender appendPreparationAudit,
        JournalPreparationOutboxAppender appendPreparationOutbox,
        JournalPostedAuditAppender appendPostedAudit,
        JournalPostedOutboxAppender appendPostedOutbox,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(acquireIdempotency);
        ArgumentNullException.ThrowIfNull(completeIdempotency);
        string requestHash = PostgresIdempotentJournalPreparationOrchestrator.ComputeRequestHash(command.Preparation);
        IdempotencyRecord acquired = await acquireIdempotency(
            connection, transaction, command.Preparation.Scope, command.Preparation.SourceIdentity.CompanyId,
            idempotencyRecordId, CommandName, idempotencyKey, requestHash, cancellationToken);

        if (!acquired.Created)
        {
            if (acquired.Status != IdempotencyRecordStatus.Completed || acquired.ResponseBodyJson is null)
            {
                throw new InvalidOperationException("IDEMPOTENCY_REQUEST_IN_PROGRESS");
            }

            JournalPostingResult replay = JsonSerializer.Deserialize<JournalPostingResult>(acquired.ResponseBodyJson)
                ?? throw new InvalidOperationException("Completed idempotency response is invalid.");
            return new IdempotentJournalPostingResult(replay, true);
        }

        JournalPostingResult posted = await PostgresJournalPostingOrchestrator.PostFromSourceAsync(
            connection, transaction, command, loadSource, appendPreparationAudit, appendPreparationOutbox,
            appendPostedAudit, appendPostedOutbox, cancellationToken);
        _ = await completeIdempotency(
            connection, transaction, command.Preparation.Scope, acquired, 201, JsonSerializer.Serialize(posted),
            posted.PostedJournal.JournalId, cancellationToken);
        return new IdempotentJournalPostingResult(posted, false);
    }
}
