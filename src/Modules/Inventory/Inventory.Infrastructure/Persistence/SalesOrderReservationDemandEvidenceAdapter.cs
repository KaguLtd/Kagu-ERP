using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Application.Transfers;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Sales.Contracts.Reservations;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public sealed class SalesOrderReservationDemandEvidenceAdapter(
    ISalesOrderReservationDemandSource source)
{
    public const string DemandSourceType = "sales.order";

    private readonly ISalesOrderReservationDemandSource source =
        source ?? throw new ArgumentNullException(nameof(source));

    public async ValueTask<IReadOnlyList<InventoryReservationDemandEvidence>> LoadAsync(
        SalesOrderReservationDemandQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        SalesOrderReservationDemandSnapshot snapshot =
            await source.LoadAsync(query, cancellationToken) ??
            throw new InventoryReservationDemandUnavailableException();
        if (snapshot.TenantId != query.TenantId || snapshot.CompanyId != query.CompanyId ||
            snapshot.OrderId != query.OrderId || snapshot.ConfirmedVersion != query.ConfirmedVersion)
        {
            throw new InventoryReservationDemandContractMismatchException();
        }

        InventoryReservationDemandEvidence[] evidence = snapshot.Lines
            .Select(line => InventoryReservationDemandEvidence.Create(
                snapshot.TenantId,
                snapshot.CompanyId,
                InventoryDemandSourceIdentity.Create(
                    DemandSourceType,
                    snapshot.OrderId,
                    line.OrderLineId,
                    snapshot.ConfirmedVersion),
                line.ItemId,
                InventoryUomCode.Create(line.BaseUomCode),
                InventoryQuantity.Create(line.MaximumReservableQuantity)))
            .ToArray();
        return Array.AsReadOnly(evidence);
    }

    public async ValueTask<AuthorizedInventoryReservationCandidate> BuildCandidateAsync(
        SalesOrderReservationDemandQuery query,
        Guid orderLineId,
        Guid reservationId,
        Guid warehouseId,
        decimal reservedQuantity,
        DateTimeOffset? expiresAt,
        ExecutionScope scope,
        InventoryWarehouseScopeEvidence warehouseScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(warehouseScope);
        if (orderLineId == Guid.Empty)
        {
            throw new InventoryReservationDemandLineUnavailableException();
        }

        IReadOnlyList<InventoryReservationDemandEvidence> demand =
            await LoadAsync(query, cancellationToken);
        InventoryReservationDemandEvidence line = demand.SingleOrDefault(
            item => item.Source.SourceLineId == orderLineId) ??
            throw new InventoryReservationDemandLineUnavailableException();
        InventoryReservationState reservation = InventoryReservationState.CreateActive(
            reservationId,
            query.TenantId,
            query.CompanyId,
            line.ItemId,
            warehouseId,
            line.BaseUom,
            line.Source,
            InventoryQuantity.Create(reservedQuantity),
            expiresAt);

        return AuthorizedInventoryReservationCandidate.Create(
            scope, warehouseScope, line, reservation);
    }
}

public sealed class InventoryReservationDemandUnavailableException()
    : InvalidOperationException("The exact confirmed reservation demand is unavailable.")
{
    public string Code { get; } = "INVENTORY_RESERVATION_DEMAND_UNAVAILABLE";
}

public sealed class InventoryReservationDemandContractMismatchException()
    : InvalidOperationException("The producer returned reservation demand outside the requested scope or version.")
{
    public string Code { get; } = "INVENTORY_RESERVATION_DEMAND_CONTRACT_MISMATCH";
}

public sealed class InventoryReservationDemandLineUnavailableException()
    : InvalidOperationException("The requested reservation demand line is unavailable.")
{
    public string Code { get; } = "INVENTORY_RESERVATION_DEMAND_LINE_UNAVAILABLE";
}
