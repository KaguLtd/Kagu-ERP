namespace KaguERP.Modules.Reporting.Domain.PartyReports;

public sealed record PartyStatementLine
{
    internal PartyStatementLine(PartyStatementEventSnapshot eventSnapshot, decimal runningExposure)
    {
        EventSnapshot = eventSnapshot;
        RunningExposure = runningExposure;
    }

    public PartyStatementEventSnapshot EventSnapshot { get; }

    public decimal RunningExposure { get; }
}
