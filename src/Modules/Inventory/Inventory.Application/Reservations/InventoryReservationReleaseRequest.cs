using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Transfers;
using KaguERP.Modules.Inventory.Domain;

namespace KaguERP.Modules.Inventory.Application.Reservations;

public sealed class InventoryReservationReleaseRequest
{
    public const string RequiredPermission = "inventory.reservation.release";

    public InventoryReservationReleaseRequest(ExecutionScope scope, Guid companyId, Guid warehouseId,
        Guid reservationId, long expectedVersion, Guid correlationId, DateOnly effectiveDate, string reason)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, RequiredPermission))
            throw new InventoryReservationAuthorizationException("INVENTORY_RESERVATION_RELEASE_PERMISSION_REQUIRED",
                "The active actor cannot release reservations for this company.");
        string normalized = reason?.Trim() ?? string.Empty;
        if (warehouseId == Guid.Empty || reservationId == Guid.Empty || correlationId == Guid.Empty ||
            expectedVersion <= 0 || expectedVersion == long.MaxValue || normalized.Length is < 1 or > 1000)
            throw new ArgumentException("Release requires identities, expected version and a reason of 1–1000 characters.");
        Scope = scope;
        CompanyId = companyId;
        WarehouseId = warehouseId;
        ReservationId = reservationId;
        ExpectedVersion = expectedVersion;
        CorrelationId = correlationId;
        EffectiveDate = effectiveDate;
        Reason = normalized;
    }

    public ExecutionScope Scope { get; }
    public Guid CompanyId { get; }
    public Guid WarehouseId { get; }
    public Guid ReservationId { get; }
    public long ExpectedVersion { get; }
    public Guid CorrelationId { get; }
    public DateOnly EffectiveDate { get; }
    public string Reason { get; }

    public void EnsureWarehouseAccess(InventoryWarehouseScopeEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        try
        {
            evidence.EnsureMatches(Scope.TenantId, CompanyId, Scope.ActorId);
        }
        catch (InventoryTransferAuthorizationException exception)
        {
            throw new InventoryReservationAuthorizationException("INVENTORY_RESERVATION_WAREHOUSE_EVIDENCE_MISMATCH",
                "Warehouse evidence does not match the release scope.", exception);
        }
        if (!evidence.WarehouseIds.Contains(WarehouseId))
            throw new InventoryReservationAuthorizationException("INVENTORY_RESERVATION_WAREHOUSE_SCOPE_REQUIRED",
                "The active actor must be scoped to the reservation warehouse.");
    }
}

public sealed record InventoryReservationReleaseResult(Guid ReservationId, Guid EventId, long Version,
    InventoryQuantity ReleasedQuantity, InventoryQuantity ConsumedQuantity, DateTimeOffset RecordedAt);
