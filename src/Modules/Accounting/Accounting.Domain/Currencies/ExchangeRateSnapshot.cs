using KaguERP.Modules.Accounting.Domain.Journals;

namespace KaguERP.Modules.Accounting.Domain.Currencies;

public sealed record ExchangeRateSnapshot
{
    private ExchangeRateSnapshot(
        Guid tenantId,
        Guid companyId,
        Guid rateSnapshotId,
        long version,
        CurrencyCode transactionCurrency,
        CurrencyCode functionalCurrency,
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

    public CurrencyCode TransactionCurrency { get; }

    public CurrencyCode FunctionalCurrency { get; }

    public string RateType { get; }

    public string Source { get; }

    public DateOnly RateDate { get; }

    public decimal FunctionalUnitsNumerator { get; }

    public decimal TransactionUnitsDenominator { get; }

    public static ExchangeRateSnapshot Create(
        Guid tenantId,
        Guid companyId,
        Guid rateSnapshotId,
        long version,
        CurrencyCode transactionCurrency,
        CurrencyCode functionalCurrency,
        string rateType,
        string source,
        DateOnly rateDate,
        decimal functionalUnitsNumerator,
        decimal transactionUnitsDenominator)
    {
        RequireId(tenantId, "RATE_TENANT_REQUIRED", "Exchange-rate tenant ID is required.");
        RequireId(companyId, "RATE_COMPANY_REQUIRED", "Exchange-rate company ID is required.");
        RequireId(rateSnapshotId, "RATE_SNAPSHOT_REQUIRED", "Exchange-rate snapshot ID is required.");

        if (version <= 0)
        {
            throw new CurrencyInvariantException("RATE_VERSION_INVALID", "Exchange-rate version must be positive.");
        }

        ArgumentNullException.ThrowIfNull(transactionCurrency);
        ArgumentNullException.ThrowIfNull(functionalCurrency);

        var canonicalRateType = RequireText(rateType, "RATE_TYPE_REQUIRED", "Exchange-rate type is required.");
        var canonicalSource = RequireText(source, "RATE_SOURCE_REQUIRED", "Exchange-rate source is required.");

        if (functionalUnitsNumerator <= decimal.Zero)
        {
            throw new CurrencyInvariantException(
                "RATE_NUMERATOR_INVALID",
                "Exchange-rate functional-units numerator must be positive.");
        }

        if (transactionUnitsDenominator <= decimal.Zero)
        {
            throw new CurrencyInvariantException(
                "RATE_DENOMINATOR_INVALID",
                "Exchange-rate transaction-units denominator must be positive.");
        }

        return new ExchangeRateSnapshot(
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
            throw new CurrencyInvariantException(code, message);
        }

        return value.Trim();
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new CurrencyInvariantException(code, message);
        }
    }
}
