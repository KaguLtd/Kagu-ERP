using System.Globalization;
using KaguERP.Api.Errors;
using KaguERP.Modules.Reporting.Application.PartyReports;

namespace KaguERP.Api.Reports;

internal sealed record PartyReportQueryApiRequest(
    Guid CompanyId,
    Guid StatementId,
    Guid AgingReportId);

internal sealed record PartyReportDimensionApiResponse(string Code, string Value);

internal sealed record PartyStatementLineApiResponse(
    Guid EventId,
    string Kind,
    string SourceType,
    Guid SourceEventId,
    Guid DueScheduleLineId,
    Guid? PaymentId,
    string ExposureEffect,
    DateOnly EffectiveDate,
    DateTimeOffset RecordedAt,
    string RunningExposure);

internal sealed record PartyAgingBucketApiResponse(
    string Code,
    int MinimumDaysOverdue,
    int MaximumDaysOverdue,
    int ItemCount,
    string RemainingAmount);

internal sealed record PartyAgingItemApiResponse(
    Guid OpenItemId,
    Guid SourceEventId,
    Guid DueScheduleLineId,
    string OriginalAmount,
    string RemainingAmount,
    DateOnly DueDate,
    int DaysOverdue,
    string BucketCode,
    bool IsDisputed,
    bool IsBlocked);

internal sealed record PartyAccountDetailReportApiResponse(
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
    IReadOnlyList<PartyReportDimensionApiResponse> Dimensions,
    Guid StatementId,
    string OpeningExposure,
    string ClosingExposure,
    IReadOnlyList<PartyStatementLineApiResponse> StatementLines,
    Guid AgingReportId,
    Guid AgingPolicyId,
    long AgingPolicyVersion,
    string AgingTotalRemaining,
    IReadOnlyList<PartyAgingBucketApiResponse> AgingBuckets,
    IReadOnlyList<PartyAgingItemApiResponse> AgingItems);

internal static partial class PartyReportQueryEndpoint
{
    internal const string Route = "/api/v1/reports/{code}/queries";

    public static IEndpointRouteBuilder MapPartyReportQueryEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Route, HandleAsync)
            .WithName("QueryPartyAccountReport");
        return endpoints;
    }

    internal static bool IsValidRequest(PartyReportQueryApiRequest? request) =>
        request is not null && request.CompanyId != Guid.Empty &&
        request.StatementId != Guid.Empty && request.AgingReportId != Guid.Empty;

    private static async Task HandleAsync(
        HttpContext context,
        string code,
        PartyReportQueryApiRequest request,
        IPartyAccountDetailReportQuery reportQuery,
        ILogger<PartyReportQueryLogCategory> logger)
    {
        if (!string.Equals(code, PartyAccountDetailReportDefinition.ReportCode, StringComparison.Ordinal))
        {
            await ApiProblemWriter.WriteAsync(
                context,
                StatusCodes.Status404NotFound,
                "REPORT_DEFINITION_NOT_FOUND");
            return;
        }
        if (!IsValidRequest(request))
        {
            await ApiProblemWriter.WriteAsync(
                context,
                StatusCodes.Status400BadRequest,
                "INVALID_PARTY_REPORT_QUERY");
            return;
        }

        try
        {
            PartyAccountDetailReportQueryResult result = await reportQuery.ExecuteAsync(
                new PartyAccountDetailReportQueryRequest(
                    request.CompanyId,
                    request.StatementId,
                    request.AgingReportId),
                context.RequestAborted);
            switch (result.Outcome)
            {
                case PartyAccountDetailReportQueryOutcome.Allowed when result.Report is not null:
                    await Results.Ok(CreateResponse(result.Report)).ExecuteAsync(context);
                    return;
                case PartyAccountDetailReportQueryOutcome.Denied:
                    await ApiProblemWriter.WriteAsync(
                        context,
                        StatusCodes.Status403Forbidden,
                        "PARTY_REPORT_QUERY_DENIED");
                    return;
                case PartyAccountDetailReportQueryOutcome.NotFound:
                    await ApiProblemWriter.WriteAsync(
                        context,
                        StatusCodes.Status404NotFound,
                        "PARTY_REPORT_NOT_FOUND");
                    return;
                default:
                    await ApiProblemWriter.WriteAsync(
                        context,
                        StatusCodes.Status503ServiceUnavailable,
                        "PARTY_REPORT_QUERY_UNAVAILABLE");
                    return;
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogQueryFailure(logger, exception.GetType().Name);
            await ApiProblemWriter.WriteAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "PARTY_REPORT_QUERY_UNAVAILABLE");
        }
    }

    internal static PartyAccountDetailReportApiResponse CreateResponse(PartyAccountDetailReport report) =>
        new(
            report.CrossFootId,
            report.ReportCode,
            report.ReportDefinitionVersion,
            report.ProjectionGenerationId,
            report.CompanyId,
            report.PartyAccountId,
            report.ControlAccountId,
            report.BalanceSide,
            report.Currency,
            report.EffectiveAsOf,
            report.DataCutoffAt,
            report.GeneratedAt,
            report.Dimensions
                .Select(item => new PartyReportDimensionApiResponse(item.Code, item.Value))
                .ToArray(),
            report.StatementId,
            FormatAmount(report.OpeningExposure),
            FormatAmount(report.ClosingExposure),
            report.StatementLines
                .Select(line => new PartyStatementLineApiResponse(
                    line.EventId,
                    line.Kind,
                    line.SourceType,
                    line.SourceEventId,
                    line.DueScheduleLineId,
                    line.PaymentId,
                    FormatAmount(line.ExposureEffect),
                    line.EffectiveDate,
                    line.RecordedAt,
                    FormatAmount(line.RunningExposure)))
                .ToArray(),
            report.AgingReportId,
            report.AgingPolicyId,
            report.AgingPolicyVersion,
            FormatAmount(report.AgingTotalRemaining),
            report.AgingBuckets
                .Select(bucket => new PartyAgingBucketApiResponse(
                    bucket.Code,
                    bucket.MinimumDaysOverdue,
                    bucket.MaximumDaysOverdue,
                    bucket.ItemCount,
                    FormatAmount(bucket.RemainingAmount)))
                .ToArray(),
            report.AgingItems
                .Select(item => new PartyAgingItemApiResponse(
                    item.OpenItemId,
                    item.SourceEventId,
                    item.DueScheduleLineId,
                    FormatAmount(item.OriginalAmount),
                    FormatAmount(item.RemainingAmount),
                    item.DueDate,
                    item.DaysOverdue,
                    item.BucketCode,
                    item.IsDisputed,
                    item.IsBlocked))
                .ToArray());

    internal static string FormatAmount(decimal amount) =>
        amount.ToString("0.0000", CultureInfo.InvariantCulture);

    [LoggerMessage(
        EventId = 1400,
        Level = LogLevel.Error,
        Message = "Party report query failed safely with error type {ErrorType}.")]
    private static partial void LogQueryFailure(ILogger logger, string errorType);
}

internal sealed class PartyReportQueryLogCategory;
