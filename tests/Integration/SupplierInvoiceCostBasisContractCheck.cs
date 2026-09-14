using KaguERP.Bootstrap;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Transfers;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Purchasing.Contracts.Costs;
using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static partial class DatabaseIntegrationCheck
{
    private static async Task AssertInvoiceReceiptEvidenceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenant, Guid company, Guid actor, Guid item, Guid warehouse)
    {
        const string fixture = "invoice_receipt_evidence_fixture";
        await transaction.SaveAsync(fixture);
        var at = new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);
        var query = new SupplierInvoiceCostQuery(tenant, company, Guid.CreateVersion7(), 1, at);
        var row = new SupplierInvoiceCostAllocation(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), item, warehouse, "EA", 8m, 1m);
        var snapshot = new SupplierInvoiceCostSnapshot(query, new(2026, 9, 12), at, "TRY", Guid.CreateVersion7(), Guid.CreateVersion7(), [row]);
        Guid movement = Guid.CreateVersion7(), policy = Guid.CreateVersion7();
        var scope = SalesScope(tenant, company, actor, PostgresInventoryCostPublicationWriter.RequiredPermission);
        var audit = new RequestAuditContext(Guid.CreateVersion7(), "invoice-receipt-proof", tenant, actor, new HashSet<Guid> { company }, null);
        async Task<long> AuditCount()
        {
            await using var sql = new NpgsqlCommand("SELECT count(*) FROM platform.audit_event WHERE tenant_id=$1 AND correlation_id=$2", connection, transaction);
            sql.Parameters.AddWithValue(tenant);
            sql.Parameters.AddWithValue(audit.CorrelationId);
            return (long)(await sql.ExecuteScalarAsync())!;
        }
        async Task Verify(InvoiceReceiptMovementReference reference) =>
            _ = await PostgresSupplierInvoiceUnitCostPreview.LoadReceiptVerifiedAsync(connection, transaction, scope, query,
                (_, _) => new InvoiceCostSourceFixture(snapshot), [reference], policy, 2, audit);
        await ThrowsAsync<InventoryReceiptCostEvidenceException>(() => Verify(new(row.AllocationId, movement, 2)));
        Assert(await AuditCount() == 0, "Missing receipt left the invoice calculation audit.");
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO inventory.stock_movement
                (tenant_id,company_id,movement_id,item_id,warehouse_id,base_uom_code,movement_kind,base_quantity,
                 effective_date,recorded_at,recorded_by,sequence_key,source_type,source_event_id,source_line_id,source_version,posting_purpose)
            SELECT $1,$2,$3,$4,$5,'EA',1,8,DATE '2026-09-12',$6,$7,coalesce(max(sequence_key),0)+1,
                   'purchasing.goods-receipt',$8,$9,2,'receipt'
            FROM inventory.stock_movement WHERE tenant_id=$1 AND company_id=$2 AND item_id=$4 AND warehouse_id=$5
              AND effective_date=DATE '2026-09-12'
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(tenant); insert.Parameters.AddWithValue(company); insert.Parameters.AddWithValue(movement);
            insert.Parameters.AddWithValue(item); insert.Parameters.AddWithValue(warehouse); insert.Parameters.AddWithValue(at);
            insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(row.ReceiptId); insert.Parameters.AddWithValue(row.ReceiptLineId);
            await insert.ExecuteNonQueryAsync();
        }
        var result = await PostgresSupplierInvoiceUnitCostPreview.LoadReceiptVerifiedAsync(connection, transaction, scope, query,
            (_, _) => new InvoiceCostSourceFixture(snapshot), [new(row.AllocationId, movement, 2)], policy, 2, audit);
        Assert(result.Costs.Single().UnitCost == .13m && result.Receipts.Single().MovementId == movement &&
            result.Receipts.Single().ReceiptVersion == 2 && result.Receipts.Single().ReceiptRecordedAt == at,
            "Verified invoice cost lost persisted receipt provenance.");
        long audits = await AuditCount();
        await ThrowsAsync<InventoryReceiptCostEvidenceException>(() => Verify(new(row.AllocationId, movement, 1)));
        await ThrowsAsync<InventoryReceiptCostEvidenceException>(() => Verify(new(Guid.CreateVersion7(), movement, 2)));
        var excessiveRow = row with { BaseQuantity = 9m };
        var excessive = new SupplierInvoiceCostSnapshot(query, new(2026, 9, 12), at, "TRY", Guid.CreateVersion7(), Guid.CreateVersion7(), [excessiveRow]);
        await ThrowsAsync<InventoryReceiptCostEvidenceException>(async () =>
            await PostgresSupplierInvoiceUnitCostPreview.LoadReceiptVerifiedAsync(connection, transaction, scope, query,
                (_, _) => new InvoiceCostSourceFixture(excessive), [new(row.AllocationId, movement, 2)], policy, 2, audit));
        Assert(await AuditCount() == audits, "Bad receipt version/capacity retained partial invoice audit.");
        var repeatedReceiptRow = row with { AllocationId = Guid.CreateVersion7(), InvoiceLineId = Guid.CreateVersion7() };
        var sharedReceipt = new SupplierInvoiceCostSnapshot(query, new(2026, 9, 12), at, "TRY", Guid.CreateVersion7(), Guid.CreateVersion7(),
            [row, repeatedReceiptRow]);
        await ThrowsAsync<InventoryReceiptCostEvidenceException>(async () =>
            await PostgresSupplierInvoiceUnitCostPreview.LoadReceiptVerifiedAsync(connection, transaction, scope, query,
                (_, _) => new InvoiceCostSourceFixture(sharedReceipt),
                [new(row.AllocationId, movement, 2), new(repeatedReceiptRow.AllocationId, movement, 2)], policy, 2, audit));
        Assert(await AuditCount() == audits, "Two invoice lines bypassed the aggregate receipt capacity guard.");
        await transaction.SaveAsync("second_receipt_batch_fixture");
        Guid secondMovement = Guid.CreateVersion7();
        var secondRow = repeatedReceiptRow with { ReceiptLineId = Guid.CreateVersion7() };
        var multiple = new SupplierInvoiceCostSnapshot(query, new(2026, 9, 12), at, "TRY", Guid.CreateVersion7(), Guid.CreateVersion7(),
            [secondRow, row]);
        InvoiceReceiptMovementReference[] batchReferences = [new(row.AllocationId, movement, 2), new(secondRow.AllocationId, secondMovement, 2)];
        await ThrowsAsync<InventoryReceiptCostEvidenceException>(async () =>
            await PostgresSupplierInvoiceUnitCostPreview.LoadReceiptVerifiedAsync(connection, transaction, scope, query,
                (_, _) => new InvoiceCostSourceFixture(multiple), batchReferences, policy, 2, audit));
        Assert(await AuditCount() == audits, "Missing second receipt retained the first receipt's invoice audit.");
        await using (var secondInsert = new NpgsqlCommand("""
            INSERT INTO inventory.stock_movement
                (tenant_id,company_id,movement_id,item_id,warehouse_id,base_uom_code,movement_kind,base_quantity,
                 effective_date,recorded_at,recorded_by,sequence_key,source_type,source_event_id,source_line_id,source_version,posting_purpose)
            SELECT tenant_id,company_id,$4,item_id,warehouse_id,base_uom_code,movement_kind,base_quantity,
                   effective_date,recorded_at,recorded_by,sequence_key+1,source_type,source_event_id,$5,source_version,posting_purpose
            FROM inventory.stock_movement WHERE tenant_id=$1 AND company_id=$2 AND movement_id=$3
            """, connection, transaction))
        {
            secondInsert.Parameters.AddWithValue(tenant); secondInsert.Parameters.AddWithValue(company);
            secondInsert.Parameters.AddWithValue(movement); secondInsert.Parameters.AddWithValue(secondMovement);
            secondInsert.Parameters.AddWithValue(secondRow.ReceiptLineId);
            await secondInsert.ExecuteNonQueryAsync();
        }
        var batchResult = await PostgresSupplierInvoiceUnitCostPreview.LoadReceiptVerifiedAsync(connection, transaction, scope, query,
            (_, _) => new InvoiceCostSourceFixture(multiple), batchReferences, policy, 2, audit);
        Assert(batchResult.Receipts.Count == 2 && batchResult.Receipts[0].MovementId == secondMovement &&
            batchResult.Receipts[1].MovementId == movement && batchResult.Costs.All(cost => cost.UnitCost == .13m),
            "Batch receipt verification lost source order or mixed receipt identities.");
        await transaction.RollbackAsync("second_receipt_batch_fixture");
        await transaction.ReleaseAsync("second_receipt_batch_fixture");
        var earlierQuery = query with { RecordedCutoff = at.AddSeconds(-1) };
        var earlier = new SupplierInvoiceCostSnapshot(earlierQuery, new(2026, 9, 12), earlierQuery.RecordedCutoff,
            "TRY", Guid.CreateVersion7(), Guid.CreateVersion7(), [row]);
        await ThrowsAsync<InventoryReceiptCostEvidenceException>(async () =>
            await PostgresSupplierInvoiceUnitCostPreview.LoadReceiptVerifiedAsync(connection, transaction, scope, earlierQuery,
                (_, _) => new InvoiceCostSourceFixture(earlier), [new(row.AllocationId, movement, 2)], policy, 2, audit));
        await using (var reversal = new NpgsqlCommand("""
            INSERT INTO inventory.stock_movement
                (tenant_id,company_id,movement_id,item_id,warehouse_id,base_uom_code,movement_kind,base_quantity,
                 effective_date,recorded_at,recorded_by,sequence_key,source_type,source_event_id,source_line_id,
                 source_version,posting_purpose,reversal_of_movement_id)
            SELECT tenant_id,company_id,$4,item_id,warehouse_id,base_uom_code,2,-base_quantity,
                   effective_date,recorded_at,recorded_by,sequence_key+1,source_type,$4,source_line_id,
                   source_version,'receipt-reversal',movement_id
            FROM inventory.stock_movement WHERE tenant_id=$1 AND company_id=$2 AND movement_id=$3
            """, connection, transaction))
        {
            reversal.Parameters.AddWithValue(tenant); reversal.Parameters.AddWithValue(company);
            reversal.Parameters.AddWithValue(movement); reversal.Parameters.AddWithValue(Guid.CreateVersion7());
            await reversal.ExecuteNonQueryAsync();
        }
        await ThrowsAsync<InventoryReceiptCostEvidenceException>(() => Verify(new(row.AllocationId, movement, 2)));
        Assert(await AuditCount() == audits, "Invisible/reversed receipt retained partial invoice audit.");
        await transaction.RollbackAsync(fixture);
        await transaction.ReleaseAsync(fixture);
    }

    private static async Task AssertSupplierInvoiceCostBasisContractAsync()
    {
        Guid tenant = Guid.NewGuid(), company = Guid.NewGuid(), actor = Guid.NewGuid(), warehouse = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);
        var query = new SupplierInvoiceCostQuery(tenant, company, Guid.NewGuid(), 3, at);
        var line = new SupplierInvoiceCostAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), warehouse, "EA", 8m, 1m);
        var rows = new List<SupplierInvoiceCostAllocation> { line };
        var fx = Guid.NewGuid();
        var rule = Guid.NewGuid();
        SupplierInvoiceCostSnapshot Snapshot(SupplierInvoiceCostQuery identity, IEnumerable<SupplierInvoiceCostAllocation> input) =>
            new(identity, new(2026, 9, 12), at, "TRY", fx, rule, input);
        var published = Snapshot(query, rows);
        rows.Clear();
        Assert(published.Allocations.Count == 1, "Invoice cost contract retained a mutable allocation list.");
        var budget = new SupplierInvoiceLineCostBudget(line.InvoiceLineId, line.ItemId, "EA", 8m, 1m);
        var capacity = new SupplierReceiptLineCostCapacity(line.ReceiptId, line.ReceiptLineId, line.ItemId, warehouse, "EA", 8m);
        var budgets = new List<SupplierInvoiceLineCostBudget> { budget };
        var reconciled = ReconciledSupplierInvoiceCost.Create(published, query, budgets, [capacity]);
        budgets.Clear();
        Assert(reconciled.InvoiceLines.Count == 1 && reconciled.Snapshot == published, "Source reconciliation retained mutable input.");
        foreach (var invalid in new[] { budget with { BaseQuantity = 7m }, budget with { BaseQuantity = 9m },
            budget with { EligibleFunctionalCost = .99m }, budget with { EligibleFunctionalCost = 1.01m },
            budget with { ItemId = Guid.NewGuid() }, budget with { BaseUomCode = "KG" } })
            await ThrowsAsync<SupplierInvoiceCostContractException>(() => Task.FromResult(
                ReconciledSupplierInvoiceCost.Create(published, query, [invalid], [capacity])));
        foreach (var invalid in new[] { capacity with { AvailableBaseQuantity = 7m }, capacity with { AvailableBaseQuantity = -1m },
            capacity with { AvailableBaseQuantity = 8.0000001m }, capacity with { WarehouseId = Guid.NewGuid() },
            capacity with { ItemId = Guid.NewGuid() }, capacity with { ReceiptId = Guid.NewGuid() } })
            await ThrowsAsync<SupplierInvoiceCostContractException>(() => Task.FromResult(
                ReconciledSupplierInvoiceCost.Create(published, query, [budget], [invalid])));
        await ThrowsAsync<SupplierInvoiceCostContractException>(() => Task.FromResult(
            ReconciledSupplierInvoiceCost.Create(published, query with { ExpectedVersion = 4 }, [budget], [capacity])));
        await ThrowsAsync<SupplierInvoiceCostContractException>(() => Task.FromResult(
            ReconciledSupplierInvoiceCost.Create(published, query, [budget, budget], [capacity])));
        await ThrowsAsync<SupplierInvoiceCostContractException>(() => Task.FromResult(
            ReconciledSupplierInvoiceCost.Create(published, query, [budget], [capacity, capacity])));
        await ThrowsAsync<SupplierInvoiceCostContractException>(() => Task.FromResult(
            ReconciledSupplierInvoiceCost.Create(published, query, [], [capacity])));
        var splitLine = line with { AllocationId = Guid.NewGuid(), ReceiptLineId = Guid.NewGuid(), BaseQuantity = 3m, EligibleFunctionalCost = .375m };
        var split = Snapshot(query, [line with { BaseQuantity = 5m, EligibleFunctionalCost = .625m }, splitLine]);
        var splitResult = ReconciledSupplierInvoiceCost.Create(split, query, [budget],
            [capacity with { AvailableBaseQuantity = 5m }, capacity with { ReceiptLineId = splitLine.ReceiptLineId, AvailableBaseQuantity = 3m }]);
        Assert(splitResult.Snapshot.Allocations.Sum(row => row.EligibleFunctionalCost) == 1m,
            "Split receipts lost exact invoice cost reconciliation.");
        var anotherInvoiceLine = line with { AllocationId = Guid.NewGuid(), InvoiceLineId = Guid.NewGuid() };
        await ThrowsAsync<SupplierInvoiceCostContractException>(() => Task.FromResult(ReconciledSupplierInvoiceCost.Create(
            Snapshot(query, [line, anotherInvoiceLine]), query, [budget, budget with { InvoiceLineId = anotherInvoiceLine.InvoiceLineId }], [capacity])));
        var scope = new ExecutionScope(tenant, actor, [new CompanyAccess(company, [PostgresInventoryCostPublicationWriter.RequiredPermission])]);
        var warehouses = InventoryWarehouseScopeEvidence.Create(tenant, company, actor, [warehouse]);
        var source = new InvoiceCostSourceFixture(published);
        var loaded = await SupplierInvoiceCostBasisAdapter.LoadAsync(scope, query, warehouses, source);
        var basis = loaded.Single();
        Assert(source.Calls == 1 && basis.Source.InvoiceId == query.InvoiceId && basis.Source.InvoiceVersion == 3 &&
            basis.Source.ReceiptId == line.ReceiptId && basis.Source.ReceiptLineId == line.ReceiptLineId &&
            basis.Source.InvoiceLineId == line.InvoiceLineId && basis.Source.AllocationId == line.AllocationId &&
            basis.Source.ExchangeRateSnapshotId == fx && basis.Source.CostRuleSnapshotId == rule &&
            basis.ItemId == line.ItemId && basis.WarehouseId == warehouse &&
            basis.CalculateUnitCost(Guid.NewGuid(), 2).UnitCost == 0.13m,
            "Invoice cost bridge lost lineage or failed exact unit-cost calculation.");
        foreach (var wrong in new[] { query with { CompanyId = Guid.NewGuid() }, query with { InvoiceId = Guid.NewGuid() },
                     query with { ExpectedVersion = 4 }, query with { RecordedCutoff = at.AddSeconds(1) } })
            await ThrowsAsync<InventoryInvoiceCostSourceMismatchException>(async () =>
                await SupplierInvoiceCostBasisAdapter.LoadAsync(scope, query, warehouses, new InvoiceCostSourceFixture(Snapshot(wrong, [line]))));
        await ThrowsAsync<InventoryInvoiceCostSourceUnavailableException>(async () =>
            await SupplierInvoiceCostBasisAdapter.LoadAsync(scope, query, warehouses, new InvoiceCostSourceFixture(null)));
        await ThrowsAsync<InventoryCostHistoryAccessException>(async () =>
            await SupplierInvoiceCostBasisAdapter.LoadAsync(scope, query,
                InventoryWarehouseScopeEvidence.Create(tenant, company, actor, []), source));
        var deniedSource = new InvoiceCostSourceFixture(published);
        await ThrowsAsync<InventoryCostHistoryAccessException>(async () =>
            await SupplierInvoiceCostBasisAdapter.LoadAsync(new ExecutionScope(tenant, actor, [new CompanyAccess(company, [])]),
                query, warehouses, deniedSource));
        Assert(deniedSource.Calls == 0, "Unprivileged cost adapter invoked the invoice producer.");
        await ThrowsAsync<InventoryTransferAuthorizationException>(async () =>
            await SupplierInvoiceCostBasisAdapter.LoadAsync(scope, query,
                InventoryWarehouseScopeEvidence.Create(tenant, company, Guid.NewGuid(), [warehouse]), deniedSource));
        await ThrowsAsync<SupplierInvoiceCostContractException>(() => Task.FromResult(Snapshot(query, [])));
        await ThrowsAsync<SupplierInvoiceCostContractException>(() => Task.FromResult(Snapshot(query, [line, line])));
        await ThrowsAsync<SupplierInvoiceCostContractException>(() => Task.FromResult(Snapshot(query, [line with { BaseQuantity = 0m }])));
        await ThrowsAsync<SupplierInvoiceCostContractException>(() => Task.FromResult(Snapshot(query, [line with { EligibleFunctionalCost = 0.00001m }])));
        await ThrowsAsync<SupplierInvoiceCostContractException>(() => Task.FromResult(Snapshot(query, [line with { ReceiptLineId = Guid.Empty }])));
        await ThrowsAsync<SupplierInvoiceCostContractException>(() => Task.FromResult(Snapshot(query,
            [line, line with { AllocationId = Guid.NewGuid(), ReceiptLineId = Guid.NewGuid(), ItemId = Guid.NewGuid() }])));
        await ThrowsAsync<SupplierInvoiceCostContractException>(() => Task.FromResult(Snapshot(query,
            [line, line with { AllocationId = Guid.NewGuid(), InvoiceLineId = Guid.NewGuid(), WarehouseId = Guid.NewGuid() }])));
        var max = Enumerable.Range(0, 500).Select(_ => line with { AllocationId = Guid.NewGuid(), InvoiceLineId = Guid.NewGuid() }).ToArray();
        Assert(Snapshot(query, max).Allocations.Count == 500, "Valid maximum invoice cost allocation set was rejected.");
        await ThrowsAsync<SupplierInvoiceCostContractException>(() => Task.FromResult(Snapshot(query, [.. max, line])));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await ThrowsAsync<OperationCanceledException>(async () =>
            await SupplierInvoiceCostBasisAdapter.LoadAsync(scope, query, warehouses, deniedSource, cancellation.Token));
        Assert(deniedSource.Calls == 0, "Cancelled invoice request invoked its source.");
    }

    private sealed class InvoiceCostSourceFixture(SupplierInvoiceCostSnapshot? result) : ISupplierInvoiceCostSource
    {
        public int Calls { get; private set; }
        public ValueTask<ReconciledSupplierInvoiceCost?> LoadAsync(SupplierInvoiceCostQuery query, CancellationToken cancellationToken = default)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            // Synthetic fixture only. Production must read independent authoritative balances under locks.
            return ValueTask.FromResult(result is null ? null : ReconciledSupplierInvoiceCost.Create(result, result.Identity,
                result.Allocations.GroupBy(line => line.InvoiceLineId).Select(group => new SupplierInvoiceLineCostBudget(
                    group.Key, group.First().ItemId, group.First().BaseUomCode, group.Sum(line => line.BaseQuantity),
                    group.Sum(line => line.EligibleFunctionalCost))),
                result.Allocations.GroupBy(line => (line.ReceiptId, line.ReceiptLineId)).Select(group => new SupplierReceiptLineCostCapacity(
                    group.Key.ReceiptId, group.Key.ReceiptLineId, group.First().ItemId, group.First().WarehouseId,
                    group.First().BaseUomCode, group.Sum(line => line.BaseQuantity)))));
        }
    }

    private static async Task AssertSupplierInvoiceCostPreviewAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenantId, Guid companyId, Guid actorId, Guid itemId, Guid warehouseId)
    {
        var at = new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);
        var query = new SupplierInvoiceCostQuery(tenantId, companyId, Guid.CreateVersion7(), 1, at);
        var row = new SupplierInvoiceCostAllocation(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), itemId, warehouseId, "EA", 8m, 1m);
        var snapshot = new SupplierInvoiceCostSnapshot(query, new(2026, 9, 12), at, "TRY", Guid.CreateVersion7(),
            Guid.CreateVersion7(), [row]);
        var scope = SalesScope(tenantId, companyId, actorId, PostgresInventoryCostPublicationWriter.RequiredPermission);
        var audit = new RequestAuditContext(Guid.CreateVersion7(), "invoice-unit-cost-preview", tenantId, actorId,
            new HashSet<Guid> { companyId }, null);
        int factoryCalls = 0;
        ISupplierInvoiceCostSource Factory(NpgsqlConnection suppliedConnection, NpgsqlTransaction suppliedTransaction)
        {
            Assert(ReferenceEquals(connection, suppliedConnection) && ReferenceEquals(transaction, suppliedTransaction),
                "Invoice source factory did not receive the exact caller transaction.");
            factoryCalls++;
            return new InvoiceCostSourceFixture(snapshot);
        }
        var policyId = Guid.CreateVersion7();
        var result = await PostgresSupplierInvoiceUnitCostPreview.LoadAsync(connection, transaction, scope, query, Factory, policyId, 2, audit);
        Assert(result.Single().UnitCost == 0.13m && result[0].Basis.Source.InvoiceId == query.InvoiceId &&
            result[0].RoundingPolicySnapshotId == policyId && factoryCalls == 1, "Audited invoice cost preview lost its source or policy.");
        var failed = audit with { CorrelationId = Guid.CreateVersion7(), TraceId = new string('x', 65) };
        await ThrowsAsync<PostgresException>(async () =>
            await PostgresSupplierInvoiceUnitCostPreview.LoadAsync(connection, transaction, scope, query, Factory, policyId, 2, failed));
        await using (var count = new NpgsqlCommand("SELECT count(*) FROM platform.audit_event WHERE tenant_id=$1 AND correlation_id=$2", connection, transaction))
        {
            count.Parameters.AddWithValue(tenantId);
            count.Parameters.AddWithValue(failed.CorrelationId);
            Assert((long)(await count.ExecuteScalarAsync())! == 0, "Failed invoice calculation left an audit record.");
        }
        int before = factoryCalls;
        await ThrowsAsync<InventoryCostHistoryAccessException>(async () =>
            await PostgresSupplierInvoiceUnitCostPreview.LoadAsync(connection, transaction, scope, query, Factory, policyId, 2,
                audit with { ActorId = Guid.NewGuid() }));
        Assert(factoryCalls == before, "Invalid audit scope invoked the invoice source.");
    }
}
