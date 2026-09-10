namespace KaguERP.Modules.Inventory.Domain;

/// <summary>
/// Quantity-only count preview at a caller-supplied snapshot. Not a posting authorization or cost result.
/// The writer must reload/validate the snapshot and reconcile movements after its cutoff.
/// </summary>
public sealed record InventoryCountDifference
{
    private InventoryCountDifference(InventoryQuantity bookQuantity, InventoryQuantity countedQuantity,
        DateTimeOffset recordedCutoff, InventoryQuantity adjustmentQuantity)
    {
        BookQuantity = bookQuantity;
        CountedQuantity = countedQuantity;
        RecordedCutoff = recordedCutoff;
        AdjustmentQuantity = adjustmentQuantity;
    }

    public InventoryQuantity BookQuantity { get; }
    public InventoryQuantity CountedQuantity { get; }
    public DateTimeOffset RecordedCutoff { get; }
    public InventoryQuantity AdjustmentQuantity { get; }
    public bool HasDifference => !AdjustmentQuantity.IsZero;

    public static InventoryCountDifference Create(InventoryQuantity bookQuantity,
        InventoryQuantity countedQuantity, DateTimeOffset recordedCutoff)
    {
        if (countedQuantity.IsNegative)
            throw new InventoryInvariantException("INVENTORY_COUNT_NEGATIVE_PHYSICAL_QUANTITY",
                "A physical count cannot be negative; a negative book balance is allowed.");
        if (recordedCutoff == default || recordedCutoff.Offset != TimeSpan.Zero ||
            recordedCutoff.Ticks % TimeSpan.TicksPerMicrosecond != 0)
            throw new InventoryInvariantException("INVENTORY_COUNT_CUTOFF_INVALID",
                "Count preview requires a PostgreSQL-safe UTC snapshot cutoff.");
        return new(bookQuantity, countedQuantity, recordedCutoff, countedQuantity + (-bookQuantity));
    }
}
