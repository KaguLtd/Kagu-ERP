using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Domain;
using Npgsql;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public sealed record InventoryPositionLockTarget(Guid ItemId, Guid WarehouseId, InventoryUomCode BaseUom);

public static class PostgresInventoryPositionLock
{
    public static async ValueTask AcquireAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        IEnumerable<InventoryPositionLockTarget> targets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(targets);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("Transaction must belong to the supplied connection.", nameof(transaction));
        }
        if (transaction.IsolationLevel != IsolationLevel.ReadCommitted)
        {
            throw new InvalidOperationException("Inventory position locks require ReadCommitted for fresh reads after lock acquisition.");
        }
        InventoryPositionLockTarget[] snapshot = targets.ToArray();
        if (snapshot.Length == 0 || snapshot.Any(target => target is null ||
            target.ItemId == Guid.Empty || target.WarehouseId == Guid.Empty || string.IsNullOrEmpty(target.BaseUom.Value)))
        {
            throw new ArgumentException("Inventory position targets require item, warehouse and base UOM.", nameof(targets));
        }
        long[] keys = snapshot.Select(target =>
        {
            string canonical = $"kagu.inventory.position.v1/{scope.TenantId:D}/{companyId:D}/{target.ItemId:D}/{target.WarehouseId:D}/{target.BaseUom.Value}";
            return BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        }).Distinct().Order().ToArray();
        foreach (long key in keys)
        {
            await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock($1)", connection, transaction);
            command.Parameters.AddWithValue(key);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
