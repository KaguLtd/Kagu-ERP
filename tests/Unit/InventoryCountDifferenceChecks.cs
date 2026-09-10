using KaguERP.Modules.Inventory.Domain;

internal static class InventoryCountDifferenceChecks
{
    public static void CountDifferencePreservesNegativeBookBalance()
    {
        var cutoff = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        foreach (var (book, counted, adjustment) in new[]
        {
            (-5m, 0m, 5m), (-5m, 2m, 7m), (10m, 8m, -2m), (8m, 8m, 0m),
            (-0.000001m, 0.000001m, 0.000002m),
        })
        {
            var result = InventoryCountDifference.Create(InventoryQuantity.Create(book), InventoryQuantity.Create(counted), cutoff);
            if (result.AdjustmentQuantity.Value != adjustment || result.BookQuantity.Value != book ||
                (result.BookQuantity + result.AdjustmentQuantity).Value != counted ||
                result.RecordedCutoff != cutoff || result.HasDifference != (adjustment != 0m))
                throw new InvalidOperationException("Count adjustment did not conserve the signed book-to-count difference.");
        }
        Reject("INVENTORY_COUNT_NEGATIVE_PHYSICAL_QUANTITY", () => InventoryCountDifference.Create(
            InventoryQuantity.Create(-5m), InventoryQuantity.Create(-1m), cutoff));
        Reject("INVENTORY_COUNT_CUTOFF_INVALID", () => InventoryCountDifference.Create(default, default, cutoff.AddTicks(1)));
        Reject("INVENTORY_QUANTITY_OUT_OF_RANGE", () => InventoryCountDifference.Create(
            InventoryQuantity.Create(-99999999999999m), InventoryQuantity.Create(99999999999999m), cutoff));
    }

    private static void Reject(string code, Action action)
    {
        try { action(); }
        catch (InventoryInvariantException exception) when (exception.Code == code) { return; }
        throw new InvalidOperationException($"Expected count difference failure {code}.");
    }
}
