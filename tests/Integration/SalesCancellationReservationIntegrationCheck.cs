using KaguERP.Bootstrap;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using KaguERP.Modules.Sales.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static partial class DatabaseIntegrationCheck
{
    private static async Task AssertAtomicSalesCancellationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ExecutionScope scope, Guid companyId, Guid orderId, Guid warehouseId, Guid itemId, Guid[] reservations)
    {
        await transaction.SaveAsync("sales_cancellation_fixture");
        Guid correlation = Guid.CreateVersion7();
        var at = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        var date = new DateOnly(2026, 9, 9);
        var command = AuthorizedSalesOrderTransitionCommand.Create(scope, companyId, orderId, 4,
            SalesOrderTransition.Cancel, correlation, at, "customer withdrew order");
        var audit = new RequestAuditContext(Guid.CreateVersion7(), "cancel-fixture", scope.TenantId, scope.ActorId, scope.CompanyIds, null);
        var otherCompany = Guid.CreateVersion7();
        var unrelatedScope = SalesScope(scope.TenantId, otherCompany, scope.ActorId, InventoryReservationReleaseRequest.RequiredPermission);
        Assert((await PostgresInventorySourceReservationLoader.LoadAsync(connection, transaction, unrelatedScope,
            otherCompany, "sales.order", orderId)).Count == 0, "Source discovery leaked another company's reservations.");
        _ = await PostgresInventoryWarehouseScopeLoader.LoadAsync(connection, transaction, scope, companyId);
        async Task AssertUnchanged()
        {
            await using var verify = new NpgsqlCommand("""
                SELECT (SELECT version=4 AND status=4 FROM sales.sales_order WHERE tenant_id=$1 AND company_id=$2 AND order_id=$3),
                    (SELECT count(*) FROM inventory.reservation_lifecycle_event WHERE tenant_id=$1 AND company_id=$2 AND reservation_id=ANY($4)),
                    (SELECT count(*) FROM platform.audit_event WHERE tenant_id=$1 AND correlation_id=$5),
                    (SELECT count(*) FROM sales.sales_order_transition_event WHERE tenant_id=$1 AND company_id=$2 AND order_id=$3 AND correlation_id=$6),
                    (SELECT count(*) FROM sales.stock_order_cancellation_receipt WHERE tenant_id=$1 AND company_id=$2 AND order_id=$3 AND correlation_id=$6)
                """, connection, transaction);
            verify.Parameters.AddWithValue(scope.TenantId);
            verify.Parameters.AddWithValue(companyId);
            verify.Parameters.AddWithValue(orderId);
            verify.Parameters.AddWithValue(reservations);
            verify.Parameters.AddWithValue(audit.CorrelationId);
            verify.Parameters.AddWithValue(correlation);
            await using var reader = await verify.ExecuteReaderAsync();
            Assert(await reader.ReadAsync() && reader.GetBoolean(0) && reader.GetInt64(1) == 0 &&
                reader.GetInt64(2) == 0 && reader.GetInt64(3) == 0 && reader.GetInt64(4) == 0,
                "Failed cancellation changed its order, releases, receipt or audit.");
        }
        var cancelOnly = SalesScope(scope.TenantId, companyId, scope.ActorId, "sales.order.cancel");
        await ThrowsAsync<InventoryReservationAuthorizationException>(async () =>
            await PostgresSalesOrderCancellationOrchestrator.CancelAsync(connection, transaction,
                AuthorizedSalesOrderTransitionCommand.Create(cancelOnly, companyId, orderId, 4,
                    SalesOrderTransition.Cancel, correlation, at, command.Reason), date, audit));
        await AssertUnchanged();
        await ThrowsAsync<InventoryReservationReleaseConflictException>(async () =>
            await PostgresSalesOrderCancellationOrchestrator.CancelAsync(connection, transaction, command, date.AddDays(-1), audit));
        await AssertUnchanged();
        var failure = await ThrowsAsync<PostgresException>(async () =>
            await PostgresSalesOrderCancellationOrchestrator.CancelAsync(connection, transaction, command, date,
                audit with { TraceId = new string('x', 65) }));
        Assert(failure.SqlState == "22001", "Cancellation audit failure did not reach the expected constraint.");
        await AssertUnchanged();
        await transaction.SaveAsync("legacy_cancel_fixture");
        await PostgresSalesOrderLifecycleWriter.TransitionAsync(connection, transaction, command);
        await ThrowsAsync<SalesOrderCancellationReservationConflictException>(async () =>
            await PostgresSalesOrderCancellationOrchestrator.CancelAsync(connection, transaction, command, date, audit));
        await transaction.RollbackAsync("legacy_cancel_fixture");
        await transaction.ReleaseAsync("legacy_cancel_fixture");
        await AssertUnchanged();

        await transaction.SaveAsync("cancel_warehouse_fixture");
        await using (var expire = new NpgsqlCommand("""
            UPDATE iam.user_warehouse_scope SET valid_to=clock_timestamp()-interval '1 second'
            WHERE tenant_id=$1 AND company_id=$2 AND user_profile_id=$3 AND warehouse_id=$4
            """, connection, transaction))
        {
            expire.Parameters.AddWithValue(scope.TenantId);
            expire.Parameters.AddWithValue(companyId);
            expire.Parameters.AddWithValue(scope.ActorId);
            expire.Parameters.AddWithValue(warehouseId);
            await expire.ExecuteNonQueryAsync();
        }
        await ThrowsAsync<InventoryReservationAuthorizationException>(async () =>
            await PostgresSalesOrderCancellationOrchestrator.CancelAsync(connection, transaction, command, date, audit));
        await transaction.RollbackAsync("cancel_warehouse_fixture");
        await transaction.ReleaseAsync("cancel_warehouse_fixture");
        await AssertUnchanged();

        await transaction.SaveAsync("manual_release_before_cancel");
        var manual = await PostgresInventoryReservationReleaseOrchestrator.ReleaseAsync(connection, transaction,
            new InventoryReservationReleaseRequest(scope, companyId, warehouseId, reservations[0], 1,
                Guid.CreateVersion7(), date, "manual release before cancellation"), audit);
        var remainingOnly = await PostgresSalesOrderCancellationOrchestrator.CancelAsync(connection, transaction, command, date, audit);
        Assert(remainingOnly.Releases.Count == 1 && remainingOnly.Releases[0].ReservationId != manual.ReservationId,
            "Cancellation attempted to release an already manually released reservation again.");
        await transaction.RollbackAsync("manual_release_before_cancel");
        await transaction.ReleaseAsync("manual_release_before_cancel");
        await AssertUnchanged();

        var cancelled = await PostgresSalesOrderCancellationOrchestrator.CancelAsync(connection, transaction, command, date, audit);
        Assert(cancelled.Cancellation.Created && cancelled.Cancellation.State.Status == SalesOrderStatus.Cancelled &&
            cancelled.Releases.Count == 2 && cancelled.Releases.Sum(release => release.ReleasedQuantity.Value) == 6m,
            "Cancellation did not release all six reserved units atomically.");
        var replay = await PostgresSalesOrderCancellationOrchestrator.CancelAsync(connection, transaction, command, date, audit);
        Assert(!replay.Cancellation.Created && replay.Releases.SequenceEqual(cancelled.Releases), "Cancellation replay changed release results.");
        await ThrowsAsync<SalesStockCancellationReceiptConflictException>(async () =>
            await PostgresSalesOrderCancellationOrchestrator.CancelAsync(connection, transaction, command, date.AddDays(1), audit));
        await using (var stock = new NpgsqlCommand("""
            SELECT sum(base_quantity) FROM inventory.stock_movement WHERE tenant_id=$1 AND company_id=$2 AND item_id=$3
            """, connection, transaction))
        {
            stock.Parameters.AddWithValue(scope.TenantId);
            stock.Parameters.AddWithValue(companyId);
            stock.Parameters.AddWithValue(itemId);
            Assert((decimal)(await stock.ExecuteScalarAsync())! == 6m, "Cancellation changed physical stock instead of reservation only.");
        }
        Guid emptyOrder = Guid.CreateVersion7();
        var emptyCommitment = SalesOrderCommitment.Create(scope.TenantId, companyId, emptyOrder,
            [SalesOrderLineCommitment.Create(Guid.CreateVersion7(), itemId, "EA", SalesOrderQuantity.Create(1m))]);
        await PostgresSalesOrderLifecycleWriter.CreateDraftAsync(connection, transaction,
            AuthorizedSalesOrderCreateCommand.Create(scope, companyId, emptyOrder, emptyCommitment));
        var emptyCommand = AuthorizedSalesOrderTransitionCommand.Create(scope, companyId, emptyOrder, 1,
            SalesOrderTransition.Cancel, Guid.CreateVersion7(), at, "draft withdrawn");
        await transaction.SaveAsync("empty_legacy_cancellation");
        await PostgresSalesOrderLifecycleWriter.TransitionAsync(connection, transaction, emptyCommand);
        await ThrowsAsync<SalesStockCancellationReceiptConflictException>(async () =>
            await PostgresSalesOrderCancellationOrchestrator.CancelAsync(connection, transaction, emptyCommand, date, audit));
        await transaction.RollbackAsync("empty_legacy_cancellation");
        await transaction.ReleaseAsync("empty_legacy_cancellation");
        var empty = await PostgresSalesOrderCancellationOrchestrator.CancelAsync(connection, transaction, emptyCommand, date, audit);
        Assert(empty.Cancellation.State.Status == SalesOrderStatus.Cancelled && empty.Releases.Count == 0,
            "A draft without reservations should cancel without manufacturing release events.");
        var emptyReplay = await PostgresSalesOrderCancellationOrchestrator.CancelAsync(connection, transaction, emptyCommand, date, audit);
        Assert(!emptyReplay.Cancellation.Created && emptyReplay.Releases.Count == 0,
            "Empty-reservation cancellation was not replayable.");
        await ThrowsAsync<SalesStockCancellationReceiptConflictException>(async () =>
            await PostgresSalesOrderCancellationOrchestrator.CancelAsync(connection, transaction, emptyCommand, date.AddDays(1), audit));
        await transaction.SaveAsync("immutable_cancel_receipt");
        var immutable = await ThrowsAsync<PostgresException>(async () =>
        {
            await using var update = new NpgsqlCommand("""
                UPDATE sales.stock_order_cancellation_receipt SET effective_date=effective_date+1
                WHERE tenant_id=$1 AND company_id=$2 AND order_id=$3
                """, connection, transaction);
            update.Parameters.AddWithValue(scope.TenantId);
            update.Parameters.AddWithValue(companyId);
            update.Parameters.AddWithValue(emptyOrder);
            await update.ExecuteNonQueryAsync();
        });
        Assert(immutable.SqlState == "55000", "Cancellation receipt allowed an in-place date correction.");
        await transaction.RollbackAsync("immutable_cancel_receipt");
        await transaction.ReleaseAsync("immutable_cancel_receipt");
        await transaction.SaveAsync("wrong_transition_receipt");
        var wrongTransition = await ThrowsAsync<PostgresException>(async () =>
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO sales.stock_order_cancellation_receipt
                    (tenant_id,company_id,order_id,correlation_id,effective_date)
                SELECT tenant_id,company_id,order_id,correlation_id,$4
                FROM sales.sales_order_transition_event
                WHERE tenant_id=$1 AND company_id=$2 AND order_id=$3 AND transition=6
                """, connection, transaction);
            insert.Parameters.AddWithValue(scope.TenantId);
            insert.Parameters.AddWithValue(companyId);
            insert.Parameters.AddWithValue(orderId);
            insert.Parameters.AddWithValue(date);
            await insert.ExecuteNonQueryAsync();
        });
        Assert(wrongTransition.SqlState == "23514", "A confirmation event acquired a cancellation receipt.");
        await transaction.RollbackAsync("wrong_transition_receipt");
        await transaction.ReleaseAsync("wrong_transition_receipt");
        await using (var privileges = new NpgsqlCommand("""
            SELECT relrowsecurity AND relforcerowsecurity,
                has_table_privilege('kagu_erp_app','sales.stock_order_cancellation_receipt','SELECT'),
                has_table_privilege('kagu_erp_app','sales.stock_order_cancellation_receipt','INSERT'),
                has_table_privilege('kagu_erp_app','sales.stock_order_cancellation_receipt','UPDATE'),
                has_table_privilege('kagu_erp_app','sales.stock_order_cancellation_receipt','DELETE')
            FROM pg_class WHERE oid='sales.stock_order_cancellation_receipt'::regclass
            """, connection, transaction))
        await using (var reader = await privileges.ExecuteReaderAsync())
            Assert(await reader.ReadAsync() && reader.GetBoolean(0) && reader.GetBoolean(1) &&
                !reader.GetBoolean(2) && !reader.GetBoolean(3) && !reader.GetBoolean(4),
                "Cancellation receipt must retain forced RLS and SELECT-only runtime privileges.");
        await using (var constraints = new NpgsqlCommand("SET CONSTRAINTS ALL IMMEDIATE", connection, transaction))
            await constraints.ExecuteNonQueryAsync();
        await transaction.RollbackAsync("sales_cancellation_fixture");
        await transaction.ReleaseAsync("sales_cancellation_fixture");
        await using var deferred = new NpgsqlCommand("SET CONSTRAINTS ALL DEFERRED", connection, transaction);
        await deferred.ExecuteNonQueryAsync();
    }
}
