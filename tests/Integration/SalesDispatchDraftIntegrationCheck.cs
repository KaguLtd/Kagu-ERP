using KaguERP.Bootstrap;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using KaguERP.Modules.Sales.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static partial class DatabaseIntegrationCheck
{
    private static async Task AssertSalesDispatchDraftAsync(NpgsqlDataSource owner, NpgsqlDataSource app,
        Guid tenantId, Guid companyId, Guid otherCompanyId, Guid actorId, Guid orderId, Guid lineId)
    {
        var scope = SalesScope(tenantId, companyId, actorId, "dispatch.create", "sales.order.view");
        var audit = new RequestAuditContext(Guid.CreateVersion7(), "dispatch-draft-fixture", tenantId, actorId,
            new HashSet<Guid> { companyId }, null);
        Guid dispatchId = Guid.CreateVersion7();
        SalesDispatchDraftSnapshot expected;
        await using (var connection = await owner.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await using var warehouseQuery = new NpgsqlCommand("""
                SELECT warehouse_id FROM iam.user_warehouse_scope
                WHERE tenant_id=$1 AND company_id=$2 AND user_profile_id=$3 ORDER BY warehouse_id LIMIT 1
                """, connection, transaction);
            warehouseQuery.Parameters.AddWithValue(tenantId);
            warehouseQuery.Parameters.AddWithValue(companyId);
            warehouseQuery.Parameters.AddWithValue(actorId);
            Guid warehouse = (Guid)(await warehouseQuery.ExecuteScalarAsync() ?? throw new InvalidOperationException("Missing draft warehouse."));
            var command = new AuthorizedSalesDispatchDraftCommand(scope, companyId, dispatchId, orderId, 4,
                new(2026, 9, 10), [new(lineId, warehouse, 3m)]);
            async Task AssertEmpty()
            {
                await using var query = new NpgsqlCommand("""
                    SELECT (SELECT count(*) FROM sales.dispatch_draft WHERE dispatch_id=$1),
                        (SELECT count(*) FROM sales.dispatch_draft_line WHERE dispatch_id=$1),
                        (SELECT count(*) FROM platform.audit_event WHERE tenant_id=$2 AND correlation_id=$3)
                    """, connection, transaction);
                query.Parameters.AddWithValue(dispatchId);
                query.Parameters.AddWithValue(tenantId);
                query.Parameters.AddWithValue(audit.CorrelationId);
                await using var reader = await query.ExecuteReaderAsync();
                Assert(await reader.ReadAsync() && reader.GetInt64(0) == 0 && reader.GetInt64(1) == 0 && reader.GetInt64(2) == 0,
                    "Failed dispatch preparation left a draft, line or audit.");
            }
            await ThrowsAsync<ArgumentException>(async () => await PostgresSalesDispatchDraftOrchestrator.CreateAsync(
                connection, transaction, command, audit with { ActorId = Guid.NewGuid() }));
            await ThrowsAsync<SalesOrderAuthorizationException>(async () => await PostgresSalesDispatchDraftOrchestrator.CreateAsync(
                connection, transaction, new(scope, companyId, dispatchId, orderId, 4, command.EffectiveDate,
                    [new(lineId, Guid.NewGuid(), 3m)]), audit));
            await ThrowsAsync<SalesOrderLifecycleException>(async () => await PostgresSalesDispatchDraftOrchestrator.CreateAsync(
                connection, transaction, new(scope, companyId, dispatchId, orderId, 3, command.EffectiveDate, command.Lines), audit));
            await ThrowsAsync<SalesOrderLifecycleException>(async () => await PostgresSalesDispatchDraftOrchestrator.CreateAsync(
                connection, transaction, new(scope, companyId, dispatchId, orderId, 4, command.EffectiveDate,
                    [new(lineId, warehouse, 11m)]), audit));
            await ThrowsAsync<PostgresException>(async () => await PostgresSalesDispatchDraftOrchestrator.CreateAsync(
                connection, transaction, command, audit with { TraceId = new string('x', 65) }));
            await AssertEmpty();

            var result = await PostgresSalesDispatchDraftOrchestrator.CreateAsync(connection, transaction, command, audit);
            expected = result.Draft;
            Assert(result.Created && result.Draft.Lines.Count == 1 && result.Draft.Lines[0].Quantity == 3m &&
                result.Draft.OrderVersion == 4 && result.Draft.CreatedBy == actorId, "Draft lost its authoritative source snapshot.");
            var replay = await PostgresSalesDispatchDraftOrchestrator.CreateAsync(connection, transaction, command, audit);
            Assert(!replay.Created && replay.Draft.RecordedAt == expected.RecordedAt && replay.Draft.Lines.SequenceEqual(expected.Lines),
                "Draft retry changed its immutable content/time.");
            await ThrowsAsync<SalesDispatchDraftConflictException>(async () => await PostgresSalesDispatchDraftOrchestrator.CreateAsync(
                connection, transaction, new(scope, companyId, dispatchId, orderId, 4, command.EffectiveDate.AddDays(1), command.Lines), audit));
            await ThrowsAsync<SalesDispatchDraftConflictException>(async () => await PostgresSalesDispatchDraftOrchestrator.CreateAsync(
                connection, transaction, new(scope, companyId, dispatchId, orderId, 4, command.EffectiveDate,
                    [new(lineId, warehouse, 2m)]), audit));

            await using (var contender = await owner.OpenConnectionAsync())
            await using (var competing = await contender.BeginTransactionAsync())
            {
                await using var timeout = new NpgsqlCommand("SET LOCAL lock_timeout='100ms'", contender, competing);
                await timeout.ExecuteNonQueryAsync();
                var blocked = await ThrowsAsync<PostgresException>(async () =>
                    await PostgresSalesDispatchDraftOrchestrator.CreateAsync(contender, competing, command, audit));
                Assert(blocked.SqlState == "55P03", "Concurrent identical draft did not wait at its transaction key.");
                await competing.RollbackAsync();
            }

            await transaction.SaveAsync("incomplete_dispatch_fixture");
            await using (var incomplete = new NpgsqlCommand("""
                INSERT INTO sales.dispatch_draft
                    (tenant_id,company_id,dispatch_id,order_id,order_version,effective_date,request_fingerprint,line_count,created_by)
                SELECT tenant_id,company_id,$2,order_id,order_version,effective_date,request_fingerprint,line_count,created_by
                FROM sales.dispatch_draft WHERE dispatch_id=$1
                """, connection, transaction))
            {
                incomplete.Parameters.AddWithValue(dispatchId);
                incomplete.Parameters.AddWithValue(Guid.CreateVersion7());
                await incomplete.ExecuteNonQueryAsync();
            }
            var missingLines = await ThrowsAsync<PostgresException>(async () =>
            {
                await using var constraints = new NpgsqlCommand("SET CONSTRAINTS ALL IMMEDIATE", connection, transaction);
                await constraints.ExecuteNonQueryAsync();
            });
            Assert(missingLines.SqlState == "23514", "An incomplete draft could pass the commit constraint.");
            await transaction.RollbackAsync("incomplete_dispatch_fixture");
            await transaction.ReleaseAsync("incomplete_dispatch_fixture");

            await transaction.SaveAsync("immutable_dispatch_fixture");
            var immutable = await ThrowsAsync<PostgresException>(async () =>
            {
                await using var update = new NpgsqlCommand("UPDATE sales.dispatch_draft_line SET base_quantity=2 WHERE dispatch_id=$1", connection, transaction);
                update.Parameters.AddWithValue(dispatchId);
                await update.ExecuteNonQueryAsync();
            });
            Assert(immutable.SqlState == "55000", "A saved draft snapshot was changed in place.");
            await transaction.RollbackAsync("immutable_dispatch_fixture");
            await transaction.ReleaseAsync("immutable_dispatch_fixture");
            await transaction.SaveAsync("revoked_draft_warehouse");
            await using (var revoke = new NpgsqlCommand("""
                UPDATE iam.user_warehouse_scope SET valid_to=clock_timestamp()-interval '1 second'
                WHERE tenant_id=$1 AND company_id=$2 AND user_profile_id=$3 AND warehouse_id=$4
                """, connection, transaction))
            {
                revoke.Parameters.AddWithValue(tenantId);
                revoke.Parameters.AddWithValue(companyId);
                revoke.Parameters.AddWithValue(actorId);
                revoke.Parameters.AddWithValue(warehouse);
                await revoke.ExecuteNonQueryAsync();
            }
            await ThrowsAsync<SalesOrderAuthorizationException>(async () =>
                await PostgresSalesDispatchDraftOrchestrator.LoadAsync(connection, transaction, scope, companyId, dispatchId, audit));
            await ThrowsAsync<SalesOrderAuthorizationException>(async () =>
                await PostgresSalesDispatchDraftOrchestrator.CreateAsync(connection, transaction, command, audit));
            await transaction.RollbackAsync("revoked_draft_warehouse");
            await transaction.ReleaseAsync("revoked_draft_warehouse");
            await transaction.SaveAsync("changed_source_draft_replay");
            var cancelScope = SalesScope(tenantId, companyId, actorId, "sales.order.cancel");
            await PostgresSalesOrderLifecycleWriter.TransitionAsync(connection, transaction,
                AuthorizedSalesOrderTransitionCommand.Create(cancelScope, companyId, orderId, 4,
                    SalesOrderTransition.Cancel, Guid.CreateVersion7(),
                    new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero), "draft replay source fixture"));
            var afterSourceChange = await PostgresSalesDispatchDraftOrchestrator.CreateAsync(connection, transaction, command, audit);
            Assert(!afterSourceChange.Created && afterSourceChange.Draft.RecordedAt == expected.RecordedAt,
                "Retry reinterpreted the original draft after a source transition.");
            await ThrowsAsync<SalesOrderLifecycleException>(async () => await PostgresSalesDispatchDraftOrchestrator.CreateAsync(
                connection, transaction, new(scope, companyId, Guid.CreateVersion7(), orderId, 4, command.EffectiveDate, command.Lines), audit));
            await transaction.RollbackAsync("changed_source_draft_replay");
            await transaction.ReleaseAsync("changed_source_draft_replay");
            await using (var unchangedOrder = new NpgsqlCommand(
                "SELECT version=4 AND status=4 FROM sales.sales_order WHERE tenant_id=$1 AND company_id=$2 AND order_id=$3", connection, transaction))
            {
                unchangedOrder.Parameters.AddWithValue(tenantId);
                unchangedOrder.Parameters.AddWithValue(companyId);
                unchangedOrder.Parameters.AddWithValue(orderId);
                Assert((bool)(await unchangedOrder.ExecuteScalarAsync())!, "A draft advanced fulfilment or changed the order version.");
            }
            await using (var privileges = new NpgsqlCommand("""
                SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='sales' AND c.relname IN ('dispatch_draft','dispatch_draft_line')
                  AND c.relrowsecurity AND c.relforcerowsecurity
                  AND has_table_privilege('kagu_erp_app',c.oid,'SELECT')
                  AND NOT has_table_privilege('kagu_erp_app',c.oid,'INSERT')
                  AND NOT has_table_privilege('kagu_erp_app',c.oid,'UPDATE')
                  AND NOT has_table_privilege('kagu_erp_app',c.oid,'DELETE')
                """, connection, transaction))
                Assert((long)(await privileges.ExecuteScalarAsync())! == 2, "Draft tables lost their forced RLS or read-only runtime gate.");
            await transaction.CommitAsync();
        }

        await using (var connection = await app.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var loaded = await PostgresSalesDispatchDraftOrchestrator.LoadAsync(connection, transaction, scope, companyId, dispatchId, audit);
            Assert(loaded.RecordedAt == expected.RecordedAt && loaded.Lines.SequenceEqual(expected.Lines), "Committed draft did not reload with app credentials.");
            var hiddenScope = SalesScope(tenantId, otherCompanyId, actorId, "dispatch.create", "sales.order.view");
            await ThrowsAsync<SalesDispatchDraftNotFoundException>(async () =>
                await PostgresSalesDispatchDraftOrchestrator.LoadAsync(connection, transaction, hiddenScope, otherCompanyId,
                    dispatchId, audit with { CompanyIds = new HashSet<Guid> { otherCompanyId } }));
            await using var rls = new NpgsqlCommand("SELECT count(*) FROM sales.dispatch_draft WHERE dispatch_id=$1", connection, transaction);
            rls.Parameters.AddWithValue(dispatchId);
            Assert((long)(await rls.ExecuteScalarAsync())! == 0, "Draft RLS exposed another company's source snapshot.");
            await transaction.RollbackAsync();
        }
        await AssertSalesDispatchGatewayAsync(owner, app, tenantId, companyId, otherCompanyId,
            actorId, orderId, lineId, expected.Lines[0].WarehouseId);
        await AssertInventoryCostHistoryAsync(owner, app, tenantId, companyId, otherCompanyId,
            actorId, expected.Lines[0].ItemId, expected.Lines[0].WarehouseId);
    }
}
