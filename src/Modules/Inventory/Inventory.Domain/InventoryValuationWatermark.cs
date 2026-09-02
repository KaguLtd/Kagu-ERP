namespace KaguERP.Modules.Inventory.Domain;

public sealed record InventoryValuationWatermark
{
    private InventoryValuationWatermark(
        Guid tenantId,
        Guid companyId,
        Guid itemId,
        Guid warehouseId,
        InventoryPosition position,
        long projectionGeneration,
        DateTimeOffset recordedCutoff,
        string sourceChecksumSha256)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        ItemId = itemId;
        WarehouseId = warehouseId;
        Position = position;
        ProjectionGeneration = projectionGeneration;
        RecordedCutoff = recordedCutoff;
        SourceChecksumSha256 = sourceChecksumSha256;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid ItemId { get; }
    public Guid WarehouseId { get; }
    public InventoryPosition Position { get; }
    public long ProjectionGeneration { get; }
    public DateTimeOffset RecordedCutoff { get; }
    public string SourceChecksumSha256 { get; }

    public static InventoryValuationWatermark Create(
        Guid tenantId,
        Guid companyId,
        Guid itemId,
        Guid warehouseId,
        InventoryPosition position,
        long projectionGeneration,
        DateTimeOffset recordedCutoff,
        string sourceChecksumSha256)
    {
        RequireId(tenantId, "INVENTORY_WATERMARK_TENANT_REQUIRED");
        RequireId(companyId, "INVENTORY_WATERMARK_COMPANY_REQUIRED");
        RequireId(itemId, "INVENTORY_WATERMARK_ITEM_REQUIRED");
        RequireId(warehouseId, "INVENTORY_WATERMARK_WAREHOUSE_REQUIRED");
        _ = InventoryPosition.Create(position.EffectiveDate, position.SequenceKey);
        if (projectionGeneration <= 0)
        {
            throw new InventoryInvariantException(
                "INVENTORY_WATERMARK_GENERATION_INVALID",
                "Inventory projection generation must be positive.");
        }
        if (recordedCutoff.Offset != TimeSpan.Zero)
        {
            throw new InventoryInvariantException(
                "INVENTORY_WATERMARK_CUTOFF_NOT_UTC",
                "Inventory watermark cutoff must be UTC.");
        }

        return new InventoryValuationWatermark(
            tenantId,
            companyId,
            itemId,
            warehouseId,
            position,
            projectionGeneration,
            recordedCutoff,
            RequireSha256(sourceChecksumSha256));
    }

    internal static string RequireSha256(string value)
    {
        string canonical = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (canonical.Length != 64 || canonical.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new InventoryInvariantException(
                "INVENTORY_CHECKSUM_INVALID",
                "Inventory checksum must be a 64-character hexadecimal SHA-256 value.");
        }

        return canonical;
    }

    private static void RequireId(Guid id, string code)
    {
        if (id == Guid.Empty)
        {
            throw new InventoryInvariantException(code, "Inventory watermark identity is required.");
        }
    }
}
