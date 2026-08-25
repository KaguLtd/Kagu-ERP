using KaguERP.Modules.Reporting.Domain.ControlAccounts;

namespace KaguERP.Modules.Reporting.Domain.PartyReports;

public sealed record CalendarDayAgingBucket
{
    private CalendarDayAgingBucket(string code, int minimumDaysOverdue, int maximumDaysOverdue)
    {
        Code = code;
        MinimumDaysOverdue = minimumDaysOverdue;
        MaximumDaysOverdue = maximumDaysOverdue;
    }

    public string Code { get; }
    public int MinimumDaysOverdue { get; }
    public int MaximumDaysOverdue { get; }

    public static CalendarDayAgingBucket Create(string code, int minimumDaysOverdue, int maximumDaysOverdue)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ReportingInvariantException("AGING_BUCKET_CODE_REQUIRED", "Aging bucket code is required.");
        }

        if (minimumDaysOverdue > maximumDaysOverdue)
        {
            throw new ReportingInvariantException(
                "AGING_BUCKET_RANGE_INVALID",
                "Aging bucket minimum days cannot exceed maximum days.");
        }

        return new CalendarDayAgingBucket(code.Trim(), minimumDaysOverdue, maximumDaysOverdue);
    }

    public bool Contains(int daysOverdue) =>
        daysOverdue >= MinimumDaysOverdue && daysOverdue <= MaximumDaysOverdue;
}
