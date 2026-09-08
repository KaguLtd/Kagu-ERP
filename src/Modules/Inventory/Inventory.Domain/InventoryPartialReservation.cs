namespace KaguERP.Modules.Inventory.Domain;

public sealed record InventoryPartialReservation
{
    private InventoryPartialReservation(InventoryQuantity reserved, InventoryQuantity unreserved)
    {
        Reserved = reserved;
        Unreserved = unreserved;
    }

    public InventoryQuantity Reserved { get; }
    public InventoryQuantity Unreserved { get; }

    public static InventoryPartialReservation Calculate(
        InventoryQuantity requested,
        InventoryQuantity onHand,
        InventoryQuantity alreadyReserved,
        InventoryQuantity blocked)
    {
        if (!requested.IsPositive || alreadyReserved.IsNegative || blocked.IsNegative)
        {
            throw new InventoryInvariantException("INVENTORY_RESERVATION_CAPACITY_INVALID",
                "Demand must be positive; reserved and blocked quantities cannot be negative.");
        }
        decimal available = decimal.Max(0m, onHand.Value - alreadyReserved.Value - blocked.Value);
        decimal allocated = decimal.Min(requested.Value, available);
        return new InventoryPartialReservation(InventoryQuantity.Create(allocated),
            InventoryQuantity.Create(requested.Value - allocated));
    }
}
