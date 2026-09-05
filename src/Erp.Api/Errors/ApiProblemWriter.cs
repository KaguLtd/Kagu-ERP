using System.Text.Json;
using System.Text.Json.Serialization;
using KaguERP.BuildingBlocks.Application.Observability;

namespace KaguERP.Api.Errors;

internal sealed record ApiProblemResponse(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("status"), JsonNumberHandling(JsonNumberHandling.Strict)] int Status,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("traceId")] string TraceId,
    [property: JsonPropertyName("correlationId")] string? CorrelationId);

internal static class ApiProblemWriter
{
    public static async Task WriteAsync(HttpContext context, int status, string code)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        CorrelationContext? correlation = context.Features.Get<CorrelationContext>();
        var response = new ApiProblemResponse(
            $"https://docs.kagu.local/errors/{code.ToLowerInvariant().Replace('_', '-')}",
            status == StatusCodes.Status401Unauthorized ? "Kimlik doğrulama gerekli" : "İstek reddedildi",
            status,
            code,
            context.TraceIdentifier,
            correlation?.Id.ToString("D"));

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            response,
            cancellationToken: context.RequestAborted);
    }
}
