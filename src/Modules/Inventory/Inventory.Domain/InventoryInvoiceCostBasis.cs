namespace KaguERP.Modules.Inventory.Domain;

public sealed record InventoryInvoiceCostLineage(Guid TenantId, Guid CompanyId, Guid InvoiceId, long InvoiceVersion,
    Guid AllocationId, Guid InvoiceLineId, Guid ReceiptId, Guid ReceiptLineId, Guid ExchangeRateSnapshotId,
    Guid CostRuleSnapshotId, DateOnly EffectiveDate, DateTimeOffset FinalizedAt);

public sealed class InventoryInvoiceCostBasis
{
    public InventoryInvoiceCostBasis(InventoryInvoiceCostLineage source, Guid itemId, Guid warehouseId,
        InventoryUomCode uom, string currency, InventoryQuantity quantity, decimal eligibleFunctionalCost)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (new[] { source.TenantId, source.CompanyId, source.InvoiceId, source.AllocationId, source.InvoiceLineId,
                source.ReceiptId, source.ReceiptLineId, source.ExchangeRateSnapshotId, source.CostRuleSnapshotId, itemId, warehouseId }.Contains(Guid.Empty) ||
            source.InvoiceVersion <= 0 || source.EffectiveDate == default || source.FinalizedAt == default ||
            source.FinalizedAt.Offset != TimeSpan.Zero || source.FinalizedAt.Ticks % TimeSpan.TicksPerMicrosecond != 0 ||
            uom == default || currency is null || currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z') ||
            !quantity.IsPositive || eligibleFunctionalCost < 0m || eligibleFunctionalCost > 9999999999999999.9999m ||
            decimal.Round(eligibleFunctionalCost, 4) != eligibleFunctionalCost)
            throw new InventoryInvariantException("INVENTORY_INVOICE_COST_BASIS_INVALID", "Invoice cost basis is invalid.");
        Source = source;
        ItemId = itemId;
        WarehouseId = warehouseId;
        BaseUom = uom;
        Currency = currency;
        Quantity = quantity;
        EligibleFunctionalCost = eligibleFunctionalCost;
    }
    public InventoryInvoiceCostLineage Source { get; }
    public Guid ItemId { get; }
    public Guid WarehouseId { get; }
    public InventoryUomCode BaseUom { get; }
    public string Currency { get; }
    public InventoryQuantity Quantity { get; }
    public decimal EligibleFunctionalCost { get; }

    public InventoryInvoiceUnitCost CalculateUnitCost(Guid roundingPolicySnapshotId, int scale)
    {
        InventoryCostArithmetic.EnsurePolicy(roundingPolicySnapshotId, scale);
        return new(this, roundingPolicySnapshotId, scale,
            InventoryCostArithmetic.Divide(EligibleFunctionalCost, Quantity.Value, scale));
    }
}

public sealed record InventoryInvoiceUnitCost(InventoryInvoiceCostBasis Basis,
    Guid RoundingPolicySnapshotId, int Scale, decimal UnitCost);
