using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Domain;
using Npgsql;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public static class PostgresInventoryReservationReplayLoader
{
    public static async ValueTask<InventoryReservationRequestResult?> LoadAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, InventoryReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var warehouses = await PostgresInventoryWarehouseScopeLoader.LoadAsync(
            connection, transaction, request.Scope, request.CompanyId, cancellationToken);
        AuthorizedInventoryReservationCandidate.EnsureAccess(request.Scope, warehouses,
            request.Scope.TenantId, request.CompanyId, request.WarehouseId);
        await using var command = new NpgsqlCommand("""
            SELECT warehouse_id,request_fingerprint,requested_quantity,reserved_quantity,reservation_id,recorded_at
            FROM inventory.reservation_request_result
            WHERE tenant_id=$1 AND company_id=$2 AND request_id=$3
            """, connection, transaction);
        command.Parameters.AddWithValue(request.Scope.TenantId);
        command.Parameters.AddWithValue(request.CompanyId);
        command.Parameters.AddWithValue(request.RequestId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        if (reader.GetGuid(0) != request.WarehouseId ||
            !string.Equals(reader.GetString(1), request.Fingerprint, StringComparison.Ordinal) ||
            reader.GetDecimal(2) != request.RequestedQuantity.Value)
        {
            throw new InventoryReservationRequestConflictException();
        }
        return new InventoryReservationRequestResult(request.RequestId,
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            InventoryQuantity.Create(reader.GetDecimal(2)), InventoryQuantity.Create(reader.GetDecimal(3)),
            reader.GetFieldValue<DateTimeOffset>(5));
    }
}
