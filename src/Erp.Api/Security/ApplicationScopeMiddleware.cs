using KaguERP.Api.Errors;
using KaguERP.BuildingBlocks.Application.Security;

namespace KaguERP.Api.Security;

public sealed class ApplicationScopeMiddleware(RequestDelegate next)
{
    private static readonly string[] UntrustedScopeHeaders = ["X-Tenant-Id", "X-Company-Id"];

    public async Task InvokeAsync(
        HttpContext context,
        IExecutionScopeResolver executionScopeResolver)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1"))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await ApiProblemWriter.WriteAsync(context, StatusCodes.Status401Unauthorized, "AUTHENTICATION_REQUIRED");
            return;
        }

        if (UntrustedScopeHeaders.Any(context.Request.Headers.ContainsKey))
        {
            await ApiProblemWriter.WriteAsync(context, StatusCodes.Status400BadRequest, "UNTRUSTED_SCOPE_HEADER");
            return;
        }

        ExecutionScope? executionScope = await executionScopeResolver.ResolveAsync(
            context.User,
            context.RequestAborted);
        if (executionScope is null)
        {
            await ApiProblemWriter.WriteAsync(context, StatusCodes.Status403Forbidden, "APPLICATION_SCOPE_REQUIRED");
            return;
        }

        context.Features.Set(executionScope);
        await next(context);
    }
}
