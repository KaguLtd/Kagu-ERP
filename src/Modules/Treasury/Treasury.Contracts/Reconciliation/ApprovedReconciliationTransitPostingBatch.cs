using System.Collections.ObjectModel;

namespace KaguERP.Modules.Treasury.Contracts.Reconciliation;

public enum ReconciliationTransitDirection : short { Incoming = 1, Outgoing = 2 }

public sealed record ReconciliationTransitPaymentRateEvidence
{
    private ReconciliationTransitPaymentRateEvidence(
        Guid tenantId, Guid companyId, Guid rateSnapshotId, long rateVersion,
        string transactionCurrency, string functionalCurrency, string rateType, string rateSource,
        DateOnly rateDate, decimal numerator, decimal denominator,
        Guid roundingPolicyId, long roundingPolicyVersion, int roundingScale)
    {
        TenantId = tenantId; CompanyId = companyId; RateSnapshotId = rateSnapshotId; RateVersion = rateVersion;
        TransactionCurrency = transactionCurrency; FunctionalCurrency = functionalCurrency;
        RateType = rateType; RateSource = rateSource; RateDate = rateDate;
        FunctionalUnitsNumerator = numerator; TransactionUnitsDenominator = denominator;
        RoundingPolicyId = roundingPolicyId; RoundingPolicyVersion = roundingPolicyVersion;
        RoundingScale = roundingScale;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid RateSnapshotId { get; }
    public long RateVersion { get; }
    public string TransactionCurrency { get; }
    public string FunctionalCurrency { get; }
    public string RateType { get; }
    public string RateSource { get; }
    public DateOnly RateDate { get; }
    public decimal FunctionalUnitsNumerator { get; }
    public decimal TransactionUnitsDenominator { get; }
    public Guid RoundingPolicyId { get; }
    public long RoundingPolicyVersion { get; }
    public int RoundingScale { get; }

    public static ReconciliationTransitPaymentRateEvidence Create(
        Guid tenantId, Guid companyId, Guid rateSnapshotId, long rateVersion,
        string transactionCurrency, string functionalCurrency, string rateType, string rateSource,
        DateOnly rateDate, decimal functionalUnitsNumerator, decimal transactionUnitsDenominator,
        Guid roundingPolicyId, long roundingPolicyVersion, int roundingScale, short roundingMode)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty || rateSnapshotId == Guid.Empty || roundingPolicyId == Guid.Empty)
            throw Error("RECONCILIATION_TRANSIT_PAYMENT_RATE_ID_REQUIRED", "Payment rate evidence identifiers are required.");
        if (rateVersion <= 0 || roundingPolicyVersion <= 0 || roundingScale is < 0 or > 4 || roundingMode != 2)
            throw Error("RECONCILIATION_TRANSIT_PAYMENT_RATE_VERSION_INVALID", "Payment rate and AwayFromZero rounding evidence are invalid.");
        RequireCurrency(transactionCurrency); RequireCurrency(functionalCurrency);
        if (string.IsNullOrWhiteSpace(rateType) || string.IsNullOrWhiteSpace(rateSource) || rateDate == default ||
            functionalUnitsNumerator <= 0m || transactionUnitsDenominator <= 0m)
            throw Error("RECONCILIATION_TRANSIT_PAYMENT_RATE_INVALID", "Payment rate evidence is incomplete.");
        return new ReconciliationTransitPaymentRateEvidence(
            tenantId, companyId, rateSnapshotId, rateVersion, transactionCurrency, functionalCurrency,
            rateType.Trim(), rateSource.Trim(), rateDate, functionalUnitsNumerator,
            transactionUnitsDenominator, roundingPolicyId, roundingPolicyVersion, roundingScale);
    }

    public decimal CalculateFunctional(decimal transactionAmount)
    {
        ReconciliationTransitPaymentMatch.RequireMoney(transactionAmount, "RECONCILIATION_TRANSIT_MATCH_AMOUNT_INVALID");
        checked
        {
            decimal unrounded = decimal.Round(
                transactionAmount * FunctionalUnitsNumerator / TransactionUnitsDenominator,
                12, MidpointRounding.AwayFromZero);
            return decimal.Round(unrounded, RoundingScale, MidpointRounding.AwayFromZero);
        }
    }

    private static void RequireCurrency(string value)
    {
        if (value is null || value.Length != 3 || value.Any(character => character is < 'A' or > 'Z'))
            throw Error("RECONCILIATION_TRANSIT_PAYMENT_CURRENCY_INVALID", "Payment currencies must contain three uppercase ASCII letters.");
    }

    private static TreasuryPostingContractException Error(string code, string message) => new(code, message);
}

