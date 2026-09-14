namespace KaguERP.Modules.Purchasing.Contracts.Costs;

public sealed record SupplierInvoiceLineCostBudget(Guid InvoiceLineId, Guid ItemId, string BaseUomCode,
    decimal BaseQuantity, decimal EligibleFunctionalCost);
public sealed record SupplierReceiptLineCostCapacity(Guid ReceiptId, Guid ReceiptLineId, Guid ItemId,
    Guid WarehouseId, string BaseUomCode, decimal AvailableBaseQuantity);

/// <summary>Arithmetic reconciliation, not proof of database provenance. Producer must lock authoritative source balances.</summary>
public sealed class ReconciledSupplierInvoiceCost
{
    private ReconciledSupplierInvoiceCost(SupplierInvoiceCostSnapshot snapshot,
        SupplierInvoiceLineCostBudget[] invoices, SupplierReceiptLineCostCapacity[] receipts)
    {
        Snapshot = snapshot;
        InvoiceLines = Array.AsReadOnly(invoices);
        ReceiptLines = Array.AsReadOnly(receipts);
    }
    public SupplierInvoiceCostSnapshot Snapshot { get; }
    public IReadOnlyList<SupplierInvoiceLineCostBudget> InvoiceLines { get; }
    public IReadOnlyList<SupplierReceiptLineCostCapacity> ReceiptLines { get; }

    public static ReconciledSupplierInvoiceCost Create(SupplierInvoiceCostSnapshot snapshot,
        SupplierInvoiceCostQuery sourceIdentity, IEnumerable<SupplierInvoiceLineCostBudget> invoiceLines,
        IEnumerable<SupplierReceiptLineCostCapacity> receiptLines)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(sourceIdentity);
        ArgumentNullException.ThrowIfNull(invoiceLines);
        ArgumentNullException.ThrowIfNull(receiptLines);
        var invoices = invoiceLines.Take(501).ToArray();
        var receipts = receiptLines.Take(501).ToArray();
        if (snapshot.Identity != sourceIdentity || invoices.Length is < 1 or > 500 || receipts.Length is < 1 or > 500 ||
            invoices.Any(line => line is null || line.InvoiceLineId == Guid.Empty || line.ItemId == Guid.Empty ||
                !ValidQuantity(line.BaseQuantity) || line.BaseQuantity == 0m || !ValidCost(line.EligibleFunctionalCost)) ||
            receipts.Any(line => line is null || line.ReceiptId == Guid.Empty || line.ReceiptLineId == Guid.Empty ||
                line.ItemId == Guid.Empty || line.WarehouseId == Guid.Empty || !ValidQuantity(line.AvailableBaseQuantity)) ||
            invoices.Select(line => line.InvoiceLineId).Distinct().Count() != invoices.Length ||
            receipts.Select(line => (line.ReceiptId, line.ReceiptLineId)).Distinct().Count() != receipts.Length)
            throw new SupplierInvoiceCostContractException();
        var invoiceGroups = snapshot.Allocations.GroupBy(line => line.InvoiceLineId).ToDictionary(group => group.Key);
        var receiptGroups = snapshot.Allocations.GroupBy(line => (line.ReceiptId, line.ReceiptLineId)).ToDictionary(group => group.Key);
        if (!invoiceGroups.Keys.ToHashSet().SetEquals(invoices.Select(line => line.InvoiceLineId)) ||
            !receiptGroups.Keys.ToHashSet().SetEquals(receipts.Select(line => (line.ReceiptId, line.ReceiptLineId))))
            throw new SupplierInvoiceCostContractException();
        foreach (var budget in invoices)
        {
            var group = invoiceGroups[budget.InvoiceLineId];
            if (group.Any(line => line.ItemId != budget.ItemId || line.BaseUomCode != budget.BaseUomCode) ||
                group.Sum(line => line.BaseQuantity) != budget.BaseQuantity ||
                group.Sum(line => line.EligibleFunctionalCost) != budget.EligibleFunctionalCost)
                throw new SupplierInvoiceCostContractException();
        }
        foreach (var capacity in receipts)
        {
            var group = receiptGroups[(capacity.ReceiptId, capacity.ReceiptLineId)];
            if (group.Any(line => line.ItemId != capacity.ItemId || line.WarehouseId != capacity.WarehouseId ||
                    line.BaseUomCode != capacity.BaseUomCode) ||
                group.Sum(line => line.BaseQuantity) > capacity.AvailableBaseQuantity)
                throw new SupplierInvoiceCostContractException();
        }
        return new(snapshot, invoices, receipts);
    }
    private static bool ValidQuantity(decimal value) => value is >= 0m and <= 99999999999999.999999m && decimal.Round(value, 6) == value;
    private static bool ValidCost(decimal value) => value is >= 0m and <= 9999999999999999.9999m && decimal.Round(value, 4) == value;
}
