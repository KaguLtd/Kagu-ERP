using KaguERP.Modules.Reporting.Domain.ControlAccounts;
using KaguERP.Modules.Reporting.Domain.PartyReports;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public sealed record PartyReportProjectionPublicationCommand(
    ProjectionGenerationPersistenceCommand Generation,
    ValidatedPartyStatement Statement,
    ValidatedPartyAgingReport Aging,
    ControlAccountBalanceSnapshot Subledger,
    ControlAccountBalanceSnapshot GeneralLedger,
    Guid PartyCrossFootId,
    Guid ControlAccountReconciliationId);

public sealed record PartyReportProjectionPublicationResult(
    Guid ProjectionGenerationId,
    Guid StatementId,
    Guid AgingReportId,
    Guid ControlAccountReconciliationId,
    bool Created);

public static class PostgresPartyReportProjectionPublisher
{
    public static async ValueTask<PartyReportProjectionPublicationResult> PublishAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PartyReportProjectionPublicationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        FinancialReportSlice slice = command.Generation.Slice;
        EnsureSameSlice(slice, command.Statement.ReportSlice, "statement");
        EnsureSameSlice(slice, command.Aging.ReportSlice, "aging");
        EnsureSameSlice(slice, command.Subledger.ReportSlice, "subledger");
        EnsureSameSlice(slice, command.GeneralLedger.ReportSlice, "general ledger");
        _ = PartyStatementAgingCrossFoot.Create(command.PartyCrossFootId, command.Statement, command.Aging);
        if (command.Subledger.ControlAccountId != command.Statement.ControlAccountId ||
            command.GeneralLedger.ControlAccountId != command.Statement.ControlAccountId)
        {
            throw new PartyReportProjectionPublicationException("control account");
        }
        ControlAccountReconciliationResult reconciliation = ControlAccountReconciliationResult.Create(
            command.ControlAccountReconciliationId, command.Subledger, command.GeneralLedger);

        ProjectionGenerationPersistenceResult generation = await PostgresProjectionGenerationWriter.PersistAsync(
            connection, transaction, command.Generation, cancellationToken);
        AgingPolicyProjectionPersistenceResult policy = await PostgresAgingPolicyProjectionWriter.PersistAsync(
            connection, transaction, command.Generation.Scope, slice, command.Aging.Policy, cancellationToken);
        PartyStatementProjectionPersistenceResult statement = await PostgresPartyStatementProjectionWriter.PersistAsync(
            connection, transaction, command.Generation.Scope, command.Statement, cancellationToken);
        PartyAgingProjectionPersistenceResult aging = await PostgresPartyAgingProjectionWriter.PersistAsync(
            connection, transaction, command.Generation.Scope, command.Aging, cancellationToken);
        ControlAccountBalanceProjectionPersistenceResult subledger =
            await PostgresControlAccountBalanceProjectionWriter.PersistAsync(
                connection, transaction, command.Generation.Scope, command.Subledger, cancellationToken);
        ControlAccountBalanceProjectionPersistenceResult generalLedger =
            await PostgresControlAccountBalanceProjectionWriter.PersistAsync(
                connection, transaction, command.Generation.Scope, command.GeneralLedger, cancellationToken);
        bool created = generation.Created || policy.Created || statement.Created || aging.Created ||
            subledger.Created || generalLedger.Created;
        return new PartyReportProjectionPublicationResult(
            slice.ProjectionGenerationId, command.Statement.StatementId, command.Aging.AgingReportId,
            reconciliation.ReconciliationId, created);
    }

    private static void EnsureSameSlice(FinancialReportSlice expected, FinancialReportSlice actual, string component)
    {
        bool same = expected.TenantId == actual.TenantId && expected.CompanyId == actual.CompanyId &&
            expected.ReportCode == actual.ReportCode &&
            expected.ReportDefinitionVersion == actual.ReportDefinitionVersion &&
            expected.EffectiveAsOf == actual.EffectiveAsOf && expected.DataCutoffAt == actual.DataCutoffAt &&
            expected.GeneratedAt == actual.GeneratedAt &&
            expected.ProjectionGenerationId == actual.ProjectionGenerationId &&
            expected.Currency == actual.Currency && expected.Dimensions.HasSameSelection(actual.Dimensions);
        if (!same)
        {
            throw new PartyReportProjectionPublicationException(component);
        }
    }
}

public sealed class PartyReportProjectionPublicationException(string component)
    : InvalidOperationException($"The {component} snapshot does not use the publication report slice.")
{
    public string Code { get; } = "PARTY_REPORT_PUBLICATION_SLICE_MISMATCH";
    public string Component { get; } = component;
}
