using KaguERP.BuildingBlocks.Application.Observability;

namespace KaguERP.Api.Observability;

internal sealed class HttpCorrelationContextAccessor(IHttpContextAccessor httpContextAccessor)
    : ICorrelationContextAccessor
{
    public CorrelationContext Current =>
        httpContextAccessor.HttpContext?.Features.Get<CorrelationContext>()
        ?? throw new InvalidOperationException("A correlation context is not available.");
}
