using KaguERP.Modules.Inventory.Application.Queries;
using KaguERP.Modules.Inventory.Application.Transfers;
using KaguERP.Modules.Inventory.Domain;
using Npgsql;
using NpgsqlTypes;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public static class PostgresInventoryOnHandLoader
{
    public static async ValueTask<InventoryOnHandSnapshot> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuthorizedInventoryOnHandQuery query,
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
            throw new InventoryOnHandAuthorizationException(
                "INVENTORY_ON_HAND_WAREHOUSE_SCOPE_STALE",
                "Inventory warehouse authorization changed before the quantity query completed.");
        }

        const string sql = """
            SELECT item_id,warehouse_id,base_uom_code,sum(base_quantity)
            FROM inventory.stock_movement
            WHERE tenant_id=$1 AND company_id=$2
              AND warehouse_id=ANY($3)
              AND effective_date <= $4
              AND recorded_at <= $5
              AND ($6::uuid IS NULL OR item_id=$6)
            GROUP BY item_id,warehouse_id,base_uom_code
            HAVING sum(base_quantity) <> 0
            ORDER BY item_id,warehouse_id
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(query.Scope.TenantId);
        command.Parameters.AddWithValue(query.CompanyId);
        command.Parameters.AddWithValue(query.WarehouseScope.WarehouseIds.ToArray());
        command.Parameters.AddWithValue(query.EffectiveAsOf);
        command.Parameters.AddWithValue(query.RecordedCutoff);
        command.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Uuid,
            Value = query.ItemId.HasValue ? query.ItemId.Value : DBNull.Value,
        });

        var lines = new List<InventoryOnHandLine>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            lines.Add(new InventoryOnHandLine(
                reader.GetGuid(0),
                reader.GetGuid(1),
                InventoryUomCode.Create(reader.GetString(2)),
                InventoryQuantity.Create(reader.GetDecimal(3))));
        }

        return new InventoryOnHandSnapshot(
            query.Scope.TenantId,
            query.CompanyId,
            query.EffectiveAsOf,
            query.RecordedCutoff,
            lines);
    }
}
