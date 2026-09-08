namespace KaguERP.Modules.Sales.Domain.Orders;

public sealed record SalesDispatchLineRequest(Guid OrderLineId, SalesOrderQuantity Quantity);

public sealed record SalesDispatchPreparedLine(
    Guid OrderLineId,
    Guid ItemId,
    string BaseUomCode,
    SalesOrderQuantity Quantity,
    decimal RemainingAfterPreparation);

public sealed class SalesDispatchPreparation
{
    private SalesDispatchPreparation(
        SalesOrderLifecycleState order,
        SalesDispatchPreparedLine[] lines)
    {
        TenantId = order.TenantId;
        CompanyId = order.CompanyId;
        OrderId = order.OrderId;
        OrderVersion = order.Version;
        Lines = Array.AsReadOnly(lines);
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid OrderId { get; }
    public long OrderVersion { get; }
    public IReadOnlyList<SalesDispatchPreparedLine> Lines { get; }

    public static SalesDispatchPreparation Create(
        SalesOrderLifecycleState order,
        long expectedVersion,
        SalesOrderCommitment commitment,
        SalesOrderFulfilmentEvidence fulfilment,
        IEnumerable<SalesDispatchLineRequest> requestedLines)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(commitment);
        ArgumentNullException.ThrowIfNull(fulfilment);
        ArgumentNullException.ThrowIfNull(requestedLines);
        if (order.Version != expectedVersion)
        {
            throw Error("VERSION_CONFLICT", "Dispatch preparation requires the current order version.");
        }
        if (order.Status is not (SalesOrderStatus.Confirmed or SalesOrderStatus.PartiallyFulfilled))
        {
            throw Error("STATUS_INVALID", "Only confirmed or partially fulfilled orders can prepare dispatch.");
        }
        commitment.EnsureMatches(order.TenantId, order.CompanyId, order.OrderId);
        fulfilment.EnsureMatches(order);
        if (!commitment.HasSameLines(fulfilment.Lines) || fulfilment.IsFullyFulfilled ||
            (order.Status == SalesOrderStatus.PartiallyFulfilled) != fulfilment.IsPartiallyFulfilled)
        {
            throw Error("EVIDENCE_MISMATCH", "Order commitment, status and fulfilment evidence must agree.");
        }

        SalesDispatchLineRequest[] requests = requestedLines.Take(SalesOrderCommitment.MaximumLineCount + 1).ToArray();
        if (requests.Length is < 1 or > SalesOrderCommitment.MaximumLineCount ||
            requests.Any(line => line is null || line.OrderLineId == Guid.Empty || line.Quantity.Value <= 0m) ||
            requests.Select(line => line.OrderLineId).Distinct().Count() != requests.Length)
        {
            throw Error("LINES_INVALID", "Dispatch preparation requires unique order lines and positive quantities.");
        }

        var orderLines = commitment.Lines.ToDictionary(line => line.OrderLineId);
        var used = fulfilment.Allocations.GroupBy(line => line.OrderLineId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.BaseQuantity.Value));
        var prepared = new List<SalesDispatchPreparedLine>(requests.Length);
        foreach (SalesDispatchLineRequest request in requests)
        {
            if (!orderLines.TryGetValue(request.OrderLineId, out SalesOrderLineCommitment? line))
            {
                throw Error("LINE_UNAVAILABLE", "The requested line does not belong to this order.");
            }
            decimal remaining = line.OrderedQuantity.Value - used.GetValueOrDefault(line.OrderLineId);
            if (request.Quantity.Value > remaining)
            {
                throw Error("EXCEEDS_REMAINING", "Dispatch quantity cannot exceed the order line remaining quantity.");
            }
            prepared.Add(new SalesDispatchPreparedLine(line.OrderLineId, line.ItemId,
                line.BaseUomCode, request.Quantity, remaining - request.Quantity.Value));
        }
        return new SalesDispatchPreparation(order, prepared.ToArray());
    }

    private static SalesOrderLifecycleException Error(string suffix, string message) =>
        new("SALES_DISPATCH_" + suffix, message);
}
