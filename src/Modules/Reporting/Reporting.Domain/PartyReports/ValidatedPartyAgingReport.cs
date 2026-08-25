using System.Collections.ObjectModel;
using KaguERP.Modules.Reporting.Domain.ControlAccounts;

namespace KaguERP.Modules.Reporting.Domain.PartyReports;

public sealed class ValidatedPartyAgingReport
{
    private ValidatedPartyAgingReport(
        Guid agingReportId,
        Guid partyAccountId,
        Guid controlAccountId,
        PartyBalanceSide balanceSide,
        decimal totalRemaining,
        FinancialReportSlice reportSlice,
        CalendarDayAgingPolicySnapshot policy,
        ReadOnlyCollection<OpenItemAgingSnapshot> items,
        ReadOnlyCollection<AgingBucketSummary> bucketSummaries)
    {
        AgingReportId = agingReportId;
        PartyAccountId = partyAccountId;
        ControlAccountId = controlAccountId;
        BalanceSide = balanceSide;
        TotalRemaining = totalRemaining;
        ReportSlice = reportSlice;
        Policy = policy;
        Items = items;
        BucketSummaries = bucketSummaries;
    }

    public Guid AgingReportId { get; }
    public Guid PartyAccountId { get; }
    public Guid ControlAccountId { get; }
    public PartyBalanceSide BalanceSide { get; }
    public decimal TotalRemaining { get; }
    public FinancialReportSlice ReportSlice { get; }
    public CalendarDayAgingPolicySnapshot Policy { get; }
    public IReadOnlyList<OpenItemAgingSnapshot> Items { get; }
    public IReadOnlyList<AgingBucketSummary> BucketSummaries { get; }

    public static ValidatedPartyAgingReport Create(
        Guid agingReportId,
        Guid partyAccountId,
        Guid controlAccountId,
        PartyBalanceSide balanceSide,
        FinancialReportSlice? reportSlice,
        CalendarDayAgingPolicySnapshot? policy,
        IEnumerable<OpenItemAgingSnapshot?>? items)
    {
        RequireId(agingReportId, "AGING_REPORT_REQUIRED", "Aging report ID is required.");
        RequireId(partyAccountId, "PARTY_REPORT_ACCOUNT_REQUIRED", "Aging party-account ID is required.");
        RequireId(controlAccountId, "PARTY_REPORT_CONTROL_ACCOUNT_REQUIRED", "Aging control-account ID is required.");
        ArgumentNullException.ThrowIfNull(reportSlice);
        ArgumentNullException.ThrowIfNull(policy);

        if (!Enum.IsDefined(balanceSide))
        {
            throw new ReportingInvariantException("PARTY_REPORT_BALANCE_SIDE_INVALID", "Party balance side is invalid.");
        }

        if (policy.TenantId != reportSlice.TenantId || policy.CompanyId != reportSlice.CompanyId)
        {
            throw new ReportingInvariantException(
                "AGING_POLICY_SCOPE_MISMATCH",
                "Aging policy and report slice tenant/company must match.");
        }

        if (items is null)
        {
            throw new ReportingInvariantException("AGING_ITEMS_REQUIRED", "Aging item collection is required.");
        }

        var copiedItems = items.ToArray();
        if (copiedItems.Any(item => item is null))
        {
            throw new ReportingInvariantException("AGING_ITEM_REQUIRED", "Aging item collection cannot contain null values.");
        }

        var validatedItems = copiedItems.Cast<OpenItemAgingSnapshot>().ToArray();
        var openItemIds = new HashSet<(Guid TenantId, Guid OpenItemId)>();
        foreach (var item in validatedItems)
        {
            EnsureItemContext(item, partyAccountId, controlAccountId, reportSlice);
            if (!openItemIds.Add((item.TenantId, item.OpenItemId)))
            {
                throw new ReportingInvariantException(
                    "AGING_OPEN_ITEM_DUPLICATE",
                    "An aging open-item ID can occur only once in a tenant.");
            }
        }

        Array.Sort(validatedItems, CompareItems);
        decimal totalRemaining;
        try
        {
            totalRemaining = validatedItems.Sum(item => item.RemainingAmount);
        }
        catch (OverflowException exception)
        {
            throw new ReportingInvariantException(
                "AGING_TOTAL_OVERFLOW",
                $"Aging total arithmetic overflowed: {exception.Message}");
        }

        var summaries = policy.Buckets
            .Select(bucket =>
            {
                var bucketItems = validatedItems.Where(item => bucket.Contains(item.DaysOverdue)).ToArray();
                return new AgingBucketSummary(
                    bucket.Code,
                    bucketItems.Length,
                    bucketItems.Sum(item => item.RemainingAmount));
            })
            .ToArray();

        if (summaries.Sum(summary => summary.RemainingAmount) != totalRemaining)
        {
            throw new ReportingInvariantException(
                "AGING_BUCKET_CROSS_FOOT_MISMATCH",
                "Aging bucket totals must exactly equal the report remaining total.");
        }

        return new ValidatedPartyAgingReport(
            agingReportId,
            partyAccountId,
            controlAccountId,
            balanceSide,
            totalRemaining,
            reportSlice,
            policy,
            Array.AsReadOnly(validatedItems),
            Array.AsReadOnly(summaries));
    }

    private static void EnsureItemContext(
        OpenItemAgingSnapshot item,
        Guid partyAccountId,
        Guid controlAccountId,
        FinancialReportSlice reportSlice)
    {
        if (item.TenantId != reportSlice.TenantId || item.CompanyId != reportSlice.CompanyId)
        {
            throw new ReportingInvariantException(
                "AGING_ITEM_SCOPE_MISMATCH",
                "Aging item tenant/company must match the report slice.");
        }

        if (item.PartyAccountId != partyAccountId || item.ControlAccountId != controlAccountId)
        {
            throw new ReportingInvariantException(
                "AGING_ITEM_ACCOUNT_MISMATCH",
                "Aging item Party/control accounts must match the report.");
        }

        if (item.Currency != reportSlice.Currency)
        {
            throw new ReportingInvariantException("AGING_ITEM_CURRENCY_MISMATCH", "Aging item currency must match the report.");
        }

        if (item.EffectiveAsOf != reportSlice.EffectiveAsOf || item.DataCutoffAt != reportSlice.DataCutoffAt)
        {
            throw new ReportingInvariantException(
                "AGING_ITEM_CUT_MISMATCH",
                "Aging item effective as-of and data cutoff must match the report slice.");
        }
    }

    private static int CompareItems(OpenItemAgingSnapshot left, OpenItemAgingSnapshot right)
    {
        var dueDateComparison = left.DueDate.CompareTo(right.DueDate);
        return dueDateComparison != 0 ? dueDateComparison : left.OpenItemId.CompareTo(right.OpenItemId);
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new ReportingInvariantException(code, message);
        }
    }
}
