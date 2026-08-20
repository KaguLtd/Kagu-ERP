using System.Diagnostics;
using KaguERP.Api.Errors;
using KaguERP.BuildingBlocks.Application.Observability;
using Microsoft.Extensions.Primitives;

namespace KaguERP.Api.Observability;

public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        StringValues suppliedValues = context.Request.Headers[HeaderName];
        Guid correlationId;

        if (suppliedValues.Count == 0)
        {
            correlationId = Guid.CreateVersion7();
        }
        else if (suppliedValues.Count != 1 ||
                 !Guid.TryParseExact(suppliedValues[0], "D", out correlationId) ||
                 correlationId == Guid.Empty)
        {
            await ApiProblemWriter.WriteAsync(
                context,
                StatusCodes.Status400BadRequest,
                "INVALID_CORRELATION_ID");
            return;
        }

        var correlation = new CorrelationContext(correlationId);
        context.Features.Set(correlation);
        Activity.Current?.SetTag("correlation.id", correlationId.ToString("D"));
        context.Response.Headers[HeaderName] = correlationId.ToString("D");

        await next(context);
    }
}
