using System.Security.Claims;
using KaguERP.BuildingBlocks.Application.Security;

namespace KaguERP.Bootstrap;

internal sealed class DenyAllExecutionScopeResolver : IExecutionScopeResolver
{
    public ValueTask<ExecutionScope?> ResolveAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<ExecutionScope?>(null);
}
