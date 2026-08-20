namespace KaguERP.BuildingBlocks.Application.Audit;

public interface IRequestAuditContextAccessor
{
    RequestAuditContext Current { get; }
}
