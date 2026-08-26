using KaguERP.BuildingBlocks.Application.Approvals;
using KaguERP.Modules.Accounting.Application.Posting;
using KaguERP.Modules.Accounting.Domain.Periods;
using Npgsql;

namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

public sealed record JournalPostingCommand(
    JournalPreparationCommand Preparation,
    Guid JournalId,
    Guid PostedAuditEventId,
    Guid PostedOutboxEventId,
    DateTimeOffset PostedAt);

public sealed record JournalPostingResult(
    JournalPreparationResult Preparation,
    PostedJournalPersistenceResult PostedJournal);

public delegate ValueTask JournalPostedAuditAppender(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    JournalPostingCommand command,
    JournalPostingResult result,
    CancellationToken cancellationToken);

public delegate ValueTask<bool> JournalPostedOutboxAppender(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    JournalPostingCommand command,
    JournalPostingResult result,
    CancellationToken cancellationToken);

public static class PostgresJournalPostingOrchestrator
{
    public static async ValueTask<JournalPostingResult> PostFromSourceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        JournalPostingCommand command,
        JournalPreparationSourceLoader loadSource,
        JournalPreparationAuditAppender appendPreparationAudit,
        JournalPreparationOutboxAppender appendPreparationOutbox,
        JournalPostedAuditAppender appendPostedAudit,
        JournalPostedOutboxAppender appendPostedOutbox,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(loadSource);
        ArgumentNullException.ThrowIfNull(appendPostedAudit);
        ArgumentNullException.ThrowIfNull(appendPostedOutbox);
        if (command.JournalId == Guid.Empty || command.PostedAuditEventId == Guid.Empty || command.PostedOutboxEventId == Guid.Empty)
        {
            throw new ArgumentException("Posted journal, audit and outbox IDs are required.", nameof(command));
        }
        if (command.PostedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Posted timestamp must use the UTC offset.", nameof(command));
        }

        CanonicalJournalPreparationSource? canonicalSource = null;
        async ValueTask<CanonicalJournalPreparationSource> CaptureSource(
            NpgsqlConnection sourceConnection,
            NpgsqlTransaction sourceTransaction,
            JournalPreparationCommand preparation,
            CancellationToken token)
        {
            canonicalSource = await loadSource(sourceConnection, sourceTransaction, preparation, token);
            return canonicalSource;
        }

        JournalPreparationResult preparationResult = await PostgresJournalPreparationOrchestrator.PrepareFromSourceAsync(
            connection, transaction, command.Preparation, CaptureSource,
            appendPreparationAudit, appendPreparationOutbox, cancellationToken);
        CanonicalJournalPreparationSource source = canonicalSource
            ?? throw new InvalidOperationException("Canonical source was not captured during preparation.");

        ApprovalCompletionEvidence approval = await PostgresAuthoritativeApprovalCompletionLoader.LoadAsync(
            connection, transaction, command.Preparation.Scope,
            command.Preparation.SourceIdentity.TenantId, command.Preparation.SourceIdentity.CompanyId,
            command.Preparation.SourceIdentity.SourceType, command.Preparation.SourceIdentity.SourceEventId,
            command.Preparation.ExpectedSourceVersion, cancellationToken);
        ValidatedPeriodLockSet periodLocks = await PostgresAuthoritativePeriodGateLoader.LoadForStandardPostingAsync(
            connection, transaction, command.Preparation.Scope, source.Draft, cancellationToken);
        var persistedDraft = new ValidatedJournalDraftPersistenceResult(
            preparationResult.JournalDraftId, preparationResult.DraftCreated, preparationResult.DraftHash);
        PostedJournalPersistenceResult posted = await PostgresPostedJournalWriter.PersistAsync(
            connection, transaction, command.Preparation.Scope, command.JournalId, persistedDraft, source.Draft,
            command.Preparation.ExpectedSourceVersion, approval, periodLocks, command.PostedAt, cancellationToken);
        var result = new JournalPostingResult(preparationResult, posted);

        await appendPostedAudit(connection, transaction, command, result, cancellationToken);
        if (!await appendPostedOutbox(connection, transaction, command, result, cancellationToken))
        {
            throw new InvalidOperationException("Journal-posted outbox event was already present.");
        }

        return result;
    }
}
