using KaguERP.Bootstrap;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Sales.Application.Orders;
using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static partial class DatabaseIntegrationCheck
{
    private static async Task AssertSalesDispatchGatewayAsync(NpgsqlDataSource owner, NpgsqlDataSource app,
        Guid tenantId, Guid companyId, Guid otherCompanyId, Guid actorId, Guid orderId, Guid lineId, Guid warehouseId)
    {
        Assert(PostgresSalesDispatchDraftGateway.MapFailure(new OperationCanceledException()) is null,
            "Dispatch gateway must preserve cancellation semantics.");
        var sanitized = PostgresSalesDispatchDraftGateway.MapFailure(new NpgsqlException("sensitive SQL detail"));
        Assert(sanitized is SalesDispatchUnavailableException && sanitized.InnerException is null &&
            !sanitized.Message.Contains("sensitive", StringComparison.Ordinal), "Gateway exposed database error detail.");
        var conflict = PostgresSalesDispatchDraftGateway.MapFailure(new InventoryStockMasterUnavailableException());
        Assert(conflict is SalesDispatchPreparationConflictException { Code: "INVENTORY_STOCK_MASTER_UNAVAILABLE" },
            "Stock readiness conflict lost its safe machine code.");
        var scope = SalesScope(tenantId, companyId, actorId, "dispatch.create", "sales.order.view");
        var gateway = new PostgresSalesDispatchDraftGateway(owner);
        var readGateway = new PostgresSalesDispatchDraftGateway(app);
        Guid draftId = Guid.CreateVersion7();
        var command = new AuthorizedSalesDispatchDraftCommand(scope, companyId, draftId, orderId, 4,
            new(2026, 9, 10), [new(lineId, warehouseId, 3m)]);
        var query = new AuthorizedSalesDispatchDraftQuery(scope, companyId, draftId);
        var audit = new RequestAuditContext(Guid.CreateVersion7(), "dispatch-gateway-fixture", tenantId, actorId,
            new HashSet<Guid> { companyId }, null);
        async Task AssertAbsent()
        {
            await using var connection = await owner.OpenConnectionAsync();
            await using var sql = new NpgsqlCommand("""
                SELECT (SELECT count(*) FROM sales.dispatch_draft WHERE tenant_id=$1 AND company_id=$2 AND dispatch_id=$3),
                       (SELECT count(*) FROM sales.dispatch_draft_line WHERE tenant_id=$1 AND company_id=$2 AND dispatch_id=$3),
                       (SELECT count(*) FROM platform.audit_event WHERE tenant_id=$1 AND correlation_id=$4)
                """, connection);
            foreach (Guid id in new[] { tenantId, companyId, draftId, audit.CorrelationId }) sql.Parameters.AddWithValue(id);
            await using var reader = await sql.ExecuteReaderAsync();
            Assert(await reader.ReadAsync() && reader.GetInt64(0) == 0 && reader.GetInt64(1) == 0 && reader.GetInt64(2) == 0,
                "Failed gateway call committed a draft, line or audit.");
        }
        await ThrowsAsync<SalesDispatchAccessException>(async () =>
            await gateway.CreateAsync(command, audit with { ActorId = Guid.NewGuid() }));
        await ThrowsAsync<SalesDispatchAccessException>(async () =>
            await gateway.LoadAsync(query, audit with { TenantId = Guid.NewGuid() }));
        await ThrowsAsync<SalesDispatchAccessException>(async () =>
            await gateway.PreviewAsync(query, audit with { CompanyIds = new HashSet<Guid> { otherCompanyId } }));
        await ThrowsAsync<SalesDispatchUnavailableException>(async () => await readGateway.CreateAsync(command, audit));
        await AssertAbsent();
        await ThrowsAsync<SalesDispatchUnavailableException>(async () =>
            await gateway.CreateAsync(command, audit with { TraceId = new string('x', 65) }));
        await AssertAbsent();
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            await ThrowsAsync<OperationCanceledException>(async () => await gateway.CreateAsync(command, audit, cancelled.Token));
        }
        await AssertAbsent();

        // Creation succeeds inside the transaction, then current item precision rejects preview.
        // The gateway must roll back BOTH operations, including the earlier creation audit.
        await ThrowsAsync<SalesDispatchPreparationConflictException>(async () => await gateway.PrepareAsync(
            new(scope, companyId, draftId, orderId, 4, command.EffectiveDate, [new(lineId, warehouseId, 0.5m)]), audit));
        await AssertAbsent();

        var prepared = await gateway.PrepareAsync(command, audit);
        var created = new SalesDispatchDraftOutcome(prepared.Preview.Draft, prepared.Created);
        Assert(created.Created && created.Draft.DispatchId == draftId && created.Draft.Lines.Single().Quantity == 3m,
            "Gateway did not return the committed immutable draft.");
        var loaded = await readGateway.LoadAsync(query, audit);
        Assert(loaded.RecordedAt == created.Draft.RecordedAt && loaded.Lines.SequenceEqual(created.Draft.Lines),
            "Independent application connection could not read the committed result.");
        var replay = await gateway.CreateAsync(command, audit);
        Assert(!replay.Created && replay.Draft.RecordedAt == created.Draft.RecordedAt,
            "Gateway replay replaced the original draft.");
        var repeatedPreparation = await gateway.PrepareAsync(command, audit);
        Assert(!repeatedPreparation.Created && repeatedPreparation.Preview.Draft.RecordedAt == created.Draft.RecordedAt &&
            repeatedPreparation.Preview.Lines.Single().RequestedQuantity == 3m,
            "Atomic preparation replay replaced the original draft or lost current preview.");
        // Gateway calls own separate transactions, so this isolated test master change must commit.
        // Restore active state in finally; never update or delete the immutable draft to simulate failure.
        async Task SetItemCompanyActive(bool active)
        {
            await using var connection = await owner.OpenConnectionAsync();
            await using var update = new NpgsqlCommand("""
                UPDATE inventory.item_company SET is_active=$4,version=version+1
                WHERE tenant_id=$1 AND company_id=$2 AND item_id=$3
                """, connection);
            update.Parameters.AddWithValue(tenantId);
            update.Parameters.AddWithValue(companyId);
            update.Parameters.AddWithValue(created.Draft.Lines[0].ItemId);
            update.Parameters.AddWithValue(active);
            Assert(await update.ExecuteNonQueryAsync() == 1, "Gateway master fixture did not update one item activation.");
        }
        var failedPreparationAudit = audit with { CorrelationId = Guid.CreateVersion7() };
        await SetItemCompanyActive(false);
        try
        {
            var original = await gateway.CreateAsync(command, audit);
            Assert(!original.Created && original.Draft.RecordedAt == created.Draft.RecordedAt,
                "Historical replay depended on current item activation.");
            await ThrowsAsync<SalesDispatchPreparationConflictException>(async () =>
                await gateway.PrepareAsync(command, failedPreparationAudit));
            var preserved = await readGateway.LoadAsync(query, audit);
            Assert(preserved.RecordedAt == created.Draft.RecordedAt && preserved.Lines.SequenceEqual(created.Draft.Lines),
                "Failed repeated preparation changed or removed the existing draft.");
            await using var connection = await owner.OpenConnectionAsync();
            await using var auditCount = new NpgsqlCommand(
                "SELECT count(*) FROM platform.audit_event WHERE tenant_id=$1 AND correlation_id=$2", connection);
            auditCount.Parameters.AddWithValue(tenantId);
            auditCount.Parameters.AddWithValue(failedPreparationAudit.CorrelationId);
            Assert((long)(await auditCount.ExecuteScalarAsync())! == 0,
                "Failed repeated preparation committed its earlier replay audit.");
        }
        finally
        {
            await SetItemCompanyActive(true);
        }
        var recovered = await gateway.PrepareAsync(command, audit);
        Assert(!recovered.Created && recovered.Preview.Draft.RecordedAt == created.Draft.RecordedAt,
            "Preparation could not resume with the same immutable identity after restoring readiness.");
        await ThrowsAsync<SalesDispatchDraftConflictException>(async () => await gateway.CreateAsync(
            new(scope, companyId, draftId, orderId, 4, command.EffectiveDate, [new(lineId, warehouseId, 2m)]), audit));
        var preview = await gateway.PreviewAsync(query, audit);
        Assert(preview.Draft.DispatchId == draftId && preview.Lines.Count == 1 &&
            preview.Lines[0].OrderLineId == lineId && preview.Lines[0].WarehouseId == warehouseId &&
            preview.Lines[0].RequestedQuantity == 3m &&
            preview.Lines[0].ReservedQuantity + preview.Lines[0].UnreservedQuantity == 3m &&
            preview.Lines[0].Consumptions.Sum(part => part.Quantity) == preview.Lines[0].ReservedQuantity,
            "Gateway lost exact preview quantities or source line mapping.");
        var hidden = SalesScope(tenantId, otherCompanyId, actorId, "dispatch.create", "sales.order.view");
        await ThrowsAsync<SalesDispatchDraftNotFoundException>(async () => await readGateway.LoadAsync(
            new(hidden, otherCompanyId, draftId), audit with { CompanyIds = new HashSet<Guid> { otherCompanyId } }));
        await ThrowsAsync<SalesDispatchDraftNotFoundException>(async () => await gateway.PreviewAsync(
            new(scope, companyId, Guid.NewGuid()), audit));

        var failedPreviewAudit = audit with { CorrelationId = Guid.CreateVersion7(), TraceId = new string('x', 65) };
        await ThrowsAsync<SalesDispatchUnavailableException>(async () => await gateway.PreviewAsync(query, failedPreviewAudit));
        await using var verifyConnection = await owner.OpenConnectionAsync();
        await using var verify = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM platform.audit_event WHERE tenant_id=$1 AND correlation_id=$2),
                   (SELECT count(*) FROM inventory.stock_movement WHERE tenant_id=$1 AND source_event_id=$3),
                   (SELECT version FROM sales.sales_order WHERE tenant_id=$1 AND company_id=$4 AND order_id=$5)
            """, verifyConnection);
        foreach (Guid id in new[] { tenantId, failedPreviewAudit.CorrelationId, draftId, companyId, orderId }) verify.Parameters.AddWithValue(id);
        await using var verified = await verify.ExecuteReaderAsync();
        Assert(await verified.ReadAsync() && verified.GetInt64(0) == 0 && verified.GetInt64(1) == 0 && verified.GetInt64(2) == 4,
            "Draft gateway left failed audit or prematurely posted stock/fulfilment.");
    }
}
