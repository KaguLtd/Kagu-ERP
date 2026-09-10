using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Bootstrap;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Application.Transfers;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Sales.Contracts.Reservations;
using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static partial class DatabaseIntegrationCheck
{
    private static async Task AssertSalesReservationCompositionAsync(NpgsqlDataSource owner,
        Guid tenantId, Guid companyId, Guid actorId, Guid orderId, Guid lineId)
    {
        await using var connection = await owner.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var scope = new ExecutionScope(tenantId, actorId, [new CompanyAccess(companyId,
            [AuthorizedInventoryReservationCandidate.RequiredPermission, "sales.order.confirm"])]);
        _ = await PostgresInventoryWarehouseScopeLoader.LoadAsync(connection, transaction, scope, companyId);
        // Read the fixture assignment, not an externally supplied warehouse ID.
        await using var warehouse = new NpgsqlCommand("""
            SELECT warehouse_id FROM iam.user_warehouse_scope
            WHERE tenant_id=$1 AND company_id=$2 AND user_profile_id=$3 ORDER BY warehouse_id LIMIT 1
            """, connection, transaction);
        warehouse.Parameters.AddWithValue(tenantId);
        warehouse.Parameters.AddWithValue(companyId);
        warehouse.Parameters.AddWithValue(actorId);
        Guid warehouseId = (Guid)(await warehouse.ExecuteScalarAsync() ?? throw new InvalidOperationException("Missing fixture warehouse."));
        var request = new InventoryReservationRequest(scope, companyId, Guid.CreateVersion7(), warehouseId,
            InventoryDemandSourceIdentity.Create("sales.order", orderId, lineId, 4),
            InventoryQuantity.Create(10m), new(2026, 9, 9));
        var audit = new RequestAuditContext(Guid.CreateVersion7(), "reservation-composition-fixture",
            tenantId, actorId, new HashSet<Guid> { companyId }, null);
        var auditFailure = await ThrowsAsync<PostgresException>(async () =>
            await PostgresSalesOrderReservationOrchestrator.CreateAsync(connection, transaction, request,
                audit with { TraceId = new string('x', 65) }));
        Assert(auditFailure.SqlState == "22001", "Audit failure fixture did not exercise its DB length constraint.");
        Assert(await PostgresInventoryReservationReplayLoader.LoadAsync(connection, transaction, request) is null,
            "Failed audit left an unaudited reservation result behind.");
        var result = await PostgresSalesOrderReservationOrchestrator.CreateAsync(connection, transaction, request, audit);
        Assert(result.RequestId == request.RequestId, "Real Sales demand was not composed with Inventory persistence.");
        await using var count = new NpgsqlCommand("""
            SELECT count(*) FROM platform.audit_event WHERE tenant_id=$1 AND correlation_id=$2
              AND action='inventory.reservation.create' AND target_id=$3
            """, connection, transaction);
        count.Parameters.AddWithValue(tenantId);
        count.Parameters.AddWithValue(audit.CorrelationId);
        count.Parameters.AddWithValue(request.RequestId.ToString("D"));
        Assert((long)(await count.ExecuteScalarAsync())! == 1, "Reservation audit was not written in the caller transaction.");
        await transaction.RollbackAsync();
        await using var after = new NpgsqlCommand("SELECT count(*) FROM inventory.reservation_request_result WHERE tenant_id=$1 AND company_id=$2 AND request_id=$3", connection);
        after.Parameters.AddWithValue(tenantId);
        after.Parameters.AddWithValue(companyId);
        after.Parameters.AddWithValue(request.RequestId);
        Assert((long)(await after.ExecuteScalarAsync())! == 0, "Caller rollback left a reservation result behind.");
    }

    private static async Task AssertReservationWriterAsync(NpgsqlDataSource owner,
        Guid tenantId, Guid companyId, Guid warehouseId, Guid secondWarehouseId, Guid actorId, Guid itemId)
    {
        await using var connection = await owner.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var scope = new ExecutionScope(tenantId, actorId,
            [new CompanyAccess(companyId, [AuthorizedInventoryReservationCandidate.RequiredPermission,
                AuthorizedImmediateStockTransferCandidate.RequiredPermission, InventoryReservationReleaseRequest.RequiredPermission])]);
        var identity = InventoryDemandSourceIdentity.Create("sales.order", Guid.CreateVersion7(), Guid.CreateVersion7(), 4);
        var producer = new ReservationWriterDemandFixture(SalesOrderReservationDemandSnapshot.Create(
            tenantId, companyId, identity.SourceId, 4,
            [SalesOrderReservationDemandLine.Create(identity.SourceLineId, itemId, "EA", 10m)]));
        InventoryReservationRequest Request(Guid warehouse, decimal quantity = 10m) => new(scope,
            companyId, Guid.CreateVersion7(), warehouse, identity, InventoryQuantity.Create(quantity), new(2026, 9, 9));
        await using (var seed = new NpgsqlCommand("""
            INSERT INTO inventory.stock_movement
                (tenant_id,company_id,movement_id,item_id,warehouse_id,base_uom_code,movement_kind,
                 base_quantity,effective_date,recorded_at,recorded_by,sequence_key,source_type,
                 source_event_id,source_line_id,source_version,posting_purpose)
            VALUES ($1,$2,$7,$3,$4,'EA',1,7,DATE '2026-09-09',clock_timestamp(),$6,1,'fixture',$7,$7,1,'opening'),
                   ($1,$2,$8,$3,$5,'EA',1,7,DATE '2026-09-09',clock_timestamp(),$6,1,'fixture',$8,$8,1,'opening');
            INSERT INTO inventory.stock_block_event
                (tenant_id,company_id,block_id,version,item_id,warehouse_id,base_uom_code,
                 blocked_quantity,effective_date,recorded_by,correlation_id,reason)
            VALUES ($1,$2,$9,1,$3,$4,'EA',2,DATE '2026-09-09',$6,$9,'fixture');
            """, connection, transaction))
        {
            foreach (Guid id in new[] {tenantId, companyId, itemId, warehouseId, secondWarehouseId,
                actorId, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()})
                seed.Parameters.AddWithValue(id);
            await seed.ExecuteNonQueryAsync();
        }

        // An intermediate future shortage must not be hidden by a later replenishment.
        await transaction.SaveAsync("scheduled_capacity_fixture");
        await using (var scheduled = new NpgsqlCommand("""
            INSERT INTO inventory.stock_movement
                (tenant_id,company_id,movement_id,item_id,warehouse_id,base_uom_code,movement_kind,
                 base_quantity,effective_date,recorded_at,recorded_by,sequence_key,source_type,
                 source_event_id,source_line_id,source_version,posting_purpose)
            VALUES ($1,$2,$6,$3,$4,'EA',2,-4,DATE '2026-09-10',TIMESTAMPTZ '2099-01-01 00:00:00+00',$5,1,'fixture',$6,$6,1,'scheduled'),
                   ($1,$2,$7,$3,$4,'EA',1,10,DATE '2026-09-11',clock_timestamp(),$5,1,'fixture',$7,$7,1,'scheduled');
            """, connection, transaction))
        {
            foreach (Guid id in new[] {tenantId, companyId, itemId, warehouseId, actorId,
                Guid.CreateVersion7(), Guid.CreateVersion7()}) scheduled.Parameters.AddWithValue(id);
            await scheduled.ExecuteNonQueryAsync();
        }
        var scheduledResult = await PostgresInventoryReservationWriter.CreateAsync(
            connection, transaction, Request(warehouseId), producer);
        Assert(scheduledResult.ReservedQuantity.Value == 1m,
            "A scheduled issue/late recorded timestamp was hidden by a later receipt or reporting cutoff.");
        await transaction.RollbackAsync("scheduled_capacity_fixture");
        await transaction.ReleaseAsync("scheduled_capacity_fixture");

        var first = Request(warehouseId);
        var created = await PostgresInventoryReservationWriter.CreateAsync(connection, transaction, first, producer);
        Assert(created.ReservedQuantity.Value == 5m && created.ReservationId is not null,
            "Reservation writer must allocate on-hand minus blocked stock.");
        int calls = producer.Calls;
        var replay = await PostgresInventoryReservationWriter.CreateAsync(connection, transaction, first, producer);
        Assert(replay == created && producer.Calls == calls,
            "Replay must return the immutable original result without loading changed demand.");
        var changed = new InventoryReservationRequest(scope, companyId, first.RequestId, warehouseId,
            identity, InventoryQuantity.Create(9m), first.EffectiveDate);
        await ThrowsAsync<InventoryReservationRequestConflictException>(async () =>
            await PostgresInventoryReservationWriter.CreateAsync(connection, transaction, changed, producer));

        var emptyRequest = Request(warehouseId);
        var empty = await PostgresInventoryReservationWriter.CreateAsync(connection, transaction, emptyRequest, producer);
        Assert(empty.ReservedQuantity.IsZero && empty.ReservationId is null,
            "A second request must not reserve already committed stock.");
        var second = await PostgresInventoryReservationWriter.CreateAsync(connection, transaction, Request(secondWarehouseId), producer);
        Assert(second.ReservedQuantity.Value == 5m,
            "The source-line capacity must apply across warehouses, not separately to each warehouse.");

        async Task<AuthorizedImmediateStockTransferCandidate> Transfer(Guid from, Guid to, decimal quantity, long sequence,
            long? receiptSequence = null)
        {
            Guid transferId = Guid.CreateVersion7();
            var source = StockMovementSourceIdentity.Create(tenantId, companyId, "inventory.transfer",
                Guid.CreateVersion7(), Guid.CreateVersion7(), 1, "stock-transfer");
            StockMovementDraft Movement(Guid at, Guid counterpart, StockMovementKind kind, decimal signed) =>
                StockMovementDraft.Create(Guid.CreateVersion7(), tenantId, companyId, itemId, at,
                    InventoryUomCode.Create("EA"), kind, InventoryQuantity.Create(signed), first.EffectiveDate,
                    new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero),
                    kind == StockMovementKind.TransferReceipt ? receiptSequence ?? sequence : sequence,
                    source, transferId, counterpart);
            var draft = ValidatedImmediateStockTransferDraft.Create(transferId,
                Movement(from, to, StockMovementKind.TransferIssue, -quantity),
                Movement(to, from, StockMovementKind.TransferReceipt, quantity));
            var allowed = await PostgresInventoryWarehouseScopeLoader.LoadAsync(connection, transaction, scope, companyId);
            return AuthorizedImmediateStockTransferCandidate.Create(scope, allowed, draft);
        }
        var protectedTransfer = await Transfer(warehouseId, secondWarehouseId, 1m, 2);
        await ThrowsAsync<InventoryProtectedStockConflictException>(async () =>
            await PostgresImmediateStockTransferWriter.PersistAsync(connection, transaction, protectedTransfer));
        Assert(await CountAsync(connection, transaction,
            "SELECT count(*) FROM inventory.stock_movement WHERE transfer_id=$1", protectedTransfer.Transfer.TransferId) == 0,
            "Capacity rejection left one or both transfer legs behind.");
        var freeTransfer = await Transfer(secondWarehouseId, warehouseId, 2m, 2);
        await transaction.SaveAsync("future_block_fixture");
        await using (var futureBlock = new NpgsqlCommand("""
            INSERT INTO inventory.stock_block_event
                (tenant_id,company_id,block_id,version,item_id,warehouse_id,base_uom_code,
                 blocked_quantity,effective_date,recorded_by,correlation_id,reason)
            VALUES ($1,$2,$6,1,$3,$4,'EA',1,DATE '2026-09-10',$5,$6,'scheduled fixture');
            """, connection, transaction))
        {
            foreach (Guid id in new[] { tenantId, companyId, itemId, secondWarehouseId, actorId, Guid.CreateVersion7() })
                futureBlock.Parameters.AddWithValue(id);
            await futureBlock.ExecuteNonQueryAsync();
        }
        await ThrowsAsync<InventoryProtectedStockConflictException>(async () =>
            await PostgresImmediateStockTransferWriter.PersistAsync(connection, transaction, freeTransfer));
        await transaction.RollbackAsync("future_block_fixture");
        await transaction.ReleaseAsync("future_block_fixture");
        Assert((await PostgresImmediateStockTransferWriter.PersistAsync(connection, transaction, freeTransfer)).Created,
            "Unreserved free stock could not be transferred.");
        Assert(!(await PostgresImmediateStockTransferWriter.PersistAsync(connection, transaction, freeTransfer)).Created,
            "Transfer replay was incorrectly rejected after the source's free capacity became zero.");
        var secondLegCollision = await Transfer(secondWarehouseId, warehouseId, 1m, 3, 2);
        var collision = await ThrowsAsync<PostgresException>(async () =>
            await PostgresImmediateStockTransferWriter.PersistAsync(connection, transaction, secondLegCollision));
        Assert(collision.SqlState == "23505", "Second-leg failure fixture did not hit the position uniqueness constraint.");
        Assert(await CountAsync(connection, transaction,
            "SELECT count(*) FROM inventory.stock_movement WHERE transfer_id=$1", secondLegCollision.Transfer.TransferId) == 0,
            "Second-leg SQL failure left a source issue or an aborted caller transaction behind.");
        var fractionalTransfer = await Transfer(warehouseId, secondWarehouseId, 0.5m, 3);
        await ThrowsAsync<InventoryStockMasterUnavailableException>(async () =>
            await PostgresImmediateStockTransferWriter.PersistAsync(connection, transaction, fractionalTransfer));
        await transaction.SaveAsync("inactive_master_fixture");
        await using (var deactivate = new NpgsqlCommand("""
            UPDATE org.warehouse SET is_active=false,version=version+1
            WHERE tenant_id=$1 AND company_id=$2 AND warehouse_id=$3
            """, connection, transaction))
        {
            deactivate.Parameters.AddWithValue(tenantId);
            deactivate.Parameters.AddWithValue(companyId);
            deactivate.Parameters.AddWithValue(warehouseId);
            await deactivate.ExecuteNonQueryAsync();
        }
        var inactiveTransfer = await Transfer(secondWarehouseId, warehouseId, 1m, 3);
        await ThrowsAsync<InventoryStockMasterUnavailableException>(async () =>
            await PostgresImmediateStockTransferWriter.PersistAsync(connection, transaction, inactiveTransfer));
        await ThrowsAsync<InventoryReservationAuthorizationException>(async () =>
            await PostgresInventoryReservationWriter.CreateAsync(connection, transaction, Request(warehouseId), producer));
        Assert(!(await PostgresImmediateStockTransferWriter.PersistAsync(connection, transaction, freeTransfer)).Created,
            "An existing transfer replay was treated as a new write after warehouse deactivation.");
        await transaction.RollbackAsync("inactive_master_fixture");
        await transaction.ReleaseAsync("inactive_master_fixture");
        var extraTransfer = await Transfer(secondWarehouseId, warehouseId, 1m, 3);
        await ThrowsAsync<InventoryProtectedStockConflictException>(async () =>
            await PostgresImmediateStockTransferWriter.PersistAsync(connection, transaction, extraTransfer));
        var full = await PostgresInventoryReservationWriter.CreateAsync(connection, transaction, Request(secondWarehouseId), producer);
        Assert(full.ReservedQuantity.IsZero, "Fulfilled reservation demand must not allocate remaining free stock.");
        Assert(await PostgresInventoryReservationWriter.CreateAsync(connection, transaction, emptyRequest, producer) == empty,
            "A zero result must be stable on replay.");

        await ThrowsAsync<InventoryReservationAuthorizationException>(async () =>
            await PostgresInventoryReservationWriter.CreateAsync(connection, transaction, Request(secondWarehouseId, 0.5m), producer));
        await ThrowsAsync<InventoryReservationAuthorizationException>(async () =>
            await PostgresInventoryReservationWriter.CreateAsync(connection, transaction, Request(secondWarehouseId, 11m), producer));
        var stale = new InventoryReservationRequest(scope, companyId, Guid.CreateVersion7(), warehouseId,
            InventoryDemandSourceIdentity.Create("sales.order", identity.SourceId, identity.SourceLineId, 3),
            InventoryQuantity.Create(10m), first.EffectiveDate);
        await ThrowsAsync<InventoryReservationDemandContractMismatchException>(async () =>
            await PostgresInventoryReservationWriter.CreateAsync(connection, transaction, stale, producer));
        var unassigned = new ExecutionScope(tenantId, Guid.CreateVersion7(),
            [new CompanyAccess(companyId, [AuthorizedInventoryReservationCandidate.RequiredPermission])]);
        var deniedRequest = new InventoryReservationRequest(unassigned, companyId, Guid.CreateVersion7(),
            warehouseId, identity, InventoryQuantity.Create(10m), first.EffectiveDate);
        calls = producer.Calls;
        await ThrowsAsync<InventoryReservationAuthorizationException>(async () =>
            await PostgresInventoryReservationWriter.CreateAsync(connection, transaction, deniedRequest, producer));
        Assert(producer.Calls == calls, "Unauthorized warehouse request reached the demand producer.");
        // Re-establish the trusted context after the deliberately denied actor.
        _ = await PostgresInventoryWarehouseScopeLoader.LoadAsync(connection, transaction, scope, companyId);

        // A different demand must still wait on the same stock position, not bypass its capacity lock.
        await using (var competitor = await owner.OpenConnectionAsync())
        await using (var competitorTransaction = await competitor.BeginTransactionAsync())
        {
            await using (var timeout = new NpgsqlCommand("SET LOCAL lock_timeout='100ms'", competitor, competitorTransaction))
                await timeout.ExecuteNonQueryAsync();
            var otherIdentity = InventoryDemandSourceIdentity.Create("sales.order", Guid.CreateVersion7(), Guid.CreateVersion7(), 4);
            var otherProducer = new ReservationWriterDemandFixture(SalesOrderReservationDemandSnapshot.Create(
                tenantId, companyId, otherIdentity.SourceId, 4,
                [SalesOrderReservationDemandLine.Create(otherIdentity.SourceLineId, itemId, "EA", 10m)]));
            var competingRequest = new InventoryReservationRequest(scope, companyId, Guid.CreateVersion7(),
                warehouseId, otherIdentity, InventoryQuantity.Create(10m), first.EffectiveDate);
            var locked = await ThrowsAsync<PostgresException>(async () =>
                await PostgresInventoryReservationWriter.CreateAsync(competitor, competitorTransaction, competingRequest, otherProducer));
            Assert(locked.SqlState == "55P03", "A competing reservation writer did not wait on the occupied stock position.");
            await competitorTransaction.RollbackAsync();
        }
        await using (var competitor = await owner.OpenConnectionAsync())
        await using (var competitorTransaction = await competitor.BeginTransactionAsync())
        {
            await using (var timeout = new NpgsqlCommand("SET LOCAL lock_timeout='100ms'", competitor, competitorTransaction))
                await timeout.ExecuteNonQueryAsync();
            var locked = await ThrowsAsync<PostgresException>(async () =>
                await PostgresImmediateStockTransferWriter.PersistAsync(competitor, competitorTransaction, extraTransfer));
            Assert(locked.SqlState == "55P03", "A competing transfer writer did not wait on the reserved stock position.");
            await competitorTransaction.RollbackAsync();
        }
        await AssertReservationReleaseBatchAsync(connection, transaction, scope, companyId,
            warehouseId, secondWarehouseId, created.ReservationId!.Value, second.ReservationId!.Value, first.EffectiveDate);
        await using (var constraints = new NpgsqlCommand("SET CONSTRAINTS ALL IMMEDIATE", connection, transaction))
            await constraints.ExecuteNonQueryAsync();
        await using (var verify = new NpgsqlCommand("""
            SELECT count(*),sum(reserved_quantity) FROM inventory.reservation_creation
            WHERE tenant_id=$1 AND company_id=$2 AND source_id=$3;
            """, connection, transaction))
        {
            verify.Parameters.AddWithValue(tenantId);
            verify.Parameters.AddWithValue(companyId);
            verify.Parameters.AddWithValue(identity.SourceId);
            await using var reader = await verify.ExecuteReaderAsync();
            Assert(await reader.ReadAsync() && reader.GetInt64(0) == 2 && reader.GetDecimal(1) == 10m,
                "Positive reservation/result pairs must satisfy deferred constraints and conserve source demand.");
        }
        await transaction.RollbackAsync();
    }

    private sealed class ReservationWriterDemandFixture(SalesOrderReservationDemandSnapshot snapshot)
        : ISalesOrderReservationDemandSource
    {
        public int Calls { get; private set; }
        public ValueTask<SalesOrderReservationDemandSnapshot?> LoadAsync(
            SalesOrderReservationDemandQuery query, CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult<SalesOrderReservationDemandSnapshot?>(snapshot);
        }
    }
}
