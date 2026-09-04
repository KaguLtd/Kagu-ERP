using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Sales.Domain.Orders;

namespace KaguERP.Modules.Sales.Application.Orders;

public sealed class AuthorizedSalesOrderCreateCommand
{
    public const string RequiredPermission = "sales.order.create";

    private AuthorizedSalesOrderCreateCommand(ExecutionScope scope, Guid companyId, Guid orderId)
    {
        Scope = scope;
        CompanyId = companyId;
        OrderId = orderId;
    }

    public ExecutionScope Scope { get; }
    public Guid CompanyId { get; }
    public Guid OrderId { get; }

    public static AuthorizedSalesOrderCreateCommand Create(
        ExecutionScope scope,
        Guid companyId,
        Guid orderId)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, RequiredPermission))
        {
            throw new SalesOrderAuthorizationException(
                "SALES_ORDER_CREATE_PERMISSION_REQUIRED",
                "The active actor cannot create sales orders for this company.");
        }
        if (orderId == Guid.Empty)
        {
            throw new SalesOrderAuthorizationException(
                "SALES_ORDER_ID_REQUIRED",
                "Sales order identity is required.");
        }

        return new AuthorizedSalesOrderCreateCommand(scope, companyId, orderId);
    }
}

public sealed class AuthorizedSalesOrderTransitionCommand
{
    private AuthorizedSalesOrderTransitionCommand(
        ExecutionScope scope,
        Guid companyId,
        Guid orderId,
        long expectedVersion,
        SalesOrderTransition transition,
        Guid correlationId,
        DateTimeOffset occurredAt,
        string? reason)
    {
        Scope = scope;
        CompanyId = companyId;
        OrderId = orderId;
        ExpectedVersion = expectedVersion;
        Transition = transition;
        CorrelationId = correlationId;
        OccurredAt = occurredAt;
        Reason = reason;
    }

    public ExecutionScope Scope { get; }
    public Guid CompanyId { get; }
    public Guid OrderId { get; }
    public long ExpectedVersion { get; }
    public SalesOrderTransition Transition { get; }
    public Guid CorrelationId { get; }
    public DateTimeOffset OccurredAt { get; }
    public string? Reason { get; }

    public static AuthorizedSalesOrderTransitionCommand Create(
        ExecutionScope scope,
        Guid companyId,
        Guid orderId,
        long expectedVersion,
        SalesOrderTransition transition,
        Guid correlationId,
        DateTimeOffset occurredAt,
        string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.EnsureAllowed(scope.TenantId, companyId);
        string permission = PermissionFor(transition);
        if (!scope.HasPermission(companyId, permission))
        {
            throw new SalesOrderAuthorizationException(
                "SALES_ORDER_TRANSITION_PERMISSION_REQUIRED",
                "The active actor cannot perform this sales order transition.");
        }
        if (orderId == Guid.Empty || expectedVersion <= 0 || correlationId == Guid.Empty ||
            occurredAt.Offset != TimeSpan.Zero || occurredAt.Ticks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new SalesOrderAuthorizationException(
                "SALES_ORDER_TRANSITION_COMMAND_INVALID",
                "Sales order transition command requires identity, version, correlation and PostgreSQL-safe UTC time.");
        }
        if (transition is SalesOrderTransition.RecordPartialFulfilment or
            SalesOrderTransition.RecordFullFulfilment)
        {
            throw new SalesOrderAuthorizationException(
                "SALES_ORDER_FULFILMENT_PERSISTENCE_NOT_READY",
                "Fulfilment transition requires authoritative persisted allocation evidence.");
        }

        return new AuthorizedSalesOrderTransitionCommand(
            scope,
            companyId,
            orderId,
            expectedVersion,
            transition,
            correlationId,
            occurredAt,
            reason);
    }

    public static string PermissionFor(SalesOrderTransition transition) => transition switch
    {
        SalesOrderTransition.Submit or SalesOrderTransition.Withdraw or SalesOrderTransition.Revise =>
            "sales.order.submit",
        SalesOrderTransition.Approve or SalesOrderTransition.Reject => "sales.order.approve",
        SalesOrderTransition.Confirm => "sales.order.confirm",
        SalesOrderTransition.Cancel => "sales.order.cancel",
        SalesOrderTransition.Close => "sales.order.close",
        SalesOrderTransition.RecordPartialFulfilment or SalesOrderTransition.RecordFullFulfilment =>
            "sales.fulfilment.record",
        _ => throw new SalesOrderAuthorizationException(
            "SALES_ORDER_TRANSITION_INVALID",
            "Sales order transition is invalid."),
    };
}

public sealed class SalesOrderAuthorizationException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
