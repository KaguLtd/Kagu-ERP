using KaguERP.Modules.Parties.Contracts.Reports;
using KaguERP.Modules.Reporting.Domain.ControlAccounts;
using KaguERP.Modules.Reporting.Domain.PartyReports;

namespace KaguERP.Modules.Reporting.Application.PartyReports;

public sealed record PartyReportProjectionJobCommand(
    PartyReportSourceQuery SourceQuery,
    string ReportCode,
    long ReportDefinitionVersion,
    Guid ProjectionGenerationId,
    Guid StatementId,
    Guid AgingReportId,
    Guid PartyCrossFootId,
    Guid ControlAccountReconciliationId,
    DateTimeOffset GeneratedAt,
    string GenerationReason);

public sealed record PartyControlAccountEvidence(
    ControlAccountBalanceSnapshot Subledger,
    ControlAccountBalanceSnapshot GeneralLedger);

public sealed record PartyReportProjectionPublication(
    PartyReportProjectionJobCommand Command,
    PartyReportSourceBatch Source,
    PartyReportProjectionBuilder.ProjectionPair Pair,
    PartyControlAccountEvidence ControlAccounts);

public sealed record PartyReportProjectionJobResult(
    Guid ProjectionGenerationId,
    Guid StatementId,
    Guid AgingReportId,
    bool Created);

public interface IPartyAgingPolicySource
{
    ValueTask<CalendarDayAgingPolicySnapshot?> LoadAsync(
        Guid tenantId,
        Guid companyId,
        DateOnly effectiveAsOf,
        DateTimeOffset recordedCutoff,
        CancellationToken cancellationToken = default);
}

public interface IPartyControlAccountEvidenceSource
{
    ValueTask<PartyControlAccountEvidence?> LoadAsync(
        PartyReportSourceBatch source,
        FinancialReportSlice reportSlice,
        CancellationToken cancellationToken = default);
}

public interface IPartyReportProjectionSink
{
    ValueTask<PartyReportProjectionJobResult> PublishAsync(
        PartyReportProjectionPublication publication,
        CancellationToken cancellationToken = default);
}

public sealed class PartyReportProjectionJob(
    IPartyReportSource partySource,
    IPartyAgingPolicySource policySource,
    IPartyControlAccountEvidenceSource controlAccountSource,
    IPartyReportProjectionSink projectionSink)
{
    public async ValueTask<PartyReportProjectionJobResult> RunAsync(
        PartyReportProjectionJobCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.GenerationReason))
        {
            throw new ArgumentException("Projection generation reason is required.", nameof(command));
        }

        PartyReportSourceBatch source = await partySource.LoadAsync(command.SourceQuery, cancellationToken) ??
            throw new PartyReportProjectionJobException(
                "PARTY_REPORT_SOURCE_NOT_FOUND", "The scoped Party report source was not found.");
        EnsureSourceMatches(command.SourceQuery, source);

        CalendarDayAgingPolicySnapshot policy = await policySource.LoadAsync(
            source.TenantId, source.CompanyId, source.EffectiveAsOf, source.RecordedCutoff,
            cancellationToken) ?? throw new PartyReportProjectionJobException(
                "PARTY_AGING_POLICY_NOT_FOUND", "No authoritative aging policy exists for the requested cut.");

        PartyReportProjectionBuilder.ProjectionPair pair = PartyReportProjectionBuilder.BuildPair(
            source, policy, command.ReportCode, command.StatementId, command.AgingReportId,
            command.PartyCrossFootId, command.ReportDefinitionVersion,
            command.ProjectionGenerationId, command.GeneratedAt);

        PartyControlAccountEvidence controlAccounts = await controlAccountSource.LoadAsync(
            source, pair.Statement.ReportSlice, cancellationToken) ??
            throw new PartyReportProjectionJobException(
                "PARTY_CONTROL_ACCOUNT_EVIDENCE_NOT_FOUND",
                "Subledger and general-ledger evidence is required before projection publication.");
        EnsureBalanceContext(source.ControlAccountId, pair.Statement.ReportSlice, controlAccounts.Subledger);
        EnsureBalanceContext(source.ControlAccountId, pair.Statement.ReportSlice, controlAccounts.GeneralLedger);
        ControlAccountReconciliationResult reconciliation = ControlAccountReconciliationResult.Create(
            command.ControlAccountReconciliationId,
            controlAccounts.Subledger,
            controlAccounts.GeneralLedger);
        if (!reconciliation.IsReconciled)
        {
            throw new PartyReportProjectionJobException(
                "PARTY_CONTROL_ACCOUNT_RECONCILIATION_DIFFERENCE",
                "Party subledger and general-ledger control-account balances do not reconcile.");
        }

        return await projectionSink.PublishAsync(
            new PartyReportProjectionPublication(command, source, pair, controlAccounts), cancellationToken);
    }

    private static void EnsureSourceMatches(PartyReportSourceQuery query, PartyReportSourceBatch source)
    {
        if (query.TenantId != source.TenantId || query.CompanyId != source.CompanyId ||
            query.PartyAccountId != source.PartyAccountId || query.EffectiveAsOf != source.EffectiveAsOf ||
            query.RecordedCutoff != source.RecordedCutoff)
        {
            throw new PartyReportProjectionJobException(
                "PARTY_REPORT_SOURCE_SCOPE_MISMATCH",
                "The Party source batch does not match the requested scope or bitemporal cut.");
        }
    }

    private static void EnsureBalanceContext(
        Guid controlAccountId,
        FinancialReportSlice expected,
        ControlAccountBalanceSnapshot actual)
    {
        FinancialReportSlice slice = actual.ReportSlice;
        bool sameSlice = expected.TenantId == slice.TenantId && expected.CompanyId == slice.CompanyId &&
            expected.ReportCode == slice.ReportCode &&
            expected.ReportDefinitionVersion == slice.ReportDefinitionVersion &&
            expected.EffectiveAsOf == slice.EffectiveAsOf && expected.DataCutoffAt == slice.DataCutoffAt &&
            expected.GeneratedAt == slice.GeneratedAt &&
            expected.ProjectionGenerationId == slice.ProjectionGenerationId &&
            expected.Currency == slice.Currency && expected.Dimensions.HasSameSelection(slice.Dimensions);
        if (actual.ControlAccountId != controlAccountId || !sameSlice)
        {
            throw new PartyReportProjectionJobException(
                "PARTY_CONTROL_ACCOUNT_EVIDENCE_MISMATCH",
                "Control-account evidence does not match the Party source and report slice.");
        }
    }
}

public sealed class PartyReportProjectionJobException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
