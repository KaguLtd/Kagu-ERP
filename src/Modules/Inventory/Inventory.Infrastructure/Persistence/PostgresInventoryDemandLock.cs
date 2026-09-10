using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Domain;
using Npgsql;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public static class PostgresInventoryDemandLock
{
    /// <summary>Acquire all demands before any positions. Source version and warehouse do not split a commercial line.</summary>
    public static async ValueTask AcquireAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ExecutionScope scope, Guid companyId, IEnumerable<InventoryDemandSourceIdentity> sources,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(sources);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!ReferenceEquals(transaction.Connection, connection) ||
            transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Demand locks require the caller's ReadCommitted transaction.");
        var keys = sources.Select(source =>
        {
            ArgumentNullException.ThrowIfNull(source);
            string canonical = $"kagu.inventory.reservation-demand.v1/{scope.TenantId:D}/{companyId:D}/{source.SourceType}/{source.SourceId:D}/{source.SourceLineId:D}";
            return BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        }).Distinct().Order().ToArray();
        if (keys.Length == 0) throw new ArgumentException("At least one demand is required.", nameof(sources));
        foreach (long key in keys)
        {
            await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock($1)", connection, transaction);
            command.Parameters.AddWithValue(key);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
