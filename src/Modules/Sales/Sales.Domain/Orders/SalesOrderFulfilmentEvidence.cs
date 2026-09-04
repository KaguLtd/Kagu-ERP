namespace KaguERP.Modules.Sales.Domain.Orders;

public sealed record SalesOrderLineCommitment
{
    private SalesOrderLineCommitment(
        Guid orderLineId,
        Guid itemId,
        string baseUomCode,
        SalesOrderQuantity orderedQuantity)
    {
        OrderLineId = orderLineId;
        ItemId = itemId;
        BaseUomCode = baseUomCode;
        OrderedQuantity = orderedQuantity;
    }

    public Guid OrderLineId { get; }
    public Guid ItemId { get; }
    public string BaseUomCode { get; }
    public SalesOrderQuantity OrderedQuantity { get; }

    public static SalesOrderLineCommitment Create(
        Guid orderLineId,
        Guid itemId,
        string baseUomCode,
        SalesOrderQuantity orderedQuantity)
    {
        string normalizedUom = baseUomCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (orderLineId == Guid.Empty || itemId == Guid.Empty ||
            normalizedUom.Length is < 1 or > 16 ||
            orderedQuantity.Value <= decimal.Zero ||
            !normalizedUom.All(character => char.IsAsciiLetterUpper(character) ||
                char.IsAsciiDigit(character) || character == '-'))
        {
            throw new SalesOrderLifecycleException(
                "SALES_ORDER_LINE_INVALID",
                "Sales order line requires identities and a canonical base UOM.");
        }

        return new SalesOrderLineCommitment(orderLineId, itemId, normalizedUom, orderedQuantity);
    }
}

public sealed record SalesOrderFulfilmentAllocation
{
    private SalesOrderFulfilmentAllocation(
        Guid allocationId,
        Guid tenantId,
        Guid companyId,
        Guid orderId,
        Guid orderLineId,
        Guid dispatchId,
        Guid dispatchLineId,
        SalesOrderQuantity baseQuantity)
    {
        AllocationId = allocationId;
        TenantId = tenantId;
        CompanyId = companyId;
        OrderId = orderId;
        OrderLineId = orderLineId;
        DispatchId = dispatchId;
        DispatchLineId = dispatchLineId;
        BaseQuantity = baseQuantity;
    }

    public Guid AllocationId { get; }
    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid OrderId { get; }
    public Guid OrderLineId { get; }
    public Guid DispatchId { get; }
    public Guid DispatchLineId { get; }
    public SalesOrderQuantity BaseQuantity { get; }

    public static SalesOrderFulfilmentAllocation Create(
        Guid allocationId,
        Guid tenantId,
        Guid companyId,
        Guid orderId,
        Guid orderLineId,
        Guid dispatchId,
        Guid dispatchLineId,
        SalesOrderQuantity baseQuantity)
    {
        if (baseQuantity.Value <= decimal.Zero || new[]
            {
                allocationId, tenantId, companyId, orderId, orderLineId, dispatchId, dispatchLineId,
            }.Any(id => id == Guid.Empty))
        {
            throw new SalesOrderLifecycleException(
                "SALES_FULFILMENT_ALLOCATION_IDENTITY_REQUIRED",
                "Fulfilment allocation requires complete source and target identities.");
        }

        return new SalesOrderFulfilmentAllocation(
            allocationId,
            tenantId,
            companyId,
            orderId,
            orderLineId,
            dispatchId,
            dispatchLineId,
            baseQuantity);
    }
}

public sealed class SalesOrderFulfilmentEvidence
{
    private SalesOrderFulfilmentEvidence(
        Guid tenantId,
        Guid companyId,
        Guid orderId,
        SalesOrderLineCommitment[] lines,
        SalesOrderFulfilmentAllocation[] allocations,
        bool isPartiallyFulfilled,
        bool isFullyFulfilled)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        OrderId = orderId;
        Lines = Array.AsReadOnly(lines);
        Allocations = Array.AsReadOnly(allocations);
        IsPartiallyFulfilled = isPartiallyFulfilled;
        IsFullyFulfilled = isFullyFulfilled;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid OrderId { get; }
    public IReadOnlyList<SalesOrderLineCommitment> Lines { get; }
    public IReadOnlyList<SalesOrderFulfilmentAllocation> Allocations { get; }
    public bool IsPartiallyFulfilled { get; }
    public bool IsFullyFulfilled { get; }

