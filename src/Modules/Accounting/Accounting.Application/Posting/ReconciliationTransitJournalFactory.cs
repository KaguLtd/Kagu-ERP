using System.Collections.ObjectModel;
using KaguERP.BuildingBlocks.Application.Approvals;
using KaguERP.Modules.Accounting.Domain.Currencies;
using KaguERP.Modules.Accounting.Domain.Journals;
using KaguERP.Modules.Treasury.Contracts.Reconciliation;

namespace KaguERP.Modules.Accounting.Application.Posting;

public sealed record ReconciliationTransitAccountMapping
{
    private ReconciliationTransitAccountMapping(
        Guid tenantId,
        Guid companyId,
        Guid treasuryAccountId,
        Guid chartOfAccountsVersionId,
        Guid postingRuleVersionId,
        Guid bankControlAccountId,
        Guid incomingTransitAccountId,
        Guid outgoingTransitAccountId,
        Guid realizedFxGainAccountId,
        Guid realizedFxLossAccountId,
        ReadOnlyCollection<ReconciliationTransitCurrencyContext> currencyContexts)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        TreasuryAccountId = treasuryAccountId;
        ChartOfAccountsVersionId = chartOfAccountsVersionId;
        PostingRuleVersionId = postingRuleVersionId;
        BankControlAccountId = bankControlAccountId;
        IncomingTransitAccountId = incomingTransitAccountId;
        OutgoingTransitAccountId = outgoingTransitAccountId;
        RealizedFxGainAccountId = realizedFxGainAccountId;
        RealizedFxLossAccountId = realizedFxLossAccountId;
        CurrencyContexts = currencyContexts;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid TreasuryAccountId { get; }
    public Guid ChartOfAccountsVersionId { get; }
    public Guid PostingRuleVersionId { get; }
    public Guid BankControlAccountId { get; }
    public Guid IncomingTransitAccountId { get; }
    public Guid OutgoingTransitAccountId { get; }
    public Guid RealizedFxGainAccountId { get; }
    public Guid RealizedFxLossAccountId { get; }
    public IReadOnlyList<ReconciliationTransitCurrencyContext> CurrencyContexts { get; }

    public static ReconciliationTransitAccountMapping Create(
        Guid tenantId,
        Guid companyId,
        Guid treasuryAccountId,
        Guid chartOfAccountsVersionId,
        Guid postingRuleVersionId,
        Guid bankControlAccountId,
        Guid incomingTransitAccountId,
        Guid outgoingTransitAccountId,
        Guid realizedFxGainAccountId,
        Guid realizedFxLossAccountId,
        IEnumerable<ReconciliationTransitCurrencyContext?>? currencyContexts)
    {
        Guid[] identifiers =
        [
            tenantId,
            companyId,
            treasuryAccountId,
            chartOfAccountsVersionId,
            postingRuleVersionId,
            bankControlAccountId,
            incomingTransitAccountId,
            outgoingTransitAccountId,
            realizedFxGainAccountId,
            realizedFxLossAccountId,
        ];
        if (identifiers.Any(identifier => identifier == Guid.Empty))
        {
            throw new ReconciliationTransitPostingException(
                "RECONCILIATION_TRANSIT_MAPPING_ID_REQUIRED",
                "Transit posting account mapping identifiers are required.");
        }
        if (new[]
            {
                bankControlAccountId, incomingTransitAccountId, outgoingTransitAccountId,
                realizedFxGainAccountId, realizedFxLossAccountId,
            }.Distinct().Count() != 5)
        {
            throw new ReconciliationTransitPostingException(
                "RECONCILIATION_TRANSIT_MAPPING_ACCOUNT_DUPLICATE",
                "Bank control, transit and realized FX gain/loss accounts must be distinct.");
        }
        if (currencyContexts is null)
        {
            throw new ReconciliationTransitPostingException(
                "RECONCILIATION_TRANSIT_CURRENCY_CONTEXT_REQUIRED",
                "Transit posting requires authoritative currency evidence for every statement booking date.");
        }
        ReconciliationTransitCurrencyContext?[] copiedContexts = currencyContexts.ToArray();
        if (copiedContexts.Length == 0 || copiedContexts.Any(context => context is null))
        {
            throw new ReconciliationTransitPostingException(
                "RECONCILIATION_TRANSIT_CURRENCY_CONTEXT_REQUIRED",
                "Transit posting currency evidence cannot be empty or contain null values.");
        }
        ReconciliationTransitCurrencyContext[] validatedContexts = copiedContexts
            .Cast<ReconciliationTransitCurrencyContext>()
            .ToArray();
        if (validatedContexts.Any(context => context.ExchangeRate.TenantId != tenantId ||
                context.ExchangeRate.CompanyId != companyId ||
                context.RoundingPolicy.TenantId != tenantId ||
                context.RoundingPolicy.CompanyId != companyId) ||
            validatedContexts.Select(context => context.BookingDate).Distinct().Count() != validatedContexts.Length)
        {
            throw new ReconciliationTransitPostingException(
                "RECONCILIATION_TRANSIT_CURRENCY_CONTEXT_INVALID",
                "Transit currency evidence must have unique dates in the mapped tenant and company.");
        }
        Array.Sort(validatedContexts, static (left, right) => left.BookingDate.CompareTo(right.BookingDate));
        return new ReconciliationTransitAccountMapping(
            tenantId,
            companyId,
            treasuryAccountId,
            chartOfAccountsVersionId,
            postingRuleVersionId,
            bankControlAccountId,
            incomingTransitAccountId,
            outgoingTransitAccountId,
            realizedFxGainAccountId,
            realizedFxLossAccountId,
            Array.AsReadOnly(validatedContexts));
    }
}

