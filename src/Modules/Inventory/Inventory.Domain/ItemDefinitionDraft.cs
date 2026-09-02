namespace KaguERP.Modules.Inventory.Domain;

public enum ItemKind
{
    Stock = 1,
    NonStock = 2,
    Service = 3,
    Expense = 4,
}

public enum ItemTrackingPolicy
{
    None = 1,
    Lot = 2,
    Serial = 3,
}

public sealed record ItemDefinitionDraft
{
    private ItemDefinitionDraft(
        Guid itemId,
        Guid tenantId,
        string code,
        string name,
        ItemKind kind,
        InventoryUomCode baseUom,
        ItemTrackingPolicy trackingPolicy,
        bool allowsFractionalQuantity,
        int quantityScale)
    {
        ItemId = itemId;
        TenantId = tenantId;
        Code = code;
        Name = name;
        Kind = kind;
        BaseUom = baseUom;
        TrackingPolicy = trackingPolicy;
        AllowsFractionalQuantity = allowsFractionalQuantity;
        QuantityScale = quantityScale;
    }

    public Guid ItemId { get; }
    public Guid TenantId { get; }
    public string Code { get; }
    public string Name { get; }
    public ItemKind Kind { get; }
    public InventoryUomCode BaseUom { get; }
    public ItemTrackingPolicy TrackingPolicy { get; }
    public bool AllowsFractionalQuantity { get; }
    public int QuantityScale { get; }

    public static ItemDefinitionDraft Create(
        Guid itemId,
        Guid tenantId,
        string code,
        string name,
        ItemKind kind,
        InventoryUomCode baseUom,
        ItemTrackingPolicy trackingPolicy,
        bool allowsFractionalQuantity,
        int quantityScale)
    {
        RequireId(itemId, "INVENTORY_ITEM_ID_REQUIRED");
        RequireId(tenantId, "INVENTORY_ITEM_TENANT_REQUIRED");
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(trackingPolicy))
        {
            throw new InventoryInvariantException("INVENTORY_ITEM_CLASSIFICATION_INVALID", "Item classification is invalid.");
        }

        string canonicalCode = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (canonicalCode.Length is 0 or > 64 ||
            canonicalCode.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '.' and not '_'))
        {
            throw new InventoryInvariantException(
                "INVENTORY_ITEM_CODE_INVALID",
                "Item code must contain 1-64 ASCII letters, digits, hyphens, dots or underscores.");
        }

        string canonicalName = (name ?? string.Empty).Trim();
        if (canonicalName.Length is 0 or > 200)
        {
            throw new InventoryInvariantException("INVENTORY_ITEM_NAME_INVALID", "Item name must contain 1-200 characters.");
        }
        if (baseUom == default)
        {
            throw new InventoryInvariantException("INVENTORY_ITEM_BASE_UOM_REQUIRED", "Item base UOM is required.");
        }
        if (kind != ItemKind.Stock && trackingPolicy != ItemTrackingPolicy.None)
        {
            throw new InventoryInvariantException(
                "INVENTORY_ITEM_TRACKING_NOT_APPLICABLE",
                "Only stock items can use lot or serial tracking.");
        }
        if (quantityScale is < 0 or > 6 ||
            (!allowsFractionalQuantity && quantityScale != 0) ||
            (allowsFractionalQuantity && quantityScale == 0))
        {
            throw new InventoryInvariantException(
                "INVENTORY_ITEM_QUANTITY_SCALE_INVALID",
                "Quantity scale must be zero for whole quantities and 1-6 for fractional quantities.");
        }
        if (trackingPolicy == ItemTrackingPolicy.Serial && allowsFractionalQuantity)
        {
            throw new InventoryInvariantException(
                "INVENTORY_SERIAL_ITEM_FRACTIONAL",
                "Serial-tracked items cannot allow fractional quantities.");
        }

        return new ItemDefinitionDraft(
            itemId,
            tenantId,
            canonicalCode,
            canonicalName,
            kind,
            baseUom,
            trackingPolicy,
            allowsFractionalQuantity,
            quantityScale);
    }

    private static void RequireId(Guid id, string code)
    {
        if (id == Guid.Empty)
        {
            throw new InventoryInvariantException(code, "Item identity is required.");
        }
    }
}
