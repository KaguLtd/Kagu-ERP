namespace KaguERP.Modules.Inventory.Domain;

/// <summary>Calculation input only. A trusted producer must verify the balance at this exact history watermark.</summary>
public sealed class InventoryValuationBalance
{
    public InventoryValuationBalance(InventoryCostHistoryEvidence history, InventoryQuantity quantity, decimal carryingValue)
    {
        ArgumentNullException.ThrowIfNull(history);
        History = history;
        Quantity = quantity;
        CarryingValue = InventoryCostArithmetic.Money(carryingValue);
    }
    public InventoryCostHistoryEvidence History { get; }
    public InventoryQuantity Quantity { get; }
    public decimal CarryingValue { get; }

    public InventoryReceiptValuationCalculation Receive(InventoryInvoiceCostBasis invoice,
        InventoryPosition receiptPosition, DateTimeOffset recordedAt, Guid roundingPolicySnapshotId, int unitCostScale)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        _ = InventoryPosition.Create(receiptPosition.EffectiveDate, receiptPosition.SequenceKey);
        InventoryCostArithmetic.EnsurePolicy(roundingPolicySnapshotId, unitCostScale);
        var mark = History.Watermark;
        if (invoice.Source.TenantId != mark.TenantId || invoice.Source.CompanyId != mark.CompanyId ||
            invoice.ItemId != mark.ItemId || invoice.WarehouseId != mark.WarehouseId ||
            invoice.BaseUom != History.BaseUom || invoice.Currency != History.Currency)
            throw new InventoryInvariantException("INVENTORY_VALUATION_SCOPE_MISMATCH", "Invoice and opening valuation scopes must match exactly.");
        if (receiptPosition <= mark.Position || recordedAt == default || recordedAt.Offset != TimeSpan.Zero ||
            recordedAt.Ticks % TimeSpan.TicksPerMicrosecond != 0 || recordedAt < mark.RecordedCutoff ||
            recordedAt < invoice.Source.FinalizedAt)
            throw new InventoryInvariantException("INVENTORY_VALUATION_CUTOFF_CONFLICT", "Receipt valuation must follow its opening balance and known invoice cost.");
        // Negative replenishment and residual-value disposition require a separate explicit policy.
        if (Quantity.IsNegative || CarryingValue < 0m || (Quantity.IsZero && CarryingValue != 0m))
            throw new InventoryInvariantException("INVENTORY_SPECIAL_VALUATION_POLICY_REQUIRED", "This calculation covers normal nonnegative opening stock only.");
        var quantity = Quantity + invoice.Quantity;
        var value = InventoryCostArithmetic.Money(CarryingValue + invoice.EligibleFunctionalCost);
        var unitCost = InventoryCostArithmetic.Divide(value, quantity.Value, unitCostScale);
        return new(this, invoice, receiptPosition, recordedAt, roundingPolicySnapshotId, unitCostScale, quantity, value, unitCost);
    }

    public InventoryIssueValuationCalculation Issue(InventoryIssueCostSelection selection,
        Guid roundingPolicySnapshotId, int amountScale)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.History.Watermark != History.Watermark || selection.History.BaseUom != History.BaseUom ||
            selection.Currency != History.Currency || selection.History.SnapshotId != History.SnapshotId ||
            selection.UnitCost != History.UnitCost || selection.Origin != History.Origin)
            throw new InventoryInvariantException("INVENTORY_VALUATION_SCOPE_MISMATCH", "Issue selection and opening valuation must use the same history evidence.");
        var value = selection.CalculateAmount(roundingPolicySnapshotId, amountScale).Amount;
        return new(this, selection, roundingPolicySnapshotId, amountScale, -value,
            Quantity + selection.Issue.BaseQuantity, InventoryCostArithmetic.Money(CarryingValue - value));
    }
}

// Results preserve the source operands. They are not posted stock movements or authorization evidence.
public sealed record InventoryReceiptValuationCalculation(InventoryValuationBalance Opening,
    InventoryInvoiceCostBasis Invoice, InventoryPosition ReceiptPosition, DateTimeOffset RecordedAt,
    Guid RoundingPolicySnapshotId, int UnitCostScale, InventoryQuantity ClosingQuantity,
    decimal ClosingValue, decimal UnitCost);

public sealed record InventoryIssueValuationCalculation(InventoryValuationBalance Opening,
    InventoryIssueCostSelection Selection, Guid RoundingPolicySnapshotId, int AmountScale,
    decimal ValueChange, InventoryQuantity ClosingQuantity, decimal ClosingValue);
