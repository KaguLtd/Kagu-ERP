namespace KaguERP.BuildingBlocks.Application.Observability;

public interface IReadinessProbe
{
    ValueTask<ReadinessResult> CheckAsync(CancellationToken cancellationToken = default);
}

public readonly record struct ReadinessResult(bool IsReady, string Status)
{
    public static ReadinessResult Ready { get; } = new(true, "ready");

    public static ReadinessResult NotReady { get; } = new(false, "not_ready");
}
