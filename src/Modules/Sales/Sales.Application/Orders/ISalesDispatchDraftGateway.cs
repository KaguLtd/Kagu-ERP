using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;

namespace KaguERP.Modules.Sales.Application.Orders;

/// <summary>Draft/read preparation only. No operation posts a dispatch or consumes stock.</summary>
public interface ISalesDispatchDraftGateway
{
    ValueTask<SalesDispatchDraftOutcome> CreateAsync(AuthorizedSalesDispatchDraftCommand command,
        RequestAuditContext audit, CancellationToken cancellationToken = default);
    ValueTask<SalesDispatchDraftSnapshot> LoadAsync(AuthorizedSalesDispatchDraftQuery query,
        RequestAuditContext audit, CancellationToken cancellationToken = default);
    ValueTask<SalesDispatchReservationPreviewOutcome> PreviewAsync(AuthorizedSalesDispatchDraftQuery query,
        RequestAuditContext audit, CancellationToken cancellationToken = default);
    ValueTask<SalesDispatchDraftPreparationOutcome> PrepareAsync(AuthorizedSalesDispatchDraftCommand command,
        RequestAuditContext audit, CancellationToken cancellationToken = default);
}

public sealed class AuthorizedSalesDispatchDraftQuery
{
    public AuthorizedSalesDispatchDraftQuery(ExecutionScope scope, Guid companyId, Guid dispatchId)
    {
        AuthorizedSalesDispatchDraftCommand.EnsurePermission(scope, companyId);
        if (dispatchId == Guid.Empty)
            throw new ArgumentException("Dispatch identity is required.", nameof(dispatchId));
        Scope = scope;
        CompanyId = companyId;
        DispatchId = dispatchId;
    }
    public ExecutionScope Scope { get; }
    public Guid CompanyId { get; }
    public Guid DispatchId { get; }
}

public sealed record SalesDispatchReservationConsumption(Guid ReservationId, long ExpectedVersion, decimal Quantity);
public sealed record SalesDispatchReservationLineOutcome(Guid OrderLineId, Guid WarehouseId,
    decimal RequestedQuantity, decimal ReservedQuantity, decimal UnreservedQuantity,
    IReadOnlyList<SalesDispatchReservationConsumption> Consumptions);
public sealed record SalesDispatchReservationPreviewOutcome(SalesDispatchDraftSnapshot Draft,
    IReadOnlyList<SalesDispatchReservationLineOutcome> Lines);
public sealed record SalesDispatchDraftPreparationOutcome(bool Created, SalesDispatchReservationPreviewOutcome Preview);

public sealed class SalesDispatchAccessException()
    : InvalidOperationException("The active actor cannot prepare or view this dispatch in the requested scope.")
{
    public string Code { get; } = "SALES_DISPATCH_ACCESS_DENIED";
}
public sealed class SalesDispatchUnavailableException()
    : InvalidOperationException("Dispatch persistence is unavailable. Retry creation with the same draft identity.")
{
    public string Code { get; } = "SALES_DISPATCH_SERVICE_UNAVAILABLE";
}
public sealed class SalesDispatchPreparationConflictException(string code)
    : InvalidOperationException("Dispatch preparation conflicts with the current source, stock master or reservation state.")
{
    public string Code { get; } = code;
}
