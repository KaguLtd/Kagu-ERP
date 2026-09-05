using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Sales.Domain.Orders;

namespace KaguERP.Modules.Sales.Application.Orders;

public sealed class AuthorizedSalesOrderLifecycleQuery
{
    public const string RequiredPermission = "sales.order.view";

    private AuthorizedSalesOrderLifecycleQuery(ExecutionScope scope, Guid companyId, Guid orderId)
    {
        Scope = scope;
        CompanyId = companyId;
        OrderId = orderId;
    }

    public ExecutionScope Scope { get; }
    public Guid CompanyId { get; }
    public Guid OrderId { get; }

    public static AuthorizedSalesOrderLifecycleQuery Create(
        ExecutionScope scope,
        Guid companyId,
        Guid orderId)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, RequiredPermission))
        {
            throw new SalesOrderAuthorizationException(
                "SALES_ORDER_VIEW_PERMISSION_REQUIRED",
                "The active actor cannot view sales orders for this company.");
        }
        if (orderId == Guid.Empty)
        {
            throw new SalesOrderAuthorizationException(
                "SALES_ORDER_ID_REQUIRED",
                "Sales order identity is required.");
        }

        return new AuthorizedSalesOrderLifecycleQuery(scope, companyId, orderId);
    }
}

public sealed class SalesOrderLifecycleView
{
    public SalesOrderLifecycleView(
        SalesOrderLifecycleState state,
        IEnumerable<SalesOrderLineCommitment> lines,
        IEnumerable<SalesOrderTransitionEvent> transitions)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(transitions);
        SalesOrderCommitment commitment = SalesOrderCommitment.Create(
            state.TenantId,
            state.CompanyId,
            state.OrderId,
            lines);
        SalesOrderTransitionEvent[] materialized = transitions.ToArray();
        long expectedVersion = 2;
        SalesOrderStatus expectedPreviousStatus = SalesOrderStatus.Draft;
        foreach (SalesOrderTransitionEvent transition in materialized)
        {
            if (transition.TenantId != state.TenantId || transition.CompanyId != state.CompanyId ||
                transition.OrderId != state.OrderId || transition.NewVersion != expectedVersion ||
                transition.PreviousVersion != expectedVersion - 1 ||
                transition.PreviousStatus != expectedPreviousStatus)
            {
                throw new SalesOrderLifecycleViewException(
                    "SALES_ORDER_TIMELINE_INVALID",
                    "Sales order transition history is incomplete, out of order or outside its scope.");
            }

            expectedPreviousStatus = transition.NewStatus;
            expectedVersion++;
        }
        if (state.Version != expectedVersion - 1 || state.Status != expectedPreviousStatus)
        {
            throw new SalesOrderLifecycleViewException(
                "SALES_ORDER_TIMELINE_STATE_MISMATCH",
                "Sales order current state does not match its transition history.");
        }

        State = state;
        Commitment = commitment;
        Transitions = Array.AsReadOnly(materialized);
    }

    public SalesOrderLifecycleState State { get; }
    public SalesOrderCommitment Commitment { get; }
    public IReadOnlyList<SalesOrderTransitionEvent> Transitions { get; }
}

public sealed class SalesOrderLifecycleViewException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
