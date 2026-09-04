using System.Collections.Frozen;

namespace KaguERP.Modules.Inventory.Application.Transfers;

public sealed class InventoryWarehouseScopeEvidence
{
    private readonly FrozenSet<Guid> warehouseIds;

    private InventoryWarehouseScopeEvidence(
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        FrozenSet<Guid> warehouseIds)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        ActorId = actorId;
        this.warehouseIds = warehouseIds;
    }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public Guid ActorId { get; }

    public IReadOnlySet<Guid> WarehouseIds => warehouseIds;

    public static InventoryWarehouseScopeEvidence Create(
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        IEnumerable<Guid> warehouseIds)
    {
        ArgumentNullException.ThrowIfNull(warehouseIds);
        if (tenantId == Guid.Empty || companyId == Guid.Empty || actorId == Guid.Empty)
        {
            throw new InventoryTransferAuthorizationException(
                "INVENTORY_TRANSFER_WAREHOUSE_EVIDENCE_SCOPE_INVALID",
                "Warehouse authorization evidence requires tenant, company and actor identities.");
        }

        Guid[] snapshot = warehouseIds.ToArray();
        FrozenSet<Guid> warehouses = snapshot.ToFrozenSet();
        if (snapshot.Length != warehouses.Count || warehouses.Contains(Guid.Empty))
        {
            throw new InventoryTransferAuthorizationException(
                "INVENTORY_TRANSFER_WAREHOUSE_SCOPE_INVALID",
                "Warehouse scope must contain unique, non-empty warehouse identities.");
        }

        return new InventoryWarehouseScopeEvidence(tenantId, companyId, actorId, warehouses);
    }

    public void EnsureMatches(Guid tenantId, Guid companyId, Guid actorId)
    {
        if (TenantId != tenantId || CompanyId != companyId || ActorId != actorId)
        {
            throw new InventoryTransferAuthorizationException(
                "INVENTORY_TRANSFER_WAREHOUSE_EVIDENCE_MISMATCH",
                "Warehouse authorization evidence does not match the active tenant, company and actor.");
        }
    }
}
