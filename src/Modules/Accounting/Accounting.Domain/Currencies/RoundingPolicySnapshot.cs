namespace KaguERP.Modules.Accounting.Domain.Currencies;

public sealed record RoundingPolicySnapshot
{
    private RoundingPolicySnapshot(
        Guid tenantId,
        Guid companyId,
        Guid policyId,
        long version,
        int scale,
        RoundingMode mode)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        PolicyId = policyId;
        Version = version;
        Scale = scale;
        Mode = mode;
    }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public Guid PolicyId { get; }

    public long Version { get; }

    public int Scale { get; }

    public RoundingMode Mode { get; }

    public static RoundingPolicySnapshot Create(
        Guid tenantId,
        Guid companyId,
        Guid policyId,
        long version,
        int scale,
        RoundingMode mode)
    {
        RequireId(tenantId, "ROUNDING_TENANT_REQUIRED", "Rounding-policy tenant ID is required.");
        RequireId(companyId, "ROUNDING_COMPANY_REQUIRED", "Rounding-policy company ID is required.");
        RequireId(policyId, "ROUNDING_POLICY_REQUIRED", "Rounding-policy ID is required.");

        if (version <= 0)
        {
            throw new CurrencyInvariantException(
                "ROUNDING_POLICY_VERSION_INVALID",
                "Rounding-policy version must be positive.");
        }

        if (scale is < 0 or > 28)
        {
            throw new CurrencyInvariantException(
                "ROUNDING_SCALE_INVALID",
                "Rounding scale must be between zero and 28.");
        }

        if (!Enum.IsDefined(mode))
        {
            throw new CurrencyInvariantException("ROUNDING_MODE_INVALID", "Rounding mode is invalid.");
        }

        return new RoundingPolicySnapshot(tenantId, companyId, policyId, version, scale, mode);
    }

    internal MidpointRounding ToMidpointRounding() => Mode switch
    {
        RoundingMode.ToEven => MidpointRounding.ToEven,
        RoundingMode.AwayFromZero => MidpointRounding.AwayFromZero,
        RoundingMode.ToZero => MidpointRounding.ToZero,
        RoundingMode.ToNegativeInfinity => MidpointRounding.ToNegativeInfinity,
        RoundingMode.ToPositiveInfinity => MidpointRounding.ToPositiveInfinity,
        _ => throw new CurrencyInvariantException("ROUNDING_MODE_INVALID", "Rounding mode is invalid."),
    };

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new CurrencyInvariantException(code, message);
        }
    }
}
