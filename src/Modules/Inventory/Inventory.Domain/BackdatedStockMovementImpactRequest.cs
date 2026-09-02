namespace KaguERP.Modules.Inventory.Domain;

public sealed record BackdatedStockMovementImpactRequest
{
    private BackdatedStockMovementImpactRequest(
        StockMovementDraft proposedMovement,
        InventoryValuationWatermark currentWatermark,
        InventoryPosition affectedFrom,
        InventoryPosition affectedThrough)
    {
        ProposedMovement = proposedMovement;
        CurrentWatermark = currentWatermark;
        AffectedFrom = affectedFrom;
        AffectedThrough = affectedThrough;
    }

    public StockMovementDraft ProposedMovement { get; }
    public InventoryValuationWatermark CurrentWatermark { get; }
    public InventoryPosition AffectedFrom { get; }
    public InventoryPosition AffectedThrough { get; }

    public static BackdatedStockMovementImpactRequest Create(
        StockMovementDraft proposedMovement,
        InventoryValuationWatermark currentWatermark)
    {
        ArgumentNullException.ThrowIfNull(proposedMovement);
        ArgumentNullException.ThrowIfNull(currentWatermark);
        if (proposedMovement.TenantId != currentWatermark.TenantId ||
            proposedMovement.CompanyId != currentWatermark.CompanyId ||
            proposedMovement.ItemId != currentWatermark.ItemId ||
            proposedMovement.WarehouseId != currentWatermark.WarehouseId)
        {
            throw new InventoryInvariantException(
                "INVENTORY_BACKDATE_SCOPE_MISMATCH",
                "Backdate movement and valuation watermark scope must match.");
        }
        InventoryPosition proposedPosition = InventoryPosition.Create(
            proposedMovement.EffectiveDate,
            proposedMovement.SequenceKey);
        if (proposedPosition >= currentWatermark.Position)
        {
            throw new InventoryInvariantException(
                "INVENTORY_MOVEMENT_NOT_BACKDATED",
                "Impact preview requires a movement earlier than the current valuation watermark.");
        }

        return new BackdatedStockMovementImpactRequest(
            proposedMovement,
            currentWatermark,
            proposedPosition,
            currentWatermark.Position);
    }
}
