namespace KaguERP.Modules.Accounting.Domain.Periods;

public sealed record PeriodLockSnapshot
{
    private PeriodLockSnapshot(
        Guid tenantId,
        Guid companyId,
        Guid periodId,
        PeriodLockScope scope,
        PeriodCloseStage stage,
        long version)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        PeriodId = periodId;
        Scope = scope;
        Stage = stage;
        Version = version;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid PeriodId { get; }
    public PeriodLockScope Scope { get; }
    public PeriodCloseStage Stage { get; }
    public long Version { get; }

    public static PeriodLockSnapshot Create(
        Guid tenantId,
        Guid companyId,
        Guid periodId,
        PeriodLockScope scope,
        PeriodCloseStage stage,
        long version)
    {
        RequireId(tenantId, "PERIOD_TENANT_REQUIRED", "Tenant ID is required.");
        RequireId(companyId, "PERIOD_COMPANY_REQUIRED", "Company ID is required.");
        RequireId(periodId, "PERIOD_ID_REQUIRED", "Period ID is required.");

        if (!Enum.IsDefined(scope))
        {
            throw new PeriodInvariantException("PERIOD_LOCK_SCOPE_INVALID", "Period lock scope is invalid.");
        }

        if (!Enum.IsDefined(stage))
        {
            throw new PeriodInvariantException("PERIOD_CLOSE_STAGE_INVALID", "Period close stage is invalid.");
        }

        if (version <= 0)
        {
            throw new PeriodInvariantException(
                "PERIOD_LOCK_VERSION_INVALID",
                "Period lock snapshot version must be greater than zero.");
        }

        return new PeriodLockSnapshot(tenantId, companyId, periodId, scope, stage, version);
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new PeriodInvariantException(code, message);
        }
    }
}
