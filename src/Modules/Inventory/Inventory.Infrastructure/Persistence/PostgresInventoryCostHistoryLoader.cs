using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Domain;
using Npgsql;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

/// <summary>Exact published generation reader. Missing publication is never inferred to be zero history.</summary>
public static class PostgresInventoryCostHistoryLoader
{
    public const string RequiredPermission = "inventory.cost.view";

    public static async ValueTask<InventoryCostHistoryEvidence> LoadAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, InventoryValuationWatermark watermark,
        InventoryUomCode uom, string currency, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(watermark);
        scope.EnsureAllowed(watermark.TenantId, watermark.CompanyId);
        if (!scope.HasPermission(watermark.CompanyId, RequiredPermission)) throw new InventoryCostHistoryAccessException();
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Cost history requires the caller's ReadCommitted transaction.");
        // Shared domain validation does not treat this temporary validation object as lookup evidence.
        _ = InventoryCostHistoryEvidence.NoHistory(watermark, uom, currency);
        var warehouses = await PostgresInventoryWarehouseScopeLoader.LoadAsync(connection, transaction,
            scope, watermark.CompanyId, cancellationToken);
        warehouses.EnsureMatches(watermark.TenantId, watermark.CompanyId, scope.ActorId);
        if (!warehouses.WarehouseIds.Contains(watermark.WarehouseId)) throw new InventoryCostHistoryAccessException();
        await using var sql = new NpgsqlCommand("""
            SELECT origin,cost_snapshot_id,unit_cost
            FROM inventory.cost_history_publication
            WHERE tenant_id=$1 AND company_id=$2 AND item_id=$3 AND warehouse_id=$4
              AND base_uom_code=$5 AND currency_code=$6 AND effective_date=$7 AND sequence_key=$8
              AND projection_generation=$9 AND recorded_cutoff=$10 AND source_checksum=$11
            """, connection, transaction);
        sql.Parameters.AddWithValue(watermark.TenantId);
        sql.Parameters.AddWithValue(watermark.CompanyId);
        sql.Parameters.AddWithValue(watermark.ItemId);
        sql.Parameters.AddWithValue(watermark.WarehouseId);
        sql.Parameters.AddWithValue(uom.Value);
        sql.Parameters.AddWithValue(currency);
        sql.Parameters.AddWithValue(watermark.Position.EffectiveDate);
        sql.Parameters.AddWithValue(watermark.Position.SequenceKey);
        sql.Parameters.AddWithValue(watermark.ProjectionGeneration);
        sql.Parameters.AddWithValue(watermark.RecordedCutoff);
        sql.Parameters.AddWithValue(watermark.SourceChecksumSha256);
        InventoryCostHistoryEvidence result;
        await using (var reader = await sql.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) throw new InventoryCostHistoryUnavailableException();
            short origin = reader.GetInt16(0);
            decimal cost = reader.GetDecimal(2);
            result = origin switch
            {
                1 when !reader.IsDBNull(1) => InventoryCostHistoryEvidence.Known(watermark, uom, currency, reader.GetGuid(1), cost),
                2 when reader.IsDBNull(1) && cost == 0m => InventoryCostHistoryEvidence.NoHistory(watermark, uom, currency),
                _ => throw new InventoryCostHistoryUnavailableException()
            };
        }
        var current = await PostgresInventoryWarehouseScopeLoader.LoadAsync(connection, transaction,
            scope, watermark.CompanyId, cancellationToken);
        current.EnsureMatches(watermark.TenantId, watermark.CompanyId, scope.ActorId);
        if (!current.WarehouseIds.Contains(watermark.WarehouseId)) throw new InventoryCostHistoryAccessException();
        return result;
    }
}

public sealed class InventoryCostHistoryAccessException()
    : InvalidOperationException("The requested cost operation permission and warehouse scope are required.")
{
    public string Code { get; } = "INVENTORY_COST_HISTORY_ACCESS_DENIED";
}
public sealed class InventoryCostHistoryUnavailableException()
    : InvalidOperationException("The exact validated cost history publication is unavailable.")
{
    public string Code { get; } = "INVENTORY_COST_HISTORY_UNAVAILABLE";
}
