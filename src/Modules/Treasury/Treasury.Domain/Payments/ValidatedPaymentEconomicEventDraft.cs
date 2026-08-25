namespace KaguERP.Modules.Treasury.Domain.Payments;

public sealed class ValidatedPaymentEconomicEventDraft
{
    private ValidatedPaymentEconomicEventDraft(
        Guid paymentId,
        Guid partyAccountId,
        Guid treasuryAccountId,
        PaymentDirection direction,
        decimal transactionAmount,
        decimal functionalAmount,
        DateOnly effectiveDate,
        DateTimeOffset recordedAt,
        PaymentSourceIdentity sourceIdentity,
        SameCurrencyPaymentRateSnapshot rateSnapshot)
    {
        PaymentId = paymentId;
        PartyAccountId = partyAccountId;
        TreasuryAccountId = treasuryAccountId;
        Direction = direction;
        TransactionAmount = transactionAmount;
        FunctionalAmount = functionalAmount;
        EffectiveDate = effectiveDate;
        RecordedAt = recordedAt;
        SourceIdentity = sourceIdentity;
        RateSnapshot = rateSnapshot;
    }

    public Guid PaymentId { get; }

    public Guid TenantId => SourceIdentity.TenantId;

    public Guid CompanyId => SourceIdentity.CompanyId;

    public Guid PartyAccountId { get; }

    public Guid TreasuryAccountId { get; }

    public PaymentDirection Direction { get; }

    public decimal TransactionAmount { get; }

    public decimal FunctionalAmount { get; }

    public DateOnly EffectiveDate { get; }

    public DateTimeOffset RecordedAt { get; }

    public PaymentSourceIdentity SourceIdentity { get; }

    public SameCurrencyPaymentRateSnapshot RateSnapshot { get; }

    public static ValidatedPaymentEconomicEventDraft Create(
        Guid paymentId,
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid treasuryAccountId,
        PaymentDirection direction,
        decimal transactionAmount,
        decimal functionalAmount,
        DateOnly effectiveDate,
        DateTimeOffset recordedAt,
        string sourceType,
        Guid sourceEventId,
        string postingPurpose,
        SameCurrencyPaymentRateSnapshot? rateSnapshot)
    {
        RequireId(paymentId, "PAYMENT_ID_REQUIRED", "Payment ID is required.");
        RequireId(partyAccountId, "PAYMENT_PARTY_ACCOUNT_REQUIRED", "Payment party-account ID is required.");
        RequireId(treasuryAccountId, "PAYMENT_TREASURY_ACCOUNT_REQUIRED", "Payment treasury-account ID is required.");

        if (!Enum.IsDefined(direction))
        {
            throw new PaymentInvariantException("PAYMENT_DIRECTION_INVALID", "Payment direction is invalid.");
        }

        if (transactionAmount <= decimal.Zero || functionalAmount <= decimal.Zero)
        {
            throw new PaymentInvariantException(
                "PAYMENT_AMOUNT_INVALID",
                "Payment transaction and functional amounts must be positive.");
        }

        if (transactionAmount != functionalAmount)
        {
            throw new PaymentInvariantException(
                "PAYMENT_FUNCTIONAL_AMOUNT_MISMATCH",
                "Same-currency payment transaction and functional amounts must match exactly.");
        }

        if (recordedAt.Offset != TimeSpan.Zero)
        {
            throw new PaymentInvariantException(
                "PAYMENT_RECORDED_AT_NOT_UTC",
                "Payment recorded timestamp must use the UTC offset.");
        }

        var sourceIdentity = PaymentSourceIdentity.Create(
            tenantId,
            companyId,
            sourceType,
            sourceEventId,
            postingPurpose);
        ArgumentNullException.ThrowIfNull(rateSnapshot);

        if (rateSnapshot.TenantId != tenantId)
        {
            throw new PaymentInvariantException("PAYMENT_RATE_TENANT_MISMATCH", "Payment and rate tenants must match.");
        }

        if (rateSnapshot.CompanyId != companyId)
        {
            throw new PaymentInvariantException("PAYMENT_RATE_COMPANY_MISMATCH", "Payment and rate companies must match.");
        }

        return new ValidatedPaymentEconomicEventDraft(
            paymentId,
            partyAccountId,
            treasuryAccountId,
            direction,
            transactionAmount,
            functionalAmount,
            effectiveDate,
            recordedAt,
            sourceIdentity,
            rateSnapshot);
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new PaymentInvariantException(code, message);
        }
    }
}
