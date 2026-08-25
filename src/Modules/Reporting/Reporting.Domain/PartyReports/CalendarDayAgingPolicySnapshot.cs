using System.Collections.ObjectModel;
using KaguERP.Modules.Reporting.Domain.ControlAccounts;

namespace KaguERP.Modules.Reporting.Domain.PartyReports;

public sealed class CalendarDayAgingPolicySnapshot
{
    private CalendarDayAgingPolicySnapshot(
        Guid tenantId,
        Guid companyId,
        Guid policyId,
        long version,
        ReadOnlyCollection<CalendarDayAgingBucket> buckets)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        PolicyId = policyId;
        Version = version;
        Buckets = buckets;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid PolicyId { get; }
    public long Version { get; }
    public IReadOnlyList<CalendarDayAgingBucket> Buckets { get; }

    public static CalendarDayAgingPolicySnapshot Create(
        Guid tenantId,
        Guid companyId,
        Guid policyId,
        long version,
        IEnumerable<CalendarDayAgingBucket?>? buckets)
    {
        RequireId(tenantId, "AGING_POLICY_TENANT_REQUIRED", "Aging policy tenant ID is required.");
        RequireId(companyId, "AGING_POLICY_COMPANY_REQUIRED", "Aging policy company ID is required.");
        RequireId(policyId, "AGING_POLICY_REQUIRED", "Aging policy ID is required.");

        if (version <= 0)
        {
            throw new ReportingInvariantException("AGING_POLICY_VERSION_INVALID", "Aging policy version must be positive.");
        }

        if (buckets is null)
        {
            throw new ReportingInvariantException("AGING_BUCKETS_REQUIRED", "Aging policy bucket collection is required.");
        }

        var copiedBuckets = buckets.ToArray();
        if (copiedBuckets.Length == 0)
        {
            throw new ReportingInvariantException("AGING_BUCKETS_REQUIRED", "Aging policy bucket collection is required.");
        }

        if (copiedBuckets.Any(bucket => bucket is null))
        {
            throw new ReportingInvariantException("AGING_BUCKET_REQUIRED", "Aging policy buckets cannot contain null values.");
        }

        var validatedBuckets = copiedBuckets.Cast<CalendarDayAgingBucket>().ToArray();
        if (validatedBuckets.Select(bucket => bucket.Code).Distinct(StringComparer.Ordinal).Count() != validatedBuckets.Length)
        {
            throw new ReportingInvariantException("AGING_BUCKET_DUPLICATE", "Aging bucket codes must be unique.");
        }

        Array.Sort(validatedBuckets, (left, right) => left.MinimumDaysOverdue.CompareTo(right.MinimumDaysOverdue));
        if (validatedBuckets[0].MinimumDaysOverdue != int.MinValue ||
            validatedBuckets[^1].MaximumDaysOverdue != int.MaxValue)
        {
            throw new ReportingInvariantException(
                "AGING_BUCKET_COVERAGE_INCOMPLETE",
                "Aging buckets must cover the complete integer day range.");
        }

        for (var index = 1; index < validatedBuckets.Length; index++)
        {
            var previousMaximum = validatedBuckets[index - 1].MaximumDaysOverdue;
            if (previousMaximum == int.MaxValue ||
                validatedBuckets[index].MinimumDaysOverdue != previousMaximum + 1)
            {
                throw new ReportingInvariantException(
                    "AGING_BUCKET_COVERAGE_INVALID",
                    "Aging bucket ranges must be contiguous and non-overlapping.");
            }
        }

        return new CalendarDayAgingPolicySnapshot(
            tenantId,
            companyId,
            policyId,
            version,
            Array.AsReadOnly(validatedBuckets));
    }

    public CalendarDayAgingBucket Resolve(int daysOverdue) =>
        Buckets.Single(bucket => bucket.Contains(daysOverdue));

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new ReportingInvariantException(code, message);
        }
    }
}
