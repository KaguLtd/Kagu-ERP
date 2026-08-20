using System.Diagnostics;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Observability;
using KaguERP.BuildingBlocks.Application.Security;

namespace KaguERP.Api.Audit;

internal sealed class HttpRequestAuditContextAccessor(
    IHttpContextAccessor httpContextAccessor,
    ICorrelationContextAccessor correlationContextAccessor,
    IExecutionScopeAccessor executionScopeAccessor)
    : IRequestAuditContextAccessor
{
    public RequestAuditContext Current
    {
        get
        {
            HttpContext context = httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("An HTTP request context is not available.");
            CorrelationContext correlation = correlationContextAccessor.Current;
            ExecutionScope executionScope = executionScopeAccessor.Current;
            string traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
            string? sessionId = context.User.FindFirst("sid")?.Value;

            return new RequestAuditContext(
                correlation.Id,
                traceId,
                executionScope.TenantId,
                executionScope.ActorId,
                executionScope.CompanyIds,
                sessionId);
        }
    }
}
