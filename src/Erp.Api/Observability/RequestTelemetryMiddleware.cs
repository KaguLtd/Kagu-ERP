using System.Diagnostics;
using KaguERP.BuildingBlocks.Application.Observability;
using Microsoft.AspNetCore.Routing;

namespace KaguERP.Api.Observability;

public sealed partial class RequestTelemetryMiddleware(
    RequestDelegate next,
    ILogger<RequestTelemetryMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        long started = Stopwatch.GetTimestamp();
        int statusCode = StatusCodes.Status500InternalServerError;

        try
        {
            await next(context);
            statusCode = context.Response.StatusCode;
        }
        finally
        {
            double elapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            string route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            string correlationId = context.Features.Get<CorrelationContext>()?.Id.ToString("D") ?? "unavailable";

            Activity.Current?.SetTag("http.route", route);
            RequestTelemetry.Record(context.Request.Method, route, statusCode, elapsedMilliseconds);
            LogRequestCompleted(
                logger,
                context.Request.Method,
                route,
                statusCode,
                elapsedMilliseconds,
                correlationId);
        }
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "HTTP request completed {RequestMethod} {Route} {StatusCode} in {ElapsedMilliseconds} ms with correlation {CorrelationId}")]
    private static partial void LogRequestCompleted(
        ILogger logger,
        string requestMethod,
        string route,
        int statusCode,
        double elapsedMilliseconds,
        string correlationId);
}
