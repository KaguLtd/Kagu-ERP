using KaguERP.Bootstrap;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static partial class DatabaseIntegrationCheck
{
    private static async Task AssertInventoryCostHistoryAsync(NpgsqlDataSource owner, NpgsqlDataSource app,
        Guid tenantId, Guid companyId, Guid otherCompanyId, Guid actorId, Guid itemId, Guid warehouseId)
    {
        await AssertSupplierInvoiceCostBasisContractAsync();
        var at = new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);
        var uom = InventoryUomCode.Create("EA");
        long generation = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        InventoryValuationWatermark Mark(long sequence = 1, string? checksum = null) =>
            InventoryValuationWatermark.Create(tenantId, companyId, itemId, warehouseId,
                InventoryPosition.Create(new(2026, 9, 12), sequence), generation, at, checksum ?? new string('a', 64));
        Guid publication = Guid.CreateVersion7(), snapshot = Guid.CreateVersion7();
        var scope = SalesScope(tenantId, companyId, actorId, PostgresInventoryCostHistoryLoader.RequiredPermission);
        await using (var connection = await owner.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await AssertInvoiceReceiptEvidenceAsync(connection, transaction, tenantId, companyId, actorId, itemId, warehouseId);
            await AssertSupplierInvoiceCaptureAsync(connection, transaction, tenantId, companyId, actorId, itemId, otherCompanyId);
            await ThrowsAsync<InventoryCostHistoryUnavailableException>(async () =>
                await PostgresInventoryCostHistoryLoader.LoadAsync(connection, transaction, scope, Mark(), uom, "TRY"));
            await using (var insert = new NpgsqlCommand("""
                INSERT INTO inventory.cost_history_publication
                    (tenant_id,company_id,publication_id,item_id,warehouse_id,base_uom_code,currency_code,
                     effective_date,sequence_key,projection_generation,recorded_cutoff,source_checksum,
                     origin,cost_snapshot_id,unit_cost,published_by)
                VALUES ($1,$2,$3,$4,$5,'EA','TRY',DATE '2026-09-12',1,$6,$7,$8,1,$9,12.34567890123456789012345678,$10),
                       ($1,$2,$11,$4,$5,'EA','TRY',DATE '2026-09-12',2,$6,$7,$8,2,NULL,0,$10),
                       ($1,$2,$12,$4,$5,'EA','TRY',DATE '2026-09-12',5,$6,$7,$8,1,$9,79228162514264337593543950335,$10)
                """, connection, transaction))
            {
                insert.Parameters.AddWithValue(tenantId);
                insert.Parameters.AddWithValue(companyId);
                insert.Parameters.AddWithValue(publication);
                insert.Parameters.AddWithValue(itemId);
                insert.Parameters.AddWithValue(warehouseId);
                insert.Parameters.AddWithValue(generation);
                insert.Parameters.AddWithValue(at);
                insert.Parameters.AddWithValue(new string('a', 64));
                insert.Parameters.AddWithValue(snapshot);
                insert.Parameters.AddWithValue(actorId);
                insert.Parameters.AddWithValue(Guid.CreateVersion7());
                insert.Parameters.AddWithValue(Guid.CreateVersion7());
                await insert.ExecuteNonQueryAsync();
            }
            await transaction.SaveAsync("immutable_cost_publication");
            await using (var update = new NpgsqlCommand("UPDATE inventory.cost_history_publication SET unit_cost=1 WHERE publication_id=$1",
                connection, transaction))
            {
                update.Parameters.AddWithValue(publication);
                var error = await ThrowsAsync<PostgresException>(async () => await update.ExecuteNonQueryAsync());
                Assert(error.SqlState == "55000", "Published cost history accepted an in-place update.");
            }
            await transaction.RollbackAsync("immutable_cost_publication");
            await transaction.ReleaseAsync("immutable_cost_publication");
            foreach (string invalidCost in new[] { "-1", "0.12345678901234567890123456789" })
            {
                await transaction.SaveAsync("invalid_cost_precision");
                // Constants above are test cases, never client-provided SQL fragments.
                await using var invalid = new NpgsqlCommand($"""
                    INSERT INTO inventory.cost_history_publication
                        (tenant_id,company_id,publication_id,item_id,warehouse_id,base_uom_code,currency_code,
                         effective_date,sequence_key,projection_generation,recorded_cutoff,source_checksum,
                         origin,cost_snapshot_id,unit_cost,published_at,published_by)
                    SELECT tenant_id,company_id,$2,item_id,warehouse_id,base_uom_code,currency_code,
                        effective_date,99,projection_generation,recorded_cutoff,source_checksum,origin,
                        cost_snapshot_id,{invalidCost},published_at,published_by
                    FROM inventory.cost_history_publication WHERE publication_id=$1
                    """, connection, transaction);
                invalid.Parameters.AddWithValue(publication);
                invalid.Parameters.AddWithValue(Guid.CreateVersion7());
                var error = await ThrowsAsync<PostgresException>(async () => await invalid.ExecuteNonQueryAsync());
                Assert(error.SqlState == PostgresErrorCodes.CheckViolation, "Invalid cost precision/sign was accepted.");
                await transaction.RollbackAsync("invalid_cost_precision");
                await transaction.ReleaseAsync("invalid_cost_precision");
            }
            await using (var privileges = new NpgsqlCommand("""
                SELECT relrowsecurity AND relforcerowsecurity
                  AND has_table_privilege('kagu_erp_app',oid,'SELECT')
                  AND NOT has_table_privilege('kagu_erp_app',oid,'INSERT')
                  AND NOT has_table_privilege('kagu_erp_app',oid,'UPDATE')
                  AND NOT has_table_privilege('kagu_erp_app',oid,'DELETE')
                FROM pg_class WHERE oid='inventory.cost_history_publication'::regclass
                """, connection, transaction))
                Assert((bool)(await privileges.ExecuteScalarAsync())!, "Cost history lost its RLS/read-only runtime boundary.");
            await transaction.CommitAsync();
        }
        await using (var connection = await app.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var known = await PostgresInventoryCostHistoryLoader.LoadAsync(connection, transaction, scope, Mark(), uom, "TRY");
            await AssertSupplierInvoiceCostPreviewAsync(connection, transaction, tenantId, companyId, actorId, itemId, warehouseId);
            Assert(known.Origin == InventoryIssueCostOrigin.LastKnownCost && known.SnapshotId == snapshot &&
                known.UnitCost == 12.34567890123456789012345678m, "Published decimal cost lost precision or provenance.");
            var empty = await PostgresInventoryCostHistoryLoader.LoadAsync(connection, transaction, scope, Mark(2), uom, "TRY");
            Assert(empty.Origin == InventoryIssueCostOrigin.NoHistoryZero && empty.UnitCost == 0m,
                "Explicit no-history publication was not recognized.");
            Guid sourceEvent = Guid.CreateVersion7();
            StockMovementDraft Issue(long sequence, decimal quantity = 3m) => StockMovementDraft.Create(Guid.CreateVersion7(), tenantId,
                companyId, itemId, warehouseId, uom, StockMovementKind.Issue, InventoryQuantity.Create(-quantity),
                new(2026, 9, 12), at, sequence, StockMovementSourceIdentity.Create(tenantId, companyId,
                    "sales.dispatch", sourceEvent, Guid.CreateVersion7(), 1, "issue"));
            var firstIssue = Issue(3);
            var secondIssue = Issue(4);
            InventoryIssueCostPreviewLine[] batch = [new(firstIssue, Mark(), "TRY"), new(secondIssue, Mark(2), "TRY")];
            var audit = new RequestAuditContext(Guid.CreateVersion7(), "issue-cost-preview", tenantId, actorId,
                new HashSet<Guid> { companyId }, null);
            async Task<long> AuditCount(Guid correlation)
            {
                await using var count = new NpgsqlCommand(
                    "SELECT count(*) FROM platform.audit_event WHERE tenant_id=$1 AND correlation_id=$2", connection, transaction);
                count.Parameters.AddWithValue(tenantId);
                count.Parameters.AddWithValue(correlation);
                return (long)(await count.ExecuteScalarAsync())!;
            }
            var resolved = await PostgresInventoryIssueCostPreview.LoadAsync(connection, transaction, scope, batch, audit);
            Assert(resolved.Count == 2 && resolved[0].Issue.MovementId == firstIssue.MovementId &&
                resolved[0].UnitCost == known.UnitCost && resolved[1].Origin == InventoryIssueCostOrigin.NoHistoryZero &&
                await AuditCount(audit.CorrelationId) == 2, "Cost batch lost order, origin or atomic audit.");
            var failed = audit with { CorrelationId = Guid.CreateVersion7() };
            var amountAudit = audit with { CorrelationId = Guid.CreateVersion7() };
            Guid roundingPolicy = Guid.CreateVersion7();
            var amounts = await PostgresInventoryIssueCostPreview.LoadAmountsAsync(connection, transaction, scope,
                batch, roundingPolicy, 2, amountAudit);
            Assert(amounts.Count == 2 && amounts[0].Amount == 37.04m && amounts[1].Amount == 0m &&
                amounts[0].Selection.History.SnapshotId == snapshot && amounts[0].RoundingPolicySnapshotId == roundingPolicy &&
                await AuditCount(amountAudit.CorrelationId) == 3, "Amount preview lost rounding, provenance or batch audit.");
            await ThrowsAsync<InventoryInvariantException>(async () =>
                await PostgresInventoryIssueCostPreview.LoadAmountsAsync(connection, transaction, scope,
                    batch, Guid.Empty, 2, failed));
            await ThrowsAsync<InventoryCostHistoryUnavailableException>(async () =>
                await PostgresInventoryIssueCostPreview.LoadAmountsAsync(connection, transaction, scope,
                    [batch[0], new(secondIssue, Mark(3), "TRY")], roundingPolicy, 2, failed));
            Assert(await AuditCount(failed.CorrelationId) == 0, "Failed amount batch retained inner cost audits.");
            await ThrowsAsync<InventoryInvariantException>(async () =>
                await PostgresInventoryIssueCostPreview.LoadAmountsAsync(connection, transaction, scope,
                    [batch[0], new(Issue(6), Mark(5), "TRY")], roundingPolicy, 2, failed));
            Assert(await AuditCount(failed.CorrelationId) == 0, "Amount overflow retained already completed inner selection audits.");
            await ThrowsAsync<InventoryInvariantException>(async () =>
                await PostgresInventoryIssueCostPreview.LoadAmountsAsync(connection, transaction, scope,
                    [new(Issue(6, 60000000000000m), Mark(), "TRY"), new(Issue(7, 60000000000000m), Mark(), "TRY")],
                    roundingPolicy, 2, failed));
            Assert(await AuditCount(failed.CorrelationId) == 0, "Aggregate position overflow retained inner cost audits.");
            await ThrowsAsync<PostgresException>(async () =>
                await PostgresInventoryIssueCostPreview.LoadAmountsAsync(connection, transaction, scope,
                    batch, roundingPolicy, 2, failed with { TraceId = new string('x', 65) }));
            Assert(await AuditCount(failed.CorrelationId) == 0, "Failed amount audit was not atomic.");
            await ThrowsAsync<InventoryCostHistoryUnavailableException>(async () =>
                await PostgresInventoryIssueCostPreview.LoadAsync(connection, transaction, scope,
                    [batch[0], new(secondIssue, Mark(3), "TRY")], failed));
            Assert(await AuditCount(failed.CorrelationId) == 0, "Second cost failure left the first line audit.");
            await ThrowsAsync<InventoryCostHistoryAccessException>(async () =>
                await PostgresInventoryIssueCostPreview.LoadAsync(connection, transaction, scope, batch,
                    failed with { ActorId = Guid.NewGuid() }));
            await ThrowsAsync<ArgumentException>(async () =>
                await PostgresInventoryIssueCostPreview.LoadAsync(connection, transaction, scope, [batch[0], batch[0]], failed));
            await ThrowsAsync<PostgresException>(async () =>
                await PostgresInventoryIssueCostPreview.LoadAsync(connection, transaction, scope, batch,
                    failed with { TraceId = new string('x', 65) }));
            Assert(await AuditCount(failed.CorrelationId) == 0, "Failed cost audit survived the savepoint.");
            await using (var movementCount = new NpgsqlCommand(
                "SELECT count(*) FROM inventory.stock_movement WHERE tenant_id=$1 AND source_event_id=$2", connection, transaction))
            {
                movementCount.Parameters.AddWithValue(tenantId);
                movementCount.Parameters.AddWithValue(sourceEvent);
                Assert((long)(await movementCount.ExecuteScalarAsync())! == 0, "Cost preview prematurely posted inventory.");
            }
            await ThrowsAsync<InventoryCostHistoryUnavailableException>(async () =>
                await PostgresInventoryCostHistoryLoader.LoadAsync(connection, transaction, scope, Mark(3), uom, "TRY"));
            await ThrowsAsync<InventoryCostHistoryUnavailableException>(async () =>
                await PostgresInventoryCostHistoryLoader.LoadAsync(connection, transaction, scope, Mark(checksum: new string('b', 64)), uom, "TRY"));
            await ThrowsAsync<InventoryCostHistoryUnavailableException>(async () =>
                await PostgresInventoryCostHistoryLoader.LoadAsync(connection, transaction, scope, Mark(), uom, "USD"));
            await ThrowsAsync<InventoryCostHistoryAccessException>(async () =>
                await PostgresInventoryCostHistoryLoader.LoadAsync(connection, transaction,
                    SalesScope(tenantId, companyId, actorId, "inventory.quantity.view"), Mark(), uom, "TRY"));
            await ThrowsAsync<ExecutionScopeDeniedException>(async () =>
                await PostgresInventoryCostHistoryLoader.LoadAsync(connection, transaction,
                    SalesScope(tenantId, otherCompanyId, actorId, PostgresInventoryCostHistoryLoader.RequiredPermission), Mark(), uom, "TRY"));
            await using (var setScope = new NpgsqlCommand("SELECT set_config('app.company_ids',$1,true)", connection, transaction))
            {
                setScope.Parameters.AddWithValue("{" + otherCompanyId + "}");
                await setScope.ExecuteNonQueryAsync();
            }
            await using (var rls = new NpgsqlCommand("SELECT count(*) FROM inventory.cost_history_publication WHERE publication_id=$1", connection, transaction))
            {
                rls.Parameters.AddWithValue(publication);
                Assert((long)(await rls.ExecuteScalarAsync())! == 0, "Cost publication leaked across company RLS.");
            }
            await transaction.RollbackAsync();
        }
        await AssertInventoryCostPublicationWriterAsync(owner, app, tenantId, companyId, actorId, itemId, warehouseId);
    }
}
