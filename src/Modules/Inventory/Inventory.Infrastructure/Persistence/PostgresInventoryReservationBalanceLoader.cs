using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Domain;
using Npgsql;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public sealed record InventoryReservationBalanceSnapshot(
    DateOnly EffectiveAsOf,
    DateTimeOffset RecordedCutoff,
    InventoryQuantity OnHand,
    decimal CreatedQuantityAtPosition,
    decimal CreatedQuantityForDemand,
    decimal ActiveReservedAtPosition,
    decimal CommittedQuantityForDemand,
    decimal BlockedAtPosition);

public static class PostgresInventoryReservationBalanceLoader
{
    /// <summary>
    /// Loads gross creation totals, lifecycle balances and blocked quantity in the same statement snapshot.
    /// The caller must acquire the request gate before this loader and keep the transaction open.
    /// Demand evidence must be loaded from the producer's contract in that same transaction.
    /// </summary>
    public static async ValueTask<InventoryReservationBalanceSnapshot> LoadAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, InventoryReservationRequest request,
        InventoryReservationDemandEvidence demand, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(demand);
        if (!ReferenceEquals(transaction.Connection, connection) ||
            transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
        {
            throw new InvalidOperationException("Reservation balances require the caller's ReadCommitted transaction.");
        }
        var warehouses = await PostgresInventoryWarehouseScopeLoader.LoadAsync(
            connection, transaction, request.Scope, request.CompanyId, cancellationToken);
        AuthorizedInventoryReservationCandidate.EnsureAccess(request.Scope, warehouses,
            request.Scope.TenantId, request.CompanyId, request.WarehouseId);
        if (demand.TenantId != request.Scope.TenantId || demand.CompanyId != request.CompanyId ||
            demand.Source != request.Source)
        {
            throw new InventoryReservationAuthorizationException("INVENTORY_RESERVATION_DEMAND_EVIDENCE_MISMATCH",
                "Demand evidence must match the exact request scope and source version.");
        }
        if (request.RequestedQuantity.Value > demand.MaximumReservableQuantity.Value)
        {
            throw new InventoryReservationAuthorizationException("INVENTORY_RESERVATION_EXCEEDS_DEMAND",
                "Requested quantity exceeds the authoritative demand capacity.");
        }

        await PostgresInventoryDemandLock.AcquireAsync(connection, transaction, request.Scope,
            request.CompanyId, [demand.Source], cancellationToken);
        await PostgresInventoryPositionLock.AcquireAsync(connection, transaction, request.Scope, request.CompanyId,
            [new(demand.ItemId, request.WarehouseId, demand.BaseUom)], cancellationToken);

        warehouses = await PostgresInventoryWarehouseScopeLoader.LoadAsync(
            connection, transaction, request.Scope, request.CompanyId, cancellationToken);
        AuthorizedInventoryReservationCandidate.EnsureAccess(request.Scope, warehouses,
            request.Scope.TenantId, request.CompanyId, request.WarehouseId);

        await using var command = new NpgsqlCommand("""
            WITH cutoff AS MATERIALIZED (SELECT clock_timestamp() AS recorded_at),
            balances AS MATERIALIZED (
                SELECT c.item_id,c.warehouse_id,c.base_uom_code,c.source_type,c.source_id,c.source_line_id,
                       coalesce(e.remaining_quantity,c.reserved_quantity) AS remaining,
                       coalesce(e.consumed_quantity,0) AS consumed
                FROM inventory.reservation_creation c
                LEFT JOIN LATERAL (
                    SELECT remaining_quantity,consumed_quantity
                    FROM inventory.reservation_lifecycle_event
                    WHERE tenant_id=c.tenant_id AND company_id=c.company_id AND reservation_id=c.reservation_id
                      AND effective_date <= $6 AND recorded_at <= (SELECT recorded_at FROM cutoff)
                    ORDER BY version DESC LIMIT 1
                ) e ON true
                WHERE c.tenant_id=$1 AND c.company_id=$2
                  AND ((c.item_id=$3 AND c.warehouse_id=$4 AND c.base_uom_code=$5)
                       OR (c.source_type=$7 AND c.source_id=$8 AND c.source_line_id=$9))
            )
            SELECT cutoff.recorded_at,
                (SELECT coalesce(sum(base_quantity),0) FROM inventory.stock_movement
                 WHERE tenant_id=$1 AND company_id=$2 AND item_id=$3 AND warehouse_id=$4
                   AND base_uom_code=$5 AND effective_date <= $6 AND recorded_at <= cutoff.recorded_at),
                (SELECT coalesce(sum(reserved_quantity),0) FROM inventory.reservation_creation
                 WHERE tenant_id=$1 AND company_id=$2 AND item_id=$3 AND warehouse_id=$4 AND base_uom_code=$5),
                (SELECT coalesce(sum(reserved_quantity),0) FROM inventory.reservation_creation
                 WHERE tenant_id=$1 AND company_id=$2 AND source_type=$7 AND source_id=$8 AND source_line_id=$9),
                (SELECT coalesce(sum(remaining),0) FROM balances
                 WHERE item_id=$3 AND warehouse_id=$4 AND base_uom_code=$5),
                (SELECT coalesce(sum(remaining+consumed),0) FROM balances
                 WHERE source_type=$7 AND source_id=$8 AND source_line_id=$9),
                (SELECT coalesce(sum(blocked_quantity),0) FROM (
                    SELECT DISTINCT ON (block_id) blocked_quantity
                    FROM inventory.stock_block_event
                    WHERE tenant_id=$1 AND company_id=$2 AND item_id=$3 AND warehouse_id=$4
                      AND base_uom_code=$5 AND effective_date <= $6 AND recorded_at <= cutoff.recorded_at
                    ORDER BY block_id,version DESC
                ) current_blocks)
            FROM cutoff
            """, connection, transaction);
        command.Parameters.AddWithValue(demand.TenantId);
        command.Parameters.AddWithValue(demand.CompanyId);
        command.Parameters.AddWithValue(demand.ItemId);
        command.Parameters.AddWithValue(request.WarehouseId);
        command.Parameters.AddWithValue(demand.BaseUom.Value);
        command.Parameters.AddWithValue(request.EffectiveDate);
        command.Parameters.AddWithValue(demand.Source.SourceType);
        command.Parameters.AddWithValue(demand.Source.SourceId);
        command.Parameters.AddWithValue(demand.Source.SourceLineId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Reservation balance snapshot is unavailable.");
        }
        return new InventoryReservationBalanceSnapshot(request.EffectiveDate,
            reader.GetFieldValue<DateTimeOffset>(0), InventoryQuantity.Create(reader.GetDecimal(1)),
            reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6));
    }
}
