namespace KaguERP.Modules.Inventory.Domain;

public sealed record ItemCompanyActivationDraft
{
    private ItemCompanyActivationDraft(
        Guid tenantId,
        Guid companyId,
        ItemDefinitionDraft item,
        bool isActive,
        long expectedVersion)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        Item = item;
        IsActive = isActive;
        ExpectedVersion = expectedVersion;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public ItemDefinitionDraft Item { get; }
    public bool IsActive { get; }
    public long ExpectedVersion { get; }

    public static ItemCompanyActivationDraft Create(
        Guid tenantId,
        Guid companyId,
        ItemDefinitionDraft item,
        bool isActive,
        long expectedVersion)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty)
        {
            throw new InventoryInvariantException(
                "INVENTORY_ITEM_COMPANY_SCOPE_REQUIRED",
                "Item company activation requires tenant and company scope.");
        }
        ArgumentNullException.ThrowIfNull(item);
        if (item.TenantId != tenantId)
        {
            throw new InventoryInvariantException(
                "INVENTORY_ITEM_COMPANY_TENANT_MISMATCH",
                "Item and company activation must belong to the same tenant.");
        }
        if (expectedVersion <= 0)
        {
            throw new InventoryInvariantException(
                "INVENTORY_ITEM_COMPANY_VERSION_INVALID",
                "Item company activation requires a positive concurrency version.");
        }

        return new ItemCompanyActivationDraft(tenantId, companyId, item, isActive, expectedVersion);
    }
}
