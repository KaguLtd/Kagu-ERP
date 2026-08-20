using KaguERP.BuildingBlocks.Application.Security;

namespace KaguERP.Api.Security;

internal sealed class HttpExecutionScopeAccessor(IHttpContextAccessor httpContextAccessor)
    : IExecutionScopeAccessor
{
    public ExecutionScope Current =>
        httpContextAccessor.HttpContext?.Features.Get<ExecutionScope>()
        ?? throw new InvalidOperationException("An authorized execution scope is not available.");
}
