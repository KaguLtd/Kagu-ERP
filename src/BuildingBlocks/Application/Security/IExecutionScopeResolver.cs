using System.Security.Claims;

namespace KaguERP.BuildingBlocks.Application.Security;

public interface IExecutionScopeResolver
{
    ValueTask<ExecutionScope?> ResolveAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