    public static SalesOrderFulfilmentEvidence Create(
        Guid tenantId,
        Guid companyId,
        Guid orderId,
        IEnumerable<SalesOrderLineCommitment> lines,
        IEnumerable<SalesOrderFulfilmentAllocation> allocations)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(allocations);
        if (tenantId == Guid.Empty || companyId == Guid.Empty || orderId == Guid.Empty)
        {
            throw new SalesOrderLifecycleException(
                "SALES_FULFILMENT_SCOPE_REQUIRED",
                "Fulfilment evidence requires tenant, company and order identities.");
        }

        SalesOrderLineCommitment[] lineSnapshot = lines.ToArray();
        SalesOrderFulfilmentAllocation[] allocationSnapshot = allocations.ToArray();
        if (lineSnapshot.Length == 0 ||
            lineSnapshot.Select(line => line.OrderLineId).Distinct().Count() != lineSnapshot.Length)
        {
            throw new SalesOrderLifecycleException(
                "SALES_ORDER_LINES_INVALID",
                "Fulfilment evidence requires unique non-empty order lines.");
        }
        if (allocationSnapshot.Select(allocation => allocation.AllocationId).Distinct().Count() !=
                allocationSnapshot.Length ||
            allocationSnapshot.Select(allocation => (allocation.DispatchId, allocation.DispatchLineId))
                .Distinct().Count() != allocationSnapshot.Length)
        {
            throw new SalesOrderLifecycleException(
                "SALES_FULFILMENT_ALLOCATION_DUPLICATE",
                "Fulfilment allocation and dispatch-line identities must be unique.");
        }

        Dictionary<Guid, SalesOrderLineCommitment> lineById =
            lineSnapshot.ToDictionary(line => line.OrderLineId);
        var fulfilledByLine = lineSnapshot.ToDictionary(line => line.OrderLineId, _ => decimal.Zero);
        foreach (SalesOrderFulfilmentAllocation allocation in allocationSnapshot)
        {
            if (allocation.TenantId != tenantId || allocation.CompanyId != companyId ||
                allocation.OrderId != orderId ||
                !lineById.TryGetValue(allocation.OrderLineId, out SalesOrderLineCommitment? line) ||
                line is null)
            {
                throw new SalesOrderLifecycleException(
                    "SALES_FULFILMENT_SCOPE_MISMATCH",
                    "Fulfilment allocation must belong to an order line in the exact scope.");
            }

            decimal fulfilled;
            try
            {
                fulfilled = checked(fulfilledByLine[allocation.OrderLineId] + allocation.BaseQuantity.Value);
            }
            catch (OverflowException exception)
            {
                throw new SalesOrderLifecycleException(
                    "SALES_FULFILMENT_QUANTITY_OVERFLOW",
                    $"Fulfilment quantity arithmetic overflowed: {exception.Message}");
            }
            if (fulfilled > line.OrderedQuantity.Value)
            {
                throw new SalesOrderLifecycleException(
                    "SALES_FULFILMENT_EXCEEDS_ORDERED",
                    "Fulfilment allocation cannot exceed the ordered base quantity.");
            }
            fulfilledByLine[allocation.OrderLineId] = fulfilled;
        }

        bool any = fulfilledByLine.Values.Any(quantity => quantity > decimal.Zero);
        bool full = lineSnapshot.All(
            line => fulfilledByLine[line.OrderLineId] == line.OrderedQuantity.Value);
        return new SalesOrderFulfilmentEvidence(
            tenantId,
            companyId,
            orderId,
            lineSnapshot,
            allocationSnapshot,
            any && !full,
            full);
    }

    public void EnsureMatches(SalesOrderLifecycleState state)
    {
        if (TenantId != state.TenantId || CompanyId != state.CompanyId || OrderId != state.OrderId)
        {
            throw new SalesOrderLifecycleException(
                "SALES_FULFILMENT_SCOPE_MISMATCH",
                "Fulfilment evidence does not match the sales order lifecycle scope.");
        }
    }
}
