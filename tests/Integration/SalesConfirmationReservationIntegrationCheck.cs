using KaguERP.Bootstrap;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using KaguERP.Modules.Sales.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static partial class DatabaseIntegrationCheck
{
    private static async Task AssertAtomicSalesConfirmationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ExecutionScope scope, Guid companyId, Guid orderId, Guid itemId, Guid firstLine, Guid secondLine, DateTimeOffset at)
    {
        await transaction.SaveAsync("atomic_confirmation_fixture");
        await using var warehouse = new NpgsqlCommand("""
            SELECT warehouse_id FROM iam.user_warehouse_scope
            WHERE tenant_id=$1 AND company_id=$2 AND user_profile_id=$3 ORDER BY warehouse_id LIMIT 1
            """, connection, transaction);
        warehouse.Parameters.AddWithValue(scope.TenantId);
        warehouse.Parameters.AddWithValue(companyId);
        warehouse.Parameters.AddWithValue(scope.ActorId);
        Guid warehouseId = (Guid)(await warehouse.ExecuteScalarAsync() ?? throw new InvalidOperationException("Missing confirmation warehouse."));
        await using (var seed = new NpgsqlCommand("""
            INSERT INTO inventory.stock_movement
                (tenant_id,company_id,movement_id,item_id,warehouse_id,base_uom_code,movement_kind,
                 base_quantity,effective_date,recorded_at,recorded_by,sequence_key,source_type,
                 source_event_id,source_line_id,source_version,posting_purpose)
            VALUES ($1,$2,$6,$3,$4,'EA',1,3,DATE '2026-09-09',clock_timestamp(),$5,999,'fixture',$6,$6,1,'confirm');
            """, connection, transaction))
        {
            foreach (Guid id in new[] {scope.TenantId, companyId, itemId, warehouseId, scope.ActorId, Guid.CreateVersion7()})
                seed.Parameters.AddWithValue(id);
            await seed.ExecuteNonQueryAsync();
        }
        Guid correlation = Guid.CreateVersion7();
        var command = AuthorizedSalesOrderTransitionCommand.Create(scope, companyId, orderId, 3,
            SalesOrderTransition.Confirm, correlation, at);
        InventoryReservationRequest Request(Guid line, decimal quantity) => new(scope, companyId,
            PostgresSalesOrderConfirmationOrchestrator.ReservationRequestId(correlation, line), warehouseId,
            InventoryDemandSourceIdentity.Create("sales.order", orderId, line, 4),
            InventoryQuantity.Create(quantity), new(2026, 9, 9));
        var requests = new[] { Request(firstLine, 3m), Request(secondLine, 3m) }.OrderBy(r => r.RequestId).ToArray();
        var audit = new RequestAuditContext(Guid.CreateVersion7(), "confirm-fixture", scope.TenantId, scope.ActorId,
            scope.CompanyIds, null);
        async Task AssertRolledBack()
        {
            await using var verify = new NpgsqlCommand("""
                SELECT (SELECT version=3 AND status=3 FROM sales.sales_order WHERE tenant_id=$1 AND company_id=$2 AND order_id=$3),
                    (SELECT count(*) FROM sales.sales_order_transition_event WHERE tenant_id=$1 AND company_id=$2 AND order_id=$3 AND correlation_id=$4),
                    (SELECT count(*) FROM inventory.reservation_request_result WHERE tenant_id=$1 AND company_id=$2 AND request_id=ANY($5)),
                    (SELECT count(*) FROM platform.audit_event WHERE tenant_id=$1 AND correlation_id=$6)
                """, connection, transaction);
            verify.Parameters.AddWithValue(scope.TenantId);
            verify.Parameters.AddWithValue(companyId);
            verify.Parameters.AddWithValue(orderId);
            verify.Parameters.AddWithValue(correlation);
            verify.Parameters.AddWithValue(requests.Select(r => r.RequestId).ToArray());
            verify.Parameters.AddWithValue(audit.CorrelationId);
            await using var reader = await verify.ExecuteReaderAsync();
            Assert(await reader.ReadAsync() && reader.GetBoolean(0) && reader.GetInt64(1) == 0 &&
                reader.GetInt64(2) == 0 && reader.GetInt64(3) == 0,
                "Failed confirmation left a confirmed header/event, reservation result or audit behind.");
        }
        await transaction.SaveAsync("legacy_confirmation_fixture");
        await PostgresSalesOrderLifecycleWriter.TransitionAsync(connection, transaction, command);
        await ThrowsAsync<SalesOrderConfirmationReservationConflictException>(async () =>
            await PostgresSalesOrderConfirmationOrchestrator.ConfirmAsync(connection, transaction, command, requests, audit));
        await transaction.RollbackAsync("legacy_confirmation_fixture");
        await transaction.ReleaseAsync("legacy_confirmation_fixture");
        await AssertRolledBack();
        var unstable = new InventoryReservationRequest(scope, companyId, Guid.CreateVersion7(), warehouseId,
            requests[0].Source, requests[0].RequestedQuantity, requests[0].EffectiveDate);
        await ThrowsAsync<ArgumentException>(async () =>
            await PostgresSalesOrderConfirmationOrchestrator.ConfirmAsync(connection, transaction, command,
                [unstable, requests[1]], audit));
        await AssertRolledBack();
        await ThrowsAsync<ArgumentException>(async () =>
            await PostgresSalesOrderConfirmationOrchestrator.ConfirmAsync(connection, transaction, command, [requests[0]], audit));
        await AssertRolledBack();
        await ThrowsAsync<InventoryReservationAuthorizationException>(async () =>
            await PostgresSalesOrderConfirmationOrchestrator.ConfirmAsync(connection, transaction, command,
                [requests[0], Request(requests[1].Source.SourceLineId, 0.5m)], audit));
        await AssertRolledBack();
        var auditFailure = await ThrowsAsync<PostgresException>(async () =>
            await PostgresSalesOrderConfirmationOrchestrator.ConfirmAsync(connection, transaction, command,
                requests, audit with { TraceId = new string('x', 65) }));
        Assert(auditFailure.SqlState == "22001", "Confirmation audit failure fixture did not reach its DB constraint.");
        await AssertRolledBack();
        var result = await PostgresSalesOrderConfirmationOrchestrator.ConfirmAsync(connection, transaction, command, requests, audit);
        Assert(result.Confirmation.Created && result.Confirmation.State.Status == SalesOrderStatus.Confirmed &&
            result.Reservations.Count == 2 && result.Reservations.Sum(r => r.ReservedQuantity.Value) == 3m &&
            result.Reservations.Count(r => r.ReservationId is null) == 1,
            "Confirmation did not atomically reserve partial stock and retain a zero-result line.");
        var replay = await PostgresSalesOrderConfirmationOrchestrator.ConfirmAsync(connection, transaction, command,
            requests.Reverse().ToArray(), audit);
        Assert(!replay.Confirmation.Created && replay.Reservations[0] == result.Reservations[1] &&
            replay.Reservations[1] == result.Reservations[0], "Confirmation replay changed its immutable line results.");
        await ThrowsAsync<InventoryReservationRequestConflictException>(async () =>
            await PostgresSalesOrderConfirmationOrchestrator.ConfirmAsync(connection, transaction, command,
                [Request(requests[0].Source.SourceLineId, 2m), requests[1]], audit));
        await using (var constraints = new NpgsqlCommand("SET CONSTRAINTS ALL IMMEDIATE", connection, transaction))
            await constraints.ExecuteNonQueryAsync();
        await transaction.RollbackAsync("atomic_confirmation_fixture");
        await transaction.ReleaseAsync("atomic_confirmation_fixture");
        // Keep the surrounding fixture's original deferred-constraint mode explicit.
        await using var deferred = new NpgsqlCommand("SET CONSTRAINTS ALL DEFERRED", connection, transaction);
        await deferred.ExecuteNonQueryAsync();
    }
}
