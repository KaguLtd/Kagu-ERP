namespace KaguERP.Modules.Inventory.Domain;

public sealed record StockMovementSourceIdentity
{
    private StockMovementSourceIdentity(
        Guid tenantId,
        Guid companyId,
        string sourceType,
        Guid sourceEventId,
        Guid sourceLineId,
        long sourceVersion,
        string postingPurpose)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        SourceType = sourceType;
        SourceEventId = sourceEventId;
        SourceLineId = sourceLineId;
        SourceVersion = sourceVersion;
        PostingPurpose = postingPurpose;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public string SourceType { get; }
    public Guid SourceEventId { get; }
    public Guid SourceLineId { get; }
    public long SourceVersion { get; }
    public string PostingPurpose { get; }

    public static StockMovementSourceIdentity Create(
        Guid tenantId,
        Guid companyId,
        string sourceType,
        Guid sourceEventId,
        Guid sourceLineId,
        long sourceVersion,
        string postingPurpose)
    {
        RequireId(tenantId, "INVENTORY_SOURCE_TENANT_REQUIRED", "Inventory source tenant is required.");
        RequireId(companyId, "INVENTORY_SOURCE_COMPANY_REQUIRED", "Inventory source company is required.");
        RequireId(sourceEventId, "INVENTORY_SOURCE_EVENT_REQUIRED", "Inventory source event is required.");
        RequireId(sourceLineId, "INVENTORY_SOURCE_LINE_REQUIRED", "Inventory source line is required.");
        if (sourceVersion <= 0)
        {
            throw new InventoryInvariantException(
                "INVENTORY_SOURCE_VERSION_INVALID",
                "Inventory source version must be positive.");
        }

        return new StockMovementSourceIdentity(
            tenantId,
            companyId,
            Canonicalize(sourceType, "INVENTORY_SOURCE_TYPE_REQUIRED", "Inventory source type"),
            sourceEventId,
            sourceLineId,
            sourceVersion,
            Canonicalize(postingPurpose, "INVENTORY_POSTING_PURPOSE_REQUIRED", "Inventory posting purpose"));
    }

    private static string Canonicalize(string value, string code, string field)
    {
        string canonical = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (canonical.Length is 0 or > 128)
        {
            throw new InventoryInvariantException(code, $"{field} must contain between 1 and 128 characters.");
        }

        return canonical;
    }

    private static void RequireId(Guid id, string code, string message)
    {
        if (id == Guid.Empty)
        {
            throw new InventoryInvariantException(code, message);
        }
    }
}
