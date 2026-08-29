using KaguERP.Modules.Parties.Contracts.Reports;
using KaguERP.Modules.Reporting.Domain.ControlAccounts;
using KaguERP.Modules.Reporting.Domain.PartyReports;

namespace KaguERP.Modules.Reporting.Application.PartyReports;

public static class PartyReportProjectionBuilder
{
    public sealed record ProjectionPair(
        ValidatedPartyStatement Statement,
        ValidatedPartyAgingReport Aging,
        PartyStatementAgingCrossFoot CrossFoot);

    public static ProjectionPair BuildPair(
        PartyReportSourceBatch source,
        CalendarDayAgingPolicySnapshot policy,
        string reportCode,
        Guid statementId,
        Guid agingReportId,
        Guid crossFootId,
        long reportDefinitionVersion,
        Guid projectionGenerationId,
        DateTimeOffset generatedAt)
    {
        ValidatedPartyStatement statement = BuildStatement(
            source, reportCode, statementId, reportDefinitionVersion, projectionGenerationId, generatedAt);
        ValidatedPartyAgingReport aging = BuildAging(
            source, policy, reportCode, agingReportId, reportDefinitionVersion,
            projectionGenerationId, generatedAt);
        PartyStatementAgingCrossFoot crossFoot = PartyStatementAgingCrossFoot.Create(
            crossFootId, statement, aging);
        return new ProjectionPair(statement, aging, crossFoot);
    }

    public static ValidatedPartyStatement BuildStatement(
        PartyReportSourceBatch source,
        string reportCode,
        Guid statementId,
        long reportDefinitionVersion,
        Guid projectionGenerationId,
        DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(source);
        FinancialReportSlice slice = CreateSlice(
            source, reportCode, reportDefinitionVersion, projectionGenerationId, generatedAt);
        PartyStatementEventSnapshot[] events = CreateStatementEvents(source);
        ValidatedPartyStatement statement = ValidatedPartyStatement.Create(
            statementId, source.PartyAccountId, source.ControlAccountId, Map(source.BalanceSide),
            source.OpeningExposure, slice, events);
        decimal expectedClosing = source.OpenItems.Sum(item => item.RemainingAmount) + source.OpeningExposure;
        if (statement.ClosingExposure != expectedClosing)
        {
            throw new ReportingInvariantException(
                "PARTY_SOURCE_CROSS_FOOT_MISMATCH",
                "Party source remaining amounts do not cross-foot to the normalized statement.");
        }
        return statement;
    }

    public static ValidatedPartyAgingReport BuildAging(
        PartyReportSourceBatch source,
        CalendarDayAgingPolicySnapshot policy,
        string reportCode,
        Guid agingReportId,
        long reportDefinitionVersion,
        Guid projectionGenerationId,
        DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(policy);
        if (source.OpenItems.Any(item => item.RestrictionEvidence == PartyReportRestrictionEvidence.Unavailable))
        {
            throw new ReportingInvariantException(
                "PARTY_RESTRICTION_EVIDENCE_UNAVAILABLE",
                "Aging cannot treat unavailable dispute or block evidence as clear.");
        }
        FinancialReportSlice slice = CreateSlice(
            source, reportCode, reportDefinitionVersion, projectionGenerationId, generatedAt);
        OpenItemAgingSnapshot[] items = source.OpenItems
            .Where(item => item.RemainingAmount > decimal.Zero)
            .Select(item => OpenItemAgingSnapshot.Create(
                item.OpenItemId, source.TenantId, source.CompanyId, source.PartyAccountId,
                source.ControlAccountId, item.SourceEventId, item.DueScheduleLineId, slice.Currency,
                item.OriginalAmount, item.RemainingAmount, item.DueDate, source.EffectiveAsOf,
                source.RecordedCutoff,
                item.RestrictionEvidence is PartyReportRestrictionEvidence.Disputed or PartyReportRestrictionEvidence.DisputedAndBlocked,
                item.RestrictionEvidence is PartyReportRestrictionEvidence.Blocked or PartyReportRestrictionEvidence.DisputedAndBlocked))
            .ToArray();
        return ValidatedPartyAgingReport.Create(
            agingReportId, source.PartyAccountId, source.ControlAccountId, Map(source.BalanceSide),
            slice, policy, items);
    }

    private static PartyStatementEventSnapshot[] CreateStatementEvents(PartyReportSourceBatch source)
    {
        var raw = source.OpenItems.SelectMany(item =>
            new[]
            {
                new RawEvent(item.OpenItemId, PartyStatementEventKind.OpenItem, item.SourceType,
                    item.SourceEventId, item.DueScheduleLineId, null, item.OriginalAmount,
                    item.EffectiveDate, item.RecordedAt),
            }.Concat(item.Impacts.Select(impact => new RawEvent(
                impact.EventId, Map(impact.Kind), item.SourceType, item.SourceEventId,
                item.DueScheduleLineId, impact.PaymentId, Effect(impact),
                impact.EffectiveDate, impact.RecordedAt))))
            .OrderBy(item => item.EffectiveDate).ThenBy(item => item.RecordedAt).ThenBy(item => item.EventId)
            .ToArray();
        return raw.Select((item, index) => PartyStatementEventSnapshot.Create(
            item.EventId, source.TenantId, source.CompanyId, source.PartyAccountId,
            source.ControlAccountId, ReportCurrencyCode.Create(source.Currency), item.Kind,
            item.SourceType, item.SourceEventId, item.DueScheduleLineId, item.PaymentId,
            item.ExposureEffect, item.EffectiveDate, index + 1L, item.RecordedAt)).ToArray();
    }

    private static FinancialReportSlice CreateSlice(
        PartyReportSourceBatch source, string reportCode, long version, Guid generationId,
        DateTimeOffset generatedAt) => FinancialReportSlice.Create(
            source.TenantId, source.CompanyId, reportCode, version, source.EffectiveAsOf,
            source.RecordedCutoff, generatedAt, generationId, ReportCurrencyCode.Create(source.Currency),
            ReportDimensionSlice.Create([]));

    private static decimal Effect(PartyReportImpactFact impact) => impact.Kind switch
    {
        PartyReportImpactKind.Allocation or PartyReportImpactKind.WriteOff => -impact.Amount,
        PartyReportImpactKind.Unallocation or PartyReportImpactKind.WriteOffReversal => impact.Amount,
        _ => throw new ArgumentOutOfRangeException(nameof(impact)),
    };

    private static PartyStatementEventKind Map(PartyReportImpactKind kind) => kind switch
    {
        PartyReportImpactKind.Allocation => PartyStatementEventKind.Allocation,
        PartyReportImpactKind.Unallocation => PartyStatementEventKind.Unallocation,
        PartyReportImpactKind.WriteOff => PartyStatementEventKind.WriteOff,
        PartyReportImpactKind.WriteOffReversal => PartyStatementEventKind.WriteOffReversal,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static PartyBalanceSide Map(PartyReportBalanceSide side) => side switch
    {
        PartyReportBalanceSide.Receivable => PartyBalanceSide.Receivable,
        PartyReportBalanceSide.Payable => PartyBalanceSide.Payable,
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    private sealed record RawEvent(
        Guid EventId, PartyStatementEventKind Kind, string SourceType, Guid SourceEventId,
        Guid DueScheduleLineId, Guid? PaymentId, decimal ExposureEffect, DateOnly EffectiveDate,
        DateTimeOffset RecordedAt);
}
