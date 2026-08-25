using KaguERP.Modules.Accounting.Domain.Journals;

namespace KaguERP.Modules.Accounting.Domain.Currencies;

public sealed record JournalCurrencyAmountSnapshot
{
    private JournalCurrencyAmountSnapshot(
        ExchangeRateSnapshot exchangeRate,
        RoundingPolicySnapshot roundingPolicy,
        JournalAmount transactionAmount,
        JournalAmount functionalAmount,
        decimal unroundedFunctionalAmount,
        decimal roundingDifference)
    {
        ExchangeRate = exchangeRate;
        RoundingPolicy = roundingPolicy;
        TransactionAmount = transactionAmount;
        FunctionalAmount = functionalAmount;
        UnroundedFunctionalAmount = unroundedFunctionalAmount;
        RoundingDifference = roundingDifference;
    }

    public ExchangeRateSnapshot ExchangeRate { get; }

    public RoundingPolicySnapshot RoundingPolicy { get; }

    public JournalAmount TransactionAmount { get; }

    public JournalAmount FunctionalAmount { get; }

    public decimal UnroundedFunctionalAmount { get; }

    public decimal RoundingDifference { get; }

    public static JournalCurrencyAmountSnapshot Create(
        ExchangeRateSnapshot exchangeRate,
        RoundingPolicySnapshot roundingPolicy,
        JournalAmount transactionAmount)
    {
        ArgumentNullException.ThrowIfNull(exchangeRate);
        ArgumentNullException.ThrowIfNull(roundingPolicy);

        if (!transactionAmount.IsValid)
        {
            throw new CurrencyInvariantException(
                "CURRENCY_TRANSACTION_AMOUNT_INVALID",
                "Transaction amount is invalid.");
        }

        if (exchangeRate.TenantId != roundingPolicy.TenantId)
        {
            throw new CurrencyInvariantException(
                "CURRENCY_POLICY_TENANT_MISMATCH",
                "Exchange-rate and rounding-policy tenants must match.");
        }

        if (exchangeRate.CompanyId != roundingPolicy.CompanyId)
        {
            throw new CurrencyInvariantException(
                "CURRENCY_POLICY_COMPANY_MISMATCH",
                "Exchange-rate and rounding-policy companies must match.");
        }

        var transactionValue = transactionAmount.Debit > decimal.Zero
            ? transactionAmount.Debit
            : transactionAmount.Credit;

        decimal unroundedFunctionalAmount;
        decimal roundedFunctionalAmount;
        try
        {
            checked
            {
                unroundedFunctionalAmount = transactionValue * exchangeRate.FunctionalUnitsNumerator /
                    exchangeRate.TransactionUnitsDenominator;
                roundedFunctionalAmount = decimal.Round(
                    unroundedFunctionalAmount,
                    roundingPolicy.Scale,
                    roundingPolicy.ToMidpointRounding());
            }
        }
        catch (OverflowException exception)
        {
            throw new CurrencyInvariantException(
                "CURRENCY_CALCULATION_OVERFLOW",
                $"Currency conversion exceeded decimal range: {exception.Message}");
        }

        if (roundedFunctionalAmount <= decimal.Zero)
        {
            throw new CurrencyInvariantException(
                "CURRENCY_FUNCTIONAL_AMOUNT_ZERO",
                "Currency conversion rounded a positive transaction amount to zero.");
        }

        var functionalAmount = transactionAmount.Debit > decimal.Zero
            ? JournalAmount.Create(roundedFunctionalAmount, decimal.Zero)
            : JournalAmount.Create(decimal.Zero, roundedFunctionalAmount);

        return new JournalCurrencyAmountSnapshot(
            exchangeRate,
            roundingPolicy,
            transactionAmount,
            functionalAmount,
            unroundedFunctionalAmount,
            roundedFunctionalAmount - unroundedFunctionalAmount);
    }

    public JournalCurrencyAmountSnapshot Reverse()
    {
        var reversedTransactionAmount = TransactionAmount.Debit > decimal.Zero
            ? JournalAmount.Create(decimal.Zero, TransactionAmount.Debit)
            : JournalAmount.Create(TransactionAmount.Credit, decimal.Zero);

        return Create(ExchangeRate, RoundingPolicy, reversedTransactionAmount);
    }
}
