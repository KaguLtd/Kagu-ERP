using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Sales.Domain.Orders;

namespace KaguERP.Modules.Sales.Application.Orders;

public sealed class AuthorizedSalesDispatchPreparation
{
    public const string RequiredPermission = "dispatch.create";

    private AuthorizedSalesDispatchPreparation(ExecutionScope scope, SalesDispatchPreparation preparation)
    {
        Scope = scope;
        Preparation = preparation;
    }

    public ExecutionScope Scope { get; }
    public SalesDispatchPreparation Preparation { get; }

    public static AuthorizedSalesDispatchPreparation Create(
        ExecutionScope scope,
        SalesOrderLifecycleState order,
        long expectedVersion,
        SalesOrderCommitment commitment,
        SalesOrderFulfilmentEvidence fulfilment,
        IEnumerable<SalesDispatchLineRequest> requestedLines)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(order);
        scope.EnsureAllowed(order.TenantId, order.CompanyId);
        if (!scope.HasPermission(order.CompanyId, RequiredPermission))
        {
            throw new SalesOrderAuthorizationException(
                "SALES_DISPATCH_CREATE_PERMISSION_REQUIRED",
                "The active actor cannot prepare dispatches for this company.");
        }

        return new AuthorizedSalesDispatchPreparation(scope,
            SalesDispatchPreparation.Create(order, expectedVersion, commitment, fulfilment, requestedLines));
    }
}
