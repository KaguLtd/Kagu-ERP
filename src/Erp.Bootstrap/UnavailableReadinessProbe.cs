using KaguERP.BuildingBlocks.Application.Observability;

namespace KaguERP.Bootstrap;

internal sealed class UnavailableReadinessProbe : IReadinessProbe
{
    public ValueTask<ReadinessResult> CheckAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(ReadinessResult.NotReady);
}
