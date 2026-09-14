namespace KaguERP.Modules.Inventory.Domain;

public sealed record InventoryReservationRemainder(Guid ReservationId, long Version,
    InventoryQuantity RemainingQuantity, DateTimeOffset RecordedAt);

public sealed record InventoryPlannedReservationConsumption(Guid ReservationId, long ExpectedVersion, InventoryQuantity Quantity);

/// <summary>Pure quantity distribution; not permission to consume or evidence of physical stock.</summary>
public sealed class InventoryDispatchReservationPlan
{
    private InventoryDispatchReservationPlan(InventoryQuantity requested, InventoryQuantity unreserved,
        InventoryPlannedReservationConsumption[] consumptions)
    {
        RequestedQuantity = requested;
        UnreservedQuantity = unreserved;
        Consumptions = Array.AsReadOnly(consumptions);
    }

    public InventoryQuantity RequestedQuantity { get; }
    public InventoryQuantity UnreservedQuantity { get; }
    public InventoryQuantity ReservedQuantity => InventoryQuantity.Create(RequestedQuantity.Value - UnreservedQuantity.Value);
    public IReadOnlyList<InventoryPlannedReservationConsumption> Consumptions { get; }

    public static InventoryDispatchReservationPlan Create(InventoryQuantity requested,
        IEnumerable<InventoryReservationRemainder> reservations)
    {
        ArgumentNullException.ThrowIfNull(reservations);
        var snapshot = reservations.Take(501).ToArray();
        if (!requested.IsPositive || snapshot.Length > 500 ||
            snapshot.Any(row => row is null || row.ReservationId == Guid.Empty || row.Version <= 0 ||
                row.RemainingQuantity.IsNegative || row.RecordedAt == default || row.RecordedAt.Offset != TimeSpan.Zero ||
                row.RecordedAt.Ticks % TimeSpan.TicksPerMicrosecond != 0) ||
            snapshot.Select(row => row.ReservationId).Distinct().Count() != snapshot.Length)
            throw new InventoryInvariantException("INVENTORY_DISPATCH_RESERVATION_INPUT_INVALID",
                "Dispatch planning requires positive quantity and bounded unique reservation remainders.");
        decimal remaining = requested.Value;
        var consumptions = new List<InventoryPlannedReservationConsumption>();
        foreach (var row in snapshot.OrderBy(row => row.RecordedAt).ThenBy(row => row.ReservationId))
        {
            decimal consume = decimal.Min(remaining, row.RemainingQuantity.Value);
            if (consume == 0m) continue;
            consumptions.Add(new(row.ReservationId, row.Version, InventoryQuantity.Create(consume)));
            remaining -= consume;
            if (remaining == 0m) break;
        }
        return new(requested, InventoryQuantity.Create(remaining), consumptions.ToArray());
    }
}
