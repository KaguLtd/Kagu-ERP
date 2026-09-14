using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Domain;
using Npgsql;
using NpgsqlTypes;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public sealed record InventoryCostPublicationOutcome(Guid PublicationId, DateTimeOffset PublishedAt, bool Created);

/// <summary>Internal persistence participant. Invoice-derived evidence must be supplied by a trusted producer.</summary>
public static class PostgresInventoryCostPublicationWriter
{
    public const string RequiredPermission = "inventory.cost.publish";

    public static async ValueTask<InventoryCostPublicationOutcome> WriteAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, Guid publicationId, InventoryCostHistoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(evidence);
        var mark = evidence.Watermark;
        scope.EnsureAllowed(mark.TenantId, mark.CompanyId);
        if (!scope.HasPermission(mark.CompanyId, RequiredPermission)) throw new InventoryCostHistoryAccessException();
        if (publicationId == Guid.Empty || !ReferenceEquals(transaction.Connection, connection) ||
            transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Publication identity and the caller's ReadCommitted transaction are required.");
        async ValueTask Authorize(Guid? warehouseId = null)
        {
            var warehouses = await PostgresInventoryWarehouseScopeLoader.LoadAsync(connection, transaction, scope, mark.CompanyId, cancellationToken);
            warehouses.EnsureMatches(mark.TenantId, mark.CompanyId, scope.ActorId);
            if (!warehouses.WarehouseIds.Contains(warehouseId ?? mark.WarehouseId)) throw new InventoryCostHistoryAccessException();
        }
        await Authorize();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"kagu.cost.publication.v1/{mark.TenantId:D}/{mark.CompanyId:D}/{publicationId:D}"));
        await using (var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock($1)", connection, transaction))
        {
            gate.Parameters.AddWithValue(BinaryPrimitives.ReadInt64BigEndian(hash));
            await gate.ExecuteNonQueryAsync(cancellationToken);
        }
        await Authorize();
        void Bind(NpgsqlCommand command)
        {
            command.Parameters.AddWithValue(mark.TenantId);
            command.Parameters.AddWithValue(mark.CompanyId);
            command.Parameters.AddWithValue(publicationId);
            command.Parameters.AddWithValue(scope.ActorId);
            command.Parameters.AddWithValue(mark.ItemId);
            command.Parameters.AddWithValue(mark.WarehouseId);
            command.Parameters.AddWithValue(evidence.BaseUom.Value);
            command.Parameters.AddWithValue(evidence.Currency);
            command.Parameters.AddWithValue(mark.Position.EffectiveDate);
            command.Parameters.AddWithValue(mark.Position.SequenceKey);
            command.Parameters.AddWithValue(mark.ProjectionGeneration);
            command.Parameters.AddWithValue(mark.RecordedCutoff);
            command.Parameters.AddWithValue(mark.SourceChecksumSha256);
            command.Parameters.AddWithValue((short)evidence.Origin);
            command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)evidence.SnapshotId ?? DBNull.Value });
            command.Parameters.AddWithValue(evidence.UnitCost);
        }
        Guid? storedWarehouse = null;
        bool matches = false;
        DateTimeOffset storedPublishedAt = default;
        await using (var existing = new NpgsqlCommand("""
            SELECT published_at,published_by=$4 AND item_id=$5 AND warehouse_id=$6 AND base_uom_code=$7
                AND currency_code=$8 AND effective_date=$9 AND sequence_key=$10 AND projection_generation=$11
                AND recorded_cutoff=$12 AND source_checksum=$13 AND origin=$14
                AND cost_snapshot_id IS NOT DISTINCT FROM $15 AND unit_cost=$16,warehouse_id
            FROM inventory.cost_history_publication WHERE tenant_id=$1 AND company_id=$2 AND publication_id=$3
            """, connection, transaction))
        {
            Bind(existing);
            await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                matches = reader.GetBoolean(1);
                storedPublishedAt = reader.GetFieldValue<DateTimeOffset>(0);
                storedWarehouse = reader.GetGuid(2);
            }
        }
        if (storedWarehouse is not null)
        {
            // Authorize the persisted position before revealing conflict/existence through a different requested warehouse.
            await Authorize(storedWarehouse);
            if (!matches) throw new InventoryCostPublicationConflictException();
            return new(publicationId, storedPublishedAt, false);
        }
        const string savepoint = "inventory_cost_publication_write";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO inventory.cost_history_publication
                    (tenant_id,company_id,publication_id,published_by,item_id,warehouse_id,base_uom_code,currency_code,
                     effective_date,sequence_key,projection_generation,recorded_cutoff,source_checksum,origin,cost_snapshot_id,unit_cost)
                VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16) RETURNING published_at
                """, connection, transaction);
            Bind(insert);
            DateTimeOffset published;
            await using (var reader = await insert.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken)) throw new InventoryCostHistoryUnavailableException();
                published = reader.GetFieldValue<DateTimeOffset>(0);
            }
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return new(publicationId, published, true);
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            if (exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                throw new InventoryCostPublicationConflictException();
            throw;
        }
    }
}

public sealed class InventoryCostPublicationConflictException()
    : InvalidOperationException("Publication identity or watermark already has a different immutable result.")
{
    public string Code { get; } = "INVENTORY_COST_PUBLICATION_CONFLICT";
}