public sealed record ReconciliationTransitPaymentMatch
{
    private ReconciliationTransitPaymentMatch(
        Guid paymentId, decimal matchedAmount, decimal paymentTransactionAmount,
        decimal paymentFunctionalAmount, ReconciliationTransitPaymentRateEvidence rateEvidence)
    {
        PaymentId = paymentId; MatchedAmount = matchedAmount;
        PaymentTransactionAmount = paymentTransactionAmount; PaymentFunctionalAmount = paymentFunctionalAmount;
        RateEvidence = rateEvidence;
    }

    public Guid PaymentId { get; }
    public decimal MatchedAmount { get; }
    public decimal PaymentTransactionAmount { get; }
    public decimal PaymentFunctionalAmount { get; }
    public ReconciliationTransitPaymentRateEvidence RateEvidence { get; }

    public static ReconciliationTransitPaymentMatch Create(
        Guid paymentId, decimal matchedAmount, decimal paymentTransactionAmount,
        decimal paymentFunctionalAmount, ReconciliationTransitPaymentRateEvidence rateEvidence)
    {
        if (paymentId == Guid.Empty)
            throw new TreasuryPostingContractException("RECONCILIATION_TRANSIT_PAYMENT_REQUIRED", "Transit posting payment ID is required.");
        RequireMoney(matchedAmount, "RECONCILIATION_TRANSIT_MATCH_AMOUNT_INVALID");
        RequireMoney(paymentTransactionAmount, "RECONCILIATION_TRANSIT_PAYMENT_AMOUNT_INVALID");
        RequireMoney(paymentFunctionalAmount, "RECONCILIATION_TRANSIT_PAYMENT_FUNCTIONAL_AMOUNT_INVALID");
        ArgumentNullException.ThrowIfNull(rateEvidence);
        if (matchedAmount > paymentTransactionAmount)
            throw new TreasuryPostingContractException("RECONCILIATION_TRANSIT_PAYMENT_CAPACITY_EXCEEDED", "Matched amount exceeds payment transaction amount.");
        if (rateEvidence.CalculateFunctional(paymentTransactionAmount) != paymentFunctionalAmount)
            throw new TreasuryPostingContractException("RECONCILIATION_TRANSIT_PAYMENT_CONVERSION_MISMATCH", "Payment functional amount does not match its immutable rate evidence.");
        return new ReconciliationTransitPaymentMatch(paymentId, matchedAmount, paymentTransactionAmount, paymentFunctionalAmount, rateEvidence);
    }

    internal static void RequireMoney(decimal value, string code)
    {
        if (value <= decimal.Zero || ((decimal.GetBits(value)[3] >> 16) & 0xFF) > 4)
            throw new TreasuryPostingContractException(code, "Transit posting amounts must be positive and cannot exceed four decimal places.");
    }
}

public sealed record ReconciliationTransitStatementFact
{
    private ReconciliationTransitStatementFact(
        Guid statementLineId, DateOnly bookingDate, ReconciliationTransitDirection direction,
        decimal statementAmount, ReadOnlyCollection<ReconciliationTransitPaymentMatch> paymentMatches)
    {
        StatementLineId = statementLineId; BookingDate = bookingDate; Direction = direction;
        StatementAmount = statementAmount; PaymentMatches = paymentMatches;
    }

    public Guid StatementLineId { get; }
    public DateOnly BookingDate { get; }
    public ReconciliationTransitDirection Direction { get; }
    public decimal StatementAmount { get; }
    public IReadOnlyList<ReconciliationTransitPaymentMatch> PaymentMatches { get; }

    public static ReconciliationTransitStatementFact Create(
        Guid statementLineId, DateOnly bookingDate, ReconciliationTransitDirection direction,
        decimal statementAmount, IEnumerable<ReconciliationTransitPaymentMatch?>? paymentMatches)
    {
        if (statementLineId == Guid.Empty || bookingDate == default || !Enum.IsDefined(direction))
            throw new TreasuryPostingContractException("RECONCILIATION_TRANSIT_STATEMENT_INVALID", "Transit statement identity, booking date and direction are required.");
        ReconciliationTransitPaymentMatch.RequireMoney(statementAmount, "RECONCILIATION_TRANSIT_STATEMENT_AMOUNT_INVALID");
        ReconciliationTransitPaymentMatch?[] copied = paymentMatches?.ToArray() ?? [];
        if (copied.Length == 0 || copied.Any(match => match is null))
            throw new TreasuryPostingContractException("RECONCILIATION_TRANSIT_MATCHES_REQUIRED", "Transit payment matches are required.");
        ReconciliationTransitPaymentMatch[] validated = copied.Cast<ReconciliationTransitPaymentMatch>().ToArray();
        if (validated.Select(match => match.PaymentId).Distinct().Count() != validated.Length)
            throw new TreasuryPostingContractException("RECONCILIATION_TRANSIT_PAYMENT_DUPLICATE", "A payment can occur only once in one statement transit journal.");
        if (validated.Sum(match => match.MatchedAmount) != statementAmount)
            throw new TreasuryPostingContractException("RECONCILIATION_TRANSIT_STATEMENT_TOTAL_MISMATCH", "Transit matches must equal the statement amount exactly.");
        Array.Sort(validated, static (left, right) => left.PaymentId.CompareTo(right.PaymentId));
        return new ReconciliationTransitStatementFact(statementLineId, bookingDate, direction, statementAmount, Array.AsReadOnly(validated));
    }
}

