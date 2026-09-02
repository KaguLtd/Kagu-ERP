namespace KaguERP.Modules.Inventory.Domain;

public readonly record struct InventoryQuantity
{
    private const decimal MaximumMagnitude = 99999999999999.999999m;

    private InventoryQuantity(decimal value)
    {
        Value = value;
    }

    public decimal Value { get; }

    public bool IsZero => Value == decimal.Zero;

    public bool IsPositive => Value > decimal.Zero;

    public bool IsNegative => Value < decimal.Zero;

    public static InventoryQuantity Create(decimal value)
    {
        if (decimal.Abs(value) > MaximumMagnitude || decimal.Round(value, 6) != value)
        {
            throw new InventoryInvariantException(
                "INVENTORY_QUANTITY_OUT_OF_RANGE",
                "Inventory quantity must fit PostgreSQL numeric(20,6) without rounding.");
        }

        return new InventoryQuantity(value);
    }

    public static InventoryQuantity operator +(InventoryQuantity left, InventoryQuantity right)
    {
        try
        {
            return Create(checked(left.Value + right.Value));
        }
        catch (OverflowException exception)
        {
            throw new InventoryInvariantException(
                "INVENTORY_QUANTITY_OUT_OF_RANGE",
                $"Inventory quantity arithmetic exceeded numeric(20,6): {exception.Message}");
        }
    }

    public static InventoryQuantity operator -(InventoryQuantity value) => Create(-value.Value);
}
