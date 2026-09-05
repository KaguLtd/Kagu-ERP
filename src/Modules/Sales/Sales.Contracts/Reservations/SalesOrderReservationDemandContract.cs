namespace KaguERP.Modules.Sales.Contracts.Reservations;

public sealed class SalesOrderReservationDemandQuery
{
    private SalesOrderReservationDemandQuery(
        Guid tenantId,
        Guid companyId,
        Guid orderId,
        long confirmedVersion)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        OrderId = orderId;
        ConfirmedVersion = confirmedVersion;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid OrderId { get; }
    public long ConfirmedVersion { get; }

    public static SalesOrderReservationDemandQuery Create(
        Guid tenantId,
        Guid companyId,
        Guid orderId,
        long confirmedVersion)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty || orderId == Guid.Empty ||
            confirmedVersion <= 0)
        {
            throw new SalesOrderReservationDemandContractException(
                "SALES_RESERVATION_DEMAND_QUERY_INVALID",
                "Reservation demand query requires exact scope, order and confirmed version.");
        }

        return new SalesOrderReservationDemandQuery(
            tenantId, companyId, orderId, confirmedVersion);
    }
}

public sealed record SalesOrderReservationDemandLine
{
    private const decimal MaximumQuantity = 99999999999999.999999m;

    private SalesOrderReservationDemandLine(
        Guid orderLineId,
        Guid itemId,
        string baseUomCode,
        decimal maximumReservableQuantity)
    {
        OrderLineId = orderLineId;
        ItemId = itemId;
        BaseUomCode = baseUomCode;
        MaximumReservableQuantity = maximumReservableQuantity;
    }

    public Guid OrderLineId { get; }
    public Guid ItemId { get; }
    public string BaseUomCode { get; }
    public decimal MaximumReservableQuantity { get; }

    public static SalesOrderReservationDemandLine Create(
        Guid orderLineId,
        Guid itemId,
        string baseUomCode,
        decimal maximumReservableQuantity)
    {
        string canonicalUom = baseUomCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (orderLineId == Guid.Empty || itemId == Guid.Empty ||
            canonicalUom.Length is < 1 or > 16 ||
            canonicalUom.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-') ||
            maximumReservableQuantity <= decimal.Zero || maximumReservableQuantity > MaximumQuantity ||
            decimal.Round(maximumReservableQuantity, 6) != maximumReservableQuantity)
        {
            throw new SalesOrderReservationDemandContractException(
                "SALES_RESERVATION_DEMAND_LINE_INVALID",
                "Reservation demand line requires item, canonical base UOM and numeric(20,6) quantity.");
        }

        return new SalesOrderReservationDemandLine(
            orderLineId, itemId, canonicalUom, maximumReservableQuantity);
    }
}

public sealed class SalesOrderReservationDemandSnapshot
{
    public const int MaximumLineCount = 500;

    private SalesOrderReservationDemandSnapshot(
        Guid tenantId,
        Guid companyId,
        Guid orderId,
        long confirmedVersion,
        SalesOrderReservationDemandLine[] lines)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        OrderId = orderId;
        ConfirmedVersion = confirmedVersion;
        Lines = Array.AsReadOnly(lines);
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid OrderId { get; }
    public long ConfirmedVersion { get; }
    public IReadOnlyList<SalesOrderReservationDemandLine> Lines { get; }

    public static SalesOrderReservationDemandSnapshot Create(
        Guid tenantId,
        Guid companyId,
        Guid orderId,
        long confirmedVersion,
        IEnumerable<SalesOrderReservationDemandLine?>? lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        _ = SalesOrderReservationDemandQuery.Create(
            tenantId, companyId, orderId, confirmedVersion);
        SalesOrderReservationDemandLine?[] copied = lines.ToArray();
        if (copied.Any(line => line is null))
        {
            throw new SalesOrderReservationDemandContractException(
                "SALES_RESERVATION_DEMAND_LINES_INVALID",
                "Reservation demand lines cannot contain null values.");
        }
        SalesOrderReservationDemandLine[] snapshot = copied
            .Cast<SalesOrderReservationDemandLine>()
            .ToArray();
        if (snapshot.Length is < 1 or > MaximumLineCount ||
            snapshot.Select(line => line.OrderLineId).Distinct().Count() != snapshot.Length)
        {
            throw new SalesOrderReservationDemandContractException(
                "SALES_RESERVATION_DEMAND_LINES_INVALID",
                $"Reservation demand requires 1-{MaximumLineCount} unique order lines.");
        }

        return new SalesOrderReservationDemandSnapshot(
            tenantId, companyId, orderId, confirmedVersion, snapshot);
    }
}

public interface ISalesOrderReservationDemandSource
{
    ValueTask<SalesOrderReservationDemandSnapshot?> LoadAsync(
        SalesOrderReservationDemandQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class SalesOrderReservationDemandContractException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
