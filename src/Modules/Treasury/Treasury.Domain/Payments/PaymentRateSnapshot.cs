namespace KaguERP.Modules.Treasury.Domain.Payments;

public sealed record PaymentRateSnapshot
{
    private PaymentRateSnapshot(
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
        decimal transactionUnitsDenominator,
        Guid roundingPolicyId,
        long roundingPolicyVersion,
        int roundingScale)
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
        RoundingPolicyId = roundingPolicyId;
        RoundingPolicyVersion = roundingPolicyVersion;
        RoundingScale = roundingScale;
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
    public Guid RoundingPolicyId { get; }
    public long RoundingPolicyVersion { get; }
    public int RoundingScale { get; }

    public static PaymentRateSnapshot Create(
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
        decimal transactionUnitsDenominator,
        Guid roundingPolicyId,
        long roundingPolicyVersion,
        int roundingScale)
    {
        RequireId(tenantId, "PAYMENT_RATE_TENANT_REQUIRED", "Payment-rate tenant ID is required.");
        RequireId(companyId, "PAYMENT_RATE_COMPANY_REQUIRED", "Payment-rate company ID is required.");
        RequireId(rateSnapshotId, "PAYMENT_RATE_SNAPSHOT_REQUIRED", "Payment-rate snapshot ID is required.");
        RequireId(roundingPolicyId, "PAYMENT_ROUNDING_POLICY_REQUIRED", "Payment rounding-policy ID is required.");
        ArgumentNullException.ThrowIfNull(transactionCurrency);
        ArgumentNullException.ThrowIfNull(functionalCurrency);
        if (version <= 0)
        {
            throw new PaymentInvariantException("PAYMENT_RATE_VERSION_INVALID", "Payment-rate version must be positive.");
        }
        if (roundingPolicyVersion <= 0)
        {
            throw new PaymentInvariantException(
                "PAYMENT_ROUNDING_POLICY_VERSION_INVALID",
                "Payment rounding-policy version must be positive.");
        }
        if (roundingScale is < 0 or > 4)
        {
            throw new PaymentInvariantException(
                "PAYMENT_ROUNDING_SCALE_INVALID",
                "Payment rounding scale must be between zero and four.");
        }
        string canonicalRateType = RequireText(rateType, "PAYMENT_RATE_TYPE_REQUIRED", "Payment-rate type is required.");
        string canonicalSource = RequireText(source, "PAYMENT_RATE_SOURCE_REQUIRED", "Payment-rate source is required.");
        if (functionalUnitsNumerator <= decimal.Zero || DecimalScale(functionalUnitsNumerator) > 12)
        {
            throw new PaymentInvariantException(
                "PAYMENT_RATE_NUMERATOR_INVALID",
                "Payment-rate functional-units numerator must be positive with at most twelve decimal places.");
        }
        if (transactionUnitsDenominator <= decimal.Zero || DecimalScale(transactionUnitsDenominator) > 12)
        {
            throw new PaymentInvariantException(
                "PAYMENT_RATE_DENOMINATOR_INVALID",
                "Payment-rate transaction-units denominator must be positive with at most twelve decimal places.");
        }
        return new PaymentRateSnapshot(
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
            transactionUnitsDenominator,
            roundingPolicyId,
            roundingPolicyVersion,
            roundingScale);
    }

    public PaymentFunctionalAmount Calculate(decimal transactionAmount)
    {
        if (transactionAmount <= decimal.Zero || DecimalScale(transactionAmount) > 4)
        {
            throw new PaymentInvariantException(
                "PAYMENT_AMOUNT_INVALID",
                "Payment transaction amount must be positive with at most four decimal places.");
        }
        try
        {
            checked
            {
                decimal unrounded = decimal.Round(
                    transactionAmount * FunctionalUnitsNumerator / TransactionUnitsDenominator,
                    12,
                    MidpointRounding.AwayFromZero);
                decimal functional = decimal.Round(unrounded, RoundingScale, MidpointRounding.AwayFromZero);
                if (functional <= decimal.Zero)
                {
                    throw new PaymentInvariantException(
                        "PAYMENT_FUNCTIONAL_AMOUNT_ZERO",
                        "Payment conversion rounded a positive transaction amount to zero.");
                }
                return new PaymentFunctionalAmount(functional, unrounded, functional - unrounded);
            }
        }
        catch (OverflowException exception)
        {
            throw new PaymentInvariantException(
                "PAYMENT_CONVERSION_OVERFLOW",
                $"Payment currency conversion exceeded decimal range: {exception.Message}");
        }
    }

    private static int DecimalScale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xFF;

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

public readonly record struct PaymentFunctionalAmount(
    decimal FunctionalAmount,
    decimal UnroundedFunctionalAmount,
    decimal RoundingDifference);
