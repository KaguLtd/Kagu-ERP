using KaguERP.Modules.Inventory.Domain;

internal static class InventoryInvoiceCostBasisChecks
{
    public static void ExactInvoiceUnitCost()
    {
        var source = new InventoryInvoiceCostLineage(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new(2026, 9, 12), new(2026, 9, 12, 0, 0, 0, TimeSpan.Zero));
        var policy = Guid.NewGuid();
        InventoryInvoiceCostBasis Basis(decimal amount, decimal quantity) => new(source, Guid.NewGuid(), Guid.NewGuid(),
            InventoryUomCode.Create("EA"), "TRY", InventoryQuantity.Create(quantity), amount);
        var third = Basis(1m, 3m).CalculateUnitCost(policy, 2);
        var midpoint = Basis(1m, 8m).CalculateUnitCost(policy, 2);
        var exact = Basis(12.5m, 2.5m).CalculateUnitCost(policy, 6);
        if (third.UnitCost != 0.33m || midpoint.UnitCost != 0.13m || exact.UnitCost != 5m ||
            exact.Scale != 6 || midpoint.RoundingPolicySnapshotId != policy || midpoint.Basis.Source != source)
            throw new InvalidOperationException("Invoice unit cost lost exact rounding or lineage.");
        if (Basis(0m, 3m).CalculateUnitCost(policy, 4).UnitCost != 0m)
            throw new InvalidOperationException("A legitimate zero-cost invoice did not remain zero.");
        if (Basis(1m, 3m).CalculateUnitCost(policy, 28).UnitCost != 0.3333333333333333333333333333m)
            throw new InvalidOperationException("Recurring decimal was rounded more than once.");
        var large = Basis(9999999999999999.9999m, 0.000001m).CalculateUnitCost(policy, 28);
        if (large.UnitCost != 9999999999999999999900m)
            throw new InvalidOperationException("Exact large unit cost overflowed due to removable trailing zeros.");
        Reject(() => Basis(-1m, 1m));
        Reject(() => Basis(1.00001m, 1m));
        Reject(() => Basis(1m, 0m));
        Reject(() => Basis(1m, 1m).CalculateUnitCost(Guid.Empty, 2));
        Reject(() => Basis(1m, 1m).CalculateUnitCost(policy, 29));
        Reject(() => Basis(9999999999999999m, 0.000007m).CalculateUnitCost(policy, 28));
        for (int amount = 0; amount <= 100; amount++)
            for (int quantity = 1; quantity <= 25; quantity++)
                if (Basis(amount, quantity).CalculateUnitCost(policy, 2).UnitCost !=
                    decimal.Round((decimal)amount / quantity, 2, MidpointRounding.AwayFromZero))
                    throw new InvalidOperationException("Bounded unit-cost samples disagree with the rounding policy.");
    }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InventoryInvariantException) { return; }
        throw new InvalidOperationException("Invalid invoice cost basis or unsupported precision was accepted.");
    }
}
