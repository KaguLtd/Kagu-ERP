using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Purchasing.Contracts.Costs;
using Npgsql;

namespace KaguERP.Bootstrap;

/// <summary>Internal invoice-input calculation, not a valuation publication or invoice posting.</summary>
public static class PostgresSupplierInvoiceUnitCostPreview
{
    public static async ValueTask<(IReadOnlyList<InventoryInvoiceUnitCost> Costs, IReadOnlyList<VerifiedInvoiceReceiptCost> Receipts)> LoadReceiptVerifiedAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, ExecutionScope scope, SupplierInvoiceCostQuery query,
        Func<NpgsqlConnection, NpgsqlTransaction, ISupplierInvoiceCostSource> sourceFactory,
        IReadOnlyList<InvoiceReceiptMovementReference> references, Guid roundingPolicySnapshotId, int scale,
        RequestAuditContext audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(references);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Verified invoice preview requires the caller's ReadCommitted transaction.");
        var captured = references.Take(501).ToArray();
        const string savepoint = "invoice_receipt_verified_preview";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            var costs = await LoadAsync(connection, transaction, scope, query, sourceFactory,
                roundingPolicySnapshotId, scale, audit, cancellationToken);
            var receipts = await PostgresInvoiceReceiptCostVerifier.VerifyAsync(connection, transaction, scope,
                query.CompanyId, query.RecordedCutoff, costs.Select(cost => cost.Basis).ToArray(), captured, cancellationToken);
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return (costs, receipts);
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }

    public static async ValueTask<IReadOnlyList<InventoryInvoiceUnitCost>> LoadAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, SupplierInvoiceCostQuery query,
        Func<NpgsqlConnection, NpgsqlTransaction, ISupplierInvoiceCostSource> sourceFactory,
        Guid roundingPolicySnapshotId, int scale, RequestAuditContext audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(sourceFactory);
        ArgumentNullException.ThrowIfNull(audit);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Invoice cost preview requires the caller's ReadCommitted transaction.");
        scope.EnsureAllowed(query.TenantId, query.CompanyId);
        if (audit.TenantId != scope.TenantId || audit.ActorId != scope.ActorId || !audit.CompanyIds.SetEquals(scope.CompanyIds) ||
            !scope.HasPermission(query.CompanyId, PostgresInventoryCostPublicationWriter.RequiredPermission))
            throw new InventoryCostHistoryAccessException();
        if (roundingPolicySnapshotId == Guid.Empty || scale is < 0 or > 28)
            throw new InventoryInvariantException("INVENTORY_COST_ROUNDING_REQUIRED", "An explicit unit-cost rounding policy is required.");
        const string savepoint = "supplier_invoice_unit_cost_preview";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            var warehouses = await PostgresInventoryWarehouseScopeLoader.LoadAsync(connection, transaction,
                scope, query.CompanyId, cancellationToken);
            // The production factory must bind the Purchasing-owned source to this exact transaction and trusted actor.
            var source = sourceFactory(connection, transaction);
            var bases = await SupplierInvoiceCostBasisAdapter.LoadAsync(scope, query, warehouses, source, cancellationToken);
            var calculated = bases.Select(basis => basis.CalculateUnitCost(roundingPolicySnapshotId, scale)).ToArray();
            var current = await PostgresInventoryWarehouseScopeLoader.LoadAsync(connection, transaction,
                scope, query.CompanyId, cancellationToken);
            current.EnsureMatches(query.TenantId, query.CompanyId, scope.ActorId);
            if (bases.Any(basis => !current.WarehouseIds.Contains(basis.WarehouseId))) throw new InventoryCostHistoryAccessException();
            await PostgresAuthorizationAuditWriter.AppendAsync(connection, transaction,
                audit with { CompanyIds = new HashSet<Guid> { query.CompanyId } }, Guid.CreateVersion7(),
                new AuthorizationAuditEvent("inventory.invoice.cost.preview", "supplier-invoice", query.InvoiceId.ToString("D"),
                    "allowed", "INVENTORY_INVOICE_UNIT_COST_PREVIEWED"), cancellationToken);
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return Array.AsReadOnly(calculated);
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }
}
