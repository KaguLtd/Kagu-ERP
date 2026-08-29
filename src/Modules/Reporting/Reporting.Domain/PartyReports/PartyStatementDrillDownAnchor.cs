using KaguERP.Modules.Reporting.Domain.ControlAccounts;

namespace KaguERP.Modules.Reporting.Domain.PartyReports;

public sealed record PartyStatementDrillDownAnchor
{
    private PartyStatementDrillDownAnchor(
        Guid statementId,
        FinancialReportSlice reportSlice,
        PartyStatementEventSnapshot eventSnapshot,
        decimal runningExposure)
    {
        StatementId = statementId;
        ReportSlice = reportSlice;
        EventSnapshot = eventSnapshot;
        RunningExposure = runningExposure;
    }

    public Guid StatementId { get; }
    public FinancialReportSlice ReportSlice { get; }
    public PartyStatementEventSnapshot EventSnapshot { get; }
    public decimal RunningExposure { get; }

    public static PartyStatementDrillDownAnchor Create(
        Guid statementId,
        FinancialReportSlice? reportSlice,
        PartyStatementEventSnapshot? eventSnapshot,
        decimal runningExposure)
    {
        if (statementId == Guid.Empty)
        {
            throw new ReportingInvariantException(
                "PARTY_DRILL_DOWN_STATEMENT_REQUIRED", "Drill-down statement ID is required.");
        }
        ArgumentNullException.ThrowIfNull(reportSlice);
        ArgumentNullException.ThrowIfNull(eventSnapshot);
        if (eventSnapshot.TenantId != reportSlice.TenantId ||
            eventSnapshot.CompanyId != reportSlice.CompanyId ||
            eventSnapshot.Currency != reportSlice.Currency ||
            eventSnapshot.EffectiveDate > reportSlice.EffectiveAsOf ||
            eventSnapshot.RecordedAt > reportSlice.DataCutoffAt)
        {
            throw new ReportingInvariantException(
                "PARTY_DRILL_DOWN_SLICE_MISMATCH",
                "Drill-down event must belong to the exact report scope and data cut.");
        }
        if (runningExposure < decimal.Zero)
        {
            throw new ReportingInvariantException(
                "PARTY_DRILL_DOWN_RUNNING_EXPOSURE_INVALID",
                "Drill-down running exposure cannot be negative in this technical subset.");
        }
        return new PartyStatementDrillDownAnchor(statementId, reportSlice, eventSnapshot, runningExposure);
    }
}
