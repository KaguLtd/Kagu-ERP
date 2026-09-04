namespace KaguERP.Modules.Inventory.Domain;

public sealed record ValidatedImmediateStockTransferDraft
{
    private ValidatedImmediateStockTransferDraft(
        Guid transferId,
        StockMovementDraft sourceIssue,
        StockMovementDraft destinationReceipt)
    {
        TransferId = transferId;
        SourceIssue = sourceIssue;
        DestinationReceipt = destinationReceipt;
    }

    public Guid TransferId { get; }
    public StockMovementDraft SourceIssue { get; }
    public StockMovementDraft DestinationReceipt { get; }

    public static ValidatedImmediateStockTransferDraft Create(
        Guid transferId,
        StockMovementDraft sourceIssue,
        StockMovementDraft destinationReceipt)
    {
        if (transferId == Guid.Empty)
        {
            throw new InventoryInvariantException("INVENTORY_TRANSFER_ID_REQUIRED", "Transfer ID is required.");
        }
        ArgumentNullException.ThrowIfNull(sourceIssue);
        ArgumentNullException.ThrowIfNull(destinationReceipt);

        if (sourceIssue.Kind != StockMovementKind.TransferIssue ||
            destinationReceipt.Kind != StockMovementKind.TransferReceipt ||
            sourceIssue.TransferId != transferId || destinationReceipt.TransferId != transferId)
        {
            throw new InventoryInvariantException(
                "INVENTORY_TRANSFER_MOVEMENT_KIND_INVALID",
                "An immediate transfer requires its exact issue and receipt movements.");
        }
        if (sourceIssue.TenantId != destinationReceipt.TenantId ||
            sourceIssue.CompanyId != destinationReceipt.CompanyId ||
            sourceIssue.ItemId != destinationReceipt.ItemId ||
            sourceIssue.BaseUomCode != destinationReceipt.BaseUomCode ||
            sourceIssue.Source != destinationReceipt.Source ||
            sourceIssue.EffectiveDate != destinationReceipt.EffectiveDate)
        {
            throw new InventoryInvariantException(
                "INVENTORY_TRANSFER_CONTEXT_MISMATCH",
                "Immediate transfer movements must share scope, item, UOM, source and effective date.");
        }
        if (sourceIssue.CounterpartWarehouseId != destinationReceipt.WarehouseId ||
            destinationReceipt.CounterpartWarehouseId != sourceIssue.WarehouseId)
        {
            throw new InventoryInvariantException(
                "INVENTORY_TRANSFER_WAREHOUSE_MISMATCH",
                "Immediate transfer movement warehouse references must be reciprocal.");
        }
        if (sourceIssue.ReversalOfMovementId.HasValue != destinationReceipt.ReversalOfMovementId.HasValue)
        {
            throw new InventoryInvariantException(
                "INVENTORY_TRANSFER_REVERSAL_PAIR_INCOMPLETE",
                "An immediate transfer reversal must link both counter movements.");
        }
        if (!(sourceIssue.BaseQuantity + destinationReceipt.BaseQuantity).IsZero)
        {
            throw new InventoryInvariantException(
                "INVENTORY_TRANSFER_QUANTITY_MISMATCH",
                "Immediate transfer issue and receipt quantities must sum exactly to zero.");
        }

        return new ValidatedImmediateStockTransferDraft(transferId, sourceIssue, destinationReceipt);
    }
}
