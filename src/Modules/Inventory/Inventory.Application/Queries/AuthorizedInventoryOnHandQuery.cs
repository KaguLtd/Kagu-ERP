using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Transfers;

namespace KaguERP.Modules.Inventory.Application.Queries;

public sealed class AuthorizedInventoryOnHandQuery
{
    public const string RequiredPermission = "inventory.quantity.view";

    private AuthorizedInventoryOnHandQuery(
        ExecutionScope scope,
        InventoryWarehouseScopeEvidence warehouseScope,
        Guid companyId,
        DateOnly effectiveAsOf,
        DateTimeOffset recordedCutoff,
        Guid? itemId)
    {
        Scope = scope;
        WarehouseScope = warehouseScope;
        CompanyId = companyId;
        EffectiveAsOf = effectiveAsOf;
        RecordedCutoff = recordedCutoff;
        ItemId = itemId;
    }

    public ExecutionScope Scope { get; }

    public InventoryWarehouseScopeEvidence WarehouseScope { get; }

    public Guid CompanyId { get; }

    public DateOnly EffectiveAsOf { get; }

    public DateTimeOffset RecordedCutoff { get; }

    public Guid? ItemId { get; }

    public static AuthorizedInventoryOnHandQuery Create(
        ExecutionScope scope,
        InventoryWarehouseScopeEvidence warehouseScope,
        Guid companyId,
        DateOnly effectiveAsOf,
        DateTimeOffset recordedCutoff,
        Guid? itemId = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(warehouseScope);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, RequiredPermission))
        {
            throw new InventoryOnHandAuthorizationException(
                "INVENTORY_ON_HAND_PERMISSION_REQUIRED",
                "The active actor cannot view inventory quantities for this company.");
        }
        if (recordedCutoff.Offset != TimeSpan.Zero)
        {
            throw new InventoryOnHandAuthorizationException(
                "INVENTORY_ON_HAND_RECORDED_CUTOFF_NOT_UTC",
                "Inventory quantity recorded cutoff must be UTC.");
        }
        if (itemId == Guid.Empty)
        {
            throw new InventoryOnHandAuthorizationException(
                "INVENTORY_ON_HAND_ITEM_INVALID",
                "Inventory quantity item filter cannot be empty.");
        }

        warehouseScope.EnsureMatches(scope.TenantId, companyId, scope.ActorId);
        if (warehouseScope.WarehouseIds.Count == 0)
        {
            throw new InventoryOnHandAuthorizationException(
                "INVENTORY_ON_HAND_WAREHOUSE_SCOPE_REQUIRED",
                "The active actor must have at least one warehouse assignment.");
        }

        return new AuthorizedInventoryOnHandQuery(
            scope,
            warehouseScope,
            companyId,
            effectiveAsOf,
            recordedCutoff,
            itemId);
    }
}

public sealed class InventoryOnHandAuthorizationException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
