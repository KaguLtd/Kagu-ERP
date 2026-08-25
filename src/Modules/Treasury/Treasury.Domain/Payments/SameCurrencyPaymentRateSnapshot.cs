namespace KaguERP.Modules.Treasury.Domain.Payments;

public sealed record SameCurrencyPaymentRateSnapshot
{
    private SameCurrencyPaymentRateSnapshot(
        Guid tenantId,
        Guid companyId,
        Guid rateSnapshotId,
        long version,
        TreasuryCurrencyCode transactionCurrency,
        TreasuryCurrencyCode functionalCurrency,
        string rateType,
        string source,
        DateOnly rateDate,
        decimal functionalUnitsNumerator,
        decimal transactionUnitsDenominator)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        RateSnapshotId = rateSnapshotId;
        Version = version;
        TransactionCurrency = transactionCurrency;
        FunctionalCurrency = functionalCurrency;
        RateType = rateType;
        Source = source;
        RateDate = rateDate;
        FunctionalUnitsNumerator = functionalUnitsNumerator;
        TransactionUnitsDenominator = transactionUnitsDenominator;
    }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public Guid RateSnapshotId { get; }

    public long Version { get; }

    public TreasuryCurrencyCode TransactionCurrency { get; }

    public TreasuryCurrencyCode FunctionalCurrency { get; }

    public string RateType { get; }

    public string Source { get; }

    public DateOnly RateDate { get; }

    public decimal FunctionalUnitsNumerator { get; }

    public decimal TransactionUnitsDenominator { get; }

    public static SameCurrencyPaymentRateSnapshot Create(
        Guid tenantId,
        Guid companyId,
        Guid rateSnapshotId,
        long version,
        TreasuryCurrencyCode? transactionCurrency,
        TreasuryCurrencyCode? functionalCurrency,
        string rateType,
        string source,
        DateOnly rateDate,
        decimal functionalUnitsNumerator,
        decimal transactionUnitsDenominator)
    {
        RequireId(tenantId, "PAYMENT_RATE_TENANT_REQUIRED", "Payment-rate tenant ID is required.");
        RequireId(companyId, "PAYMENT_RATE_COMPANY_REQUIRED", "Payment-rate company ID is required.");
        RequireId(rateSnapshotId, "PAYMENT_RATE_SNAPSHOT_REQUIRED", "Payment-rate snapshot ID is required.");
        ArgumentNullException.ThrowIfNull(transactionCurrency);
        ArgumentNullException.ThrowIfNull(functionalCurrency);

        if (version <= 0)
        {
            throw new PaymentInvariantException(
                "PAYMENT_RATE_VERSION_INVALID",
                "Payment-rate snapshot version must be positive.");
        }

        var canonicalRateType = RequireText(rateType, "PAYMENT_RATE_TYPE_REQUIRED", "Payment-rate type is required.");
        var canonicalSource = RequireText(source, "PAYMENT_RATE_SOURCE_REQUIRED", "Payment-rate source is required.");

        if (functionalUnitsNumerator <= decimal.Zero)
        {
            throw new PaymentInvariantException(
                "PAYMENT_RATE_NUMERATOR_INVALID",
                "Payment-rate functional-units numerator must be positive.");
        }

        if (transactionUnitsDenominator <= decimal.Zero)
        {
            throw new PaymentInvariantException(
                "PAYMENT_RATE_DENOMINATOR_INVALID",
                "Payment-rate transaction-units denominator must be positive.");
        }

        if (transactionCurrency != functionalCurrency || functionalUnitsNumerator != transactionUnitsDenominator)
        {
            throw new PaymentInvariantException(
                "PAYMENT_CROSS_CURRENCY_NOT_SUPPORTED",
                "This technical slice requires matching transaction/functional currencies and an identity rate.");
        }

        return new SameCurrencyPaymentRateSnapshot(
            tenantId,
            companyId,
            rateSnapshotId,
            version,
            transactionCurrency,
            functionalCurrency,
            canonicalRateType,
            canonicalSource,
            rateDate,
            functionalUnitsNumerator,
            transactionUnitsDenominator);
    }

    private static string RequireText(string value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PaymentInvariantException(code, message);
        }

        return value.Trim();
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new PaymentInvariantException(code, message);
        }
    }
}
