using KaguERP.BuildingBlocks.Application.Observability;

namespace KaguERP.Api.Observability;

internal static class ReadinessEndpoint
{
    public static IEndpointRouteBuilder MapKaguErpHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => Results.Ok(new { status = "ok" }))
            .WithName("GetLiveness");
        endpoints.MapGet("/health/ready", CheckReadinessAsync)
            .WithName("GetReadiness");
        return endpoints;
    }

    private static async Task<IResult> CheckReadinessAsync(
        IReadinessProbe readinessProbe,
        CancellationToken cancellationToken)
    {
        ReadinessResult result = await readinessProbe.CheckAsync(cancellationToken);
        return Results.Json(
            new { status = result.Status },
            statusCode: result.IsReady
                ? StatusCodes.Status200OK
                : StatusCodes.Status503ServiceUnavailable);
    }
}
