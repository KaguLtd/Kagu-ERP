using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Queries;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Application.Transfers;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static partial class DatabaseIntegrationCheck
{
    private static async Task AssertReservationLifecyclePersistenceAsync(
        NpgsqlDataSource dataSource, Guid tenantId, Guid companyId, Guid warehouseId, Guid actorId, Guid itemId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        Guid reservationId = Guid.CreateVersion7();
        var scope = new ExecutionScope(tenantId, actorId,
            [new CompanyAccess(companyId, [AuthorizedInventoryReservationCandidate.RequiredPermission,
                InventoryReservationReleaseRequest.RequiredPermission])]);
        var request = new InventoryReservationRequest(scope, companyId, Guid.CreateVersion7(), warehouseId,
            InventoryDemandSourceIdentity.Create("sales.order", Guid.CreateVersion7(), Guid.CreateVersion7(), 4),
            InventoryQuantity.Create(10m), new DateOnly(2026, 9, 8));
        await using (var seed = new NpgsqlCommand("""
            INSERT INTO inventory.reservation_creation
                (tenant_id,company_id,warehouse_id,recorded_by,item_id,request_id,reservation_id,
                 source_id,source_line_id,source_type,source_version,base_uom_code,requested_quantity,
                 reserved_quantity,effective_date,recorded_at,correlation_id,policy_version)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,'sales.order',4,'EA',10,10,DATE '2026-09-08',
                    TIMESTAMPTZ '2026-09-08 12:00:00+00',$6,'DEC-MP01-025/v1');
            INSERT INTO inventory.reservation_request_result
                (tenant_id,company_id,warehouse_id,recorded_by,request_id,reservation_id,
                 requested_quantity,reserved_quantity,request_fingerprint)
            VALUES ($1,$2,$3,$4,$6,$7,10,10,$10);
            """, connection, transaction))
        {
            foreach (Guid id in new[] { tenantId,companyId,warehouseId,actorId,itemId,request.RequestId,
                reservationId,request.Source.SourceId,request.Source.SourceLineId })
                seed.Parameters.AddWithValue(id);
            seed.Parameters.AddWithValue(request.Fingerprint);
            await seed.ExecuteNonQueryAsync();
        }
        async Task InsertEvent(long version, short transition, decimal quantity, decimal consumed, decimal remaining)
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO inventory.reservation_lifecycle_event
                    (tenant_id,company_id,reservation_id,event_id,version,transition,quantity,
                     consumed_quantity,remaining_quantity,effective_date,occurred_at,actor_id,correlation_id,reason)
                VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,DATE '2026-09-08',TIMESTAMPTZ '2026-09-08 12:01:00+00',$10,$4,'fixture')
                """, connection, transaction);
            insert.Parameters.AddWithValue(tenantId);
            insert.Parameters.AddWithValue(companyId);
            insert.Parameters.AddWithValue(reservationId);
            insert.Parameters.AddWithValue(Guid.CreateVersion7());
            insert.Parameters.AddWithValue(version);
            insert.Parameters.AddWithValue(transition);
            insert.Parameters.AddWithValue(quantity);
            insert.Parameters.AddWithValue(consumed);
            insert.Parameters.AddWithValue(remaining);
            insert.Parameters.AddWithValue(actorId);
            await insert.ExecuteNonQueryAsync();
        }
        await InsertEvent(2, 1, 6m, 6m, 4m);
        await transaction.SaveAsync("excess_consumption");
        var excessive = await ThrowsAsync<PostgresException>(() => InsertEvent(3, 1, 5m, 11m, 0m));
        Assert(excessive.SqlState == "23514", "Reservation cannot consume beyond its remaining quantity.");
        await transaction.RollbackAsync("excess_consumption");
        await AssertReservationReleaseWriterAsync(connection, transaction, scope, companyId, warehouseId,
            reservationId, request.EffectiveDate);
        var demand = InventoryReservationDemandEvidence.Create(tenantId, companyId, request.Source,
            itemId, InventoryUomCode.Create("EA"), InventoryQuantity.Create(10m));
        await PostgresInventoryReservationRequestGate.AcquireAsync(connection, transaction, request);
        var balance = await PostgresInventoryReservationBalanceLoader.LoadAsync(connection, transaction, request, demand);
        Assert(balance.ActiveReservedAtPosition == 0m && balance.CommittedQuantityForDemand == 6m,
            "Release must clear active reservation while retaining the six consumed units of demand.");
        Guid blockId = Guid.CreateVersion7();
        async Task BlockEvent(long version, decimal remaining, DateOnly effectiveDate)
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO inventory.stock_block_event
                    (tenant_id,company_id,block_id,version,item_id,warehouse_id,base_uom_code,
                     blocked_quantity,effective_date,recorded_by,correlation_id,reason)
                VALUES ($1,$2,$3,$4,$5,$6,'EA',$7,$8,$9,$10,'fixture')
                """, connection, transaction);
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(blockId);
            command.Parameters.AddWithValue(version);
            command.Parameters.AddWithValue(itemId);
            command.Parameters.AddWithValue(warehouseId);
            command.Parameters.AddWithValue(remaining);
            command.Parameters.AddWithValue(effectiveDate);
            command.Parameters.AddWithValue(actorId);
            command.Parameters.AddWithValue(Guid.CreateVersion7());
            await command.ExecuteNonQueryAsync();
        }
        await BlockEvent(1, 3m, request.EffectiveDate);
        await BlockEvent(2, 2m, request.EffectiveDate);
        await transaction.SaveAsync("block_increase");
        var increase = await ThrowsAsync<PostgresException>(() => BlockEvent(3, 3m, request.EffectiveDate));
        Assert(increase.SqlState == "23514", "A block release cannot increase blocked quantity.");
        await transaction.RollbackAsync("block_increase");
        await BlockEvent(3, 0m, request.EffectiveDate.AddDays(1));
        balance = await PostgresInventoryReservationBalanceLoader.LoadAsync(connection, transaction, request, demand);
        Assert(balance.BlockedAtPosition == 2m,
            "Blocked balance must use the last effective version, not sum history or apply a future release.");
        var position = new InventoryPositionLockTarget(itemId, warehouseId, InventoryUomCode.Create("EA"));
        var capacityBeforeRelease = await PostgresInventoryPositionCapacityLoader.LoadAsync(
            connection, transaction, scope, companyId, position, request.EffectiveDate);
        var capacityAfterRelease = await PostgresInventoryPositionCapacityLoader.LoadAsync(
            connection, transaction, scope, companyId, position, request.EffectiveDate.AddDays(1));
        Assert(capacityBeforeRelease.MinimumAvailable == -2m && capacityBeforeRelease.HasProtectedDeficit &&
               capacityAfterRelease.MinimumAvailable == 0m && !capacityAfterRelease.HasProtectedDeficit,
            "Capacity must respect effective block release and must not retain released reservation creation quantities.");
        var terminal = await ThrowsAsync<PostgresException>(() => InsertEvent(4, 1, 1m, 7m, 0m));
        Assert(terminal.SqlState == "23514", "Released reservation cannot consume again.");
        await transaction.RollbackAsync();
    }

    private static async Task AssertReservationRequestGateAsync(
        NpgsqlDataSource ownerDataSource, NpgsqlDataSource appDataSource,
        Guid tenantId, Guid companyId, Guid warehouseId, Guid actorId, Guid itemId)
    {
        var scope = new ExecutionScope(tenantId, actorId,
            [new CompanyAccess(companyId, [AuthorizedInventoryReservationCandidate.RequiredPermission])]);
        var request = new InventoryReservationRequest(scope, companyId, Guid.CreateVersion7(), warehouseId,
            InventoryDemandSourceIdentity.Create("sales.order", Guid.CreateVersion7(), Guid.CreateVersion7(), 4),
            InventoryQuantity.Create(10m), new DateOnly(2026, 9, 8));
        var changed = new InventoryReservationRequest(scope, companyId, request.RequestId, warehouseId,
            request.Source, InventoryQuantity.Create(11m), request.EffectiveDate);
        await using var first = await ownerDataSource.OpenConnectionAsync();
        await using var second = await appDataSource.OpenConnectionAsync();
        await using var owner = await first.BeginTransactionAsync();
        Assert(await PostgresInventoryReservationRequestGate.AcquireAsync(first, owner, request) is null,
            "A new reservation request must have no replay result.");
        await using (var competitor = await second.BeginTransactionAsync())
        {
            await using var timeout = new NpgsqlCommand("SET LOCAL lock_timeout='100ms'", second, competitor);
            await timeout.ExecuteNonQueryAsync();
            var blocked = await ThrowsAsync<PostgresException>(async () =>
                await PostgresInventoryReservationRequestGate.AcquireAsync(second, competitor, changed));
            Assert(blocked.SqlState == "55P03", "Changed contents must still contend for the same request lock.");
            await competitor.RollbackAsync();
        }
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO inventory.reservation_request_result
                (tenant_id,company_id,request_id,warehouse_id,request_fingerprint,
                 requested_quantity,reserved_quantity,reservation_id,recorded_by)
            VALUES ($1,$2,$3,$4,$5,10,0,NULL,$6)
            """, first, owner))
        {
            insert.Parameters.AddWithValue(tenantId);
            insert.Parameters.AddWithValue(companyId);
            insert.Parameters.AddWithValue(request.RequestId);
            insert.Parameters.AddWithValue(warehouseId);
            insert.Parameters.AddWithValue(request.Fingerprint);
            insert.Parameters.AddWithValue(actorId);
            await insert.ExecuteNonQueryAsync();
        }
        await owner.CommitAsync();
        await using var retry = await second.BeginTransactionAsync();
        var replay = await PostgresInventoryReservationRequestGate.AcquireAsync(second, retry, request);
        Assert(replay is not null && replay.ReservedQuantity.IsZero && replay.ReservationId is null,
            "Runtime-role retry must return the committed zero result after acquiring the lock.");
        var demand = InventoryReservationDemandEvidence.Create(tenantId, companyId, request.Source,
            itemId, InventoryUomCode.Create("EA"), InventoryQuantity.Create(10m));
        var balance = await PostgresInventoryReservationBalanceLoader.LoadAsync(second, retry, request, demand);
        Assert(balance.OnHand.IsZero && balance.CreatedQuantityAtPosition == 0m &&
            balance.CreatedQuantityForDemand == 0m && balance.EffectiveAsOf == request.EffectiveDate &&
            balance.RecordedCutoff.Offset == TimeSpan.Zero,
            "Empty inventory balances must remain zero; zero request results must not reserve stock.");
        await ThrowsAsync<InventoryReservationRequestConflictException>(async () =>
            await PostgresInventoryReservationRequestGate.AcquireAsync(second, retry, changed));
        await retry.RollbackAsync();
    }

    private static async Task AssertReservationZeroResultAsync(
        NpgsqlDataSource dataSource, Guid tenantId, Guid companyId, Guid warehouseId, Guid actorId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        Guid requestId = Guid.CreateVersion7();
        var scope = new ExecutionScope(tenantId, actorId,
            [new CompanyAccess(companyId, [AuthorizedInventoryReservationCandidate.RequiredPermission])]);
        var source = InventoryDemandSourceIdentity.Create("sales.order", Guid.CreateVersion7(), Guid.CreateVersion7(), 4);
        var request = new InventoryReservationRequest(scope, companyId, requestId, warehouseId,
            source, InventoryQuantity.Create(10m), new DateOnly(2026, 9, 8));
        await using var insert = new NpgsqlCommand("""
            INSERT INTO inventory.reservation_request_result
                (tenant_id,company_id,request_id,warehouse_id,request_fingerprint,
                 requested_quantity,reserved_quantity,reservation_id,recorded_by)
            VALUES ($1,$2,$3,$4,$6,10,0,NULL,$5)
            """, connection, transaction);
        insert.Parameters.AddWithValue(tenantId);
        insert.Parameters.AddWithValue(companyId);
        insert.Parameters.AddWithValue(requestId);
        insert.Parameters.AddWithValue(warehouseId);
        insert.Parameters.AddWithValue(actorId);
        insert.Parameters.AddWithValue(request.Fingerprint);
        Assert(await insert.ExecuteNonQueryAsync() == 1, "Zero allocation must have a durable request result.");
        var replay = await PostgresInventoryReservationReplayLoader.LoadAsync(connection, transaction, request);
        Assert(replay is not null && replay.ReservationId is null && replay.ReservedQuantity.IsZero &&
            replay.RequestedQuantity.Value == 10m, "Zero-result retry must return the original zero allocation.");
        var changed = new InventoryReservationRequest(scope, companyId, requestId, warehouseId,
            source, InventoryQuantity.Create(11m), new DateOnly(2026, 9, 8));
        await ThrowsAsync<InventoryReservationRequestConflictException>(async () =>
            await PostgresInventoryReservationReplayLoader.LoadAsync(connection, transaction, changed));
        await transaction.SaveAsync("duplicate_result");
        var duplicate = await ThrowsAsync<PostgresException>(async () => await insert.ExecuteNonQueryAsync());
        Assert(duplicate.SqlState == "23505", "Duplicate reservation request must be rejected.");
        await transaction.RollbackAsync("duplicate_result");
        await using var mutate = new NpgsqlCommand("""
            UPDATE inventory.reservation_request_result SET requested_quantity=11
            WHERE tenant_id=$1 AND company_id=$2 AND request_id=$3
            """, connection, transaction);
        mutate.Parameters.AddWithValue(tenantId);
        mutate.Parameters.AddWithValue(companyId);
        mutate.Parameters.AddWithValue(requestId);
        var immutable = await ThrowsAsync<PostgresException>(async () => await mutate.ExecuteNonQueryAsync());
        Assert(immutable.SqlState == "23514", "Reservation result must remain immutable even for the migration role.");
        await transaction.RollbackAsync();
    }

    private static async Task AssertReservationCreationSchemaAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT c.relrowsecurity AND c.relforcerowsecurity
                AND has_table_privilege(current_user, c.oid, 'SELECT')
                AND NOT has_table_privilege(current_user, c.oid, 'INSERT')
                AND NOT has_table_privilege(current_user, c.oid, 'UPDATE')
                AND NOT has_table_privilege(current_user, c.oid, 'DELETE')
                AND EXISTS (SELECT 1 FROM pg_constraint k WHERE k.conrelid=c.oid
                    AND k.conname='uq_reservation_creation_request' AND k.contype='u')
                AND EXISTS (SELECT 1 FROM pg_constraint k WHERE k.conrelid=c.oid
                    AND k.conname='ck_reservation_creation_quantity' AND k.contype='c')
                AND EXISTS (SELECT 1 FROM pg_trigger t WHERE t.tgrelid=c.oid
                    AND t.tgname='trg_reservation_creation_immutable' AND t.tgenabled='O')
            FROM pg_class c WHERE c.oid='inventory.reservation_creation'::regclass
            """, connection);
        Assert(await command.ExecuteScalarAsync() is true,
            "Reservation creation must enforce RLS, request uniqueness, quantity and immutability while writes remain closed.");
    }

    private static async Task AssertInventoryPositionLockAsync(
        NpgsqlDataSource dataSource, ExecutionScope scope, Guid companyId, Guid itemId, Guid warehouseId)
    {
        InventoryPositionLockTarget[] targets = [new(itemId, warehouseId, InventoryUomCode.Create("EA"))];
        await using var first = await dataSource.OpenConnectionAsync();
        await using var second = await dataSource.OpenConnectionAsync();
        await using var owner = await first.BeginTransactionAsync();
        await PostgresInventoryPositionLock.AcquireAsync(first, owner, scope, companyId, targets);
        await using (var competitor = await second.BeginTransactionAsync())
        {
            await using var timeout = new NpgsqlCommand("SET LOCAL lock_timeout = '100ms'", second, competitor);
            await timeout.ExecuteNonQueryAsync();
            var conflict = await ThrowsAsync<PostgresException>(async () =>
                await PostgresInventoryPositionLock.AcquireAsync(second, competitor, scope, companyId, targets));
            Assert(conflict.SqlState == "55P03", "The same inventory position must wait for its transaction lock.");
            await competitor.RollbackAsync();
        }
        await owner.RollbackAsync();
        await using var retry = await second.BeginTransactionAsync();
        await PostgresInventoryPositionLock.AcquireAsync(second, retry, scope, companyId, targets);
        await retry.RollbackAsync();
    }

    private static async Task<Guid> AssertInventoryQuantityMovementFoundationAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        Guid itemId = Guid.CreateVersion7();
        await AssertReservationCreationSchemaAsync(appDataSource);
        Guid sourceWarehouseId = Guid.CreateVersion7();
        Guid destinationWarehouseId = Guid.CreateVersion7();
        Guid transferId = Guid.CreateVersion7();
        Guid sourceEventId = Guid.CreateVersion7();
        Guid sourceLineId = Guid.CreateVersion7();
        var scope = new ExecutionScope(
            tenantId,
            actorId,
            [new CompanyAccess(
                companyId,
                [
                    AuthorizedImmediateStockTransferCandidate.RequiredPermission,
                    AuthorizedInventoryOnHandQuery.RequiredPermission,
                    AuthorizedInventoryMovementQuery.RequiredPermission,
                ])]);
        StockMovementSourceIdentity source = StockMovementSourceIdentity.Create(
            tenantId,
            companyId,
            "inventory.transfer",
            sourceEventId,
            sourceLineId,
            1,
            "stock-transfer");
        await AssertInventoryPositionLockAsync(appDataSource, scope, companyId, itemId, sourceWarehouseId);
        StockMovementDraft issue = CreateTransferMovement(
            tenantId,
            companyId,
            itemId,
            sourceWarehouseId,
            destinationWarehouseId,
            transferId,
            source,
            Guid.CreateVersion7(),
            1,
            StockMovementKind.TransferIssue,
            -10m);
        StockMovementDraft receipt = CreateTransferMovement(
            tenantId,
            companyId,
            itemId,
            destinationWarehouseId,
            sourceWarehouseId,
            transferId,
            source,
            Guid.CreateVersion7(),
            1,
            StockMovementKind.TransferReceipt,
            10m);
        ValidatedImmediateStockTransferDraft transfer =
            ValidatedImmediateStockTransferDraft.Create(transferId, issue, receipt);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetScopeAsync(connection, transaction, tenantId, companyId);
            const string warehouseSql = """
                INSERT INTO org.warehouse
                    (tenant_id,company_id,warehouse_id,code,name,created_by,updated_by)
                VALUES ($1,$2,$3,$4,$5,$6,$6),($1,$2,$7,$8,$9,$6,$6)
                """;
            await using (var warehouse = new NpgsqlCommand(warehouseSql, connection, transaction))
            {
                warehouse.Parameters.AddWithValue(tenantId);
                warehouse.Parameters.AddWithValue(companyId);
                warehouse.Parameters.AddWithValue(sourceWarehouseId);
                warehouse.Parameters.AddWithValue($"SRC-{sourceWarehouseId:N}");
                warehouse.Parameters.AddWithValue("Source warehouse");
                warehouse.Parameters.AddWithValue(actorId);
                warehouse.Parameters.AddWithValue(destinationWarehouseId);
                warehouse.Parameters.AddWithValue($"DST-{destinationWarehouseId:N}");
                warehouse.Parameters.AddWithValue("Destination warehouse");
                Assert(await warehouse.ExecuteNonQueryAsync() == 2, "Inventory fixture warehouses were not inserted.");
            }

            const string itemSql = """
                INSERT INTO inventory.item
                    (tenant_id,item_id,code,name,kind,base_uom_code,tracking_policy,
                     allows_fractional_quantity,quantity_scale,created_by,updated_by)
                VALUES ($1,$2,$3,$4,1,'EA',1,false,0,$5,$5)
                """;
            await using (var item = new NpgsqlCommand(itemSql, connection, transaction))
            {
                item.Parameters.AddWithValue(tenantId);
                item.Parameters.AddWithValue(itemId);
                item.Parameters.AddWithValue($"ITEM-{itemId:N}");
                item.Parameters.AddWithValue("Inventory integration item");
                item.Parameters.AddWithValue(actorId);
                Assert(await item.ExecuteNonQueryAsync() == 1, "Inventory fixture item was not inserted.");
            }

            const string activationSql = """
                INSERT INTO inventory.item_company
                    (tenant_id,company_id,item_id,created_by,updated_by)
                VALUES ($1,$2,$3,$4,$4)
                """;
            await using (var activation = new NpgsqlCommand(activationSql, connection, transaction))
            {
                activation.Parameters.AddWithValue(tenantId);
                activation.Parameters.AddWithValue(companyId);
                activation.Parameters.AddWithValue(itemId);
                activation.Parameters.AddWithValue(actorId);
                Assert(await activation.ExecuteNonQueryAsync() == 1, "Inventory item company activation was not inserted.");
            }

            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection assignmentConnection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction assignmentTransaction = await assignmentConnection.BeginTransactionAsync())
        {
            const string assignmentSql = """
                INSERT INTO iam.user_warehouse_scope
                    (user_profile_id,tenant_id,company_id,warehouse_id,valid_from,created_by)
                VALUES ($1,$2,$3,$4,clock_timestamp() - interval '1 minute',$1),
                       ($1,$2,$3,$5,clock_timestamp() - interval '1 minute',$1)
                """;
            await using var assignment = new NpgsqlCommand(
                assignmentSql,
                assignmentConnection,
                assignmentTransaction);
            assignment.Parameters.AddWithValue(actorId);
            assignment.Parameters.AddWithValue(tenantId);
            assignment.Parameters.AddWithValue(companyId);
            assignment.Parameters.AddWithValue(sourceWarehouseId);
            assignment.Parameters.AddWithValue(destinationWarehouseId);
            Assert(await assignment.ExecuteNonQueryAsync() == 2,
                "Inventory warehouse assignments were not inserted.");
            await assignmentTransaction.CommitAsync();
        }

        InventoryWarehouseScopeEvidence warehouseScope;
        await AssertReservationZeroResultAsync(migratorDataSource, tenantId, companyId, sourceWarehouseId, actorId);
        await AssertReservationRequestGateAsync(migratorDataSource, appDataSource,
            tenantId, companyId, sourceWarehouseId, actorId, itemId);
        await AssertReservationLifecyclePersistenceAsync(migratorDataSource,
            tenantId, companyId, sourceWarehouseId, actorId, itemId);
        await AssertReservationWriterAsync(migratorDataSource, tenantId, companyId,
            sourceWarehouseId, destinationWarehouseId, actorId, itemId);
        AuthorizedImmediateStockTransferCandidate candidate;
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            var unassignedScope = new ExecutionScope(
                tenantId,
                Guid.CreateVersion7(),
                [new CompanyAccess(companyId, [AuthorizedImmediateStockTransferCandidate.RequiredPermission])]);
            InventoryWarehouseScopeEvidence hiddenWarehouseScope =
                await PostgresInventoryWarehouseScopeLoader.LoadAsync(
                    connection,
                    transaction,
                    unassignedScope,
                    companyId);
            InventoryTransferAuthorizationException? warehouseDenied = null;
            try
            {
                _ = AuthorizedImmediateStockTransferCandidate.Create(
                    unassignedScope,
                    hiddenWarehouseScope,
                    transfer);
            }
            catch (InventoryTransferAuthorizationException exception)
            {
                warehouseDenied = exception;
            }
            Assert(warehouseDenied?.Code == "INVENTORY_TRANSFER_WAREHOUSE_SCOPE_REQUIRED",
                "An actor without authoritative warehouse assignments obtained a transfer candidate.");

            warehouseScope = await PostgresInventoryWarehouseScopeLoader.LoadAsync(
                connection,
                transaction,
                scope,
                companyId);
            candidate = AuthorizedImmediateStockTransferCandidate.Create(scope, warehouseScope, transfer);
            ImmediateStockTransferPersistenceResult created =
                await PostgresImmediateStockTransferWriter.PersistAsync(
                    connection, transaction, candidate);
            ImmediateStockTransferPersistenceResult replay =
                await PostgresImmediateStockTransferWriter.PersistAsync(
                    connection, transaction, candidate);
            Assert(created.Created && !replay.Created && replay.TransferId == transferId,
                "Immediate inventory transfer retry did not return its immutable first result.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection scopedConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction scopedTransaction = await scopedConnection.BeginTransactionAsync())
        {
            await SetScopeAsync(scopedConnection, scopedTransaction, tenantId, companyId);
            Assert(await CountAsync(
                    scopedConnection,
                    scopedTransaction,
                    "SELECT count(*) FROM inventory.stock_movement WHERE transfer_id=$1",
                    transferId) == 2,
                "Immediate inventory transfer did not persist exactly two movements.");

            InventoryWarehouseScopeEvidence queryWarehouseScope =
                await PostgresInventoryWarehouseScopeLoader.LoadAsync(
                    scopedConnection,
                    scopedTransaction,
                    scope,
                    companyId);
            InventoryOnHandSnapshot beforeRecordedCutoff =
                await PostgresInventoryOnHandLoader.LoadAsync(
                    scopedConnection,
                    scopedTransaction,
                    AuthorizedInventoryOnHandQuery.Create(
                        scope,
                        queryWarehouseScope,
                        companyId,
                        new DateOnly(2026, 9, 2),
                        new DateTimeOffset(2026, 9, 2, 11, 59, 59, TimeSpan.Zero)));
            Assert(beforeRecordedCutoff.Lines.Count == 0,
                "Late-recorded inventory movements leaked before their recorded cutoff.");

            InventoryOnHandSnapshot currentOnHand =
                await PostgresInventoryOnHandLoader.LoadAsync(
                    scopedConnection,
                    scopedTransaction,
                    AuthorizedInventoryOnHandQuery.Create(
                        scope,
                        queryWarehouseScope,
                        companyId,
                        new DateOnly(2026, 9, 2),
                        new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero),
                        itemId));
            Assert(currentOnHand.Lines.Count == 2 &&
                   currentOnHand.Lines.Single(line => line.WarehouseId == sourceWarehouseId).OnHand.Value == -10m &&
                   currentOnHand.Lines.Single(line => line.WarehouseId == destinationWarehouseId).OnHand.Value == 10m,
                "Inventory on-hand did not reproduce the exact warehouse transfer quantities.");

            AuthorizedInventoryMovementQuery firstMovementQuery =
                AuthorizedInventoryMovementQuery.Create(
                    scope,
                    queryWarehouseScope,
                    companyId,
                    itemId,
                    new DateOnly(2026, 9, 1),
                    new DateOnly(2026, 9, 2),
                    new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero),
                    1);
            InventoryMovementPage firstMovementPage =
                await PostgresInventoryMovementLoader.LoadAsync(
                    scopedConnection,
                    scopedTransaction,
                    firstMovementQuery);
            Assert(firstMovementPage.Lines.Count == 1 && firstMovementPage.NextCursor is not null,
                "Inventory movement first page did not expose a stable continuation cursor.");
            InventoryMovementPage secondMovementPage =
                await PostgresInventoryMovementLoader.LoadAsync(
                    scopedConnection,
                    scopedTransaction,
                    AuthorizedInventoryMovementQuery.Create(
                        scope,
                        queryWarehouseScope,
                        companyId,
                        itemId,
                        new DateOnly(2026, 9, 1),
                        new DateOnly(2026, 9, 2),
                        new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero),
                        1,
                        firstMovementPage.NextCursor));
            InventoryMovementLine[] movementTimeline =
                [.. firstMovementPage.Lines, .. secondMovementPage.Lines];
            Assert(movementTimeline.Length == 2 && secondMovementPage.NextCursor is null &&
                   movementTimeline.Select(line => line.MovementId).Distinct().Count() == 2 &&
                   movementTimeline.All(line => line.Source == source && line.TransferId == transferId) &&
                   movementTimeline.Sum(line => line.BaseQuantity.Value) == 0m,
                "Inventory movement pagination lost source lineage or transfer conservation.");
            await scopedTransaction.CommitAsync();
        }

        Guid changedTransferId = Guid.CreateVersion7();
        ValidatedImmediateStockTransferDraft changedTransfer = ValidatedImmediateStockTransferDraft.Create(
            changedTransferId,
            CreateTransferMovement(
                tenantId,
                companyId,
                itemId,
                sourceWarehouseId,
                destinationWarehouseId,
                changedTransferId,
                source,
                Guid.CreateVersion7(),
                1,
                StockMovementKind.TransferIssue,
                -10m),
            CreateTransferMovement(
                tenantId,
                companyId,
                itemId,
                destinationWarehouseId,
                sourceWarehouseId,
                changedTransferId,
                source,
                Guid.CreateVersion7(),
                1,
                StockMovementKind.TransferReceipt,
                10m));
        AuthorizedImmediateStockTransferCandidate changedCandidate =
            AuthorizedImmediateStockTransferCandidate.Create(
                scope,
                warehouseScope,
                changedTransfer);
        await using (NpgsqlConnection conflictConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction conflictTransaction = await conflictConnection.BeginTransactionAsync())
        {
            await SetScopeAsync(conflictConnection, conflictTransaction, tenantId, companyId);
            ImmediateStockTransferPersistenceConflictException conflict =
                await ThrowsAsync<ImmediateStockTransferPersistenceConflictException>(() =>
                    PostgresImmediateStockTransferWriter.PersistAsync(
                        conflictConnection,
                        conflictTransaction,
                        changedCandidate).AsTask());
            Assert(conflict.ExistingTransferId == transferId,
                "Changed transfer content did not report the canonical existing transfer.");
            await conflictTransaction.RollbackAsync();
        }

        Guid reversalTransferId = Guid.CreateVersion7();
        StockMovementSourceIdentity reversalSource = StockMovementSourceIdentity.Create(
            tenantId,
            companyId,
            "inventory.transfer-reversal",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            1,
            "stock-transfer-reversal");
        ValidatedImmediateStockTransferDraft reversalTransfer =
            ValidatedImmediateStockTransferDraft.Create(
                reversalTransferId,
                CreateTransferMovement(
                    tenantId,
                    companyId,
                    itemId,
                    destinationWarehouseId,
                    sourceWarehouseId,
                    reversalTransferId,
                    reversalSource,
                    Guid.CreateVersion7(),
                    2,
                    StockMovementKind.TransferIssue,
                    -10m,
                    new DateTimeOffset(2026, 9, 2, 13, 0, 0, TimeSpan.Zero),
                    receipt.MovementId),
                CreateTransferMovement(
                    tenantId,
                    companyId,
                    itemId,
                    sourceWarehouseId,
                    destinationWarehouseId,
                    reversalTransferId,
                    reversalSource,
                    Guid.CreateVersion7(),
                    2,
                    StockMovementKind.TransferReceipt,
                    10m,
                    new DateTimeOffset(2026, 9, 2, 13, 0, 0, TimeSpan.Zero),
                    issue.MovementId));
        Guid invalidReversalTransferId = Guid.CreateVersion7();
        ValidatedImmediateStockTransferDraft invalidReversalTransfer =
            ValidatedImmediateStockTransferDraft.Create(
                invalidReversalTransferId,
                CreateTransferMovement(
                    tenantId,
                    companyId,
                    itemId,
                    destinationWarehouseId,
                    sourceWarehouseId,
                    invalidReversalTransferId,
                    reversalSource,
                    Guid.CreateVersion7(),
                    2,
                    StockMovementKind.TransferIssue,
                    -9m,
                    new DateTimeOffset(2026, 9, 2, 13, 0, 0, TimeSpan.Zero),
                    receipt.MovementId),
                CreateTransferMovement(
                    tenantId,
                    companyId,
                    itemId,
                    sourceWarehouseId,
                    destinationWarehouseId,
                    invalidReversalTransferId,
                    reversalSource,
                    Guid.CreateVersion7(),
                    2,
                    StockMovementKind.TransferReceipt,
                    9m,
                    new DateTimeOffset(2026, 9, 2, 13, 0, 0, TimeSpan.Zero),
                    issue.MovementId));
        await using (NpgsqlConnection invalidReversalConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction invalidReversalTransaction =
                     await invalidReversalConnection.BeginTransactionAsync())
        {
            InventoryWarehouseScopeEvidence invalidReversalWarehouseScope =
                await PostgresInventoryWarehouseScopeLoader.LoadAsync(
                    invalidReversalConnection,
                    invalidReversalTransaction,
                    scope,
                    companyId);
            _ = await PostgresImmediateStockTransferWriter.PersistAsync(
                invalidReversalConnection,
                invalidReversalTransaction,
                AuthorizedImmediateStockTransferCandidate.Create(
                    scope,
                    invalidReversalWarehouseScope,
                    invalidReversalTransfer));
            PostgresException invalidReversalRejected = await ThrowsAsync<PostgresException>(
                () => invalidReversalTransaction.CommitAsync());
            Assert(invalidReversalRejected.SqlState == PostgresErrorCodes.CheckViolation,
                "Stock movement reversal accepted quantities that did not exactly counter the originals.");
        }

        await using (NpgsqlConnection reversalConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction reversalTransaction = await reversalConnection.BeginTransactionAsync())
        {
            InventoryWarehouseScopeEvidence reversalWarehouseScope =
                await PostgresInventoryWarehouseScopeLoader.LoadAsync(
                    reversalConnection,
                    reversalTransaction,
                    scope,
                    companyId);
            ImmediateStockTransferPersistenceResult reversed =
                await PostgresImmediateStockTransferWriter.PersistAsync(
                    reversalConnection,
                    reversalTransaction,
                    AuthorizedImmediateStockTransferCandidate.Create(
                        scope,
                        reversalWarehouseScope,
                        reversalTransfer));
            Assert(reversed.Created, "Inventory transfer reversal was not appended.");
            await reversalTransaction.CommitAsync();
        }

        await using (NpgsqlConnection reversedBalanceConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction reversedBalanceTransaction =
                     await reversedBalanceConnection.BeginTransactionAsync())
        {
            InventoryWarehouseScopeEvidence reversedBalanceScope =
                await PostgresInventoryWarehouseScopeLoader.LoadAsync(
                    reversedBalanceConnection,
                    reversedBalanceTransaction,
                    scope,
                    companyId);
            InventoryOnHandSnapshot reversedOnHand =
                await PostgresInventoryOnHandLoader.LoadAsync(
                    reversedBalanceConnection,
                    reversedBalanceTransaction,
                    AuthorizedInventoryOnHandQuery.Create(
                        scope,
                        reversedBalanceScope,
                        companyId,
                        new DateOnly(2026, 9, 2),
                        new DateTimeOffset(2026, 9, 2, 13, 0, 0, TimeSpan.Zero),
                        itemId));
            Assert(reversedOnHand.Lines.Count == 0,
                "Inventory transfer reversal did not return both warehouse balances to zero.");
            await reversedBalanceTransaction.CommitAsync();
        }

        await using (NpgsqlConnection hiddenConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction hiddenTransaction = await hiddenConnection.BeginTransactionAsync())
        {
            await SetScopeAsync(hiddenConnection, hiddenTransaction, tenantId, otherCompanyId);
            Assert(await CountAsync(
                    hiddenConnection,
                    hiddenTransaction,
                    "SELECT count(*) FROM inventory.stock_movement WHERE transfer_id=$1",
                    transferId) == 0,
                "Inventory movement leaked across company scope.");
            await hiddenTransaction.CommitAsync();
        }

        await using (NpgsqlConnection invalidConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction invalidTransaction = await invalidConnection.BeginTransactionAsync())
        {
            await SetScopeAsync(invalidConnection, invalidTransaction, tenantId, companyId);
            await InsertTransferMovementAsync(
                invalidConnection,
                invalidTransaction,
                tenantId,
                companyId,
                actorId,
                itemId,
                sourceWarehouseId,
                destinationWarehouseId,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                2,
                3,
                -1m);
            PostgresException rejected = await ThrowsAsync<PostgresException>(
                () => invalidTransaction.CommitAsync());
            Assert(rejected.SqlState == PostgresErrorCodes.CheckViolation,
                "Deferred transfer guard accepted an incomplete inventory transfer.");
        }

        await using NpgsqlConnection privilegeConnection = await appDataSource.OpenConnectionAsync();
        await using var privilege = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user,'inventory.stock_movement','SELECT'),has_table_privilege(current_user,'inventory.stock_movement','INSERT'),has_table_privilege(current_user,'inventory.stock_movement','UPDATE'),has_table_privilege(current_user,'inventory.stock_movement','DELETE')",
            privilegeConnection);
        await using NpgsqlDataReader reader = await privilege.ExecuteReaderAsync();
        Assert(await reader.ReadAsync() && reader.GetBoolean(0) && reader.GetBoolean(1) &&
               !reader.GetBoolean(2) && !reader.GetBoolean(3),
            "Runtime stock movement privileges are not append-only.");
        return itemId;
    }

    private static async Task InsertTransferMovementAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        Guid itemId,
        Guid warehouseId,
        Guid counterpartWarehouseId,
        Guid transferId,
        Guid sourceEventId,
        Guid sourceLineId,
        Guid movementId,
        long sequenceKey,
        short movementKind,
        decimal quantity)
    {
        const string sql = """
            INSERT INTO inventory.stock_movement
                (tenant_id,company_id,movement_id,item_id,warehouse_id,base_uom_code,movement_kind,
                 base_quantity,effective_date,recorded_at,recorded_by,sequence_key,source_type,
                 source_event_id,source_line_id,source_version,posting_purpose,transfer_id,
                 counterpart_warehouse_id)
            VALUES ($1,$2,$3,$4,$5,'EA',$6,$7,DATE '2026-09-02',TIMESTAMPTZ '2026-09-02 12:00:00+00',
                    $8,$9,'inventory.transfer',$10,$11,1,'stock-transfer',$12,$13)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(companyId);
        command.Parameters.AddWithValue(movementId);
        command.Parameters.AddWithValue(itemId);
        command.Parameters.AddWithValue(warehouseId);
        command.Parameters.AddWithValue(movementKind);
        command.Parameters.AddWithValue(quantity);
        command.Parameters.AddWithValue(actorId);
        command.Parameters.AddWithValue(sequenceKey);
        command.Parameters.AddWithValue(sourceEventId);
        command.Parameters.AddWithValue(sourceLineId);
        command.Parameters.AddWithValue(transferId);
        command.Parameters.AddWithValue(counterpartWarehouseId);
        Assert(await command.ExecuteNonQueryAsync() == 1, "Inventory transfer movement was not inserted.");
    }

    private static StockMovementDraft CreateTransferMovement(
        Guid tenantId,
        Guid companyId,
        Guid itemId,
        Guid warehouseId,
        Guid counterpartWarehouseId,
        Guid transferId,
        StockMovementSourceIdentity source,
        Guid movementId,
        long sequenceKey,
        StockMovementKind movementKind,
        decimal quantity,
        DateTimeOffset? recordedAt = null,
        Guid? reversalOfMovementId = null) =>
        StockMovementDraft.Create(
            movementId,
            tenantId,
            companyId,
            itemId,
            warehouseId,
            InventoryUomCode.Create("EA"),
            movementKind,
            InventoryQuantity.Create(quantity),
            new DateOnly(2026, 9, 2),
            recordedAt ?? new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero),
            sequenceKey,
            source,
            transferId,
            counterpartWarehouseId,
            reversalOfMovementId);
}
