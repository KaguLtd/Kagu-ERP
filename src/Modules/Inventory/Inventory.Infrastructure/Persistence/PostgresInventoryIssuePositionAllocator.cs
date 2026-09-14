using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Domain;
using Npgsql;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public sealed record InventoryIssuePositionRequest(Guid LineId, InventoryValuationWatermark Watermark, InventoryUomCode BaseUom);
public sealed record InventoryIssuePositionCandidate(Guid LineId, long SequenceKey);
public sealed record InventoryIssuePositionBatch(DateTimeOffset RecordedAt, IReadOnlyList<InventoryIssuePositionCandidate> Lines);

/// <summary>Transaction-local candidates, not durable reservations. Caller must persist before releasing the position locks.</summary>
public static class PostgresInventoryIssuePositionAllocator
{
    public static async ValueTask<InventoryIssuePositionBatch> PrepareAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, Guid companyId, DateOnly effectiveDate,
        DateTimeOffset notBefore, IReadOnlyList<InventoryIssuePositionRequest> requests, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(requests);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, PostgresInventoryCostHistoryLoader.RequiredPermission))
            throw new InventoryCostHistoryAccessException();
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Issue positions require the caller's ReadCommitted transaction.");
        var lines = requests.Take(501).ToArray();
        if (effectiveDate == default || notBefore == default || notBefore.Offset != TimeSpan.Zero ||
            notBefore.Ticks % TimeSpan.TicksPerMicrosecond != 0 || lines.Length is < 1 or > 500 ||
            lines.Any(line => line is null || line.LineId == Guid.Empty || line.Watermark is null || line.BaseUom == default) ||
            lines.Select(line => line.LineId).Distinct().Count() != lines.Length)
            throw new ArgumentException("Position preparation requires valid chronology and 1–500 unique lines.");
        foreach (var line in lines)
        {
            if (line.Watermark.TenantId != scope.TenantId || line.Watermark.CompanyId != companyId ||
                line.Watermark.Position.EffectiveDate > effectiveDate)
                throw new InventoryInvariantException("INVENTORY_POSITION_SCOPE_CONFLICT", "Cost position must belong to the issue scope and precede its date.");
        }
        async ValueTask Authorize()
        {
            var evidence = await PostgresInventoryWarehouseScopeLoader.LoadAsync(connection, transaction, scope, companyId, cancellationToken);
            evidence.EnsureMatches(scope.TenantId, companyId, scope.ActorId);
            if (lines.Any(line => !evidence.WarehouseIds.Contains(line.Watermark.WarehouseId)))
                throw new InventoryCostHistoryAccessException();
        }
        await Authorize();
        await PostgresInventoryPositionLock.AcquireAsync(connection, transaction, scope, companyId,
            lines.Select(line => new InventoryPositionLockTarget(line.Watermark.ItemId, line.Watermark.WarehouseId, line.BaseUom)), cancellationToken);
        await Authorize();
        DateTimeOffset recordedAt;
        await using (var clock = new NpgsqlCommand("SELECT clock_timestamp()", connection, transaction))
        await using (var reader = await clock.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Database clock is unavailable.");
            recordedAt = reader.GetFieldValue<DateTimeOffset>(0);
        }
        if (recordedAt < notBefore || lines.Any(line => line.Watermark.RecordedCutoff > recordedAt))
            throw new InventoryInvariantException("INVENTORY_POSITION_CUTOFF_CONFLICT", "Database clock precedes source recording or cost cutoff.");
        var sequences = new Dictionary<Guid, long>();
        foreach (var group in lines.GroupBy(line => (line.Watermark.ItemId, line.Watermark.WarehouseId)))
        {
            if (group.Select(line => line.BaseUom).Distinct().Count() != 1)
                throw new ArgumentException("A stock item position must have one base UOM.");
            await using var sql = new NpgsqlCommand("""
                SELECT coalesce(max(sequence_key),0) FROM inventory.stock_movement
                WHERE tenant_id=$1 AND company_id=$2 AND item_id=$3 AND warehouse_id=$4 AND effective_date=$5
                """, connection, transaction);
            sql.Parameters.AddWithValue(scope.TenantId);
            sql.Parameters.AddWithValue(companyId);
            sql.Parameters.AddWithValue(group.Key.ItemId);
            sql.Parameters.AddWithValue(group.Key.WarehouseId);
            sql.Parameters.AddWithValue(effectiveDate);
            long sequence = (long)(await sql.ExecuteScalarAsync(cancellationToken))!;
            sequence = Math.Max(sequence, group.Where(line => line.Watermark.Position.EffectiveDate == effectiveDate)
                .Select(line => line.Watermark.Position.SequenceKey).DefaultIfEmpty(0).Max());
            if (sequence > long.MaxValue - group.Count())
                throw new InventoryInvariantException("INVENTORY_POSITION_SEQUENCE_EXHAUSTED", "Inventory sequence range is exhausted.");
            foreach (var line in group.OrderBy(line => line.LineId)) sequences.Add(line.LineId, ++sequence);
        }
        return new(recordedAt, Array.AsReadOnly(lines.Select(line => new InventoryIssuePositionCandidate(line.LineId, sequences[line.LineId])).ToArray()));
    }
}
