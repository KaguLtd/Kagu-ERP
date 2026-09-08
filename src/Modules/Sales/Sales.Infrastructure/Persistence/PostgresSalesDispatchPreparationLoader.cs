using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using Npgsql;

namespace KaguERP.Modules.Sales.Infrastructure.Persistence;

/// <summary>
/// Read-only first-dispatch preparation. This result cannot authorize stock posting.
/// Replace the confirmed-only boundary with persisted allocation loading before enabling dispatch posting.
/// </summary>
public static class PostgresSalesDispatchPreparationLoader
{
    public static async ValueTask<AuthorizedSalesDispatchPreparation> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        Guid orderId,
        long expectedVersion,
        IEnumerable<SalesDispatchLineRequest> requestedLines,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, AuthorizedSalesDispatchPreparation.RequiredPermission))
        {
            throw new SalesOrderAuthorizationException(
                "SALES_DISPATCH_CREATE_PERMISSION_REQUIRED",
                "The active actor cannot prepare dispatches for this company.");
        }
        var query = AuthorizedSalesOrderLifecycleQuery.Create(scope, companyId, orderId);
        ArgumentNullException.ThrowIfNull(requestedLines);
        if (expectedVersion <= 0)
        {
            throw new SalesOrderLifecycleException("SALES_DISPATCH_VERSION_CONFLICT",
                "Dispatch preparation requires a positive expected order version.");
        }
        SalesOrderLifecycleView view = await PostgresSalesOrderLifecycleLoader.LoadAsync(
            connection, transaction, query, cancellationToken);
        if (view.State.Version != expectedVersion)
        {
            throw new SalesOrderLifecycleException("SALES_DISPATCH_VERSION_CONFLICT",
                "Dispatch preparation requires the current order version.");
        }
        if (view.State.Status != SalesOrderStatus.Confirmed || view.Transitions.Any(
            transition => transition.Transition is SalesOrderTransition.RecordPartialFulfilment or
                SalesOrderTransition.RecordFullFulfilment))
        {
            throw new SalesOrderLifecycleException("SALES_DISPATCH_FIRST_PREPARATION_ONLY",
                "Only confirmed orders without fulfilment history support this preparation query.");
        }

        var evidence = SalesOrderFulfilmentEvidence.Create(scope.TenantId, companyId, orderId,
            view.Commitment.Lines, []);
        return AuthorizedSalesDispatchPreparation.Create(scope, view.State, expectedVersion,
            view.Commitment, evidence, requestedLines);
    }
}
