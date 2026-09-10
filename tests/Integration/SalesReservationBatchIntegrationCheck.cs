using KaguERP.Bootstrap;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using KaguERP.Modules.Sales.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static partial class DatabaseIntegrationCheck
{
    private static async Task AssertSalesReservationBatchAsync(NpgsqlDataSource owner,
        Guid tenantId, Guid companyId, Guid makerId, Guid approverId, Guid itemId)
    {
        await using var connection = await owner.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var maker = SalesScope(tenantId, companyId, makerId, "sales.order.create", "sales.order.submit",
            "sales.order.confirm", "sales.order.cancel", AuthorizedInventoryReservationCandidate.RequiredPermission,
            InventoryReservationReleaseRequest.RequiredPermission);
        var approver = SalesScope(tenantId, companyId, approverId, "sales.order.approve", "sales.order.confirm");
        Guid orderId = Guid.CreateVersion7();
        Guid firstLine = Guid.CreateVersion7(), secondLine = Guid.CreateVersion7();
        var commitment = SalesOrderCommitment.Create(tenantId, companyId, orderId,
            [SalesOrderLineCommitment.Create(firstLine, itemId, "EA", SalesOrderQuantity.Create(3m)),
             SalesOrderLineCommitment.Create(secondLine, itemId, "EA", SalesOrderQuantity.Create(3m))]);
        await PostgresSalesOrderLifecycleWriter.CreateDraftAsync(connection, transaction,
            AuthorizedSalesOrderCreateCommand.Create(maker, companyId, orderId, commitment));
        var at = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
        await PostgresSalesOrderLifecycleWriter.TransitionAsync(connection, transaction,
            AuthorizedSalesOrderTransitionCommand.Create(maker, companyId, orderId, 1,
                SalesOrderTransition.Submit, Guid.CreateVersion7(), at));
        await PostgresSalesOrderLifecycleWriter.TransitionAsync(connection, transaction,
            AuthorizedSalesOrderTransitionCommand.Create(approver, companyId, orderId, 2,
                SalesOrderTransition.Approve, Guid.CreateVersion7(), at.AddMinutes(1)));
        await AssertAtomicSalesConfirmationAsync(connection, transaction, maker, companyId, orderId,
            itemId, firstLine, secondLine, at.AddMinutes(2));
        await PostgresSalesOrderLifecycleWriter.TransitionAsync(connection, transaction,
            AuthorizedSalesOrderTransitionCommand.Create(approver, companyId, orderId, 3,
                SalesOrderTransition.Confirm, Guid.CreateVersion7(), at.AddMinutes(2)));
        await using var warehouse = new NpgsqlCommand("""
            SELECT warehouse_id FROM iam.user_warehouse_scope
            WHERE tenant_id=$1 AND company_id=$2 AND user_profile_id=$3 ORDER BY warehouse_id LIMIT 1
            """, connection, transaction);
        warehouse.Parameters.AddWithValue(tenantId);
        warehouse.Parameters.AddWithValue(companyId);
        warehouse.Parameters.AddWithValue(makerId);
        Guid warehouseId = (Guid)(await warehouse.ExecuteScalarAsync() ?? throw new InvalidOperationException("Missing batch warehouse."));
        await using (var seed = new NpgsqlCommand("""
            INSERT INTO inventory.stock_movement
                (tenant_id,company_id,movement_id,item_id,warehouse_id,base_uom_code,movement_kind,
                 base_quantity,effective_date,recorded_at,recorded_by,sequence_key,source_type,
                 source_event_id,source_line_id,source_version,posting_purpose)
            VALUES ($1,$2,$6,$3,$4,'EA',1,6,DATE '2026-09-09',clock_timestamp(),$5,1000,'fixture',$6,$6,1,'batch');
            """, connection, transaction))
        {
            foreach (Guid id in new[] {tenantId, companyId, itemId, warehouseId, makerId, Guid.CreateVersion7()})
                seed.Parameters.AddWithValue(id);
            await seed.ExecuteNonQueryAsync();
        }
        InventoryReservationRequest Request(Guid id, Guid line, decimal quantity) => new(maker, companyId, id,
            warehouseId, InventoryDemandSourceIdentity.Create("sales.order", orderId, line, 4),
            InventoryQuantity.Create(quantity), new(2026, 9, 9));
        Guid[] keys = new[] { Guid.CreateVersion7(), Guid.CreateVersion7() }.Order().ToArray();
        var first = Request(keys[0], firstLine, 3m);
        var second = Request(keys[1], secondLine, 3m);
        var audit = new RequestAuditContext(Guid.CreateVersion7(), "batch-fixture", tenantId, makerId,
            new HashSet<Guid> { companyId }, null);
        await ThrowsAsync<InventoryReservationAuthorizationException>(async () =>
            await PostgresSalesOrderReservationBatch.CreateAsync(connection, transaction,
                [first, Request(keys[1], secondLine, 0.5m)], audit));
        await using (var verify = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM inventory.reservation_creation WHERE tenant_id=$1 AND company_id=$2 AND source_id=$3),
                   (SELECT count(*) FROM platform.audit_event WHERE tenant_id=$1 AND correlation_id=$4),
                   (SELECT count(*) FROM inventory.reservation_request_result WHERE tenant_id=$1 AND company_id=$2 AND request_id=ANY($5))
            """, connection, transaction))
        {
            verify.Parameters.AddWithValue(tenantId);
            verify.Parameters.AddWithValue(companyId);
            verify.Parameters.AddWithValue(orderId);
            verify.Parameters.AddWithValue(audit.CorrelationId);
            verify.Parameters.AddWithValue(keys);
            await using var reader = await verify.ExecuteReaderAsync();
            Assert(await reader.ReadAsync() && reader.GetInt64(0) == 0 && reader.GetInt64(1) == 0 && reader.GetInt64(2) == 0,
                "Second-line failure left a first-line reservation, result or audit in the batch.");
        }
        var results = await PostgresSalesOrderReservationBatch.CreateAsync(connection, transaction, [second, first], audit);
        Assert(results.Count == 2 && results[0].RequestId == second.RequestId && results[1].RequestId == first.RequestId &&
            results.All(result => result.ReservedQuantity.Value == 3m),
            "Batch did not preserve caller order and exact capacity across two real Sales lines.");
        var replay = await PostgresSalesOrderReservationBatch.CreateAsync(connection, transaction, [first, second], audit);
        Assert(replay[0] == results[1] && replay[1] == results[0], "Reordered batch replay changed original line results.");
        await ThrowsAsync<ArgumentException>(async () =>
            await PostgresSalesOrderReservationBatch.CreateAsync(connection, transaction, [first, first], audit));
        await AssertAtomicSalesCancellationAsync(connection, transaction, maker, companyId, orderId,
            warehouseId, itemId, results.Select(result => result.ReservationId!.Value).ToArray());
        await using (var constraints = new NpgsqlCommand("SET CONSTRAINTS ALL IMMEDIATE", connection, transaction))
            await constraints.ExecuteNonQueryAsync();
        await transaction.RollbackAsync();
    }
}
