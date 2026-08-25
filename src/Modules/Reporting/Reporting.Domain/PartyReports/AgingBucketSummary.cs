namespace KaguERP.Modules.Reporting.Domain.PartyReports;

public sealed record AgingBucketSummary(string BucketCode, int ItemCount, decimal RemainingAmount);
