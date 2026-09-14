using System.Globalization;
using KaguERP.Modules.Inventory.Domain;

internal static class InventoryIssueCostBatchChecks
{
    public static void TotalsAndProvenance()
    {
        Guid tenant = Guid.NewGuid(), company = Guid.NewGuid(), item = Guid.NewGuid(), warehouse = Guid.NewGuid();
        Guid source = Guid.NewGuid(), policy = Guid.NewGuid(), snapshot = Guid.NewGuid();
        var date = new DateOnly(2026, 9, 13);
        var at = new DateTimeOffset(2026, 9, 13, 0, 0, 0, TimeSpan.Zero);
        var uom = InventoryUomCode.Create("EA");
        InventoryIssueCostAmount Line(long sequence, decimal quantity = 3m, decimal cost = .125m,
            Guid? sourceOverride = null, string currency = "TRY", Guid? itemOverride = null)
        {
            var stockItem = itemOverride ?? item;
            var mark = InventoryValuationWatermark.Create(tenant, company, stockItem, warehouse,
                InventoryPosition.Create(date, 1), 1, at, new string('a', 64));
            var issue = StockMovementDraft.Create(Guid.NewGuid(), tenant, company, stockItem, warehouse, uom,
                StockMovementKind.Issue, InventoryQuantity.Create(-quantity), date, at, sequence,
                StockMovementSourceIdentity.Create(tenant, company, "sales.dispatch", sourceOverride ?? source, Guid.NewGuid(), 1, "issue"));
            return InventoryIssueCostSelection.Create(issue, InventoryCostHistoryEvidence.Known(mark, uom, currency, snapshot, cost))
                .CalculateAmount(policy, 2);
        }
        var first = Line(2);
        var second = Line(3);
        var input = new List<InventoryIssueCostAmount> { first, second };
        var batch = InventoryIssueCostBatch.Create(input);
        input.Clear();
        if (batch.Fingerprint.Length != 64 || batch.Fingerprint != InventoryIssueCostBatch.Create([second, first]).Fingerprint)
            throw new InvalidOperationException("Cost fingerprint depends on input ordering or is not SHA-256.");
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            if (batch.Fingerprint != InventoryIssueCostBatch.Create([first with { Amount = .3800m }, second]).Fingerprint)
                throw new InvalidOperationException("Cost fingerprint depends on culture or decimal representation.");
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
        var firstHistory = first.Selection.History;
        var reexpressed = InventoryIssueCostSelection.Create(first.Selection.Issue,
            InventoryCostHistoryEvidence.Known(firstHistory.Watermark, uom, "TRY", snapshot, .12500m)).CalculateAmount(policy, 2);
        if (batch.Fingerprint != InventoryIssueCostBatch.Create([reexpressed, second]).Fingerprint)
            throw new InvalidOperationException("Equivalent unit cost scale changed the snapshot fingerprint.");
        var otherSnapshot = InventoryIssueCostSelection.Create(first.Selection.Issue,
            InventoryCostHistoryEvidence.Known(firstHistory.Watermark, uom, "TRY", Guid.NewGuid(), .125m)).CalculateAmount(policy, 2);
        if (batch.Fingerprint == InventoryIssueCostBatch.Create([otherSnapshot, second]).Fingerprint)
            throw new InvalidOperationException("Changed source cost snapshot was omitted from fingerprint.");
        Guid otherPolicy = Guid.NewGuid();
        if (batch.Fingerprint == InventoryIssueCostBatch.Create([first with { RoundingPolicySnapshotId = otherPolicy },
                second with { RoundingPolicySnapshotId = otherPolicy }]).Fingerprint)
            throw new InvalidOperationException("Changed rounding policy was omitted from fingerprint.");
        var otherMark = InventoryValuationWatermark.Create(tenant, company, item, warehouse,
            firstHistory.Watermark.Position, 2, at, new string('b', 64));
        var otherGeneration = InventoryIssueCostSelection.Create(first.Selection.Issue,
            InventoryCostHistoryEvidence.Known(otherMark, uom, "TRY", snapshot, .125m)).CalculateAmount(policy, 2);
        if (batch.Fingerprint == InventoryIssueCostBatch.Create([otherGeneration, second]).Fingerprint)
            throw new InvalidOperationException("Changed valuation generation/checksum was omitted from fingerprint.");
        if (batch.TotalAmount != .76m || batch.Positions.Single().QuantityChange.Value != -6m ||
            batch.Positions.Single().Amount != .76m || batch.Lines[0] != first || batch.Currency != "TRY")
            throw new InvalidOperationException("Batch lost source rounding, order or exact quantity totals.");
        Reject(() => InventoryIssueCostBatch.Create([]));
        Reject(() => InventoryIssueCostBatch.Create([first, first]));
        Reject(() => InventoryIssueCostBatch.Create([first with { Amount = .37m }]));
        Reject(() => InventoryIssueCostBatch.Create([first, Line(3, sourceOverride: Guid.NewGuid())]));
        Reject(() => InventoryIssueCostBatch.Create([first, Line(3, currency: "USD")]));
        Reject(() => InventoryIssueCostBatch.Create([first, Line(2)]));
        Reject(() => InventoryIssueCostBatch.Create([first, second with { RoundingPolicySnapshotId = Guid.NewGuid() }]));
        Reject(() => InventoryIssueCostBatch.Create([Line(2, 1m, 6000000000000000m), Line(3, 1m, 6000000000000000m)]));
        Reject(() => InventoryIssueCostBatch.Create([Line(2, 60000000000000m, 0m), Line(3, 60000000000000m, 0m)]));
        var distinct = InventoryIssueCostBatch.Create([first, Line(2, itemOverride: Guid.NewGuid())]);
        if (distinct.Positions.Count != 2 || distinct.TotalAmount != .76m)
            throw new InvalidOperationException("Distinct stock positions were conflated.");
        var maximum = Enumerable.Range(2, 500).Select(sequence => Line(sequence, 1m, 1m)).ToArray();
        if (InventoryIssueCostBatch.Create(maximum).TotalAmount != 500m)
            throw new InvalidOperationException("Valid maximum cost batch was rejected.");
        Reject(() => InventoryIssueCostBatch.Create([.. maximum, Line(502)]));
    }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InventoryInvariantException) { return; }
        throw new InvalidOperationException("Invalid issue cost batch was accepted.");
    }
}
