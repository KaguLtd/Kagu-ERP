using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Domain;
using Npgsql;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public sealed record InvoiceReceiptMovementReference(Guid AllocationId, Guid MovementId, long ReceiptVersion);
public sealed record VerifiedInvoiceReceiptCost(InventoryInvoiceCostBasis Basis, Guid MovementId,
    long ReceiptVersion, DateOnly ReceiptEffectiveDate, DateTimeOffset ReceiptRecordedAt);

public static class PostgresInvoiceReceiptCostVerifier
{
    public static async ValueTask<IReadOnlyList<VerifiedInvoiceReceiptCost>> VerifyAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, Guid companyId, DateTimeOffset cutoff,
        IReadOnlyList<InventoryInvoiceCostBasis> bases, IReadOnlyList<InvoiceReceiptMovementReference> references,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(bases);
        ArgumentNullException.ThrowIfNull(references);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, PostgresInventoryCostPublicationWriter.RequiredPermission)) throw new InventoryCostHistoryAccessException();
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Receipt evidence requires the caller's ReadCommitted transaction.");
        var input = bases.Take(501).ToArray();
        var refs = references.Take(501).ToArray();
        if (cutoff == default || cutoff.Offset != TimeSpan.Zero || cutoff.Ticks % TimeSpan.TicksPerMicrosecond != 0 ||
            input.Length is < 1 or > 500 || refs.Length != input.Length ||
            input.Any(line => line is null || line.Source.TenantId != scope.TenantId || line.Source.CompanyId != companyId || line.Source.FinalizedAt > cutoff) ||
            refs.Any(line => line is null || line.AllocationId == Guid.Empty || line.MovementId == Guid.Empty || line.ReceiptVersion <= 0) ||
            input.Select(line => line.Source.AllocationId).Distinct().Count() != input.Length ||
            refs.Select(line => line.AllocationId).Distinct().Count() != refs.Length ||
            !input.Select(line => line.Source.AllocationId).ToHashSet().SetEquals(refs.Select(line => line.AllocationId)) ||
            input.Select(line => (line.Source.InvoiceId, line.Source.InvoiceVersion)).Distinct().Count() != 1)
            throw new InventoryReceiptCostEvidenceException();
        var warehouses = await PostgresInventoryWarehouseScopeLoader.LoadAsync(connection, transaction, scope, companyId, cancellationToken);
        warehouses.EnsureMatches(scope.TenantId, companyId, scope.ActorId);
        if (input.Any(line => !warehouses.WarehouseIds.Contains(line.WarehouseId))) throw new InventoryCostHistoryAccessException();
        var lookup = refs.ToDictionary(line => line.AllocationId);
        var output = new Dictionary<Guid, VerifiedInvoiceReceiptCost>();
        var movements = new Dictionary<Guid, ReceiptEvidence>();
        // One statement snapshot: a multi-receipt invoice must not mix independently committed receipt/reversal states.
        await using (var sql = new NpgsqlCommand("""
            SELECT movement_id,item_id,warehouse_id,base_uom_code,source_event_id,source_line_id,source_version,
                   base_quantity,effective_date,recorded_at
            FROM inventory.stock_movement m
            WHERE tenant_id=$1 AND company_id=$2 AND movement_id=ANY($3) AND movement_kind=1
              AND source_type='purchasing.goods-receipt' AND posting_purpose='receipt'
              AND recorded_at<=$4 AND reversal_of_movement_id IS NULL
              AND NOT EXISTS (SELECT 1 FROM inventory.stock_movement r
                  WHERE r.tenant_id=m.tenant_id AND r.company_id=m.company_id
                    AND r.reversal_of_movement_id=m.movement_id AND r.recorded_at<=$4)
            """, connection, transaction))
        {
            sql.Parameters.AddWithValue(scope.TenantId);
            sql.Parameters.AddWithValue(companyId);
            sql.Parameters.AddWithValue(refs.Select(reference => reference.MovementId).Distinct().ToArray());
            sql.Parameters.AddWithValue(cutoff);
            await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!warehouses.WarehouseIds.Contains(reader.GetGuid(2))) throw new InventoryCostHistoryAccessException();
                movements.Add(reader.GetGuid(0), new(reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3),
                    reader.GetGuid(4), reader.GetGuid(5), reader.GetInt64(6), reader.GetDecimal(7),
                    reader.GetFieldValue<DateOnly>(8), reader.GetFieldValue<DateTimeOffset>(9)));
            }
        }
        foreach (var group in input.GroupBy(line => lookup[line.Source.AllocationId].MovementId))
        {
            if (!movements.TryGetValue(group.Key, out var receipt)) throw new InventoryReceiptCostEvidenceException();
            if (group.Sum(line => line.Quantity.Value) > receipt.Quantity) throw new InventoryReceiptCostEvidenceException();
            foreach (var basis in group)
            {
                var reference = lookup[basis.Source.AllocationId];
                if (receipt.ItemId != basis.ItemId || receipt.WarehouseId != basis.WarehouseId ||
                    receipt.BaseUom != basis.BaseUom.Value || receipt.ReceiptId != basis.Source.ReceiptId ||
                    receipt.ReceiptLineId != basis.Source.ReceiptLineId || receipt.Version != reference.ReceiptVersion)
                    throw new InventoryReceiptCostEvidenceException();
                output.Add(basis.Source.AllocationId, new(basis, reference.MovementId, reference.ReceiptVersion,
                    receipt.EffectiveDate, receipt.RecordedAt));
            }
        }
        var current = await PostgresInventoryWarehouseScopeLoader.LoadAsync(connection, transaction, scope, companyId, cancellationToken);
        current.EnsureMatches(scope.TenantId, companyId, scope.ActorId);
        if (input.Any(line => !current.WarehouseIds.Contains(line.WarehouseId))) throw new InventoryCostHistoryAccessException();
        return Array.AsReadOnly(input.Select(line => output[line.Source.AllocationId]).ToArray());
    }

    private sealed record ReceiptEvidence(Guid ItemId, Guid WarehouseId, string BaseUom, Guid ReceiptId,
        Guid ReceiptLineId, long Version, decimal Quantity, DateOnly EffectiveDate, DateTimeOffset RecordedAt);
}

public sealed class InventoryReceiptCostEvidenceException()
    : InvalidOperationException("Invoice allocation does not match available recorded receipt evidence.");
