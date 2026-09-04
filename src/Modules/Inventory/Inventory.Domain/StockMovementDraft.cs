namespace KaguERP.Modules.Inventory.Domain;

public enum StockMovementKind
{
    Receipt = 1,
    Issue = 2,
    TransferIssue = 3,
    TransferReceipt = 4,
    Adjustment = 5,
}

public sealed record StockMovementDraft
{
    private StockMovementDraft(
        Guid movementId,
        Guid tenantId,
        Guid companyId,
        Guid itemId,
        Guid warehouseId,
        InventoryUomCode baseUom,
        StockMovementKind kind,
        InventoryQuantity baseQuantity,
        DateOnly effectiveDate,
        DateTimeOffset recordedAt,
        long sequenceKey,
        StockMovementSourceIdentity source,
        Guid? transferId,
        Guid? counterpartWarehouseId,
        Guid? reversalOfMovementId)
    {
        MovementId = movementId;
        TenantId = tenantId;
        CompanyId = companyId;
        ItemId = itemId;
        WarehouseId = warehouseId;
        BaseUom = baseUom;
        Kind = kind;
        BaseQuantity = baseQuantity;
        EffectiveDate = effectiveDate;
        RecordedAt = recordedAt;
        SequenceKey = sequenceKey;
        Source = source;
        TransferId = transferId;
        CounterpartWarehouseId = counterpartWarehouseId;
        ReversalOfMovementId = reversalOfMovementId;
    }

    public Guid MovementId { get; }
    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid ItemId { get; }
    public Guid WarehouseId { get; }
    public InventoryUomCode BaseUom { get; }
    public string BaseUomCode => BaseUom.Value;
    public StockMovementKind Kind { get; }
    public InventoryQuantity BaseQuantity { get; }
    public DateOnly EffectiveDate { get; }
    public DateTimeOffset RecordedAt { get; }
    public long SequenceKey { get; }
    public StockMovementSourceIdentity Source { get; }
    public Guid? TransferId { get; }
    public Guid? CounterpartWarehouseId { get; }
    public Guid? ReversalOfMovementId { get; }

    public static StockMovementDraft Create(
        Guid movementId,
        Guid tenantId,
        Guid companyId,
        Guid itemId,
        Guid warehouseId,
        InventoryUomCode baseUom,
        StockMovementKind kind,
        InventoryQuantity baseQuantity,
        DateOnly effectiveDate,
        DateTimeOffset recordedAt,
        long sequenceKey,
        StockMovementSourceIdentity source,
        Guid? transferId = null,
        Guid? counterpartWarehouseId = null,
        Guid? reversalOfMovementId = null)
    {
        RequireId(movementId, "INVENTORY_MOVEMENT_ID_REQUIRED", "Inventory movement ID is required.");
        RequireId(tenantId, "INVENTORY_MOVEMENT_TENANT_REQUIRED", "Inventory movement tenant is required.");
        RequireId(companyId, "INVENTORY_MOVEMENT_COMPANY_REQUIRED", "Inventory movement company is required.");
        RequireId(itemId, "INVENTORY_MOVEMENT_ITEM_REQUIRED", "Inventory movement item is required.");
        RequireId(warehouseId, "INVENTORY_MOVEMENT_WAREHOUSE_REQUIRED", "Inventory movement warehouse is required.");
        ArgumentNullException.ThrowIfNull(source);
        if (baseUom == default)
        {
            throw new InventoryInvariantException("INVENTORY_MOVEMENT_BASE_UOM_REQUIRED", "Movement base UOM is required.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw new InventoryInvariantException("INVENTORY_MOVEMENT_KIND_INVALID", "Inventory movement kind is invalid.");
        }
        if (baseQuantity.IsZero)
        {
            throw new InventoryInvariantException("INVENTORY_MOVEMENT_QUANTITY_ZERO", "Inventory movement quantity cannot be zero.");
        }
        if (kind is StockMovementKind.Receipt or StockMovementKind.TransferReceipt && !baseQuantity.IsPositive ||
            kind is StockMovementKind.Issue or StockMovementKind.TransferIssue && !baseQuantity.IsNegative)
        {
            throw new InventoryInvariantException(
                "INVENTORY_MOVEMENT_QUANTITY_SIGN_INVALID",
                "Receipt quantities must be positive and issue quantities must be negative.");
        }
        if (effectiveDate == default)
        {
            throw new InventoryInvariantException("INVENTORY_MOVEMENT_EFFECTIVE_DATE_REQUIRED", "Effective date is required.");
        }
        if (recordedAt.Offset != TimeSpan.Zero)
        {
            throw new InventoryInvariantException("INVENTORY_MOVEMENT_RECORDED_AT_NOT_UTC", "Recorded timestamp must be UTC.");
        }
        if (sequenceKey <= 0)
        {
            throw new InventoryInvariantException("INVENTORY_MOVEMENT_SEQUENCE_INVALID", "Sequence key must be positive.");
        }
        if (source.TenantId != tenantId || source.CompanyId != companyId)
        {
            throw new InventoryInvariantException("INVENTORY_MOVEMENT_SOURCE_SCOPE_MISMATCH", "Movement and source scope must match.");
        }

        bool isTransfer = kind is StockMovementKind.TransferIssue or StockMovementKind.TransferReceipt;
        if (isTransfer && (!transferId.HasValue || transferId.Value == Guid.Empty ||
                           !counterpartWarehouseId.HasValue || counterpartWarehouseId.Value == Guid.Empty ||
                           counterpartWarehouseId == warehouseId))
        {
            throw new InventoryInvariantException(
                "INVENTORY_TRANSFER_CONTEXT_INVALID",
                "Transfer movements require a transfer ID and a distinct counterpart warehouse.");
        }
        if (!isTransfer && (transferId is not null || counterpartWarehouseId is not null))
        {
            throw new InventoryInvariantException(
                "INVENTORY_NON_TRANSFER_CONTEXT_INVALID",
                "Non-transfer movements cannot carry transfer context.");
        }
        if (reversalOfMovementId == Guid.Empty || reversalOfMovementId == movementId)
        {
            throw new InventoryInvariantException(
                "INVENTORY_MOVEMENT_REVERSAL_REFERENCE_INVALID",
                "A reversal must reference a distinct non-empty stock movement.");
        }

        return new StockMovementDraft(
            movementId,
            tenantId,
            companyId,
            itemId,
            warehouseId,
            baseUom,
            kind,
            baseQuantity,
            effectiveDate,
            recordedAt,
            sequenceKey,
            source,
            transferId,
            counterpartWarehouseId,
            reversalOfMovementId);
    }

    private static void RequireId(Guid id, string code, string message)
    {
        if (id == Guid.Empty)
        {
            throw new InventoryInvariantException(code, message);
        }
    }
}
