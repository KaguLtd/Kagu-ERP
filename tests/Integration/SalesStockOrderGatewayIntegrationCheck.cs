using KaguERP.Bootstrap;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using KaguERP.Modules.Sales.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static partial class DatabaseIntegrationCheck
{
    private static async Task AssertSalesStockOrderGatewayAsync(NpgsqlDataSource owner, NpgsqlDataSource app,
        Guid tenantId, Guid companyId, Guid makerId, Guid approverId, Guid itemId)
    {
        var maker = SalesScope(tenantId, companyId, makerId, "sales.order.create", "sales.order.submit",
            "sales.order.confirm", "sales.order.cancel", "inventory.reservation.create", "inventory.reservation.release");
        var approver = SalesScope(tenantId, companyId, approverId, "sales.order.approve");
        Guid order = Guid.CreateVersion7(), line = Guid.CreateVersion7(), warehouse;
        var at = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        await using (var connection = await owner.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var commitment = SalesOrderCommitment.Create(tenantId, companyId, order,
                [SalesOrderLineCommitment.Create(line, itemId, "EA", SalesOrderQuantity.Create(3m))]);
            await PostgresSalesOrderLifecycleWriter.CreateDraftAsync(connection, transaction,
                AuthorizedSalesOrderCreateCommand.Create(maker, companyId, order, commitment));
            await PostgresSalesOrderLifecycleWriter.TransitionAsync(connection, transaction,
                AuthorizedSalesOrderTransitionCommand.Create(maker, companyId, order, 1,
                    SalesOrderTransition.Submit, Guid.CreateVersion7(), at));
            await PostgresSalesOrderLifecycleWriter.TransitionAsync(connection, transaction,
                AuthorizedSalesOrderTransitionCommand.Create(approver, companyId, order, 2,
                    SalesOrderTransition.Approve, Guid.CreateVersion7(), at.AddMinutes(1)));
            await using var query = new NpgsqlCommand("""
                SELECT warehouse_id FROM iam.user_warehouse_scope
                WHERE tenant_id=$1 AND company_id=$2 AND user_profile_id=$3 ORDER BY warehouse_id LIMIT 1
                """, connection, transaction);
            query.Parameters.AddWithValue(tenantId);
            query.Parameters.AddWithValue(companyId);
            query.Parameters.AddWithValue(makerId);
            warehouse = (Guid)(await query.ExecuteScalarAsync() ?? throw new InvalidOperationException("Missing gateway warehouse."));
            await transaction.CommitAsync();
        }

        // Deliberately committed fixture in the disposable integration database: the gateway owns
        // its connection and transaction, so an ambient savepoint cannot prove its commit boundary.
        var transition = AuthorizedSalesOrderTransitionCommand.Create(maker, companyId, order, 3,
            SalesOrderTransition.Confirm, Guid.CreateVersion7(), at.AddMinutes(2));
        var command = new SalesStockOrderConfirmationCommand(transition, new(2026, 9, 10),
            [new(line, warehouse, 3m)]);
        var audit = new RequestAuditContext(Guid.CreateVersion7(), "stock-gateway", tenantId, makerId,
            new HashSet<Guid> { companyId }, null);
        var gateway = new PostgresSalesStockOrderGateway(owner);
        await ThrowsAsync<SalesStockOrderAccessException>(async () =>
            await gateway.ConfirmAsync(command, audit with { ActorId = Guid.CreateVersion7() }));
        var noInventory = SalesScope(tenantId, companyId, makerId, "sales.order.confirm");
        await ThrowsAsync<SalesStockOrderAccessException>(async () => await gateway.ConfirmAsync(
            new(AuthorizedSalesOrderTransitionCommand.Create(noInventory, companyId, order, 3,
                SalesOrderTransition.Confirm, transition.CorrelationId, transition.OccurredAt), command.EffectiveDate,
                command.Lines), audit));

        // Runtime write grants remain closed. Supplying application credentials must not fall back to owner.
        await ThrowsAsync<SalesStockOrderUnavailableException>(async () =>
            await new PostgresSalesStockOrderGateway(app).ConfirmAsync(command, audit));
        await AssertGatewayOrderVersionAsync(owner, tenantId, companyId, order, 3);
        await AssertGatewayFailedConfirmationEmptyAsync(owner, tenantId, companyId,
            PostgresSalesOrderConfirmationOrchestrator.ReservationRequestId(transition.CorrelationId, line), audit.CorrelationId);
        await ThrowsAsync<SalesStockOrderUnavailableException>(async () =>
            await gateway.ConfirmAsync(command, audit with { TraceId = new string('x', 65) }));
        await AssertGatewayOrderVersionAsync(owner, tenantId, companyId, order, 3);
        await AssertGatewayFailedConfirmationEmptyAsync(owner, tenantId, companyId,
            PostgresSalesOrderConfirmationOrchestrator.ReservationRequestId(transition.CorrelationId, line), audit.CorrelationId);

        var confirmed = await gateway.ConfirmAsync(command, audit);
        Assert(confirmed.Order.Created && confirmed.Order.State.Version == 4 &&
            confirmed.Reservations.Count == 1 && confirmed.Reservations[0].OrderLineId == line &&
            confirmed.Reservations[0].WarehouseId == warehouse && confirmed.Reservations[0].RequestedQuantity == 3m,
            "Gateway lost committed confirmation or line mapping.");
        await AssertGatewayOrderVersionAsync(owner, tenantId, companyId, order, 4);
        var replay = await gateway.ConfirmAsync(command, audit);
        Assert(!replay.Order.Created && replay.Reservations.SequenceEqual(confirmed.Reservations),
            "Gateway retry changed immutable reservation results.");
        await ThrowsAsync<SalesOrderGatewayConflictException>(async () => await gateway.ConfirmAsync(
            new(transition, command.EffectiveDate, [new(line, warehouse, 2m)]), audit));

        var cancel = new SalesStockOrderCancellationCommand(AuthorizedSalesOrderTransitionCommand.Create(
            maker, companyId, order, 4, SalesOrderTransition.Cancel, Guid.CreateVersion7(), at.AddMinutes(3),
            "Gateway fixture cancellation"), command.EffectiveDate);
        await ThrowsAsync<SalesStockOrderUnavailableException>(async () =>
            await gateway.CancelAsync(cancel, audit with { TraceId = new string('x', 65) }));
        await AssertGatewayOrderVersionAsync(owner, tenantId, companyId, order, 4);
        var cancelled = await gateway.CancelAsync(cancel, audit);
        Assert(cancelled.Order.Created && cancelled.Order.State.Status == SalesOrderStatus.Cancelled,
            "Gateway cancellation was not committed.");
        await AssertGatewayOrderVersionAsync(owner, tenantId, companyId, order, 5);
        var cancelledReplay = await gateway.CancelAsync(cancel, audit);
        Assert(!cancelledReplay.Order.Created && cancelledReplay.Releases.SequenceEqual(cancelled.Releases),
            "Gateway cancellation retry changed release results.");
        await ThrowsAsync<SalesOrderGatewayConflictException>(async () =>
            await gateway.CancelAsync(new(cancel.Transition, command.EffectiveDate.AddDays(1)), audit));
        await using (var receiptConnection = await app.OpenConnectionAsync())
        await using (var receiptTransaction = await receiptConnection.BeginTransactionAsync())
        {
            foreach (var (selectedTenant, selectedCompany, expectedCount) in new[]
            {
                (tenantId, companyId, 1L), (tenantId, Guid.NewGuid(), 0L), (Guid.NewGuid(), companyId, 0L),
            })
            {
                await using var context = new NpgsqlCommand("""
                    SELECT set_config('app.tenant_id',$1,true),set_config('app.company_ids',$2,true),set_config('app.actor_id',$3,true)
                    """, receiptConnection, receiptTransaction);
                context.Parameters.AddWithValue(selectedTenant.ToString("D"));
                context.Parameters.AddWithValue("{" + selectedCompany.ToString("D") + "}");
                context.Parameters.AddWithValue(makerId.ToString("D"));
                await context.ExecuteNonQueryAsync();
                // Omit application tenant/company predicates intentionally to exercise the RLS defense.
                await using var query = new NpgsqlCommand(
                    "SELECT count(*) FROM sales.stock_order_cancellation_receipt WHERE order_id=$1",
                    receiptConnection, receiptTransaction);
                query.Parameters.AddWithValue(order);
                Assert((long)(await query.ExecuteScalarAsync())! == expectedCount,
                    "Cancellation receipt RLS failed the exact, other-company or other-tenant scope.");
            }
            await receiptTransaction.RollbackAsync();
        }

        Assert(PostgresSalesStockOrderGateway.MapFailure(new OperationCanceledException()) is null,
            "Cancellation must retain its original semantics.");
        var sqlFailure = PostgresSalesStockOrderGateway.MapFailure(new NpgsqlException("sensitive SQL detail"));
        Assert(sqlFailure is SalesStockOrderUnavailableException && sqlFailure.InnerException is null &&
            !sqlFailure.Message.Contains("sensitive", StringComparison.Ordinal), "SQL details escaped the gateway.");
    }

    private static async Task AssertGatewayOrderVersionAsync(NpgsqlDataSource owner, Guid tenant, Guid company,
        Guid order, long expected)
    {
        await using var connection = await owner.OpenConnectionAsync();
        await using var query = new NpgsqlCommand(
            "SELECT version FROM sales.sales_order WHERE tenant_id=$1 AND company_id=$2 AND order_id=$3", connection);
        query.Parameters.AddWithValue(tenant);
        query.Parameters.AddWithValue(company);
        query.Parameters.AddWithValue(order);
        Assert(await query.ExecuteScalarAsync() is long version && version == expected,
            "An independent connection did not observe the expected committed/rolled-back order version.");
    }

    private static async Task AssertGatewayFailedConfirmationEmptyAsync(NpgsqlDataSource owner, Guid tenant,
        Guid company, Guid request, Guid auditCorrelation)
    {
        await using var connection = await owner.OpenConnectionAsync();
        await using var query = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM inventory.reservation_request_result
                    WHERE tenant_id=$1 AND company_id=$2 AND request_id=$3),
                   (SELECT count(*) FROM inventory.reservation_creation
                    WHERE tenant_id=$1 AND company_id=$2 AND request_id=$3),
                   (SELECT count(*) FROM platform.audit_event WHERE tenant_id=$1 AND correlation_id=$4)
            """, connection);
        query.Parameters.AddWithValue(tenant);
        query.Parameters.AddWithValue(company);
        query.Parameters.AddWithValue(request);
        query.Parameters.AddWithValue(auditCorrelation);
        await using var reader = await query.ExecuteReaderAsync();
        Assert(await reader.ReadAsync() && reader.GetInt64(0) == 0 && reader.GetInt64(1) == 0 && reader.GetInt64(2) == 0,
            "Failed gateway confirmation committed an orphan reservation, result or audit.");
    }
}
