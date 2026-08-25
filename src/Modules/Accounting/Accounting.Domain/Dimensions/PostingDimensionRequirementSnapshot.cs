using System.Collections.ObjectModel;

namespace KaguERP.Modules.Accounting.Domain.Dimensions;

public sealed record PostingDimensionRequirementSnapshot
{
    private PostingDimensionRequirementSnapshot(
        Guid tenantId,
        Guid companyId,
        Guid postingRuleVersionId,
        long version,
        ReadOnlyCollection<Guid> requiredDimensionIds)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        PostingRuleVersionId = postingRuleVersionId;
        Version = version;
        RequiredDimensionIds = requiredDimensionIds;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid PostingRuleVersionId { get; }
    public long Version { get; }
    public IReadOnlyList<Guid> RequiredDimensionIds { get; }

    public static PostingDimensionRequirementSnapshot Create(
        Guid tenantId,
        Guid companyId,
        Guid postingRuleVersionId,
        long version,
        IEnumerable<Guid>? requiredDimensionIds)
    {
        RequireId(tenantId, "DIMENSION_TENANT_REQUIRED", "Tenant ID is required.");
        RequireId(companyId, "DIMENSION_COMPANY_REQUIRED", "Company ID is required.");
        RequireId(
            postingRuleVersionId,
            "DIMENSION_RULE_VERSION_REQUIRED",
            "Posting-rule version ID is required.");

        if (version <= 0)
        {
            throw new DimensionInvariantException(
                "DIMENSION_REQUIREMENT_VERSION_INVALID",
                "Dimension requirement snapshot version must be greater than zero.");
        }

        if (requiredDimensionIds is null)
        {
            throw new DimensionInvariantException(
                "DIMENSION_REQUIREMENTS_REQUIRED",
                "Required-dimension collection is required; use an empty collection when no dimensions are required.");
        }

        var copiedIds = requiredDimensionIds.ToArray();
        if (copiedIds.Any(dimensionId => dimensionId == Guid.Empty))
        {
            throw new DimensionInvariantException(
                "DIMENSION_ID_REQUIRED",
                "Required dimension IDs cannot be empty.");
        }

        if (copiedIds.Distinct().Count() != copiedIds.Length)
        {
            throw new DimensionInvariantException(
                "DIMENSION_REQUIREMENT_DUPLICATE",
                "A dimension can occur only once in a posting-rule requirement snapshot.");
        }

        Array.Sort(copiedIds);
        return new PostingDimensionRequirementSnapshot(
            tenantId,
            companyId,
            postingRuleVersionId,
            version,
            Array.AsReadOnly(copiedIds));
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new DimensionInvariantException(code, message);
        }
    }
}
