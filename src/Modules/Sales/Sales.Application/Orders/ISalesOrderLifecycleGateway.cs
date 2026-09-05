using KaguERP.Modules.Sales.Domain.Orders;
using KaguERP.BuildingBlocks.Application.Audit;

namespace KaguERP.Modules.Sales.Application.Orders;

public interface ISalesOrderLifecycleGateway
{
    ValueTask<SalesOrderLifecyclePersistenceOutcome> CreateDraftAsync(
        AuthorizedSalesOrderCreateCommand command,
        RequestAuditContext auditContext,
        CancellationToken cancellationToken = default);

    ValueTask<SalesOrderLifecyclePersistenceOutcome> TransitionAsync(
        AuthorizedSalesOrderTransitionCommand command,
        RequestAuditContext auditContext,
        CancellationToken cancellationToken = default);

    ValueTask<SalesOrderLifecycleView> LoadAsync(
        AuthorizedSalesOrderLifecycleQuery query,
        RequestAuditContext auditContext,
        CancellationToken cancellationToken = default);
}

public sealed record SalesOrderLifecyclePersistenceOutcome(
    SalesOrderLifecycleState State,
    SalesOrderCommitment Commitment,
    SalesOrderTransitionEvent? Event,
    bool Created);

public sealed class SalesOrderLifecycleUnavailableException()
    : InvalidOperationException("Sales order lifecycle persistence is unavailable.")
{
    public string Code { get; } = "SALES_ORDER_SERVICE_UNAVAILABLE";
}

public sealed class SalesOrderGatewayNotFoundException()
    : InvalidOperationException("The sales order does not exist in the active scope.")
{
    public string Code { get; } = "SALES_ORDER_NOT_FOUND";
}

public sealed class SalesOrderGatewayConflictException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class UnavailableSalesOrderLifecycleGateway : ISalesOrderLifecycleGateway
{
    public ValueTask<SalesOrderLifecyclePersistenceOutcome> CreateDraftAsync(
        AuthorizedSalesOrderCreateCommand command,
        RequestAuditContext auditContext,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<SalesOrderLifecyclePersistenceOutcome>(
            new SalesOrderLifecycleUnavailableException());

    public ValueTask<SalesOrderLifecyclePersistenceOutcome> TransitionAsync(
        AuthorizedSalesOrderTransitionCommand command,
        RequestAuditContext auditContext,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<SalesOrderLifecyclePersistenceOutcome>(
            new SalesOrderLifecycleUnavailableException());

    public ValueTask<SalesOrderLifecycleView> LoadAsync(
        AuthorizedSalesOrderLifecycleQuery query,
        RequestAuditContext auditContext,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<SalesOrderLifecycleView>(new SalesOrderLifecycleUnavailableException());
}
