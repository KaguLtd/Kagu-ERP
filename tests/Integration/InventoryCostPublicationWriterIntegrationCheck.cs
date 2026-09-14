using KaguERP.Bootstrap;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static partial class DatabaseIntegrationCheck
{
    private static async Task AssertInventoryCostPublicationWriterAsync(NpgsqlDataSource owner, NpgsqlDataSource app,
        Guid tenantId, Guid companyId, Guid actorId, Guid itemId, Guid warehouseId)
    {
        var scope = SalesScope(tenantId, companyId, actorId, PostgresInventoryCostPublicationWriter.RequiredPermission,
            PostgresInventoryCostHistoryLoader.RequiredPermission);
        var at = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);
        InventoryValuationWatermark Mark(long sequence) => InventoryValuationWatermark.Create(tenantId, companyId,
            itemId, warehouseId, InventoryPosition.Create(new(2026, 9, 10), sequence), 1, at, new string('c', 64));
        var uom = InventoryUomCode.Create("EA");
        Guid publicationId = Guid.CreateVersion7(), snapshotId = Guid.CreateVersion7();
        var evidence = InventoryCostHistoryEvidence.Known(Mark(100), uom, "TRY", snapshotId, 4.25m);
        var audit = new RequestAuditContext(Guid.CreateVersion7(), "cost-publication-writer", tenantId, actorId,
            new HashSet<Guid> { companyId }, null);
        await using (var connection = await app.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var denied = await ThrowsAsync<PostgresException>(async () =>
                await PostgresInventoryCostPublicationOrchestrator.PublishAsync(connection, transaction, scope,
                    publicationId, evidence, audit));
            Assert(denied.SqlState == PostgresErrorCodes.InsufficientPrivilege, "Runtime cost publication writes were enabled.");
            await transaction.RollbackAsync();
        }
        await using (var connection = await owner.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            async Task AssertAbsent(Guid id)
            {
                await using var query = new NpgsqlCommand("""
                    SELECT (SELECT count(*) FROM inventory.cost_history_publication WHERE tenant_id=$1 AND company_id=$2 AND publication_id=$3),
                           (SELECT count(*) FROM platform.audit_event WHERE tenant_id=$1 AND correlation_id=$4)
                    """, connection, transaction);
                foreach (Guid value in new[] { tenantId, companyId, id, audit.CorrelationId }) query.Parameters.AddWithValue(value);
                await using var reader = await query.ExecuteReaderAsync();
                Assert(await reader.ReadAsync() && reader.GetInt64(0) == 0 && reader.GetInt64(1) == 0,
                    "Failed publication left a result or audit.");
            }
            await ThrowsAsync<InventoryCostHistoryAccessException>(async () =>
                await PostgresInventoryCostPublicationOrchestrator.PublishAsync(connection, transaction, scope,
                    publicationId, evidence, audit with { ActorId = Guid.NewGuid() }));
            await ThrowsAsync<InventoryCostHistoryAccessException>(async () =>
                await PostgresInventoryCostPublicationOrchestrator.PublishAsync(connection, transaction,
                    SalesScope(tenantId, companyId, actorId, PostgresInventoryCostHistoryLoader.RequiredPermission),
                    publicationId, evidence, audit));
            await ThrowsAsync<PostgresException>(async () =>
                await PostgresInventoryCostPublicationOrchestrator.PublishAsync(connection, transaction, scope,
                    publicationId, evidence, audit with { TraceId = new string('x', 65) }));
            await AssertAbsent(publicationId);
            var result = await PostgresInventoryCostPublicationOrchestrator.PublishAsync(connection, transaction,
                scope, publicationId, evidence, audit);
            Assert(result.Created && result.PublicationId == publicationId && result.PublishedAt.Offset == TimeSpan.Zero,
                "Cost publication did not return its canonical persisted result.");
            var replay = await PostgresInventoryCostPublicationOrchestrator.PublishAsync(connection, transaction, scope,
                publicationId, InventoryCostHistoryEvidence.Known(Mark(100), uom, "TRY", snapshotId, 4.250000m), audit);
            Assert(!replay.Created && replay.PublishedAt == result.PublishedAt, "Cost replay changed its timestamp or value identity.");
            await ThrowsAsync<InventoryCostPublicationConflictException>(async () =>
                await PostgresInventoryCostPublicationOrchestrator.PublishAsync(connection, transaction, scope,
                    publicationId, InventoryCostHistoryEvidence.Known(Mark(100), uom, "TRY", snapshotId, 5m), audit));
            await ThrowsAsync<InventoryCostPublicationConflictException>(async () =>
                await PostgresInventoryCostPublicationOrchestrator.PublishAsync(connection, transaction, scope,
                    Guid.CreateVersion7(), evidence, audit));
            var zeroId = Guid.CreateVersion7();
            var zero = await PostgresInventoryCostPublicationOrchestrator.PublishAsync(connection, transaction, scope,
                zeroId, InventoryCostHistoryEvidence.NoHistory(Mark(101), uom, "TRY"), audit);
            var loaded = await PostgresInventoryCostHistoryLoader.LoadAsync(connection, transaction, scope, Mark(101), uom, "TRY");
            Assert(zero.Created && loaded.Origin == InventoryIssueCostOrigin.NoHistoryZero && loaded.UnitCost == 0m,
                "Explicit zero publication did not round-trip.");
            var issue = StockMovementDraft.Create(Guid.CreateVersion7(), tenantId, companyId, itemId, warehouseId,
                uom, StockMovementKind.Issue, InventoryQuantity.Create(-3m), new(2026, 9, 10), at, 102,
                StockMovementSourceIdentity.Create(tenantId, companyId, "sales.dispatch", Guid.CreateVersion7(),
                    Guid.CreateVersion7(), 1, "issue"));
            var costPreview = await PostgresInventoryIssueCostPreview.LoadAsync(connection, transaction, scope,
                [new(issue, Mark(100), "TRY")], audit);
            Assert(costPreview.Single().UnitCost == 4.25m && costPreview[0].History.SnapshotId == snapshotId,
                "Publication writer → persisted reader → issue preview lost cost or lineage.");
            await using (var competingConnection = await owner.OpenConnectionAsync())
            await using (var competingTransaction = await competingConnection.BeginTransactionAsync())
            {
                await using (var timeout = new NpgsqlCommand("SET LOCAL lock_timeout='100ms'", competingConnection, competingTransaction))
                    await timeout.ExecuteNonQueryAsync();
                var blocked = await ThrowsAsync<PostgresException>(async () =>
                    await PostgresInventoryCostPublicationOrchestrator.PublishAsync(competingConnection, competingTransaction,
                        scope, publicationId, evidence, audit));
                Assert(blocked.SqlState == PostgresErrorCodes.LockNotAvailable, "Same publication did not serialize across connections.");
                await competingTransaction.RollbackAsync();
            }
            await transaction.RollbackAsync();
        }
    }
}
