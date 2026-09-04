using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Transfers;

namespace KaguERP.Modules.Inventory.Application.Queries;

public sealed class InventoryMovementCursor
{
    private InventoryMovementCursor(
        DateOnly effectiveDate,
        DateTimeOffset recordedAt,
        Guid warehouseId,
        long sequenceKey,
        Guid movementId)
    {
        EffectiveDate = effectiveDate;
        RecordedAt = recordedAt;
        WarehouseId = warehouseId;
        SequenceKey = sequenceKey;
        MovementId = movementId;
    }

    public DateOnly EffectiveDate { get; }

    public DateTimeOffset RecordedAt { get; }

    public Guid WarehouseId { get; }

    public long SequenceKey { get; }

    public Guid MovementId { get; }

    public static InventoryMovementCursor Create(
        DateOnly effectiveDate,
        DateTimeOffset recordedAt,
        Guid warehouseId,
        long sequenceKey,
        Guid movementId)
    {
        if (recordedAt.Offset != TimeSpan.Zero || warehouseId == Guid.Empty ||
            movementId == Guid.Empty || sequenceKey <= 0)
        {
            throw new InventoryMovementQueryException(
                "INVENTORY_MOVEMENT_CURSOR_INVALID",
                "Inventory movement cursor requires UTC time, identities and a positive sequence.");
        }

        return new InventoryMovementCursor(
            effectiveDate,
            recordedAt,
            warehouseId,
            sequenceKey,
            movementId);
    }
}

public sealed class AuthorizedInventoryMovementQuery
{
    public const string RequiredPermission = "inventory.movement.view";

    private AuthorizedInventoryMovementQuery(
        ExecutionScope scope,
        InventoryWarehouseScopeEvidence warehouseScope,
        Guid companyId,
        Guid itemId,
        DateOnly effectiveFrom,
        DateOnly effectiveThrough,
        DateTimeOffset recordedCutoff,
        int pageSize,
        InventoryMovementCursor? after)
    {
        Scope = scope;
        WarehouseScope = warehouseScope;
        CompanyId = companyId;
        ItemId = itemId;
        EffectiveFrom = effectiveFrom;
        EffectiveThrough = effectiveThrough;
        RecordedCutoff = recordedCutoff;
        PageSize = pageSize;
        After = after;
    }

    public ExecutionScope Scope { get; }
    public InventoryWarehouseScopeEvidence WarehouseScope { get; }
    public Guid CompanyId { get; }
    public Guid ItemId { get; }
    public DateOnly EffectiveFrom { get; }
    public DateOnly EffectiveThrough { get; }
    public DateTimeOffset RecordedCutoff { get; }
    public int PageSize { get; }
    public InventoryMovementCursor? After { get; }

    public static AuthorizedInventoryMovementQuery Create(
        ExecutionScope scope,
        InventoryWarehouseScopeEvidence warehouseScope,
        Guid companyId,
        Guid itemId,
        DateOnly effectiveFrom,
        DateOnly effectiveThrough,
        DateTimeOffset recordedCutoff,
        int pageSize,
        InventoryMovementCursor? after = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(warehouseScope);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, RequiredPermission))
        {
            throw new InventoryMovementQueryException(
                "INVENTORY_MOVEMENT_PERMISSION_REQUIRED",
                "The active actor cannot view inventory movements for this company.");
        }
        if (itemId == Guid.Empty || effectiveThrough < effectiveFrom ||
            recordedCutoff.Offset != TimeSpan.Zero || pageSize is < 1 or > 200 ||
            (after is not null &&
             (after.EffectiveDate < effectiveFrom || after.EffectiveDate > effectiveThrough ||
              after.RecordedAt > recordedCutoff)))
        {
            throw new InventoryMovementQueryException(
                "INVENTORY_MOVEMENT_QUERY_INVALID",
                "Inventory movement query requires an item, valid dates, UTC cutoff and page size from 1 to 200.");
        }

        warehouseScope.EnsureMatches(scope.TenantId, companyId, scope.ActorId);
        if (warehouseScope.WarehouseIds.Count == 0)
        {
            throw new InventoryMovementQueryException(
                "INVENTORY_MOVEMENT_WAREHOUSE_SCOPE_REQUIRED",
                "The active actor must have at least one warehouse assignment.");
        }

        return new AuthorizedInventoryMovementQuery(
            scope,
            warehouseScope,
            companyId,
            itemId,
            effectiveFrom,
            effectiveThrough,
            recordedCutoff,
            pageSize,
            after);
    }
}

public sealed class InventoryMovementQueryException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
