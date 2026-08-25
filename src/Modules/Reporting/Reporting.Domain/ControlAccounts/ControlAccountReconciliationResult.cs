namespace KaguERP.Modules.Reporting.Domain.ControlAccounts;

public sealed class ControlAccountReconciliationResult
{
    private ControlAccountReconciliationResult(
        Guid reconciliationId,
        ControlAccountBalanceSnapshot subledger,
        ControlAccountBalanceSnapshot generalLedger,
        decimal difference)
    {
        ReconciliationId = reconciliationId;
        Subledger = subledger;
        GeneralLedger = generalLedger;
        Difference = difference;
    }

    public Guid ReconciliationId { get; }

    public ControlAccountBalanceSnapshot Subledger { get; }

    public ControlAccountBalanceSnapshot GeneralLedger { get; }

    public decimal Difference { get; }

    public bool IsReconciled => Difference == decimal.Zero;

    public static ControlAccountReconciliationResult Create(
        Guid reconciliationId,
        ControlAccountBalanceSnapshot? subledger,
        ControlAccountBalanceSnapshot? generalLedger)
    {
        if (reconciliationId == Guid.Empty)
        {
            throw new ReportingInvariantException(
                "REPORT_RECONCILIATION_REQUIRED",
                "Control-account reconciliation ID is required.");
        }

        ArgumentNullException.ThrowIfNull(subledger);
        ArgumentNullException.ThrowIfNull(generalLedger);

        if (subledger.LedgerSide != LedgerSide.Subledger || generalLedger.LedgerSide != LedgerSide.GeneralLedger)
        {
            throw new ReportingInvariantException(
                "REPORT_RECONCILIATION_LEDGER_SIDE_MISMATCH",
                "Reconciliation requires one subledger snapshot and one general-ledger snapshot.");
        }

        if (subledger.ControlAccountId != generalLedger.ControlAccountId)
        {
            throw new ReportingInvariantException(
                "REPORT_RECONCILIATION_ACCOUNT_MISMATCH",
                "Reconciliation snapshots must use the same control account.");
        }

        EnsureSameSlice(subledger.ReportSlice, generalLedger.ReportSlice);

        decimal difference;
        try
        {
            difference = subledger.ClosingBalance - generalLedger.ClosingBalance;
        }
        catch (OverflowException exception)
        {
            throw new ReportingInvariantException(
                "REPORT_RECONCILIATION_OVERFLOW",
                $"Control-account reconciliation arithmetic overflowed: {exception.Message}");
        }

        return new ControlAccountReconciliationResult(reconciliationId, subledger, generalLedger, difference);
    }

    private static void EnsureSameSlice(FinancialReportSlice left, FinancialReportSlice right)
    {
        if (left.TenantId != right.TenantId)
        {
            throw Mismatch("REPORT_RECONCILIATION_TENANT_MISMATCH", "tenant");
        }

        if (left.CompanyId != right.CompanyId)
        {
            throw Mismatch("REPORT_RECONCILIATION_COMPANY_MISMATCH", "company");
        }

        if (left.ReportCode != right.ReportCode || left.ReportDefinitionVersion != right.ReportDefinitionVersion)
        {
            throw Mismatch("REPORT_RECONCILIATION_DEFINITION_MISMATCH", "report definition");
        }

        if (left.EffectiveAsOf != right.EffectiveAsOf)
        {
            throw Mismatch("REPORT_RECONCILIATION_AS_OF_MISMATCH", "effective as-of date");
        }

        if (left.DataCutoffAt != right.DataCutoffAt)
        {
            throw Mismatch("REPORT_RECONCILIATION_DATA_CUTOFF_MISMATCH", "data cutoff");
        }

        if (left.ProjectionGenerationId != right.ProjectionGenerationId)
        {
            throw Mismatch("REPORT_RECONCILIATION_GENERATION_MISMATCH", "projection generation");
        }

        if (left.Currency != right.Currency)
        {
            throw Mismatch("REPORT_RECONCILIATION_CURRENCY_MISMATCH", "currency");
        }

        if (!left.Dimensions.HasSameSelection(right.Dimensions))
        {
            throw Mismatch("REPORT_RECONCILIATION_DIMENSION_MISMATCH", "dimension selection");
        }
    }

    private static ReportingInvariantException Mismatch(string code, string field) =>
        new(code, $"Control-account reconciliation snapshots must use the same {field}.");
}
