using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Modules.Sales.Domain.Orders;

namespace KaguERP.Modules.Sales.Application.Orders;

/// <summary>Stock-only use cases. The implementation owns the order/reservation/audit transaction.</summary>
public interface ISalesStockOrderGateway
{
    ValueTask<SalesStockOrderConfirmationOutcome> ConfirmAsync(
        SalesStockOrderConfirmationCommand command, RequestAuditContext auditContext,
        CancellationToken cancellationToken = default);

    ValueTask<SalesStockOrderCancellationOutcome> CancelAsync(
        SalesStockOrderCancellationCommand command, RequestAuditContext auditContext,
        CancellationToken cancellationToken = default);
}

// Transport input carries no actor, tenant, item, UOM, reservation key or source version.
// Those values come from trusted execution scope and the persisted order commitment.
public sealed record SalesStockOrderLineSelection(Guid OrderLineId, Guid WarehouseId, decimal Quantity);

public sealed class SalesStockOrderConfirmationCommand
{
    public SalesStockOrderConfirmationCommand(AuthorizedSalesOrderTransitionCommand transition,
        DateOnly effectiveDate, IReadOnlyList<SalesStockOrderLineSelection> lines)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(lines);
        if (transition.Transition != SalesOrderTransition.Confirm || transition.ExpectedVersion == long.MaxValue)
            throw new ArgumentException("Stock confirmation requires a confirm transition with an incrementable version.", nameof(transition));
        if (lines.Count is < 1 or > 500)
            throw new ArgumentException("Stock confirmation requires 1–500 line selections.", nameof(lines));
        var snapshot = lines.ToArray();
        if (snapshot.Any(line => line is null || line.OrderLineId == Guid.Empty || line.WarehouseId == Guid.Empty) ||
            snapshot.Select(line => line.OrderLineId).Distinct().Count() != snapshot.Length)
            throw new ArgumentException("Stock confirmation requires unique order lines and warehouse identities.", nameof(lines));
        foreach (var line in snapshot)
            _ = SalesOrderQuantity.Create(line.Quantity);
        Transition = transition;
        EffectiveDate = effectiveDate;
        Lines = Array.AsReadOnly(snapshot);
    }

    public AuthorizedSalesOrderTransitionCommand Transition { get; }
    public DateOnly EffectiveDate { get; }
    public IReadOnlyList<SalesStockOrderLineSelection> Lines { get; }
}

public sealed class SalesStockOrderCancellationCommand
{
    public SalesStockOrderCancellationCommand(AuthorizedSalesOrderTransitionCommand transition, DateOnly effectiveDate)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (transition.Transition != SalesOrderTransition.Cancel || transition.ExpectedVersion == long.MaxValue ||
            (transition.Reason?.Trim().Length ?? 0) is < 1 or > 500)
            throw new ArgumentException("Stock cancellation requires a cancel transition, incrementable version and reason.", nameof(transition));
        Transition = transition;
        EffectiveDate = effectiveDate;
    }

    public AuthorizedSalesOrderTransitionCommand Transition { get; }
    public DateOnly EffectiveDate { get; }
}

public sealed record SalesStockOrderReservationOutcome(Guid OrderLineId, Guid WarehouseId,
    Guid RequestId, Guid? ReservationId, decimal RequestedQuantity, decimal ReservedQuantity,
    DateTimeOffset RecordedAt);

public sealed record SalesStockOrderReleaseOutcome(Guid ReservationId, Guid EventId, long Version,
    decimal ReleasedQuantity, decimal ConsumedQuantity, DateTimeOffset RecordedAt);

public sealed record SalesStockOrderConfirmationOutcome(SalesOrderLifecyclePersistenceOutcome Order,
    IReadOnlyList<SalesStockOrderReservationOutcome> Reservations);

public sealed record SalesStockOrderCancellationOutcome(SalesOrderLifecyclePersistenceOutcome Order,
    IReadOnlyList<SalesStockOrderReleaseOutcome> Releases);

public sealed class SalesStockOrderAccessException()
    : InvalidOperationException("The active actor cannot perform this stock-order operation in the requested scope.")
{
    public string Code { get; } = "SALES_STOCK_ORDER_ACCESS_DENIED";
}

public sealed class SalesStockOrderUnavailableException()
    : InvalidOperationException("Stock-order persistence is unavailable. Retry with the same operation identity.")
{
    public string Code { get; } = "SALES_STOCK_ORDER_SERVICE_UNAVAILABLE";
}

public sealed class UnavailableSalesStockOrderGateway : ISalesStockOrderGateway
{
    public ValueTask<SalesStockOrderConfirmationOutcome> ConfirmAsync(SalesStockOrderConfirmationCommand command,
        RequestAuditContext auditContext, CancellationToken cancellationToken = default) =>
        ValueTask.FromException<SalesStockOrderConfirmationOutcome>(new SalesStockOrderUnavailableException());

    public ValueTask<SalesStockOrderCancellationOutcome> CancelAsync(SalesStockOrderCancellationCommand command,
        RequestAuditContext auditContext, CancellationToken cancellationToken = default) =>
        ValueTask.FromException<SalesStockOrderCancellationOutcome>(new SalesStockOrderUnavailableException());
}
