namespace KaguERP.Modules.Sales.Domain.Orders;

public sealed class SalesOrderCommitment
{
    public const int MaximumLineCount = 500;

    private SalesOrderCommitment(
        Guid tenantId,
        Guid companyId,
        Guid orderId,
        SalesOrderLineCommitment[] lines)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        OrderId = orderId;
        Lines = Array.AsReadOnly(lines);
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid OrderId { get; }
    public IReadOnlyList<SalesOrderLineCommitment> Lines { get; }

    public static SalesOrderCommitment Create(
        Guid tenantId,
        Guid companyId,
        Guid orderId,
        IEnumerable<SalesOrderLineCommitment> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (tenantId == Guid.Empty || companyId == Guid.Empty || orderId == Guid.Empty)
        {
            throw new SalesOrderLifecycleException(
                "SALES_ORDER_COMMITMENT_SCOPE_REQUIRED",
                "Sales order commitment requires tenant, company and order identities.");
        }

        SalesOrderLineCommitment[] snapshot = lines.ToArray();
        if (snapshot.Length is < 1 or > MaximumLineCount ||
            snapshot.Select(line => line.OrderLineId).Distinct().Count() != snapshot.Length)
        {
            throw new SalesOrderLifecycleException(
                "SALES_ORDER_LINES_INVALID",
                $"Sales order commitment requires 1-{MaximumLineCount} unique lines.");
        }

        return new SalesOrderCommitment(tenantId, companyId, orderId, snapshot);
    }

    public void EnsureMatches(Guid tenantId, Guid companyId, Guid orderId)
    {
        if (TenantId != tenantId || CompanyId != companyId || OrderId != orderId)
        {
            throw new SalesOrderLifecycleException(
                "SALES_ORDER_COMMITMENT_SCOPE_MISMATCH",
                "Sales order commitment does not match the lifecycle scope.");
        }
    }

    public bool HasSameLines(IReadOnlyList<SalesOrderLineCommitment> otherLines) =>
        Lines.Count == otherLines.Count && Lines
            .OrderBy(line => line.OrderLineId)
            .Zip(otherLines.OrderBy(line => line.OrderLineId))
            .All(pair => pair.First == pair.Second);
}
