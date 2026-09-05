using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Transfers;
using KaguERP.Modules.Inventory.Domain;

namespace KaguERP.Modules.Inventory.Application.Reservations;

public sealed class AuthorizedInventoryReservationCandidate
{
    public const string RequiredPermission = "inventory.reservation.create";

    private AuthorizedInventoryReservationCandidate(
        ExecutionScope scope,
        InventoryWarehouseScopeEvidence warehouseScope,
        InventoryReservationDemandEvidence demandEvidence,
        InventoryReservationState reservation)
    {
        Scope = scope;
        WarehouseScope = warehouseScope;
        DemandEvidence = demandEvidence;
        Reservation = reservation;
    }

    public ExecutionScope Scope { get; }

    public InventoryWarehouseScopeEvidence WarehouseScope { get; }

    public InventoryReservationDemandEvidence DemandEvidence { get; }

    public InventoryReservationState Reservation { get; }

    public static AuthorizedInventoryReservationCandidate Create(
        ExecutionScope scope,
        InventoryWarehouseScopeEvidence warehouseScope,
        InventoryReservationDemandEvidence demandEvidence,
        InventoryReservationState reservation)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(warehouseScope);
        ArgumentNullException.ThrowIfNull(demandEvidence);
        ArgumentNullException.ThrowIfNull(reservation);

        scope.EnsureAllowed(reservation.TenantId, reservation.CompanyId);
        if (!scope.HasPermission(reservation.CompanyId, RequiredPermission))
        {
            throw new InventoryReservationAuthorizationException(
                "INVENTORY_RESERVATION_PERMISSION_REQUIRED",
                "The active actor cannot create inventory reservations for this company.");
        }

        try
        {
            warehouseScope.EnsureMatches(reservation.TenantId, reservation.CompanyId, scope.ActorId);
        }
        catch (InventoryTransferAuthorizationException exception)
        {
            throw new InventoryReservationAuthorizationException(
                "INVENTORY_RESERVATION_WAREHOUSE_EVIDENCE_MISMATCH",
                "Warehouse authorization evidence does not match the reservation scope.",
                exception);
        }

        if (!warehouseScope.WarehouseIds.Contains(reservation.WarehouseId))
        {
            throw new InventoryReservationAuthorizationException(
                "INVENTORY_RESERVATION_WAREHOUSE_SCOPE_REQUIRED",
                "The active actor must be scoped to the reservation warehouse.");
        }

        demandEvidence.EnsureMatches(reservation);

        return new AuthorizedInventoryReservationCandidate(
            scope, warehouseScope, demandEvidence, reservation);
    }
}

public sealed class InventoryReservationAuthorizationException(
    string code,
    string message,
    Exception? innerException = null) : InvalidOperationException(message, innerException)
{
    public string Code { get; } = code;
}
