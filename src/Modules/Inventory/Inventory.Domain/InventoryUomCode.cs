namespace KaguERP.Modules.Inventory.Domain;

public readonly record struct InventoryUomCode
{
    private InventoryUomCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static InventoryUomCode Create(string value)
    {
        string canonical = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (canonical.Length is 0 or > 16 ||
            canonical.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new InventoryInvariantException(
                "INVENTORY_UOM_CODE_INVALID",
                "Inventory UOM code must contain 1-16 ASCII letters, digits or hyphens.");
        }

        return new InventoryUomCode(canonical);
    }

    public override string ToString() => Value;
}