public sealed record ApprovedReconciliationTransitPostingBatch
{
    public const string ApprovalSubjectType = "treasury.reconciliation-proposal";
    public const long ApprovalSubjectVersion = 1;

    private ApprovedReconciliationTransitPostingBatch(
        Guid tenantId, Guid companyId, Guid reconciliationId, Guid treasuryAccountId,
        string currency, string functionalCurrency, DateTimeOffset recordedAt,
        ReadOnlyCollection<ReconciliationTransitStatementFact> statements)
    {
        TenantId = tenantId; CompanyId = companyId; ReconciliationId = reconciliationId;
        TreasuryAccountId = treasuryAccountId; Currency = currency; FunctionalCurrency = functionalCurrency;
        RecordedAt = recordedAt; Statements = statements;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid ReconciliationId { get; }
    public Guid TreasuryAccountId { get; }
    public string Currency { get; }
    public string FunctionalCurrency { get; }
    public DateTimeOffset RecordedAt { get; }
    public IReadOnlyList<ReconciliationTransitStatementFact> Statements { get; }

    public static ApprovedReconciliationTransitPostingBatch Create(
        Guid tenantId, Guid companyId, Guid reconciliationId, Guid treasuryAccountId,
        string currency, DateTimeOffset recordedAt, IEnumerable<ReconciliationTransitStatementFact?>? statements)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty || reconciliationId == Guid.Empty || treasuryAccountId == Guid.Empty)
            throw new TreasuryPostingContractException("RECONCILIATION_TRANSIT_BATCH_ID_REQUIRED", "Transit batch identifiers are required.");
        ReconciliationTransitStatementFact?[] copied = statements?.ToArray() ?? [];
        if (copied.Length == 0 || copied.Any(statement => statement is null) || recordedAt.Offset != TimeSpan.Zero)
            throw new TreasuryPostingContractException("RECONCILIATION_TRANSIT_BATCH_INVALID", "Transit statements and UTC recorded time are required.");
        ReconciliationTransitStatementFact[] validated = copied.Cast<ReconciliationTransitStatementFact>().ToArray();
        if (validated.Select(statement => statement.StatementLineId).Distinct().Count() != validated.Length)
            throw new TreasuryPostingContractException("RECONCILIATION_TRANSIT_STATEMENT_DUPLICATE", "A statement line can produce only one transit journal.");
        ReconciliationTransitPaymentMatch[] matches = validated.SelectMany(statement => statement.PaymentMatches).ToArray();
        if (matches.Any(match => match.RateEvidence.TenantId != tenantId || match.RateEvidence.CompanyId != companyId ||
                !string.Equals(match.RateEvidence.TransactionCurrency, currency, StringComparison.Ordinal)))
            throw new TreasuryPostingContractException("RECONCILIATION_TRANSIT_PAYMENT_SCOPE_MISMATCH", "Payment rate evidence must match batch scope and transaction currency.");
        string[] functionalCurrencies = matches.Select(match => match.RateEvidence.FunctionalCurrency).Distinct().ToArray();
        if (functionalCurrencies.Length != 1)
            throw new TreasuryPostingContractException("RECONCILIATION_TRANSIT_FUNCTIONAL_CURRENCY_MISMATCH", "All reconciled payments must share one functional currency.");
        foreach (IGrouping<Guid, ReconciliationTransitPaymentMatch> payment in matches.GroupBy(match => match.PaymentId))
        {
            ReconciliationTransitPaymentMatch first = payment.First();
            if (payment.Any(match => match.PaymentTransactionAmount != first.PaymentTransactionAmount ||
                    match.PaymentFunctionalAmount != first.PaymentFunctionalAmount || match.RateEvidence != first.RateEvidence) ||
                payment.Sum(match => match.MatchedAmount) != first.PaymentTransactionAmount)
                throw new TreasuryPostingContractException("RECONCILIATION_TRANSIT_PAYMENT_TOTAL_MISMATCH", "Payment matches must exactly consume one immutable payment snapshot.");
        }
        Array.Sort(validated, static (left, right) => left.BookingDate != right.BookingDate
            ? left.BookingDate.CompareTo(right.BookingDate) : left.StatementLineId.CompareTo(right.StatementLineId));
        return new ApprovedReconciliationTransitPostingBatch(
            tenantId, companyId, reconciliationId, treasuryAccountId, currency,
            functionalCurrencies[0], recordedAt, Array.AsReadOnly(validated));
    }
}

public sealed class TreasuryPostingContractException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