public sealed record ReconciliationTransitCurrencyContext
{
    private ReconciliationTransitCurrencyContext(
        DateOnly bookingDate,
        ExchangeRateSnapshot exchangeRate,
        RoundingPolicySnapshot roundingPolicy,
        ExchangeRateSnapshot functionalIdentityRate,
        RoundingPolicySnapshot functionalRoundingPolicy)
    {
        BookingDate = bookingDate;
        ExchangeRate = exchangeRate;
        RoundingPolicy = roundingPolicy;
        FunctionalIdentityRate = functionalIdentityRate;
        FunctionalRoundingPolicy = functionalRoundingPolicy;
    }

    public DateOnly BookingDate { get; }
    public ExchangeRateSnapshot ExchangeRate { get; }
    public RoundingPolicySnapshot RoundingPolicy { get; }
    public ExchangeRateSnapshot FunctionalIdentityRate { get; }
    public RoundingPolicySnapshot FunctionalRoundingPolicy { get; }

    public static ReconciliationTransitCurrencyContext Create(
        DateOnly bookingDate,
        ExchangeRateSnapshot exchangeRate,
        RoundingPolicySnapshot roundingPolicy,
        ExchangeRateSnapshot functionalIdentityRate,
        RoundingPolicySnapshot functionalRoundingPolicy)
    {
        ArgumentNullException.ThrowIfNull(exchangeRate);
        ArgumentNullException.ThrowIfNull(roundingPolicy);
        ArgumentNullException.ThrowIfNull(functionalIdentityRate);
        ArgumentNullException.ThrowIfNull(functionalRoundingPolicy);
        if (bookingDate == default || exchangeRate.RateDate != bookingDate ||
            functionalIdentityRate.RateDate != bookingDate)
        {
            throw new ReconciliationTransitPostingException(
                "RECONCILIATION_TRANSIT_RATE_DATE_MISMATCH",
                "Transit exchange-rate evidence must use the statement booking date.");
        }
        if (exchangeRate.TenantId != roundingPolicy.TenantId ||
            exchangeRate.CompanyId != roundingPolicy.CompanyId ||
            functionalIdentityRate.TenantId != exchangeRate.TenantId ||
            functionalIdentityRate.CompanyId != exchangeRate.CompanyId ||
            functionalRoundingPolicy.TenantId != exchangeRate.TenantId ||
            functionalRoundingPolicy.CompanyId != exchangeRate.CompanyId)
        {
            throw new ReconciliationTransitPostingException(
                "RECONCILIATION_TRANSIT_CURRENCY_SCOPE_MISMATCH",
                "Transit exchange-rate and rounding evidence must share tenant and company scope.");
        }
        if (functionalIdentityRate.TransactionCurrency != exchangeRate.FunctionalCurrency ||
            functionalIdentityRate.FunctionalCurrency != exchangeRate.FunctionalCurrency ||
            functionalIdentityRate.FunctionalUnitsNumerator != functionalIdentityRate.TransactionUnitsDenominator)
        {
            throw new ReconciliationTransitPostingException(
                "RECONCILIATION_TRANSIT_FUNCTIONAL_IDENTITY_RATE_INVALID",
                "Realized FX lines require a functional-currency identity rate.");
        }
        if (roundingPolicy.Mode != RoundingMode.AwayFromZero || roundingPolicy.Scale != 2 ||
            functionalRoundingPolicy.Mode != RoundingMode.AwayFromZero || functionalRoundingPolicy.Scale != 2)
        {
            throw new ReconciliationTransitPostingException(
                "RECONCILIATION_TRANSIT_ROUNDING_POLICY_INVALID",
                "Reconciliation uses the approved two-decimal AwayFromZero monetary rounding policy.");
        }
        return new ReconciliationTransitCurrencyContext(
            bookingDate, exchangeRate, roundingPolicy, functionalIdentityRate, functionalRoundingPolicy);
    }
}

