namespace KaguERP.Modules.Purchasing.Contracts.Costs;

public sealed record SupplierInvoiceCostQuery(Guid TenantId, Guid CompanyId, Guid InvoiceId,
    long ExpectedVersion, DateTimeOffset RecordedCutoff);

public sealed record SupplierInvoiceCostAllocation(Guid AllocationId, Guid InvoiceLineId, Guid ReceiptId,
    Guid ReceiptLineId, Guid ItemId, Guid WarehouseId, string BaseUomCode,
    decimal BaseQuantity, decimal EligibleFunctionalCost);

/// <summary>Published only by a trusted, transaction-bound finalized-invoice producer.</summary>
public sealed class SupplierInvoiceCostSnapshot
{
    public SupplierInvoiceCostSnapshot(SupplierInvoiceCostQuery identity, DateOnly effectiveDate,
        DateTimeOffset finalizedAt, string functionalCurrency, Guid exchangeRateSnapshotId,
        Guid costRuleSnapshotId, IEnumerable<SupplierInvoiceCostAllocation> allocations)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(allocations);
        if (identity.TenantId == Guid.Empty || identity.CompanyId == Guid.Empty || identity.InvoiceId == Guid.Empty ||
            identity.ExpectedVersion <= 0 || identity.RecordedCutoff == default || identity.RecordedCutoff.Offset != TimeSpan.Zero ||
            identity.RecordedCutoff.Ticks % TimeSpan.TicksPerMicrosecond != 0 || effectiveDate == default ||
            finalizedAt == default || finalizedAt.Offset != TimeSpan.Zero || finalizedAt > identity.RecordedCutoff ||
            finalizedAt.Ticks % TimeSpan.TicksPerMicrosecond != 0 || exchangeRateSnapshotId == Guid.Empty ||
            costRuleSnapshotId == Guid.Empty || functionalCurrency is null || functionalCurrency.Length != 3 ||
            functionalCurrency.Any(character => character is < 'A' or > 'Z'))
            throw new SupplierInvoiceCostContractException();
        var rows = allocations.Take(501).ToArray();
        if (rows.Length is < 1 or > 500 || rows.Any(row => row is null || row.AllocationId == Guid.Empty ||
                row.InvoiceLineId == Guid.Empty || row.ReceiptId == Guid.Empty || row.ReceiptLineId == Guid.Empty ||
                row.ItemId == Guid.Empty || row.WarehouseId == Guid.Empty || !ValidUom(row.BaseUomCode) ||
                row.BaseQuantity <= 0m || row.BaseQuantity > 99999999999999.999999m ||
                decimal.Round(row.BaseQuantity, 6) != row.BaseQuantity || row.EligibleFunctionalCost < 0m ||
                row.EligibleFunctionalCost > 9999999999999999.9999m || decimal.Round(row.EligibleFunctionalCost, 4) != row.EligibleFunctionalCost) ||
            rows.Select(row => row.AllocationId).Distinct().Count() != rows.Length ||
            rows.Select(row => (row.InvoiceLineId, row.ReceiptId, row.ReceiptLineId)).Distinct().Count() != rows.Length)
            throw new SupplierInvoiceCostContractException();
        if (rows.GroupBy(row => row.InvoiceLineId).Any(group =>
                group.Select(row => (row.ItemId, row.BaseUomCode)).Distinct().Count() != 1 ||
                group.Sum(row => row.BaseQuantity) > 99999999999999.999999m ||
                group.Sum(row => row.EligibleFunctionalCost) > 9999999999999999.9999m) ||
            rows.GroupBy(row => (row.ReceiptId, row.ReceiptLineId)).Any(group =>
                group.Select(row => (row.ItemId, row.WarehouseId, row.BaseUomCode)).Distinct().Count() != 1))
            throw new SupplierInvoiceCostContractException();
        Identity = identity;
        EffectiveDate = effectiveDate;
        FinalizedAt = finalizedAt;
        FunctionalCurrency = functionalCurrency;
        ExchangeRateSnapshotId = exchangeRateSnapshotId;
        CostRuleSnapshotId = costRuleSnapshotId;
        Allocations = Array.AsReadOnly(rows);
    }
    public SupplierInvoiceCostQuery Identity { get; }
    public DateOnly EffectiveDate { get; }
    public DateTimeOffset FinalizedAt { get; }
    public string FunctionalCurrency { get; }
    public Guid ExchangeRateSnapshotId { get; }
    public Guid CostRuleSnapshotId { get; }
    public IReadOnlyList<SupplierInvoiceCostAllocation> Allocations { get; }
    private static bool ValidUom(string? value) => value is { Length: >= 1 and <= 16 } &&
        value.All(character => char.IsAsciiDigit(character) || character is >= 'A' and <= 'Z' or '-');
}

public interface ISupplierInvoiceCostSource
{
    ValueTask<ReconciledSupplierInvoiceCost?> LoadAsync(SupplierInvoiceCostQuery query, CancellationToken cancellationToken = default);
}
public sealed class SupplierInvoiceCostContractException()
    : InvalidOperationException("Invoice cost snapshot requires exact finalized source, bounded allocations and cost lineage.");
