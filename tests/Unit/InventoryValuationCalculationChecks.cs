using KaguERP.Modules.Inventory.Domain;

internal static class InventoryValuationCalculationChecks
{
    public static void QuantityAndValueConservation()
    {
        Guid tenant = Guid.NewGuid(), company = Guid.NewGuid(), item = Guid.NewGuid(), warehouse = Guid.NewGuid();
        Guid policy = Guid.NewGuid(), snapshot = Guid.NewGuid();
        var date = new DateOnly(2026, 9, 12);
        var at = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
        var uom = InventoryUomCode.Create("EA");
        var mark = InventoryValuationWatermark.Create(tenant, company, item, warehouse,
            InventoryPosition.Create(date, 1), 1, at, new string('a', 64));
        var history = InventoryCostHistoryEvidence.Known(mark, uom, "TRY", snapshot, 100m);
        var opening = new InventoryValuationBalance(history, InventoryQuantity.Create(2m), 200m);
        var lineage = new InventoryInvoiceCostLineage(tenant, company, Guid.NewGuid(), 1, Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), date, at);
        InventoryInvoiceCostBasis Invoice(decimal quantity, decimal amount, string currency = "TRY") =>
            new(lineage, item, warehouse, uom, currency, InventoryQuantity.Create(quantity), amount);
        var position = InventoryPosition.Create(date, 2);
        var receipt = opening.Receive(Invoice(3m, 360m), position, at, policy, 6);
        if (receipt.ClosingQuantity.Value != 5m || receipt.ClosingValue != 560m || receipt.UnitCost != 112m ||
            receipt.Invoice.Source != lineage || receipt.RoundingPolicySnapshotId != policy || receipt.Opening != opening)
            throw new InvalidOperationException("Weighted average did not conserve original invoice cost and quantity.");

        InventoryIssueCostSelection Select(decimal quantity, InventoryCostHistoryEvidence? evidence = null) =>
            InventoryIssueCostSelection.Create(StockMovementDraft.Create(Guid.NewGuid(), tenant, company, item, warehouse,
                uom, StockMovementKind.Issue, InventoryQuantity.Create(-quantity), date, at, 2,
                StockMovementSourceIdentity.Create(tenant, company, "sales.dispatch", Guid.NewGuid(), Guid.NewGuid(), 1, "issue")),
                evidence ?? history);
        var issue = opening.Issue(Select(3m), policy, 2);
        if (issue.ClosingQuantity.Value != -1m || issue.ValueChange != -300m || issue.ClosingValue != -100m ||
            issue.Selection.History.SnapshotId != snapshot || opening.CarryingValue != 200m)
            throw new InvalidOperationException("Negative issue mutated opening evidence or lost signed value.");

        var noHistory = InventoryCostHistoryEvidence.NoHistory(mark, uom, "TRY");
        var empty = new InventoryValuationBalance(noHistory, InventoryQuantity.Create(0m), 0m);
        var zero = empty.Issue(Select(3m, noHistory), policy, 2);
        if (zero.ClosingValue != 0m || zero.ClosingQuantity.Value != -3m || zero.Selection.Origin != InventoryIssueCostOrigin.NoHistoryZero)
            throw new InvalidOperationException("No-history issue was blocked or conflated with a known cost.");
        var fractional = empty.Receive(Invoice(3m, 1m), position, at, policy, 2);
        if (fractional.UnitCost != .33m || fractional.ClosingValue != 1m)
            throw new InvalidOperationException("Rounded unit cost replaced the original invoice total.");
        var fractionalHistory = InventoryCostHistoryEvidence.Known(mark, uom, "TRY", snapshot, .125m);
        var residual = new InventoryValuationBalance(fractionalHistory, InventoryQuantity.Create(1m), .125m)
            .Issue(Select(1m, fractionalHistory), policy, 2);
        if (residual.ValueChange != -.13m || residual.ClosingQuantity.Value != 0m || residual.ClosingValue != -.005m)
            throw new InvalidOperationException("Midpoint rounding or residual value was silently erased.");

