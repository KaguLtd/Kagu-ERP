using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Transfers;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Purchasing.Contracts.Costs;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public static class SupplierInvoiceCostBasisAdapter
{
    public static async ValueTask<IReadOnlyList<InventoryInvoiceCostBasis>> LoadAsync(ExecutionScope scope,
        SupplierInvoiceCostQuery query, InventoryWarehouseScopeEvidence warehouseScope,
        ISupplierInvoiceCostSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(warehouseScope);
        scope.EnsureAllowed(query.TenantId, query.CompanyId);
        warehouseScope.EnsureMatches(query.TenantId, query.CompanyId, scope.ActorId);
        if (!scope.HasPermission(query.CompanyId, PostgresInventoryCostPublicationWriter.RequiredPermission))
            throw new InventoryCostHistoryAccessException();
        if (query.InvoiceId == Guid.Empty || query.ExpectedVersion <= 0 || query.RecordedCutoff == default ||
            query.RecordedCutoff.Offset != TimeSpan.Zero || query.RecordedCutoff.Ticks % TimeSpan.TicksPerMicrosecond != 0)
            throw new SupplierInvoiceCostContractException();
        cancellationToken.ThrowIfCancellationRequested();
        var reconciled = await source.LoadAsync(query, cancellationToken)
            ?? throw new InventoryInvoiceCostSourceUnavailableException();
        var published = reconciled.Snapshot;
        if (published.Identity != query || published.FinalizedAt > query.RecordedCutoff)
            throw new InventoryInvoiceCostSourceMismatchException();
        if (published.Allocations.Any(line => !warehouseScope.WarehouseIds.Contains(line.WarehouseId)))
            throw new InventoryCostHistoryAccessException();
        return Array.AsReadOnly(published.Allocations.Select(line => new InventoryInvoiceCostBasis(
            new(query.TenantId, query.CompanyId, query.InvoiceId, query.ExpectedVersion, line.AllocationId,
                line.InvoiceLineId, line.ReceiptId, line.ReceiptLineId, published.ExchangeRateSnapshotId,
                published.CostRuleSnapshotId, published.EffectiveDate, published.FinalizedAt), line.ItemId,
            line.WarehouseId, InventoryUomCode.Create(line.BaseUomCode), published.FunctionalCurrency,
            InventoryQuantity.Create(line.BaseQuantity), line.EligibleFunctionalCost)).ToArray());
    }
}
public sealed class InventoryInvoiceCostSourceUnavailableException()
    : InvalidOperationException("Finalized invoice cost source is unavailable; this is not proof of no cost history.");
public sealed class InventoryInvoiceCostSourceMismatchException()
    : InvalidOperationException("Invoice cost source does not match the requested scope, version or cutoff.");
