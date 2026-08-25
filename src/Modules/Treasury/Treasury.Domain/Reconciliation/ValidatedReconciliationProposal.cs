using System.Collections.ObjectModel;
using KaguERP.Modules.Treasury.Domain.Payments;
using KaguERP.Modules.Treasury.Domain.Statements;

namespace KaguERP.Modules.Treasury.Domain.Reconciliation;

public sealed class ValidatedReconciliationProposal
{
    private ValidatedReconciliationProposal(
        Guid reconciliationId,
        Guid tenantId,
        Guid companyId,
        Guid treasuryAccountId,
        TreasuryCurrencyCode currency,
        ReadOnlyCollection<ReconciliationMatchDraft> matches)
    {
        ReconciliationId = reconciliationId;
        TenantId = tenantId;
        CompanyId = companyId;
        TreasuryAccountId = treasuryAccountId;
        Currency = currency;
        Matches = matches;
    }

    public Guid ReconciliationId { get; }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public Guid TreasuryAccountId { get; }

    public TreasuryCurrencyCode Currency { get; }

    public IReadOnlyList<ReconciliationMatchDraft> Matches { get; }

    public static ValidatedReconciliationProposal Create(
        Guid reconciliationId,
        Guid tenantId,
        Guid companyId,
        Guid treasuryAccountId,
        TreasuryCurrencyCode? currency,
        IEnumerable<ReconciliationMatchDraft?>? matches)
    {
        RequireId(reconciliationId, "RECONCILIATION_ID_REQUIRED", "Reconciliation ID is required.");
        RequireId(tenantId, "RECONCILIATION_TENANT_REQUIRED", "Reconciliation tenant ID is required.");
        RequireId(companyId, "RECONCILIATION_COMPANY_REQUIRED", "Reconciliation company ID is required.");
        RequireId(
            treasuryAccountId,
            "RECONCILIATION_TREASURY_ACCOUNT_REQUIRED",
            "Reconciliation treasury-account ID is required.");
        ArgumentNullException.ThrowIfNull(currency);

        if (matches is null)
        {
            throw new ReconciliationInvariantException(
                "RECONCILIATION_MATCHES_REQUIRED",
                "Reconciliation match collection is required.");
        }

        var copiedMatches = matches.ToArray();
        if (copiedMatches.Length == 0)
        {
            throw new ReconciliationInvariantException(
                "RECONCILIATION_MATCHES_REQUIRED",
                "Reconciliation match collection is required.");
        }

        if (copiedMatches.Any(match => match is null))
        {
            throw new ReconciliationInvariantException(
                "RECONCILIATION_MATCH_REQUIRED",
                "Reconciliation match collection cannot contain null values.");
        }

        var validatedMatches = copiedMatches.Cast<ReconciliationMatchDraft>().ToArray();
        var pairs = new HashSet<(Guid StatementLineId, Guid MovementId)>();
        var statementSnapshots = new Dictionary<Guid, ValidatedStatementLineDraft>();
        var movementSnapshots = new Dictionary<Guid, InternalMovementCapacitySnapshot>();
        var statementTotals = new Dictionary<Guid, decimal>();
        var movementTotals = new Dictionary<Guid, decimal>();

        foreach (var match in validatedMatches)
        {
            EnsureProposalScope(match, tenantId, companyId, treasuryAccountId, currency);

            var statementLineId = match.StatementLine.StatementLineId;
            var movementId = match.Movement.MovementId;
            if (!pairs.Add((statementLineId, movementId)))
            {
                throw new ReconciliationInvariantException(
                    "RECONCILIATION_MATCH_DUPLICATE",
                    "A statement-line/internal-movement pair can occur only once in a proposal.");
            }

            EnsureConsistentSnapshot(
                statementSnapshots,
                statementLineId,
                match.StatementLine,
                "RECONCILIATION_STATEMENT_SNAPSHOT_CONFLICT");
            EnsureConsistentSnapshot(
                movementSnapshots,
                movementId,
                match.Movement,
                "RECONCILIATION_MOVEMENT_SNAPSHOT_CONFLICT");

            AddAmount(statementTotals, statementLineId, match.MatchedAmount);
            AddAmount(movementTotals, movementId, match.MatchedAmount);
        }

        foreach (var (statementLineId, total) in statementTotals)
        {
            if (total > statementSnapshots[statementLineId].MatchCapacity)
            {
                throw new ReconciliationInvariantException(
                    "RECONCILIATION_STATEMENT_CAPACITY_EXCEEDED",
                    "Reconciliation matches cannot exceed a statement line's absolute amount.");
            }
        }

        foreach (var (movementId, total) in movementTotals)
        {
            if (total > movementSnapshots[movementId].UsableAmount)
            {
                throw new ReconciliationInvariantException(
                    "RECONCILIATION_MOVEMENT_CAPACITY_EXCEEDED",
                    "Reconciliation matches cannot exceed an internal movement's usable amount.");
            }
        }

        Array.Sort(validatedMatches, CompareMatches);
        return new ValidatedReconciliationProposal(
            reconciliationId,
            tenantId,
            companyId,
            treasuryAccountId,
            currency,
            Array.AsReadOnly(validatedMatches));
    }

    private static void EnsureProposalScope(
        ReconciliationMatchDraft match,
        Guid tenantId,
        Guid companyId,
        Guid treasuryAccountId,
        TreasuryCurrencyCode currency)
    {
        if (match.StatementLine.TenantId != tenantId)
        {
            throw new ReconciliationInvariantException(
                "RECONCILIATION_PROPOSAL_TENANT_MISMATCH",
                "Every reconciliation match must belong to the proposal tenant.");
        }

        if (match.StatementLine.CompanyId != companyId)
        {
            throw new ReconciliationInvariantException(
                "RECONCILIATION_PROPOSAL_COMPANY_MISMATCH",
                "Every reconciliation match must belong to the proposal company.");
        }

        if (match.StatementLine.TreasuryAccountId != treasuryAccountId)
        {
            throw new ReconciliationInvariantException(
                "RECONCILIATION_PROPOSAL_ACCOUNT_MISMATCH",
                "Every reconciliation match must belong to the proposal treasury account.");
        }

        if (match.StatementLine.Currency != currency)
        {
            throw new ReconciliationInvariantException(
                "RECONCILIATION_PROPOSAL_CURRENCY_MISMATCH",
                "Every reconciliation match must use the proposal currency.");
        }
    }

    private static void EnsureConsistentSnapshot<T>(
        IDictionary<Guid, T> snapshots,
        Guid id,
        T snapshot,
        string errorCode)
        where T : notnull
    {
        if (snapshots.TryGetValue(id, out var existing) && !EqualityComparer<T>.Default.Equals(existing, snapshot))
        {
            throw new ReconciliationInvariantException(
                errorCode,
                "A repeated reconciliation participant must use one immutable snapshot.");
        }

        snapshots[id] = snapshot;
    }

    private static void AddAmount(Dictionary<Guid, decimal> totals, Guid id, decimal amount)
    {
        totals.TryGetValue(id, out var current);
        totals[id] = current + amount;
    }

    private static int CompareMatches(ReconciliationMatchDraft left, ReconciliationMatchDraft right)
    {
        var statementComparison = left.StatementLine.StatementLineId.CompareTo(right.StatementLine.StatementLineId);
        return statementComparison != 0
            ? statementComparison
            : left.Movement.MovementId.CompareTo(right.Movement.MovementId);
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new ReconciliationInvariantException(code, message);
        }
    }
}