public sealed record ReconciliationTransitJournalSource(
    Guid StatementLineId,
    CanonicalJournalPreparationSource Source,
    ApprovalSubjectReference ApprovalSubject);

public static class ReconciliationTransitJournalFactory
{
    public const string SourceType = "treasury.reconciliation-statement";
    public const string PostingPurpose = "treasury.reconciliation.transit-close";
    public const long SourceVersion = 1;

    public static IReadOnlyList<ReconciliationTransitJournalSource> Create(
        ApprovedReconciliationTransitPostingBatch batch,
        ReconciliationTransitAccountMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(mapping);
        if (batch.TenantId != mapping.TenantId || batch.CompanyId != mapping.CompanyId ||
            batch.TreasuryAccountId != mapping.TreasuryAccountId)
        {
            throw new ReconciliationTransitPostingException(
                "RECONCILIATION_TRANSIT_MAPPING_SCOPE_MISMATCH",
                "Transit posting batch and account mapping must identify the same tenant, company and treasury account.");
        }
        Dictionary<DateOnly, ReconciliationTransitCurrencyContext> currencyByDate =
            mapping.CurrencyContexts.ToDictionary(context => context.BookingDate);
        if (mapping.CurrencyContexts.Any(context =>
                !string.Equals(batch.Currency, context.ExchangeRate.TransactionCurrency.Value, StringComparison.Ordinal) ||
                !string.Equals(batch.FunctionalCurrency, context.ExchangeRate.FunctionalCurrency.Value, StringComparison.Ordinal) ||
                !string.Equals(batch.FunctionalCurrency, context.FunctionalIdentityRate.FunctionalCurrency.Value, StringComparison.Ordinal)))
        {
            throw new ReconciliationTransitPostingException(
                "RECONCILIATION_TRANSIT_MAPPING_CURRENCY_MISMATCH",
                "Transit posting transaction and functional currencies must match the mapped statement evidence.");
        }

        var paymentParts = batch.Statements
            .SelectMany(statement => statement.PaymentMatches.Select(match => new
            {
                statement.StatementLineId,
                statement.Direction,
                Match = match,
            }))
            .ToArray();
        Dictionary<(Guid StatementLineId, Guid PaymentId), JournalCurrencyAmountSnapshot> paymentSnapshots =
            paymentParts.ToDictionary(
                part => (part.StatementLineId, part.Match.PaymentId),
                part => CreatePaymentSnapshot(batch, part.Match, part.Direction));
        foreach (var payment in paymentParts.GroupBy(part => part.Match.PaymentId))
        {
            decimal allocatedFunctional = payment.Sum(part =>
            {
                JournalCurrencyAmountSnapshot snapshot = paymentSnapshots[(part.StatementLineId, part.Match.PaymentId)];
                return snapshot.FunctionalAmount.Debit > decimal.Zero
                    ? snapshot.FunctionalAmount.Debit
                    : snapshot.FunctionalAmount.Credit;
            });
            if (allocatedFunctional != payment.First().Match.PaymentFunctionalAmount)
            {
                throw new ReconciliationTransitPostingException(
                    "RECONCILIATION_TRANSIT_PAYMENT_FUNCTIONAL_ALLOCATION_MISMATCH",
                    "Split payment matches do not cross-foot to the immutable payment functional amount.");
            }
        }

        ApprovalSubjectReference approvalSubject = ApprovalSubjectReference.Create(
            batch.TenantId,
            batch.CompanyId,
            ApprovedReconciliationTransitPostingBatch.ApprovalSubjectType,
            batch.ReconciliationId,
            ApprovedReconciliationTransitPostingBatch.ApprovalSubjectVersion);

        return batch.Statements.Select(statement =>
        {
            if (!currencyByDate.TryGetValue(statement.BookingDate, out ReconciliationTransitCurrencyContext? currency))
            {
                throw new ReconciliationTransitPostingException(
                    "RECONCILIATION_TRANSIT_RATE_DATE_REQUIRED",
                    "Every approved statement booking date requires authoritative currency evidence.");
            }
            JournalCurrencyAmountSnapshot bankSnapshot = JournalCurrencyAmountSnapshot.Create(
                currency.ExchangeRate,
                currency.RoundingPolicy,
                statement.Direction == ReconciliationTransitDirection.Incoming
                    ? JournalAmount.Create(statement.StatementAmount, decimal.Zero)
                    : JournalAmount.Create(decimal.Zero, statement.StatementAmount));
            decimal bankFunctional = statement.Direction == ReconciliationTransitDirection.Incoming
                ? bankSnapshot.FunctionalAmount.Debit
                : bankSnapshot.FunctionalAmount.Credit;
            decimal transitFunctional = statement.PaymentMatches.Sum(match =>
            {
                JournalCurrencyAmountSnapshot snapshot = paymentSnapshots[(statement.StatementLineId, match.PaymentId)];
                return statement.Direction == ReconciliationTransitDirection.Incoming
                    ? snapshot.FunctionalAmount.Credit
                    : snapshot.FunctionalAmount.Debit;
            });
            var lines = new List<JournalLineDraft>(statement.PaymentMatches.Count + 2);
            if (statement.Direction == ReconciliationTransitDirection.Incoming)
            {
                lines.Add(CreateLine(mapping.BankControlAccountId, statement.StatementLineId, bankSnapshot));
                lines.AddRange(statement.PaymentMatches.Select(match => CreateLine(
                    mapping.IncomingTransitAccountId,
                    match.PaymentId,
                    paymentSnapshots[(statement.StatementLineId, match.PaymentId)])));
                AddFxDifference(lines, mapping, currency, statement.StatementLineId,
                    bankFunctional - transitFunctional, positiveDifferenceIsGain: true);
            }
            else
            {
                lines.AddRange(statement.PaymentMatches.Select(match => CreateLine(
                    mapping.OutgoingTransitAccountId,
                    match.PaymentId,
                    paymentSnapshots[(statement.StatementLineId, match.PaymentId)])));
                lines.Add(CreateLine(mapping.BankControlAccountId, statement.StatementLineId, bankSnapshot));
                AddFxDifference(lines, mapping, currency, statement.StatementLineId,
                    bankFunctional - transitFunctional, positiveDifferenceIsGain: false);
            }

            ValidatedJournalDraft draft = ValidatedJournalDraft.Create(
                batch.TenantId,
                batch.CompanyId,
                statement.StatementLineId,
                mapping.PostingRuleVersionId,
                SourceType,
                PostingPurpose,
                statement.BookingDate,
                batch.RecordedAt,
                CurrencyCode.Create(batch.FunctionalCurrency),
                lines);
            return new ReconciliationTransitJournalSource(
                statement.StatementLineId,
                new CanonicalJournalPreparationSource(draft, mapping.ChartOfAccountsVersionId, SourceVersion),
                approvalSubject);
        }).ToArray();
    }

