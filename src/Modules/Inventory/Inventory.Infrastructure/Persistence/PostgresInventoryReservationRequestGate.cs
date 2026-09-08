using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using KaguERP.Modules.Inventory.Application.Reservations;
using Npgsql;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public static class PostgresInventoryReservationRequestGate
{
    /// <summary>
    /// Holds the request lock until the caller commits or rolls back. A null result requires
    /// demand/position locking and authoritative capacity validation before writing.
    /// </summary>
    public static async ValueTask<InventoryReservationRequestResult?> AcquireAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, InventoryReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(request);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("Transaction must belong to the supplied connection.", nameof(transaction));
        }
        if (transaction.IsolationLevel != IsolationLevel.ReadCommitted)
        {
            throw new InvalidOperationException("Reservation request replay requires ReadCommitted.");
        }
        var warehouses = await PostgresInventoryWarehouseScopeLoader.LoadAsync(
            connection, transaction, request.Scope, request.CompanyId, cancellationToken);
        AuthorizedInventoryReservationCandidate.EnsureAccess(request.Scope, warehouses,
            request.Scope.TenantId, request.CompanyId, request.WarehouseId);

        // Fingerprint, actor and warehouse must not split one request into different locks.
        string canonical = $"kagu.inventory.reservation-request.v1/{request.Scope.TenantId:D}/{request.CompanyId:D}/{request.RequestId:D}";
        long key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        await using (var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock($1)", connection, transaction))
        {
            command.Parameters.AddWithValue(key);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        // A fresh statement snapshot sees a competing transaction's committed result.
        // Reload authorization too: time-based warehouse assignments may expire during the wait.
        return await PostgresInventoryReservationReplayLoader.LoadAsync(
            connection, transaction, request, cancellationToken);
    }
}
