using KaguERP.Api.Errors;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;

namespace KaguERP.Api.Security;

internal static class ExecutionScopeEndpoint
{
    private const string ReadOwnProfilePermission = "profile.read";

    public static IEndpointRouteBuilder MapExecutionScopeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/me/scopes", HandleAsync)
            .WithName("GetExecutionScopes");
        return endpoints;
    }

    private static async Task HandleAsync(
        HttpContext context,
        IExecutionScopeAccessor executionScopeAccessor,
        IRequestAuditContextAccessor auditContextAccessor,
        IAuthorizationAuditWriter auditWriter)
    {
        ExecutionScope scope = executionScopeAccessor.Current;
        Guid[] permittedCompanyIds = scope.CompanyIds
            .Where(companyId => scope.HasPermission(companyId, ReadOwnProfilePermission))
            .Order()
            .ToArray();

        if (permittedCompanyIds.Length == 0)
        {
            await auditWriter.WriteAsync(
                auditContextAccessor.Current,
                new AuthorizationAuditEvent(
                    "iam.scope.read",
                    "current-user-scope",
                    scope.ActorId.ToString("D"),
                    "denied",
                    "PERMISSION_REQUIRED"),
                context.RequestAborted);
            await ApiProblemWriter.WriteAsync(
                context,
                StatusCodes.Status403Forbidden,
                "PERMISSION_REQUIRED");
            return;
        }

        await auditWriter.WriteAsync(
            auditContextAccessor.Current with { CompanyIds = permittedCompanyIds.ToHashSet() },
            new AuthorizationAuditEvent(
                "iam.scope.read",
                "current-user-scope",
                scope.ActorId.ToString("D"),
                "allowed",
                "PROFILE_READ_GRANTED"),
            context.RequestAborted);

        await Results.Ok(new
        {
            tenantId = scope.TenantId,
            actorId = scope.ActorId,
            companyIds = permittedCompanyIds,
        }).ExecuteAsync(context);
    }
}