    private static JournalCurrencyAmountSnapshot CreatePaymentSnapshot(
        ApprovedReconciliationTransitPostingBatch batch,
        ReconciliationTransitPaymentMatch match,
        ReconciliationTransitDirection direction)
    {
        ReconciliationTransitPaymentRateEvidence evidence = match.RateEvidence;
        if (!string.Equals(evidence.TransactionCurrency, batch.Currency, StringComparison.Ordinal) ||
            !string.Equals(evidence.FunctionalCurrency, batch.FunctionalCurrency, StringComparison.Ordinal))
        {
            throw new ReconciliationTransitPostingException(
                "RECONCILIATION_TRANSIT_PAYMENT_CURRENCY_MISMATCH",
                "Payment rate evidence does not match reconciliation currencies.");
        }
        ExchangeRateSnapshot rate = ExchangeRateSnapshot.Create(
            evidence.TenantId, evidence.CompanyId, evidence.RateSnapshotId, evidence.RateVersion,
            CurrencyCode.Create(evidence.TransactionCurrency), CurrencyCode.Create(evidence.FunctionalCurrency),
            evidence.RateType, evidence.RateSource, evidence.RateDate,
            evidence.FunctionalUnitsNumerator, evidence.TransactionUnitsDenominator);
        RoundingPolicySnapshot rounding = RoundingPolicySnapshot.Create(
            evidence.TenantId, evidence.CompanyId, evidence.RoundingPolicyId,
            evidence.RoundingPolicyVersion, evidence.RoundingScale, RoundingMode.AwayFromZero);
        JournalAmount transactionAmount = direction == ReconciliationTransitDirection.Incoming
            ? JournalAmount.Create(decimal.Zero, match.MatchedAmount)
            : JournalAmount.Create(match.MatchedAmount, decimal.Zero);
        return JournalCurrencyAmountSnapshot.Create(rate, rounding, transactionAmount);
    }

