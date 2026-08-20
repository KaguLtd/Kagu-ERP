namespace KaguERP.BuildingBlocks.Application.Observability;

public interface ICorrelationContextAccessor
{
    CorrelationContext Current { get; }
}
