using KaguERP.Modules.Inventory.Domain;

namespace KaguERP.Modules.Inventory.Application.Reservations;

public sealed class InventoryReservationDemandEvidence
{
    private InventoryReservationDemandEvidence(
        Guid tenantId,
        Guid companyId,
        InventoryDemandSourceIdentity source,
        Guid itemId,
        InventoryUomCode baseUom,
        InventoryQuantity maximumReservableQuantity)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        Source = source;
        ItemId = itemId;
        BaseUom = baseUom;
        MaximumReservableQuantity = maximumReservableQuantity;
    }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public InventoryDemandSourceIdentity Source { get; }

    public Guid ItemId { get; }

    public InventoryUomCode BaseUom { get; }

    public InventoryQuantity MaximumReservableQuantity { get; }

    public static InventoryReservationDemandEvidence Create(
        Guid tenantId,
        Guid companyId,
        InventoryDemandSourceIdentity source,
        Guid itemId,
        InventoryUomCode baseUom,
        InventoryQuantity maximumReservableQuantity)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (tenantId == Guid.Empty || companyId == Guid.Empty || itemId == Guid.Empty ||
            string.IsNullOrEmpty(baseUom.Value) || !maximumReservableQuantity.IsPositive)
        {
            throw new InventoryReservationAuthorizationException(
                "INVENTORY_RESERVATION_DEMAND_EVIDENCE_INVALID",
                "Reservation demand evidence requires scope, item, base UOM and positive capacity.");
        }

        return new InventoryReservationDemandEvidence(
            tenantId,
            companyId,
            source,
            itemId,
            baseUom,
            maximumReservableQuantity);
    }

    public void EnsureMatches(InventoryReservationState reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (TenantId != reservation.TenantId || CompanyId != reservation.CompanyId ||
            Source != reservation.Source || ItemId != reservation.ItemId ||
            BaseUom != reservation.BaseUom)
        {
            throw new InventoryReservationAuthorizationException(
                "INVENTORY_RESERVATION_DEMAND_EVIDENCE_MISMATCH",
                "Demand evidence does not match the exact reservation source, scope, item and base UOM.");
        }
        if (reservation.ReservedQuantity.Value > MaximumReservableQuantity.Value)
        {
            throw new InventoryReservationAuthorizationException(
                "INVENTORY_RESERVATION_EXCEEDS_DEMAND",
                "Reservation quantity cannot exceed the authoritative demand capacity.");
        }
    }
}
