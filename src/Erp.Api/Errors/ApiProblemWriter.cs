using System.Text.Json;
using KaguERP.BuildingBlocks.Application.Observability;

namespace KaguERP.Api.Errors;

internal static class ApiProblemWriter
{
    public static async Task WriteAsync(HttpContext context, int status, string code)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        CorrelationContext? correlation = context.Features.Get<CorrelationContext>();
        var response = new
        {
            type = $"https://docs.kagu.local/errors/{code.ToLowerInvariant().Replace('_', '-')}",
            title = status == StatusCodes.Status401Unauthorized ? "Kimlik doğrulama gerekli" : "İstek reddedildi",
            status,
            code,
            traceId = context.TraceIdentifier,
            correlationId = correlation?.Id.ToString("D"),
        };

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            response,
            cancellationToken: context.RequestAborted);
    }
}
