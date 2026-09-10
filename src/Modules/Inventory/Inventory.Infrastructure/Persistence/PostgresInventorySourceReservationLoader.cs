using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Domain;
using Npgsql;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public sealed record InventorySourceReservationHead(Guid ReservationId, Guid WarehouseId, long Version,
    InventoryQuantity RemainingQuantity, InventoryReservationTransition? LastTransition, Guid? LastCorrelationId);

/// <summary>Inventory-owned source discovery. Caller must freeze the producer before cancellation discovery.</summary>
public static class PostgresInventorySourceReservationLoader
{
    public static async ValueTask<IReadOnlyList<InventorySourceReservationHead>> LoadAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, ExecutionScope scope, Guid companyId,
        string sourceType, Guid sourceId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Source discovery requires the caller's ReadCommitted transaction.");
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, InventoryReservationReleaseRequest.RequiredPermission))
            throw new InventoryReservationAuthorizationException("INVENTORY_RESERVATION_RELEASE_PERMISSION_REQUIRED",
                "Source reservation discovery requires release permission.");
        if (sourceId == Guid.Empty || string.IsNullOrWhiteSpace(sourceType))
            throw new ArgumentException("Source identity is required.");
        var warehouses = await PostgresInventoryWarehouseScopeLoader.LoadAsync(connection, transaction, scope, companyId, cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT c.reservation_id,c.warehouse_id,coalesce(e.version,1),
                   coalesce(e.remaining_quantity,c.reserved_quantity),e.transition,e.correlation_id
            FROM inventory.reservation_creation c
            LEFT JOIN LATERAL (
                SELECT version,remaining_quantity,transition,correlation_id
                FROM inventory.reservation_lifecycle_event
                WHERE tenant_id=c.tenant_id AND company_id=c.company_id AND reservation_id=c.reservation_id
                ORDER BY version DESC LIMIT 1
            ) e ON true
            WHERE c.tenant_id=$1 AND c.company_id=$2 AND c.source_type=$3 AND c.source_id=$4
            ORDER BY c.reservation_id LIMIT 501
            """, connection, transaction);
        command.Parameters.AddWithValue(scope.TenantId);
        command.Parameters.AddWithValue(companyId);
        command.Parameters.AddWithValue(sourceType);
        command.Parameters.AddWithValue(sourceId);
        var result = new List<InventorySourceReservationHead>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!warehouses.WarehouseIds.Contains(reader.GetGuid(1)))
                throw new InventoryReservationAuthorizationException("INVENTORY_RESERVATION_WAREHOUSE_SCOPE_REQUIRED",
                    "The actor must be scoped to every warehouse in the source reservation set.");
            result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt64(2),
                InventoryQuantity.Create(reader.GetDecimal(3)),
                reader.IsDBNull(4) ? null : (InventoryReservationTransition)reader.GetInt16(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5)));
        }
        if (result.Count > 500)
            throw new InventorySourceReservationLimitException();
        return result.AsReadOnly();
    }
}

public sealed class InventorySourceReservationLimitException()
    : InvalidOperationException("Source reservation cancellation exceeds the supported atomic batch limit.")
{
    public string Code { get; } = "INVENTORY_SOURCE_RESERVATION_LIMIT_EXCEEDED";
}