    private static JournalLineDraft CreateLine(
        Guid accountId,
        Guid sourceLineId,
        JournalCurrencyAmountSnapshot snapshot)
    {
        return JournalLineDraft.Create(accountId, sourceLineId, snapshot.FunctionalAmount, [], snapshot);
    }

    private static void AddFxDifference(
        List<JournalLineDraft> lines,
        ReconciliationTransitAccountMapping mapping,
        ReconciliationTransitCurrencyContext currency,
        Guid statementLineId,
        decimal signedDifference,
        bool positiveDifferenceIsGain)
    {
        if (signedDifference == decimal.Zero)
        {
            return;
        }
        bool isGain = signedDifference > decimal.Zero == positiveDifferenceIsGain;
        decimal amount = Math.Abs(signedDifference);
        JournalAmount functionalTransactionAmount = isGain
            ? JournalAmount.Create(decimal.Zero, amount)
            : JournalAmount.Create(amount, decimal.Zero);
        JournalCurrencyAmountSnapshot snapshot = JournalCurrencyAmountSnapshot.Create(
            currency.FunctionalIdentityRate,
            currency.FunctionalRoundingPolicy,
            functionalTransactionAmount);
        lines.Add(CreateLine(
            isGain ? mapping.RealizedFxGainAccountId : mapping.RealizedFxLossAccountId,
            statementLineId,
            snapshot));
    }
}

public sealed class ReconciliationTransitPostingException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
