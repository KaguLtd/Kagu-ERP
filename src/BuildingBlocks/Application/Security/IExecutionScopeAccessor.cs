namespace KaguERP.BuildingBlocks.Application.Security;

public interface IExecutionScopeAccessor
{
    ExecutionScope Current { get; }
}
