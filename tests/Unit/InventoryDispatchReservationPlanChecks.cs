using KaguERP.Modules.Inventory.Domain;

internal static class InventoryDispatchReservationPlanChecks
{
    public static void DistributionConservesQuantity()
    {
        var at = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        var first = new InventoryReservationRemainder(Guid.NewGuid(), 2, InventoryQuantity.Create(3m), at);
        var second = new InventoryReservationRemainder(Guid.NewGuid(), 1, InventoryQuantity.Create(4m), at.AddSeconds(1));
        var zero = new InventoryReservationRemainder(Guid.NewGuid(), 3, InventoryQuantity.Create(0m), at.AddSeconds(-1));
        var input = new List<InventoryReservationRemainder> { second, zero, first };
        var plan = InventoryDispatchReservationPlan.Create(InventoryQuantity.Create(5m), input);
        input.Clear();
        if (plan.Consumptions.Count != 2 || plan.Consumptions[0].ReservationId != first.ReservationId ||
            plan.Consumptions[0].ExpectedVersion != 2 || plan.Consumptions[0].Quantity.Value != 3m ||
            plan.Consumptions[1].Quantity.Value != 2m || !plan.UnreservedQuantity.IsZero)
            throw new InvalidOperationException("Dispatch distribution was not deterministic, bounded or immutable.");
        var uncovered = InventoryDispatchReservationPlan.Create(InventoryQuantity.Create(10m), [first, second]);
        if (uncovered.ReservedQuantity.Value != 7m || uncovered.UnreservedQuantity.Value != 3m)
            throw new InvalidOperationException("Unreserved dispatch quantity was suppressed or reserved stock was invented.");
        var empty = InventoryDispatchReservationPlan.Create(InventoryQuantity.Create(2m), []);
        if (empty.UnreservedQuantity.Value != 2m || empty.Consumptions.Count != 0)
            throw new InvalidOperationException("A dispatch without reservations must retain its full unreserved quantity.");
        for (int requested = 1; requested <= 50; requested++)
        {
            var sample = InventoryDispatchReservationPlan.Create(InventoryQuantity.Create(requested / 10m), [second, first]);
            if (sample.Consumptions.Sum(row => row.Quantity.Value) + sample.UnreservedQuantity.Value != sample.RequestedQuantity.Value ||
                sample.Consumptions.Any(row => row.Quantity.Value > (row.ReservationId == first.ReservationId ? 3m : 4m)))
                throw new InvalidOperationException("Dispatch allocation violated conservation or per-reservation capacity.");
        }
        Reject(() => InventoryDispatchReservationPlan.Create(default, []));
        Reject(() => InventoryDispatchReservationPlan.Create(InventoryQuantity.Create(1m), [first, first]));
        Reject(() => InventoryDispatchReservationPlan.Create(InventoryQuantity.Create(1m), [first with { Version = 0 }]));
        Reject(() => InventoryDispatchReservationPlan.Create(InventoryQuantity.Create(1m), [first with { RemainingQuantity = InventoryQuantity.Create(-1m) }]));
        Reject(() => InventoryDispatchReservationPlan.Create(InventoryQuantity.Create(1m),
            Enumerable.Range(0, 501).Select(_ => first with { ReservationId = Guid.NewGuid() })));
    }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InventoryInvariantException exception) when (exception.Code == "INVENTORY_DISPATCH_RESERVATION_INPUT_INVALID") { return; }
        throw new InvalidOperationException("Invalid reservation planning input was accepted.");
    }
}
