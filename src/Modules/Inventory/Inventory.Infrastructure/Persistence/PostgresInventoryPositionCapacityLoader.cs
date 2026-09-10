using KaguERP.BuildingBlocks.Application.Security;
using Npgsql;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public sealed record InventoryPositionCapacitySnapshot(
    DateTimeOffset RecordedCutoff, decimal MinimumAvailable, bool HasProtectedDeficit);

/// <summary>
/// Internal safety projection, not a historical report or permission evidence.
/// Caller must own the position lock and validate warehouse access before using this result.
/// Every persisted scheduled change is considered, including future effective/recorded dates;
/// a recorded-cutoff report must not hide a commitment from a new write's capacity check.
/// </summary>
public static class PostgresInventoryPositionCapacityLoader
{
    public static async ValueTask<InventoryPositionCapacitySnapshot> LoadAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, ExecutionScope scope, Guid companyId,
        InventoryPositionLockTarget position, DateOnly effectiveFrom, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(position);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!ReferenceEquals(transaction.Connection, connection) ||
            transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Capacity must be loaded in the caller's ReadCommitted transaction after position locking.");
        await using var command = new NpgsqlCommand("""
            WITH movements AS MATERIALIZED (
                SELECT effective_date,base_quantity FROM inventory.stock_movement
                WHERE tenant_id=$1 AND company_id=$2 AND item_id=$3 AND warehouse_id=$4 AND base_uom_code=$5
            ), creations AS MATERIALIZED (
                SELECT reservation_id,reserved_quantity FROM inventory.reservation_creation
                WHERE tenant_id=$1 AND company_id=$2 AND item_id=$3 AND warehouse_id=$4 AND base_uom_code=$5
            ), lifecycle AS MATERIALIZED (
                SELECT e.reservation_id,e.version,e.effective_date,e.remaining_quantity
                FROM inventory.reservation_lifecycle_event e
                JOIN creations c ON c.reservation_id=e.reservation_id
                WHERE e.tenant_id=$1 AND e.company_id=$2
            ), blocks AS MATERIALIZED (
                SELECT block_id,version,effective_date,blocked_quantity FROM inventory.stock_block_event
                WHERE tenant_id=$1 AND company_id=$2 AND item_id=$3 AND warehouse_id=$4 AND base_uom_code=$5
            ), cuts AS (
                SELECT $6::date AS effective_date
                UNION SELECT effective_date FROM movements WHERE effective_date >= $6
                UNION SELECT effective_date FROM lifecycle WHERE effective_date >= $6
                UNION SELECT effective_date FROM blocks WHERE effective_date >= $6
            ), balances AS (
                SELECT d.effective_date,
                    (SELECT coalesce(sum(base_quantity),0) FROM movements WHERE effective_date<=d.effective_date) AS on_hand,
                    (SELECT coalesce(sum(coalesce(e.remaining_quantity,c.reserved_quantity)),0)
                     FROM creations c LEFT JOIN LATERAL (
                         SELECT remaining_quantity FROM lifecycle
                         WHERE reservation_id=c.reservation_id AND effective_date<=d.effective_date
                         ORDER BY version DESC LIMIT 1
                     ) e ON true) AS reserved,
                    (SELECT coalesce(sum(blocked_quantity),0) FROM (
                         SELECT DISTINCT ON (block_id) blocked_quantity FROM blocks
                         WHERE effective_date<=d.effective_date ORDER BY block_id,version DESC
                     ) b) AS blocked
                FROM cuts d
            )
            SELECT clock_timestamp(),min(on_hand-reserved-blocked),
                   bool_or(reserved+blocked>0 AND on_hand<reserved+blocked)
            FROM balances
            """, connection, transaction);
        command.Parameters.AddWithValue(scope.TenantId);
        command.Parameters.AddWithValue(companyId);
        command.Parameters.AddWithValue(position.ItemId);
        command.Parameters.AddWithValue(position.WarehouseId);
        command.Parameters.AddWithValue(position.BaseUom.Value);
        command.Parameters.AddWithValue(effectiveFrom);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Inventory capacity snapshot is unavailable.");
        return new(reader.GetFieldValue<DateTimeOffset>(0), reader.GetDecimal(1), reader.GetBoolean(2));
    }
}

public sealed class InventoryProtectedStockConflictException()
    : InvalidOperationException("The stock change would leave reserved or blocked stock without physical coverage.")
{
    public string Code { get; } = "INVENTORY_PROTECTED_STOCK_CONFLICT";
}
