using KaguERP.Modules.Accounting.Application.Posting;
using KaguERP.Modules.Accounting.Infrastructure.Persistence;
using KaguERP.Modules.Treasury.Application.Reconciliation;
using KaguERP.Modules.Treasury.Contracts.Reconciliation;
using KaguERP.Modules.Treasury.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.Bootstrap;

public sealed record ReconciliationStatementJournalCommand(
    Guid StatementLineId,
    JournalPostingCommand JournalCommand);

public sealed record ApprovedReconciliationPostingResult(
    ReconciliationApprovalPersistenceResult Approval,
    IReadOnlyList<JournalPostingResult> PostedJournals);

public static class PostgresApprovedReconciliationPostingOrchestrator
{
    public static async ValueTask<ApprovedReconciliationPostingResult> PostAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuthorizedReconciliationApproval approval,
        DateTimeOffset approvalRecordedAt,
        ReconciliationTransitAccountMapping accountMapping,
        IEnumerable<ReconciliationStatementJournalCommand?>? statementCommands,
        JournalPreparationAuditAppender appendPreparationAudit,
        JournalPreparationOutboxAppender appendPreparationOutbox,
        JournalPostedAuditAppender appendPostedAudit,
        JournalPostedOutboxAppender appendPostedOutbox,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(accountMapping);
        ArgumentNullException.ThrowIfNull(appendPreparationAudit);
        ArgumentNullException.ThrowIfNull(appendPreparationOutbox);
        ArgumentNullException.ThrowIfNull(appendPostedAudit);
        ArgumentNullException.ThrowIfNull(appendPostedOutbox);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        ArgumentNullException.ThrowIfNull(statementCommands);
        ReconciliationStatementJournalCommand?[] copiedCommands = statementCommands.ToArray();
        if (copiedCommands.Length == 0 || copiedCommands.Any(command => command is null))
        {
            throw new ApprovedReconciliationPostingException(
                "RECONCILIATION_POSTING_COMMANDS_REQUIRED",
                "One journal posting command is required for every approved statement line.");
        }
        ReconciliationStatementJournalCommand[] commands = copiedCommands
            .Cast<ReconciliationStatementJournalCommand>()
            .ToArray();
        if (commands.Any(command => command.StatementLineId == Guid.Empty || command.JournalCommand is null) ||
            commands.Select(command => command.StatementLineId).Distinct().Count() != commands.Length)
        {
            throw new ApprovedReconciliationPostingException(
                "RECONCILIATION_POSTING_COMMANDS_INVALID",
                "Statement journal commands require unique, non-empty statement identities.");
        }

        ReconciliationApprovalPersistenceResult persistedApproval =
            await PostgresReconciliationApprovalWriter.PersistAsync(
                connection,
                transaction,
                approval,
                approvalRecordedAt,
                cancellationToken);
        ApprovedReconciliationTransitPostingBatch batch =
            await PostgresApprovedReconciliationTransitPostingLoader.LoadAsync(
                connection,
                transaction,
                approval.Scope,
                approval.Proposal.CompanyId,
                approval.Proposal.ReconciliationId,
                cancellationToken)
            ?? throw new ApprovedReconciliationPostingException(
                "RECONCILIATION_APPROVED_FACT_UNAVAILABLE",
                "The approved reconciliation transit fact is not visible in the active transaction.");
        IReadOnlyList<ReconciliationTransitJournalSource> sources =
            ReconciliationTransitJournalFactory.Create(batch, accountMapping);
        if (!sources.Select(source => source.StatementLineId).Order()
                .SequenceEqual(commands.Select(command => command.StatementLineId).Order()))
        {
            throw new ApprovedReconciliationPostingException(
                "RECONCILIATION_POSTING_COMMAND_SET_MISMATCH",
                "Journal commands must exactly cover the approved reconciliation statement set.");
        }

        Dictionary<Guid, JournalPostingCommand> commandByStatement = commands
            .ToDictionary(command => command.StatementLineId, command => command.JournalCommand);
        var results = new List<JournalPostingResult>(sources.Count);
        foreach (ReconciliationTransitJournalSource source in sources)
        {
            JournalPostingCommand command = commandByStatement[source.StatementLineId];
            ValidateCommand(command, source);
            JournalPostingResult result = await PostgresJournalPostingOrchestrator.PostFromSourceAsync(
                connection,
                transaction,
                command,
                (_, _, _, _) => ValueTask.FromResult(source.Source),
                appendPreparationAudit,
                appendPreparationOutbox,
                appendPostedAudit,
                appendPostedOutbox,
                cancellationToken);
            results.Add(result);
        }

        return new ApprovedReconciliationPostingResult(persistedApproval, results.AsReadOnly());
    }

    private static void ValidateCommand(
        JournalPostingCommand command,
        ReconciliationTransitJournalSource source)
    {
        if (command.Preparation.SourceIdentity != source.Source.Draft.PostingIdentity ||
            command.Preparation.ExpectedSourceVersion != source.Source.SourceVersion ||
            command.Preparation.ResolveApprovalSubject() != source.ApprovalSubject)
        {
            throw new ApprovedReconciliationPostingException(
                "RECONCILIATION_POSTING_COMMAND_SOURCE_MISMATCH",
                "Journal posting command must bind the exact statement source and reconciliation approval subject.");
        }
    }
}

public sealed class ApprovedReconciliationPostingException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
