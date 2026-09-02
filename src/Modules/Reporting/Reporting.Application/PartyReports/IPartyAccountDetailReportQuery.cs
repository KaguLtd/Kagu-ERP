namespace KaguERP.Modules.Reporting.Application.PartyReports;

public sealed record PartyAccountDetailReportQueryRequest(
    Guid CompanyId,
    Guid StatementId,
    Guid AgingReportId);

public enum PartyAccountDetailReportQueryOutcome
{
    Allowed,
    Denied,
    NotFound,
    Unavailable
}

public sealed record PartyReportDimension(string Code, string Value);

public sealed record PartyStatementLine(
    Guid EventId,
    string Kind,
    string SourceType,
    Guid SourceEventId,
    Guid DueScheduleLineId,
    Guid? PaymentId,
    decimal ExposureEffect,
    DateOnly EffectiveDate,
    DateTimeOffset RecordedAt,
    decimal RunningExposure);

public sealed record PartyAgingBucket(
    string Code,
    int MinimumDaysOverdue,
    int MaximumDaysOverdue,
    int ItemCount,
    decimal RemainingAmount);

public sealed record PartyAgingItem(
    Guid OpenItemId,
    Guid SourceEventId,
    Guid DueScheduleLineId,
    decimal OriginalAmount,
    decimal RemainingAmount,
    DateOnly DueDate,
    int DaysOverdue,
    string BucketCode,
    bool IsDisputed,
    bool IsBlocked);

public sealed record PartyAccountDetailReport(
    Guid CrossFootId,
    string ReportCode,
    long ReportDefinitionVersion,
    Guid ProjectionGenerationId,
    Guid CompanyId,
    Guid PartyAccountId,
    Guid ControlAccountId,
    string BalanceSide,
    string Currency,
    DateOnly EffectiveAsOf,
    DateTimeOffset DataCutoffAt,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<PartyReportDimension> Dimensions,
    Guid StatementId,
    decimal OpeningExposure,
    decimal ClosingExposure,
    IReadOnlyList<PartyStatementLine> StatementLines,
    Guid AgingReportId,
    Guid AgingPolicyId,
    long AgingPolicyVersion,
    decimal AgingTotalRemaining,
    IReadOnlyList<PartyAgingBucket> AgingBuckets,
    IReadOnlyList<PartyAgingItem> AgingItems);

public sealed record PartyAccountDetailReportQueryResult(
    PartyAccountDetailReportQueryOutcome Outcome,
    PartyAccountDetailReport? Report);

public interface IPartyAccountDetailReportQuery
{
    ValueTask<PartyAccountDetailReportQueryResult> ExecuteAsync(
        PartyAccountDetailReportQueryRequest request,
        CancellationToken cancellationToken = default);
}
