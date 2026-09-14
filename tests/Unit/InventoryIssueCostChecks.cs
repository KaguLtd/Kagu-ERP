using KaguERP.Modules.Inventory.Domain;

internal static class InventoryIssueCostChecks
{
    public static void LastKnownOrExplicitZero()
    {
        Guid tenant = Guid.NewGuid(), company = Guid.NewGuid(), item = Guid.NewGuid(), warehouse = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
        var date = new DateOnly(2026, 9, 12);
        var uom = InventoryUomCode.Create("EA");
        InventoryValuationWatermark Mark(Guid? companyOverride = null, long sequence = 1, DateTimeOffset? cutoff = null) =>
            InventoryValuationWatermark.Create(tenant, companyOverride ?? company, item, warehouse,
                InventoryPosition.Create(date, sequence), 1, cutoff ?? at, new string('a', 64));
        var issue = StockMovementDraft.Create(Guid.NewGuid(), tenant, company, item, warehouse, uom,
            StockMovementKind.Issue, InventoryQuantity.Create(-3m), date, at, 2,
            StockMovementSourceIdentity.Create(tenant, company, "sales.dispatch", Guid.NewGuid(), Guid.NewGuid(), 1, "issue"));
        Guid snapshot = Guid.NewGuid();
        var known = InventoryCostHistoryEvidence.Known(Mark(), uom, "TRY", snapshot, 12.345678m);
        var selected = InventoryIssueCostSelection.Create(issue, known);
        if (selected.UnitCost != 12.345678m || selected.History.SnapshotId != snapshot ||
            selected.Origin != InventoryIssueCostOrigin.LastKnownCost)
            throw new InvalidOperationException("Issue did not retain exact known cost and provenance.");
        var zero = InventoryIssueCostSelection.Create(issue, InventoryCostHistoryEvidence.NoHistory(Mark(), uom, "TRY"));
        var knownZero = InventoryIssueCostSelection.Create(issue, InventoryCostHistoryEvidence.Known(Mark(), uom, "TRY", snapshot, 0m));
        if (zero.UnitCost != 0m || zero.Origin != InventoryIssueCostOrigin.NoHistoryZero || zero.History.SnapshotId is not null ||
            knownZero.Origin != InventoryIssueCostOrigin.LastKnownCost)
            throw new InvalidOperationException("No-history zero and recorded zero were conflated.");
        _ = InventoryCostHistoryEvidence.Known(Mark(sequence: 3), uom, "TRY", Guid.NewGuid(), 20m);
        if (selected.UnitCost != 12.345678m) throw new InvalidOperationException("Later cost changed the old issue.");
        Reject(() => InventoryIssueCostSelection.Create(issue,
            InventoryCostHistoryEvidence.Known(Mark(Guid.NewGuid()), uom, "TRY", snapshot, 10m)));
        Reject(() => InventoryIssueCostSelection.Create(issue,
            InventoryCostHistoryEvidence.NoHistory(Mark(sequence: 2), uom, "TRY")));
        Reject(() => InventoryIssueCostSelection.Create(issue,
            InventoryCostHistoryEvidence.NoHistory(Mark(cutoff: at.AddSeconds(1)), uom, "TRY")));
        Reject(() => InventoryIssueCostSelection.Create(issue,
            InventoryCostHistoryEvidence.NoHistory(Mark(), InventoryUomCode.Create("KG"), "TRY")));
        Reject(() => InventoryCostHistoryEvidence.Known(Mark(), uom, "TRY", Guid.Empty, 10m));
        Reject(() => InventoryCostHistoryEvidence.Known(Mark(), uom, "TRY", snapshot, -1m));
        Reject(() => InventoryCostHistoryEvidence.NoHistory(Mark(), uom, "try"));
        try { _ = InventoryIssueCostSelection.Create(issue, null!); }
        catch (ArgumentNullException) { return; }
        throw new InvalidOperationException("Missing evidence was silently priced at zero.");
    }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InventoryInvariantException) { return; }
        throw new InvalidOperationException("Invalid cost history was silently accepted.");
    }
}
