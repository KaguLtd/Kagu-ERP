using KaguERP.Modules.Treasury.Domain.Payments;
using KaguERP.Modules.Treasury.Domain.Statements;

namespace KaguERP.Modules.Treasury.Domain.Reconciliation;

public sealed record ReconciliationMatchDraft
{
    private ReconciliationMatchDraft(
        ValidatedStatementLineDraft statementLine,
        InternalMovementCapacitySnapshot movement,
        decimal matchedAmount)
    {
        StatementLine = statementLine;
        Movement = movement;
        MatchedAmount = matchedAmount;
    }

    public ValidatedStatementLineDraft StatementLine { get; }

    public InternalMovementCapacitySnapshot Movement { get; }

    public decimal MatchedAmount { get; }

    public static ReconciliationMatchDraft Create(
        ValidatedStatementLineDraft? statementLine,
        InternalMovementCapacitySnapshot? movement,
        decimal matchedAmount)
    {
        ArgumentNullException.ThrowIfNull(statementLine);
        ArgumentNullException.ThrowIfNull(movement);

        if (matchedAmount <= decimal.Zero)
        {
            throw new ReconciliationInvariantException(
                "RECONCILIATION_MATCH_AMOUNT_INVALID",
                "Reconciliation matched amount must be positive.");
        }

        if (statementLine.TenantId != movement.TenantId)
        {
            throw new ReconciliationInvariantException(
                "RECONCILIATION_MATCH_TENANT_MISMATCH",
                "Statement line and internal movement tenants must match.");
        }

        if (statementLine.CompanyId != movement.CompanyId)
        {
            throw new ReconciliationInvariantException(
                "RECONCILIATION_MATCH_COMPANY_MISMATCH",
                "Statement line and internal movement companies must match.");
        }

        if (statementLine.TreasuryAccountId != movement.TreasuryAccountId)
        {
            throw new ReconciliationInvariantException(
                "RECONCILIATION_MATCH_ACCOUNT_MISMATCH",
                "Statement line and internal movement treasury accounts must match.");
        }

        if (statementLine.Currency != movement.Currency)
        {
            throw new ReconciliationInvariantException(
                "RECONCILIATION_MATCH_CURRENCY_MISMATCH",
                "Statement line and internal movement currencies must match.");
        }

        var expectedDirection = statementLine.SignedAmount > decimal.Zero
            ? PaymentDirection.Incoming
            : PaymentDirection.Outgoing;
        if (movement.Direction != expectedDirection)
        {
            throw new ReconciliationInvariantException(
                "RECONCILIATION_MATCH_DIRECTION_MISMATCH",
                "Statement-line sign and internal movement direction must agree.");
        }

        return new ReconciliationMatchDraft(statementLine, movement, matchedAmount);
    }
}
