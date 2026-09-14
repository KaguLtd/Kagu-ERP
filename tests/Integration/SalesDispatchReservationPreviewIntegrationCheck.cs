using KaguERP.Bootstrap;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Accounting.Domain.Currencies;
using KaguERP.Modules.Accounting.Infrastructure.Persistence;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using KaguERP.Modules.Sales.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static partial class DatabaseIntegrationCheck
{
    private static async Task AssertSalesDispatchReservationPreviewAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, Guid companyId, Guid orderId, Guid itemId,
        Guid firstLine, Guid secondLine, Guid warehouseId)
    {
        const string fixture = "dispatch_preview_fixture";
        await transaction.SaveAsync(fixture);
        var date = new DateOnly(2026, 9, 9);
        var audit = new RequestAuditContext(Guid.CreateVersion7(), "dispatch-preview-fixture", scope.TenantId,
            scope.ActorId, new HashSet<Guid> { companyId }, null);
        Guid dispatchId = Guid.CreateVersion7();
        await PostgresSalesDispatchDraftOrchestrator.CreateAsync(connection, transaction,
            new(scope, companyId, dispatchId, orderId, 4, date,
                [new(firstLine, warehouseId, 3m), new(secondLine, warehouseId, 3m)]), audit);
        async Task<SalesDispatchReservationPreview> Preview() =>
            await PostgresSalesDispatchReservationPreview.LoadAsync(connection, transaction, scope,
                companyId, dispatchId, audit);
        async Task<long> LifecycleCount()
        {
            await using var sql = new NpgsqlCommand("""
                SELECT count(*) FROM inventory.reservation_lifecycle_event WHERE tenant_id=$1 AND company_id=$2
                """, connection, transaction);
            sql.Parameters.AddWithValue(scope.TenantId);
            sql.Parameters.AddWithValue(companyId);
            return (long)(await sql.ExecuteScalarAsync())!;
        }
        async Task<long> AuditCount()
        {
            await using var sql = new NpgsqlCommand(
                "SELECT count(*) FROM platform.audit_event WHERE tenant_id=$1 AND correlation_id=$2", connection, transaction);
            sql.Parameters.AddWithValue(scope.TenantId);
            sql.Parameters.AddWithValue(audit.CorrelationId);
            return (long)(await sql.ExecuteScalarAsync())!;
        }
        long before = await LifecycleCount();
        var result = await Preview();
        Assert(result.Lines.Count == 2 && result.Lines.All(line =>
            line.Plan.ReservedQuantity.Value == 3m && line.Plan.UnreservedQuantity.Value == 0m &&
            line.Plan.Consumptions.Count == 1 && line.Plan.Consumptions[0].ExpectedVersion == 1),
            "Dispatch preview did not match each source line to its own remaining reservation.");
        Guid reservationId = result.Lines.Single(line => line.OrderLineId == firstLine).Plan.Consumptions[0].ReservationId;
        Assert(result.Lines.SelectMany(line => line.Plan.Consumptions).Select(part => part.ReservationId).Distinct().Count() == 2,
            "Dispatch preview borrowed the same reservation for different order lines.");
        Assert(await LifecycleCount() == before, "Preview consumed a reservation.");

        var costScope = SalesScope(scope.TenantId, companyId, scope.ActorId, "dispatch.create", "sales.order.view",
            "inventory.cost.view", "inventory.cost.publish");
        var costAt = result.Draft.RecordedAt;
        var watermark = InventoryValuationWatermark.Create(scope.TenantId, companyId, itemId, warehouseId,
            InventoryPosition.Create(date, 1), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), costAt, new string('d', 64));
        SalesDispatchCostLineContext[] contexts = [new(firstLine, Guid.CreateVersion7(), 2, watermark, "TRY"),
            new(secondLine, Guid.CreateVersion7(), 3, watermark, "TRY")];
        var mapped = PostgresSalesDispatchCostPreview.BuildLines(result.Draft, contexts.Reverse().ToArray(), costAt);
        Assert(mapped.Count == 2 && mapped.All(line => line.Issue.BaseQuantity.Value == -3m &&
            line.Issue.Source.SourceEventId == dispatchId && line.Issue.Source.SourceVersion == 1 &&
            line.Issue.EffectiveDate == date && line.Issue.ItemId == itemId), "Dispatch mapper lost authoritative draft identity or quantity.");
        static void RejectMapping(Action action)
        {
            try { action(); }
            catch (ArgumentException) { return; }
            catch (InventoryInvariantException) { return; }
            throw new InvalidOperationException("Invalid dispatch cost mapping was accepted.");
        }
        RejectMapping(() => PostgresSalesDispatchCostPreview.BuildLines(result.Draft, [contexts[0]], costAt));
        RejectMapping(() => PostgresSalesDispatchCostPreview.BuildLines(result.Draft, [contexts[0], contexts[0]], costAt));
        RejectMapping(() => PostgresSalesDispatchCostPreview.BuildLines(result.Draft,
            [contexts[0], contexts[1] with { MovementId = contexts[0].MovementId }], costAt));
        RejectMapping(() => PostgresSalesDispatchCostPreview.BuildLines(result.Draft,
            [contexts[0], contexts[1] with { SequenceKey = 2 }], costAt));
        RejectMapping(() => PostgresSalesDispatchCostPreview.BuildLines(result.Draft,
            [contexts[0], contexts[1] with { OrderLineId = Guid.NewGuid() }], costAt));
        RejectMapping(() => PostgresSalesDispatchCostPreview.BuildLines(result.Draft, contexts, costAt.AddTicks(-10)));
        RejectMapping(() => PostgresSalesDispatchCostPreview.BuildLines(result.Draft,
            [contexts[0] with { Currency = "try" }, contexts[1]], costAt));
        RejectMapping(() => PostgresSalesDispatchCostPreview.BuildLines(result.Draft,
            [contexts[0] with { Currency = "USD" }, contexts[1]], costAt));
        RejectMapping(() => PostgresSalesDispatchCostPreview.BuildLines(result.Draft with { CompanyId = Guid.NewGuid() }, contexts, costAt));
        long auditsBeforeCost = await AuditCount();
        await ThrowsAsync<InventoryCostHistoryAccessException>(async () =>
            await PostgresSalesDispatchCostPreview.LoadAsync(connection, transaction, scope, companyId, dispatchId,
                contexts, costAt, Guid.CreateVersion7(), 2, audit));
        await ThrowsAsync<InventoryCostHistoryUnavailableException>(async () =>
            await PostgresSalesDispatchCostPreview.LoadAsync(connection, transaction, costScope, companyId, dispatchId,
                contexts, costAt, Guid.CreateVersion7(), 2, audit));
        Assert(await AuditCount() == auditsBeforeCost, "Missing cost retained draft/reservation audits.");
        _ = await PostgresInventoryCostPublicationWriter.WriteAsync(connection, transaction, costScope, Guid.CreateVersion7(),
            InventoryCostHistoryEvidence.Known(watermark, InventoryUomCode.Create("EA"), "TRY", Guid.CreateVersion7(), 12.345m));
        var costPreview = await PostgresSalesDispatchCostPreview.LoadAsync(connection, transaction, costScope,
            companyId, dispatchId, contexts, costAt, Guid.CreateVersion7(), 2, audit);
        Assert(costPreview.Amounts.Count == 2 && costPreview.Amounts.All(line => line.Amount == 37.04m) &&
            costPreview.Reservation.Draft.DispatchId == dispatchId && await LifecycleCount() == before &&
            await AuditCount() == auditsBeforeCost + 5, "Dispatch cost chain lost amount, reservation or atomic audit.");
        long auditAfterCost = await AuditCount();
        await ThrowsAsync<ArgumentException>(async () =>
            await PostgresSalesDispatchCostPreview.LoadAsync(connection, transaction, costScope, companyId, dispatchId,
                [contexts[0]], costAt, Guid.CreateVersion7(), 2, audit));
        Assert(await AuditCount() == auditAfterCost, "Bad line mapping retained earlier successful draft audits.");
        var historyContexts = contexts.Select(line => new SalesDispatchCostHistoryContext(line.OrderLineId,
            line.MovementId, line.Watermark, line.Currency)).ToArray();
        async Task<Guid> AddPolicy(short scale, RoundingMode mode)
        {
            Guid id = Guid.CreateVersion7();
            await using var insert = new NpgsqlCommand("""
                INSERT INTO accounting.rounding_policy_snapshot
                    (tenant_id,company_id,policy_id,version,scale,rounding_mode,created_by)
                VALUES ($1,$2,$3,7,$4,$5,$6)
                """, connection, transaction);
            insert.Parameters.AddWithValue(scope.TenantId);
            insert.Parameters.AddWithValue(companyId);
            insert.Parameters.AddWithValue(id);
            insert.Parameters.AddWithValue(scale);
            insert.Parameters.AddWithValue((short)mode);
            insert.Parameters.AddWithValue(scope.ActorId);
            await insert.ExecuteNonQueryAsync();
            return id;
        }
        Guid policy = await AddPolicy(2, RoundingMode.AwayFromZero);
        var authoritative = await PostgresSalesDispatchRoundingPreview.LoadAsync(connection, transaction, costScope,
            companyId, dispatchId, historyContexts, policy, 7, audit);
        Assert(authoritative.Policy.PolicyId == policy && authoritative.Policy.Version == 7 &&
            authoritative.Policy.Scale == 2 && authoritative.Preview.Amounts.All(line =>
                line.Amount == 37.04m && line.RoundingPolicySnapshotId == policy && line.AmountScale == 2),
            "Dispatch amount lost authoritative policy identity, version or scale.");
        long beforePolicyFailures = await AuditCount();
        await ThrowsAsync<AuthoritativeCurrencyEvidenceException>(async () =>
            await PostgresSalesDispatchRoundingPreview.LoadAsync(connection, transaction, costScope,
                companyId, dispatchId, historyContexts, policy, 6, audit));
        await ThrowsAsync<AuthoritativeCurrencyEvidenceException>(async () =>
            await PostgresSalesDispatchRoundingPreview.LoadAsync(connection, transaction, costScope,
                companyId, dispatchId, historyContexts, Guid.CreateVersion7(), 7, audit));
        Guid differentCompany = Guid.NewGuid();
        await ThrowsAsync<AuthoritativeCurrencyEvidenceException>(async () =>
            await PostgresRoundingPolicySnapshotLoader.LoadAsync(connection, transaction,
                SalesScope(scope.TenantId, differentCompany, scope.ActorId, "inventory.cost.view"), differentCompany, policy, 7));
        foreach (Guid unsupported in new[] { await AddPolicy(4, RoundingMode.AwayFromZero), await AddPolicy(2, RoundingMode.ToEven) })
            await ThrowsAsync<AuthoritativeCurrencyEvidenceException>(async () =>
                await PostgresSalesDispatchRoundingPreview.LoadAsync(connection, transaction, costScope,
                    companyId, dispatchId, historyContexts, unsupported, 7, audit));
        await ThrowsAsync<InventoryCostHistoryAccessException>(async () =>
            await PostgresSalesDispatchRoundingPreview.LoadAsync(connection, transaction, scope,
                companyId, dispatchId, historyContexts, policy, 7, audit));
        await ThrowsAsync<InventoryCostHistoryAccessException>(async () =>
            await PostgresSalesDispatchRoundingPreview.LoadAsync(connection, transaction, costScope,
                companyId, dispatchId, historyContexts, policy, 7, audit with { ActorId = Guid.NewGuid() }));
        Assert(await AuditCount() == beforePolicyFailures, "Policy failure retained partial dispatch audit.");
        var allocated = await PostgresSalesDispatchCostPreview.LoadAllocatedAsync(connection, transaction,
            costScope, companyId, dispatchId, historyContexts, Guid.CreateVersion7(), 2, audit);
        long persistedSequence;
        await using (var maximum = new NpgsqlCommand("""
            SELECT coalesce(max(sequence_key),0) FROM inventory.stock_movement
            WHERE tenant_id=$1 AND company_id=$2 AND item_id=$3 AND warehouse_id=$4 AND effective_date=$5
            """, connection, transaction))
        {
            maximum.Parameters.AddWithValue(scope.TenantId);
            maximum.Parameters.AddWithValue(companyId);
            maximum.Parameters.AddWithValue(itemId);
            maximum.Parameters.AddWithValue(warehouseId);
            maximum.Parameters.AddWithValue(date);
            persistedSequence = (long)(await maximum.ExecuteScalarAsync())!;
        }
        long expectedStart = Math.Max(persistedSequence, watermark.Position.SequenceKey) + 1;
        Assert(allocated.Amounts.Select(line => line.Selection.Issue.SequenceKey).Order().SequenceEqual(new[] { expectedStart, expectedStart + 1 }) &&
            allocated.Amounts.All(line => line.Amount == 37.04m && line.Selection.Issue.RecordedAt >= result.Draft.RecordedAt),
            "Allocated dispatch positions did not follow persisted/floor sequence and DB recording time.");
        var repeatedAllocation = await PostgresSalesDispatchCostPreview.LoadAllocatedAsync(connection, transaction,
            costScope, companyId, dispatchId, historyContexts.Reverse().ToArray(), Guid.CreateVersion7(), 2, audit);
        Assert(allocated.Amounts.Select(line => (line.Selection.Issue.Source.SourceLineId, line.Selection.Issue.SequenceKey))
            .SequenceEqual(repeatedAllocation.Amounts.Select(line => (line.Selection.Issue.Source.SourceLineId, line.Selection.Issue.SequenceKey))),
            "Read-only allocation depended on input ordering or secretly reserved a sequence.");
        var exhaustedMark = InventoryValuationWatermark.Create(scope.TenantId, companyId, itemId, warehouseId,
            InventoryPosition.Create(date, long.MaxValue), watermark.ProjectionGeneration, costAt, new string('e', 64));
        long beforeExhaustion = await AuditCount();
        await ThrowsAsync<InventoryInvariantException>(async () =>
            await PostgresSalesDispatchCostPreview.LoadAllocatedAsync(connection, transaction, costScope,
                companyId, dispatchId, historyContexts.Select(line => line with { Watermark = exhaustedMark }).ToArray(),
                Guid.CreateVersion7(), 2, audit));
        Assert(await AuditCount() == beforeExhaustion, "Sequence exhaustion retained prior draft/reservation audits.");
        await ThrowsAsync<InventoryCostHistoryAccessException>(async () =>
            await PostgresSalesDispatchCostPreview.LoadAllocatedAsync(connection, transaction, scope,
                companyId, dispatchId, historyContexts, Guid.CreateVersion7(), 2, audit));
        await using (var stockCount = new NpgsqlCommand(
            "SELECT count(*) FROM inventory.stock_movement WHERE tenant_id=$1 AND company_id=$2 AND source_event_id=$3",
            connection, transaction))
        {
            stockCount.Parameters.AddWithValue(scope.TenantId);
            stockCount.Parameters.AddWithValue(companyId);
            stockCount.Parameters.AddWithValue(dispatchId);
            Assert((long)(await stockCount.ExecuteScalarAsync())! == 0, "Dispatch cost preview prematurely posted stock.");
        }

        InventoryDispatchReservationLineQuery Query(Guid line, Guid item, Guid warehouse, DateOnly effective) =>
            new(InventoryDemandSourceIdentity.Create("sales.order", orderId, line, 4), item, warehouse,
                InventoryUomCode.Create("EA"), InventoryQuantity.Create(3m), effective);
        await ThrowsAsync<InventoryStockMasterUnavailableException>(async () =>
            await PostgresInventoryDispatchReservationPreview.LoadAsync(connection, transaction, scope, companyId,
                [Query(firstLine, Guid.NewGuid(), warehouseId, date)]));
        await ThrowsAsync<InventoryDispatchPreviewConflictException>(async () =>
            await PostgresInventoryDispatchReservationPreview.LoadAsync(connection, transaction, scope, companyId,
                [Query(firstLine, itemId, warehouseId, date.AddDays(-1))]));
        await ThrowsAsync<InventoryReservationAuthorizationException>(async () =>
            await PostgresInventoryDispatchReservationPreview.LoadAsync(connection, transaction, scope, companyId,
                [Query(firstLine, itemId, Guid.NewGuid(), date)]));
        var missingPermission = SalesScope(scope.TenantId, companyId, scope.ActorId, "sales.order.view");
        await ThrowsAsync<InventoryReservationAuthorizationException>(async () =>
            await PostgresInventoryDispatchReservationPreview.LoadAsync(connection, transaction, missingPermission,
                companyId, [Query(firstLine, itemId, warehouseId, date)]));
        var unrelated = await PostgresInventoryDispatchReservationPreview.LoadAsync(connection, transaction,
            scope, companyId, [Query(Guid.NewGuid(), itemId, warehouseId, date)]);
        Assert(unrelated[0].Plan.UnreservedQuantity.Value == 3m && unrelated[0].Plan.Consumptions.Count == 0,
            "Preview borrowed another source line's reservation.");

        await ThrowsAsync<InventoryStockMasterUnavailableException>(async () =>
            await PostgresInventoryDispatchReservationPreview.LoadAsync(connection, transaction, scope, companyId,
                [new(InventoryDemandSourceIdentity.Create("sales.order", orderId, firstLine, 4), itemId,
                    warehouseId, InventoryUomCode.Create("EA"), InventoryQuantity.Create(0.5m), date)]));
        foreach (string master in new[] { "item", "item_company", "warehouse" })
        {
            await transaction.SaveAsync("preview_inactive_master");
            // Fixed fixture SQL only: never interpolate a caller-provided table or identifier.
            string update = master switch
            {
                "item" => "UPDATE inventory.item SET is_active=false,version=version+1 WHERE tenant_id=$1 AND item_id=$2",
                "item_company" => "UPDATE inventory.item_company SET is_active=false,version=version+1 WHERE tenant_id=$1 AND item_id=$2 AND company_id=$3",
                _ => "UPDATE org.warehouse SET is_active=false,version=version+1 WHERE tenant_id=$1 AND warehouse_id=$2 AND company_id=$3"
            };
            await using (var deactivate = new NpgsqlCommand(update, connection, transaction))
            {
                deactivate.Parameters.AddWithValue(scope.TenantId);
                deactivate.Parameters.AddWithValue(master == "warehouse" ? warehouseId : itemId);
                if (master != "item") deactivate.Parameters.AddWithValue(companyId);
                Assert(await deactivate.ExecuteNonQueryAsync() == 1, "Inactive master fixture did not select one record.");
            }
            // History remains readable; only the current operational preparation is rejected.
            var historical = await PostgresSalesDispatchDraftOrchestrator.LoadAsync(connection, transaction,
                scope, companyId, dispatchId, audit);
            Assert(historical.DispatchId == dispatchId && historical.Lines.Count == 2,
                "Inactive stock master hid the immutable historical draft.");
            long auditBefore = await AuditCount();
            await ThrowsAsync<InventoryStockMasterUnavailableException>(async () => await Preview());
            Assert(await AuditCount() == auditBefore, "Invalid stock master left a partial preparation audit.");
            await transaction.RollbackAsync("preview_inactive_master");
            await transaction.ReleaseAsync("preview_inactive_master");
        }

        await transaction.SaveAsync("preview_release");
        await PostgresInventoryReservationReleaseOrchestrator.ReleaseAsync(connection, transaction,
            new(scope, companyId, warehouseId, reservationId, 1, Guid.CreateVersion7(), date, "Preview fixture release"), audit);
        var released = await Preview();
        Assert(released.Lines.Single(line => line.OrderLineId == firstLine).Plan.UnreservedQuantity.Value == 3m &&
            released.Lines.Single(line => line.OrderLineId == secondLine).Plan.ReservedQuantity.Value == 3m,
            "Released capacity was reused or another order line lost its reservation.");
        await transaction.RollbackAsync("preview_release");
        await transaction.ReleaseAsync("preview_release");

        await transaction.SaveAsync("preview_future");
        await PostgresInventoryReservationReleaseOrchestrator.ReleaseAsync(connection, transaction,
            new(scope, companyId, warehouseId, reservationId, 1, Guid.CreateVersion7(), date.AddDays(1), "Future fixture release"), audit);
        await ThrowsAsync<InventoryDispatchPreviewConflictException>(async () => await Preview());
        await transaction.RollbackAsync("preview_future");
        await transaction.ReleaseAsync("preview_future");

        await transaction.SaveAsync("preview_cancel");
        await PostgresSalesOrderLifecycleWriter.TransitionAsync(connection, transaction,
            AuthorizedSalesOrderTransitionCommand.Create(scope, companyId, orderId, 4,
                SalesOrderTransition.Cancel, Guid.CreateVersion7(), new(2026, 9, 9, 14, 0, 0, TimeSpan.Zero),
                "Dispatch preview cancelled-source fixture"));
        await ThrowsAsync<SalesOrderLifecycleException>(async () => await Preview());
        await transaction.RollbackAsync("preview_cancel");
        await transaction.ReleaseAsync("preview_cancel");

        var failedAudit = audit with { CorrelationId = Guid.CreateVersion7(), TraceId = new string('x', 65) };
        await ThrowsAsync<PostgresException>(async () => await PostgresSalesDispatchReservationPreview.LoadAsync(
            connection, transaction, scope, companyId, dispatchId, failedAudit));
        await using (var check = new NpgsqlCommand("SELECT count(*) FROM platform.audit_event WHERE tenant_id=$1 AND correlation_id=$2",
            connection, transaction))
        {
            check.Parameters.AddWithValue(scope.TenantId);
            check.Parameters.AddWithValue(failedAudit.CorrelationId);
            Assert((long)(await check.ExecuteScalarAsync())! == 0, "Failed preview retained partial audit.");
        }
        Assert(await LifecycleCount() == before, "Read-only preview scenarios changed reservation lifecycle.");
        await using (var constraints = new NpgsqlCommand("SET CONSTRAINTS ALL IMMEDIATE", connection, transaction))
            await constraints.ExecuteNonQueryAsync();
        await transaction.RollbackAsync(fixture);
        await transaction.ReleaseAsync(fixture);
    }
}
