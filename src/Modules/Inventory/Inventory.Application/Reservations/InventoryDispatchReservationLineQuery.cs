using KaguERP.Modules.Inventory.Domain;

namespace KaguERP.Modules.Inventory.Application.Reservations;

public sealed class InventoryDispatchReservationLineQuery
{
    public InventoryDispatchReservationLineQuery(InventoryDemandSourceIdentity source, Guid itemId, Guid warehouseId,
        InventoryUomCode baseUomCode, InventoryQuantity requestedQuantity, DateOnly effectiveDate)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.SourceType != "sales.order" || itemId == Guid.Empty || warehouseId == Guid.Empty ||
            string.IsNullOrWhiteSpace(baseUomCode.Value) || !requestedQuantity.IsPositive || effectiveDate == default)
            throw new ArgumentException("Dispatch reservation query requires an exact sales-order source, position, quantity and date.");
        Source = source;
        ItemId = itemId;
        WarehouseId = warehouseId;
        BaseUomCode = baseUomCode;
        RequestedQuantity = requestedQuantity;
        EffectiveDate = effectiveDate;
    }
    public InventoryDemandSourceIdentity Source { get; }
    public Guid ItemId { get; }
    public Guid WarehouseId { get; }
    public InventoryUomCode BaseUomCode { get; }
    public InventoryQuantity RequestedQuantity { get; }
    public DateOnly EffectiveDate { get; }
}

public sealed record InventoryDispatchReservationLinePreview(Guid OrderLineId, Guid WarehouseId,
    InventoryDispatchReservationPlan Plan);
