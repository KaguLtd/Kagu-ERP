using System.Security.Cryptography;
using System.Text;
using KaguERP.Modules.Parties.Contracts.Reports;
using KaguERP.Modules.Reporting.Domain.ControlAccounts;
using KaguERP.Modules.Reporting.Domain.PartyReports;

namespace KaguERP.Modules.Reporting.Application.PartyReports;

public static class PartyReportRefreshPermissions
{
    public const string Refresh = "reporting.party-account.refresh";
}

public interface IPartyReportRefreshFailure
{
    string Code { get; }
}

public sealed record PartyReportRefreshRequest
{
    private PartyReportRefreshRequest(
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        string reportCode,
        long reportDefinitionVersion,
        DateOnly effectiveAsOf,
        DateTimeOffset recordedCutoff,
        Guid projectionGenerationId,
        Guid statementId,
        Guid agingReportId,
        Guid partyCrossFootId,
        Guid controlAccountReconciliationId,
        DateTimeOffset generatedAt,
        string generationReason,
        DateTimeOffset scheduledFor,
        string timezoneName,
        string businessCalendarCode,
        string missedRunPolicy)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        PartyAccountId = partyAccountId;
        ReportCode = reportCode;
        ReportDefinitionVersion = reportDefinitionVersion;
        EffectiveAsOf = effectiveAsOf;
        RecordedCutoff = recordedCutoff;
        ProjectionGenerationId = projectionGenerationId;
        StatementId = statementId;
        AgingReportId = agingReportId;
        PartyCrossFootId = partyCrossFootId;
        ControlAccountReconciliationId = controlAccountReconciliationId;
        GeneratedAt = generatedAt;
        GenerationReason = generationReason;
        ScheduledFor = scheduledFor;
        TimezoneName = timezoneName;
        BusinessCalendarCode = businessCalendarCode;
        MissedRunPolicy = missedRunPolicy;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid PartyAccountId { get; }
    public string ReportCode { get; }
    public long ReportDefinitionVersion { get; }
    public DateOnly EffectiveAsOf { get; }
    public DateTimeOffset RecordedCutoff { get; }
    public Guid ProjectionGenerationId { get; }
    public Guid StatementId { get; }
    public Guid AgingReportId { get; }
    public Guid PartyCrossFootId { get; }
    public Guid ControlAccountReconciliationId { get; }
    public DateTimeOffset GeneratedAt { get; }
    public string GenerationReason { get; }
    public DateTimeOffset ScheduledFor { get; }
    public string TimezoneName { get; }
    public string BusinessCalendarCode { get; }
    public string MissedRunPolicy { get; }

    public static PartyReportRefreshRequest Create(
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        string reportCode,
        long reportDefinitionVersion,
        DateOnly effectiveAsOf,
        DateTimeOffset recordedCutoff,
        Guid projectionGenerationId,
        Guid statementId,
        Guid agingReportId,
        Guid partyCrossFootId,
        Guid controlAccountReconciliationId,
        DateTimeOffset generatedAt,
        string generationReason,
        DateTimeOffset scheduledFor,
        string timezoneName,
        string businessCalendarCode,
        string missedRunPolicy)
    {
        RequireId(tenantId, nameof(tenantId));
        RequireId(companyId, nameof(companyId));
        RequireId(partyAccountId, nameof(partyAccountId));
        RequireId(projectionGenerationId, nameof(projectionGenerationId));
        RequireId(statementId, nameof(statementId));
        RequireId(agingReportId, nameof(agingReportId));
        RequireId(partyCrossFootId, nameof(partyCrossFootId));
        RequireId(controlAccountReconciliationId, nameof(controlAccountReconciliationId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reportDefinitionVersion);
        if (effectiveAsOf == default)
        {
            throw new ArgumentException("Effective as-of date is required.", nameof(effectiveAsOf));
        }
        RequireUtc(recordedCutoff, nameof(recordedCutoff));
        RequireUtc(generatedAt, nameof(generatedAt));
        RequireUtc(scheduledFor, nameof(scheduledFor));

        string normalizedReportCode = RequireText(reportCode, 120, nameof(reportCode));
        string normalizedReason = RequireText(generationReason, 240, nameof(generationReason));
        string normalizedTimezone = RequireText(timezoneName, 120, nameof(timezoneName));
        string normalizedCalendar = RequireText(businessCalendarCode, 120, nameof(businessCalendarCode));
        string normalizedMissedRunPolicy = RequireText(missedRunPolicy, 20, nameof(missedRunPolicy));
        if (normalizedMissedRunPolicy is not ("skip" or "run-once" or "catch-up"))
        {
            throw new ArgumentException("Missed-run policy must be skip, run-once or catch-up.", nameof(missedRunPolicy));
        }

        return new PartyReportRefreshRequest(
            tenantId,
            companyId,
            partyAccountId,
            normalizedReportCode,
            reportDefinitionVersion,
            effectiveAsOf,
            recordedCutoff,
            projectionGenerationId,
            statementId,
            agingReportId,
            partyCrossFootId,
            controlAccountReconciliationId,
            generatedAt,
            normalizedReason,
            scheduledFor,
            normalizedTimezone,
            normalizedCalendar,
            normalizedMissedRunPolicy);
    }

    public PartyReportProjectionJobCommand ToProjectionCommand() => new(
        new PartyReportSourceQuery(TenantId, CompanyId, PartyAccountId, EffectiveAsOf, RecordedCutoff),
        ReportCode,
        ReportDefinitionVersion,
        ProjectionGenerationId,
        StatementId,
        AgingReportId,
        PartyCrossFootId,
        ControlAccountReconciliationId,
        GeneratedAt,
        GenerationReason);

    public string ComputeFingerprintSha256()
    {
        string canonical = string.Join('\n',
            "party-report-refresh-v1",
            TenantId.ToString("N"),
            CompanyId.ToString("N"),
            PartyAccountId.ToString("N"),
            ReportCode,
            ReportDefinitionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            EffectiveAsOf.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            RecordedCutoff.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ProjectionGenerationId.ToString("N"),
            StatementId.ToString("N"),
            AgingReportId.ToString("N"),
            PartyCrossFootId.ToString("N"),
            ControlAccountReconciliationId.ToString("N"),
            GeneratedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            GenerationReason,
            ScheduledFor.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            TimezoneName,
            BusinessCalendarCode,
            MissedRunPolicy);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static void RequireId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A non-empty ID is required.", parameterName);
        }
    }

