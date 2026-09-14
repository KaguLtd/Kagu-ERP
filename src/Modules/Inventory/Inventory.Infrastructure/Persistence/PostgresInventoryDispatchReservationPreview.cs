using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Domain;
using Npgsql;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public static class PostgresInventoryDispatchReservationPreview
{
    public static async ValueTask<IReadOnlyList<InventoryDispatchReservationLinePreview>> LoadAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, ExecutionScope scope, Guid companyId,
        IReadOnlyList<InventoryDispatchReservationLineQuery> queries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(queries);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, "dispatch.create") || !scope.HasPermission(companyId, "sales.order.view"))
            throw new InventoryReservationAuthorizationException("INVENTORY_DISPATCH_PREVIEW_PERMISSION_REQUIRED",
                "Dispatch preparation and source-order viewing permissions are required.");
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Dispatch preview requires the caller's ReadCommitted transaction.");
        var snapshot = queries.Take(501).ToArray();
        if (snapshot.Length is < 1 or > 500 || snapshot.Any(query => query is null) ||
            snapshot.Select(query => query.Source.SourceLineId).Distinct().Count() != snapshot.Length ||
            snapshot.Select(query => query.Source.SourceId).Distinct().Count() != 1 ||
            snapshot.Select(query => query.Source.SourceVersion).Distinct().Count() != 1 ||
            snapshot.Select(query => query.EffectiveDate).Distinct().Count() != 1)
            throw new ArgumentException("Dispatch preview requires one source order/version/date and unique bounded lines.");
        await EnsureWarehousesAsync(connection, transaction, scope, companyId, snapshot, cancellationToken);
        // All demand locks precede every position lock, matching reservation create/release batches.
        await PostgresInventoryDemandLock.AcquireAsync(connection, transaction, scope, companyId,
            snapshot.Select(query => query.Source), cancellationToken);
        await PostgresInventoryPositionLock.AcquireAsync(connection, transaction, scope, companyId,
            snapshot.Select(query => new InventoryPositionLockTarget(query.ItemId, query.WarehouseId, query.BaseUomCode)), cancellationToken);
        await EnsureWarehousesAsync(connection, transaction, scope, companyId, snapshot, cancellationToken);
        var result = new List<InventoryDispatchReservationLinePreview>(snapshot.Length);
        // The immutable draft is historical; operational preparation requires current stock masters.
        // Keep these row locks until transaction end, including lines with no reservations.
        foreach (var query in snapshot.OrderBy(query => query.ItemId).ThenBy(query => query.WarehouseId))
            _ = await PostgresInventoryStockMasterLoader.EnsureAsync(connection, transaction, scope,
                companyId, query.ItemId, query.BaseUomCode, [query.WarehouseId], query.RequestedQuantity,
                cancellationToken);
        int historyCount = 0;
        foreach (var query in snapshot)
        {
            var heads = new List<InventoryReservationRemainder>();
            await using var sql = new NpgsqlCommand("""
                SELECT c.reservation_id,coalesce(e.version,1),coalesce(e.remaining_quantity,c.reserved_quantity),
                       c.recorded_at,c.item_id,c.base_uom_code,coalesce(e.effective_date,c.effective_date)
                FROM inventory.reservation_creation c
                LEFT JOIN LATERAL (
                    SELECT version,remaining_quantity,effective_date FROM inventory.reservation_lifecycle_event
                    WHERE tenant_id=c.tenant_id AND company_id=c.company_id AND reservation_id=c.reservation_id
                    ORDER BY version DESC LIMIT 1
                ) e ON true
                WHERE c.tenant_id=$1 AND c.company_id=$2 AND c.source_type='sales.order'
                  AND c.source_id=$3 AND c.source_line_id=$4 AND c.source_version=$5 AND c.warehouse_id=$6
                ORDER BY c.recorded_at,c.reservation_id LIMIT 501
                """, connection, transaction);
            sql.Parameters.AddWithValue(scope.TenantId);
            sql.Parameters.AddWithValue(companyId);
            sql.Parameters.AddWithValue(query.Source.SourceId);
            sql.Parameters.AddWithValue(query.Source.SourceLineId);
            sql.Parameters.AddWithValue(query.Source.SourceVersion);
            sql.Parameters.AddWithValue(query.WarehouseId);
            await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (++historyCount > 500) throw new InventoryDispatchPreviewLimitException();
                if (reader.GetGuid(4) != query.ItemId || reader.GetString(5) != query.BaseUomCode.Value ||
                    reader.GetFieldValue<DateOnly>(6) > query.EffectiveDate)
                    throw new InventoryDispatchPreviewConflictException();
                heads.Add(new(reader.GetGuid(0), reader.GetInt64(1), InventoryQuantity.Create(reader.GetDecimal(2)),
                    reader.GetFieldValue<DateTimeOffset>(3)));
            }
            result.Add(new(query.Source.SourceLineId, query.WarehouseId,
                InventoryDispatchReservationPlan.Create(query.RequestedQuantity, heads)));
        }
        await EnsureWarehousesAsync(connection, transaction, scope, companyId, snapshot, cancellationToken);
        return result.AsReadOnly();
    }

    private static async ValueTask EnsureWarehousesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ExecutionScope scope, Guid companyId, InventoryDispatchReservationLineQuery[] queries, CancellationToken cancellationToken)
    {
        var evidence = await PostgresInventoryWarehouseScopeLoader.LoadAsync(connection, transaction, scope, companyId, cancellationToken);
        evidence.EnsureMatches(scope.TenantId, companyId, scope.ActorId);
        if (queries.Any(query => !evidence.WarehouseIds.Contains(query.WarehouseId)))
            throw new InventoryReservationAuthorizationException("INVENTORY_DISPATCH_PREVIEW_WAREHOUSE_REQUIRED",
                "The actor must be scoped to every selected warehouse.");
    }
}

public sealed class InventoryDispatchPreviewConflictException()
    : InvalidOperationException("Reservation position or lifecycle date does not match the dispatch preview.")
{
    public string Code { get; } = "INVENTORY_DISPATCH_PREVIEW_CONFLICT";
}

public sealed class InventoryDispatchPreviewLimitException()
    : InvalidOperationException("Dispatch preview exceeds the supported reservation history limit.")
{
    public string Code { get; } = "INVENTORY_DISPATCH_PREVIEW_LIMIT";
}