        Reject(() => opening.Receive(Invoice(1m, 120m, "USD"), position, at, policy, 6));
        Reject(() => opening.Receive(Invoice(1m, 120m), mark.Position, at, policy, 6));
        Reject(() => opening.Receive(Invoice(1m, 120m), position, at.AddTicks(1), policy, 6));
        Reject(() => opening.Receive(Invoice(1m, 120m), position, at.AddSeconds(-1), policy, 6));
        Reject(() => opening.Receive(Invoice(1m, 120m), position, at, Guid.Empty, 6));
        Reject(() => new InventoryValuationBalance(history, InventoryQuantity.Create(-1m), -100m)
            .Receive(Invoice(2m, 240m), position, at, policy, 6));
        Reject(() => new InventoryValuationBalance(history, InventoryQuantity.Create(0m), .01m)
            .Receive(Invoice(1m, 120m), position, at, policy, 6));
        Reject(() => opening.Issue(Select(1m, noHistory), policy, 2));
        var otherCurrency = InventoryCostHistoryEvidence.Known(mark, uom, "USD", snapshot, 100m);
        Reject(() => opening.Issue(Select(1m, otherCurrency), policy, 2));
        var otherSnapshot = InventoryCostHistoryEvidence.Known(mark, uom, "TRY", Guid.NewGuid(), 100m);
        Reject(() => opening.Issue(Select(1m, otherSnapshot), policy, 2));
        var otherCompanyInvoice = new InventoryInvoiceCostBasis(lineage with { CompanyId = Guid.NewGuid() },
            item, warehouse, uom, "TRY", InventoryQuantity.Create(1m), 10m);
        Reject(() => opening.Receive(otherCompanyInvoice, position, at, policy, 6));
        var otherWarehouseInvoice = new InventoryInvoiceCostBasis(lineage, item, Guid.NewGuid(), uom, "TRY",
            InventoryQuantity.Create(1m), 10m);
        Reject(() => opening.Receive(otherWarehouseInvoice, position, at, policy, 6));
        Reject(() => opening.Issue(Select(1m), policy, 5));
        Reject(() => _ = new InventoryValuationBalance(history, default, .00001m));
        var hugeHistory = InventoryCostHistoryEvidence.Known(mark, uom, "TRY", snapshot, decimal.MaxValue);
        Reject(() => new InventoryValuationBalance(hugeHistory, default, 0m).Issue(Select(2m, hugeHistory), policy, 2));
        Reject(() => new InventoryValuationBalance(history, InventoryQuantity.Create(99999999999999.999999m), 0m)
            .Receive(Invoice(1m, 1m), position, at, policy, 6));
        Reject(() => new InventoryValuationBalance(history, InventoryQuantity.Create(1m), 9999999999999999.9999m)
            .Receive(Invoice(1m, 1m), position, at, policy, 6));

        for (int amount = 0; amount <= 100; amount++)
            for (int quantity = 1; quantity <= 25; quantity++)
            {
                var result = opening.Receive(Invoice(quantity, amount), position, at, policy, 4);
                if (result.ClosingValue - opening.CarryingValue != amount ||
                    result.ClosingQuantity.Value - opening.Quantity.Value != quantity ||
                    result.UnitCost != decimal.Round((200m + amount) / (2m + quantity), 4, MidpointRounding.AwayFromZero))
                    throw new InvalidOperationException("Receipt quantity/value conservation failed.");
                var outgoing = opening.Issue(Select(quantity), policy, 2);
                if (outgoing.ClosingValue != opening.CarryingValue + outgoing.ValueChange ||
                    outgoing.ClosingQuantity.Value != opening.Quantity.Value - quantity)
                    throw new InvalidOperationException("Issue quantity/value conservation failed.");
            }
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InventoryInvariantException) { return; }
        throw new InvalidOperationException("Invalid valuation input was accepted.");
    }
}
