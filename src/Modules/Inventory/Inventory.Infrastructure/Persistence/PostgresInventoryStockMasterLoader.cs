using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Domain;
using Npgsql;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

internal static class PostgresInventoryStockMasterLoader
{
    internal static async ValueTask<short> EnsureAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ExecutionScope scope, Guid companyId, Guid itemId, InventoryUomCode uom,
        IReadOnlyCollection<Guid> warehouseIds, InventoryQuantity quantity, CancellationToken cancellationToken)
    {
        scope.EnsureAllowed(scope.TenantId, companyId);
        Guid[] warehouses = warehouseIds.Distinct().Order().ToArray();
        if (warehouses.Length == 0 || !quantity.IsPositive)
            throw new InventoryStockMasterUnavailableException();
        await using var command = new NpgsqlCommand("""
            SELECT w.warehouse_id,i.quantity_scale
            FROM inventory.item i
            JOIN inventory.item_company c ON c.tenant_id=i.tenant_id AND c.item_id=i.item_id
            JOIN org.warehouse w ON w.tenant_id=c.tenant_id AND w.company_id=c.company_id
            WHERE i.tenant_id=$1 AND c.company_id=$2 AND i.item_id=$3 AND w.warehouse_id=ANY($4)
              AND i.base_uom_code=$5 AND i.is_active AND c.is_active AND w.is_active
              AND i.kind=1 AND i.tracking_policy=1
            ORDER BY w.warehouse_id
            FOR SHARE OF i,c,w
            """, connection, transaction);
        command.Parameters.AddWithValue(scope.TenantId);
        command.Parameters.AddWithValue(companyId);
        command.Parameters.AddWithValue(itemId);
        command.Parameters.AddWithValue(warehouses);
        command.Parameters.AddWithValue(uom.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        int found = 0;
        short precision = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            precision = reader.GetInt16(1);
            if (decimal.Round(quantity.Value, precision) != quantity.Value)
                throw new InventoryStockMasterUnavailableException();
            found++;
        }
        if (found != warehouses.Length) throw new InventoryStockMasterUnavailableException();
        return precision;
    }
}

public sealed class InventoryStockMasterUnavailableException()
    : InvalidOperationException("Active untracked stock item, company assignment, warehouses and valid quantity scale are required.")
{
    public string Code { get; } = "INVENTORY_STOCK_MASTER_UNAVAILABLE";
}
