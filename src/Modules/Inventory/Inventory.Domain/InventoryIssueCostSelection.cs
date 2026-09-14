namespace KaguERP.Modules.Inventory.Domain;

public enum InventoryIssueCostOrigin
{
    LastKnownCost = 1,
    NoHistoryZero = 2
}

/// <summary>Explicit successful history lookup, not a nullable result or a database failure.</summary>
public sealed class InventoryCostHistoryEvidence
{
    private InventoryCostHistoryEvidence(InventoryValuationWatermark watermark, InventoryUomCode uom,
        string currency, Guid? snapshotId, decimal unitCost)
    {
        ArgumentNullException.ThrowIfNull(watermark);
        if (uom == default || currency is null || currency.Length != 3 ||
            currency.Any(character => character is < 'A' or > 'Z') || unitCost < 0m ||
            snapshotId == Guid.Empty || watermark.RecordedCutoff == default ||
            watermark.RecordedCutoff.Ticks % TimeSpan.TicksPerMicrosecond != 0)
            throw new InventoryInvariantException("INVENTORY_COST_HISTORY_INVALID", "Cost history evidence is invalid.");
        Watermark = watermark;
        BaseUom = uom;
        Currency = currency;
        SnapshotId = snapshotId;
        UnitCost = unitCost;
    }

    public InventoryValuationWatermark Watermark { get; }
    public InventoryUomCode BaseUom { get; }
    public string Currency { get; }
    public Guid? SnapshotId { get; }
    public decimal UnitCost { get; }
    public InventoryIssueCostOrigin Origin => SnapshotId.HasValue
        ? InventoryIssueCostOrigin.LastKnownCost : InventoryIssueCostOrigin.NoHistoryZero;

    public static InventoryCostHistoryEvidence Known(InventoryValuationWatermark watermark,
        InventoryUomCode uom, string currency, Guid snapshotId, decimal unitCost) =>
        new(watermark, uom, currency, snapshotId, unitCost);

    public static InventoryCostHistoryEvidence NoHistory(InventoryValuationWatermark watermark,
        InventoryUomCode uom, string currency) => new(watermark, uom, currency, null, 0m);
}

/// <summary>Immutable unit-cost choice only; does not calculate rounded ledger amounts or authorize posting.</summary>
public sealed class InventoryIssueCostSelection
{
    private InventoryIssueCostSelection(StockMovementDraft issue, InventoryCostHistoryEvidence history)
    {
        Issue = issue;
        History = history;
    }
    public StockMovementDraft Issue { get; }
    public InventoryCostHistoryEvidence History { get; }
    public decimal UnitCost => History.UnitCost;
    public string Currency => History.Currency;
    public InventoryIssueCostOrigin Origin => History.Origin;

    public InventoryIssueCostAmount CalculateAmount(Guid roundingPolicySnapshotId, int amountScale)
    {
        InventoryCostArithmetic.EnsurePolicy(roundingPolicySnapshotId, amountScale);
        if (amountScale > 4)
            throw new InventoryInvariantException("INVENTORY_VALUE_SCALE_INVALID", "Ledger amount scale cannot exceed numeric(20,4).");
        decimal amount = InventoryCostArithmetic.Money(InventoryCostArithmetic.Multiply(UnitCost,
            -Issue.BaseQuantity.Value, amountScale));
        return new(this, roundingPolicySnapshotId, amountScale, amount);
    }

    public static InventoryIssueCostSelection Create(StockMovementDraft issue, InventoryCostHistoryEvidence history)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(history);
        EnsureCompatible(issue, history.Watermark, history.BaseUom);
        return new(issue, history);
    }

    public static void EnsureCompatible(StockMovementDraft issue, InventoryValuationWatermark watermark, InventoryUomCode uom)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(watermark);
        if (issue.Kind != StockMovementKind.Issue || issue.TenantId != watermark.TenantId ||
            issue.CompanyId != watermark.CompanyId || issue.ItemId != watermark.ItemId ||
            issue.WarehouseId != watermark.WarehouseId || issue.BaseUom != uom)
            throw new InventoryInvariantException("INVENTORY_ISSUE_COST_SCOPE_MISMATCH",
                "Issue and cost-history position must match exactly.");
        if (watermark.Position >= InventoryPosition.Create(issue.EffectiveDate, issue.SequenceKey) ||
            watermark.RecordedCutoff > issue.RecordedAt || issue.RecordedAt == default ||
            issue.RecordedAt.Ticks % TimeSpan.TicksPerMicrosecond != 0)
            throw new InventoryInvariantException("INVENTORY_ISSUE_COST_CUTOFF_CONFLICT",
                "Cost history must precede the issue and be known at its recorded cutoff.");
    }
}

public sealed record InventoryIssueCostAmount(InventoryIssueCostSelection Selection,
    Guid RoundingPolicySnapshotId, int AmountScale, decimal Amount);
