using KaguERP.Modules.Inventory.Application.Queries;
using KaguERP.Modules.Inventory.Application.Transfers;
using KaguERP.Modules.Inventory.Domain;
using Npgsql;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public static class PostgresInventoryMovementLoader
{
    public static async ValueTask<InventoryMovementPage> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuthorizedInventoryMovementQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(query);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }

        InventoryWarehouseScopeEvidence currentWarehouseScope =
            await PostgresInventoryWarehouseScopeLoader.LoadAsync(
                connection,
                transaction,
                query.Scope,
                query.CompanyId,
                cancellationToken);
        if (query.WarehouseScope.WarehouseIds.Any(
                warehouseId => !currentWarehouseScope.WarehouseIds.Contains(warehouseId)))
        {
            throw new InventoryMovementQueryException(
                "INVENTORY_MOVEMENT_WAREHOUSE_SCOPE_STALE",
                "Inventory warehouse authorization changed before the movement query completed.");
        }

        const string sql = """
            SELECT movement_id,item_id,warehouse_id,base_uom_code,movement_kind,base_quantity,
                   effective_date,recorded_at,sequence_key,source_type,source_event_id,
                   source_line_id,source_version,posting_purpose,transfer_id,counterpart_warehouse_id,
                   reversal_of_movement_id
            FROM inventory.stock_movement
            WHERE tenant_id=$1 AND company_id=$2 AND item_id=$3
              AND warehouse_id=ANY($4)
              AND effective_date BETWEEN $5 AND $6
              AND recorded_at <= $7
              AND (NOT $8 OR (effective_date,recorded_at,warehouse_id,sequence_key,movement_id)
                   < ($9,$10,$11,$12,$13))
            ORDER BY effective_date DESC,recorded_at DESC,warehouse_id DESC,sequence_key DESC,movement_id DESC
            LIMIT $14
            """;
        InventoryMovementCursor? cursor = query.After;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(query.Scope.TenantId);
        command.Parameters.AddWithValue(query.CompanyId);
        command.Parameters.AddWithValue(query.ItemId);
        command.Parameters.AddWithValue(query.WarehouseScope.WarehouseIds.ToArray());
        command.Parameters.AddWithValue(query.EffectiveFrom);
        command.Parameters.AddWithValue(query.EffectiveThrough);
        command.Parameters.AddWithValue(query.RecordedCutoff);
        command.Parameters.AddWithValue(cursor is not null);
        command.Parameters.AddWithValue(cursor?.EffectiveDate ?? query.EffectiveThrough);
        command.Parameters.AddWithValue(cursor?.RecordedAt ?? query.RecordedCutoff);
        command.Parameters.AddWithValue(cursor?.WarehouseId ?? Guid.Empty);
        command.Parameters.AddWithValue(cursor?.SequenceKey ?? 0L);
        command.Parameters.AddWithValue(cursor?.MovementId ?? Guid.Empty);
        command.Parameters.AddWithValue(query.PageSize + 1);

        var lines = new List<InventoryMovementLine>(query.PageSize + 1);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            lines.Add(new InventoryMovementLine(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                InventoryUomCode.Create(reader.GetString(3)),
                (StockMovementKind)reader.GetInt16(4),
                InventoryQuantity.Create(reader.GetDecimal(5)),
                reader.GetFieldValue<DateOnly>(6),
                reader.GetFieldValue<DateTimeOffset>(7),
                reader.GetInt64(8),
                StockMovementSourceIdentity.Create(
                    query.Scope.TenantId,
                    query.CompanyId,
                    reader.GetString(9),
                    reader.GetGuid(10),
                    reader.GetGuid(11),
                    reader.GetInt64(12),
                reader.GetString(13)),
                reader.IsDBNull(14) ? null : reader.GetGuid(14),
                reader.IsDBNull(15) ? null : reader.GetGuid(15),
                reader.IsDBNull(16) ? null : reader.GetGuid(16)));
        }

        bool hasMore = lines.Count > query.PageSize;
        if (hasMore)
        {
            lines.RemoveAt(lines.Count - 1);
        }
        InventoryMovementLine? last = hasMore ? lines[^1] : null;
        InventoryMovementCursor? next = last is null
            ? null
            : InventoryMovementCursor.Create(
                last.EffectiveDate,
                last.RecordedAt,
                last.WarehouseId,
                last.SequenceKey,
                last.MovementId);

        return new InventoryMovementPage(
            query.Scope.TenantId,
            query.CompanyId,
            query.ItemId,
            query.EffectiveFrom,
            query.EffectiveThrough,
            query.RecordedCutoff,
            Array.AsReadOnly(lines.ToArray()),
            next);
    }
}