    private static void RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The timestamp must use the UTC offset.", parameterName);
        }
    }

    private static string RequireText(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-blank value is required.", parameterName);
        }
        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"The value cannot exceed {maximumLength} characters.", parameterName);
        }
        return normalized;
    }
}

public sealed record PartyReportRefreshEnqueueCommand(
    Guid WorkItemId,
    string RequestKey,
    PartyReportRefreshRequest Request,
    int MaxAttempts,
    DateTimeOffset AvailableAt,
    DateTimeOffset CreatedAt,
    Guid CreatedBy);

public sealed record PartyReportRefreshEnqueueResult(Guid WorkItemId, bool Created);

public sealed record PartyReportRefreshLease(
    Guid WorkItemId,
    PartyReportRefreshRequest Request,
    Guid LeaseToken,
    int AttemptNumber,
    int MaxAttempts);

public interface IPartyReportRefreshWorkStore
{
    ValueTask<PartyReportRefreshEnqueueResult> EnqueueAsync(
        PartyReportRefreshEnqueueCommand command,
        CancellationToken cancellationToken = default);

    ValueTask<PartyReportRefreshLease?> TryClaimAsync(
        DateTimeOffset claimedAt,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    ValueTask CompleteAsync(
        PartyReportRefreshLease lease,
        PartyReportProjectionJobResult result,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default);

    ValueTask<bool> FailAsync(
        PartyReportRefreshLease lease,
        string errorCode,
        DateTimeOffset failedAt,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default);
}

public enum PartyReportRefreshCycleDisposition
{
    Idle = 0,
    Completed = 1,
    RetryScheduled = 2,
    Failed = 3,
}

public sealed record PartyReportRefreshCycleResult(
    PartyReportRefreshCycleDisposition Disposition,
    Guid? WorkItemId,
    int? AttemptNumber,
    string? ErrorCode)
{
    public static PartyReportRefreshCycleResult Idle { get; } =
        new(PartyReportRefreshCycleDisposition.Idle, null, null, null);
}

public interface IPartyReportRefreshCycle
{
    ValueTask<PartyReportRefreshCycleResult> ProcessNextAsync(
        CancellationToken cancellationToken = default);
}

public sealed class PartyReportRefreshProcessor(
    IPartyReportRefreshWorkStore queue,
    PartyReportProjectionJob projectionJob,
    TimeProvider timeProvider,
    TimeSpan leaseDuration) : IPartyReportRefreshCycle
{
    public async ValueTask<PartyReportRefreshCycleResult> ProcessNextAsync(
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        PartyReportRefreshLease? lease = await queue.TryClaimAsync(now, leaseDuration, cancellationToken);
        if (lease is null)
        {
            return PartyReportRefreshCycleResult.Idle;
        }

        try
        {
            PartyReportProjectionJobResult result = await projectionJob.RunAsync(
                lease.Request.ToProjectionCommand(),
                cancellationToken);
            await queue.CompleteAsync(lease, result, timeProvider.GetUtcNow(), cancellationToken);
            return new PartyReportRefreshCycleResult(
                PartyReportRefreshCycleDisposition.Completed,
                lease.WorkItemId,
                lease.AttemptNumber,
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            string errorCode = SafeErrorCode(exception);
            TimeSpan retryDelay = ComputeRetryDelay(lease.AttemptNumber);
            bool willRetry = await queue.FailAsync(
                lease,
                errorCode,
                timeProvider.GetUtcNow(),
                retryDelay,
                cancellationToken);
            return new PartyReportRefreshCycleResult(
                willRetry
                    ? PartyReportRefreshCycleDisposition.RetryScheduled
                    : PartyReportRefreshCycleDisposition.Failed,
                lease.WorkItemId,
                lease.AttemptNumber,
                errorCode);
        }
    }

    private static TimeSpan ComputeRetryDelay(int attemptNumber)
    {
        int exponent = Math.Clamp(attemptNumber - 1, 0, 6);
        return TimeSpan.FromSeconds(Math.Min(300, 5 * (1 << exponent)));
    }

    private static string SafeErrorCode(Exception exception) => exception switch
    {
        PartyReportProjectionJobException jobException => jobException.Code,
        ReportingInvariantException invariantException => invariantException.Code,
        IPartyReportRefreshFailure refreshFailure => refreshFailure.Code,
        _ => "PARTY_REPORT_REFRESH_FAILED",
    };
}
