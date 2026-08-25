namespace KaguERP.Modules.Accounting.Domain.Accounts;

public sealed record AccountPostingSnapshot
{
    private AccountPostingSnapshot(
        Guid tenantId,
        Guid companyId,
        Guid accountId,
        Guid chartOfAccountsVersionId,
        AccountKind kind,
        bool isActive,
        long version)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        AccountId = accountId;
        ChartOfAccountsVersionId = chartOfAccountsVersionId;
        Kind = kind;
        IsActive = isActive;
        Version = version;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid AccountId { get; }
    public Guid ChartOfAccountsVersionId { get; }
    public AccountKind Kind { get; }
    public bool IsActive { get; }
    public long Version { get; }

    public static AccountPostingSnapshot Create(
        Guid tenantId,
        Guid companyId,
        Guid accountId,
        Guid chartOfAccountsVersionId,
        AccountKind kind,
        bool isActive,
        long version)
    {
        RequireId(tenantId, "ACCOUNT_TENANT_REQUIRED", "Tenant ID is required.");
        RequireId(companyId, "ACCOUNT_COMPANY_REQUIRED", "Company ID is required.");
        RequireId(accountId, "ACCOUNT_ID_REQUIRED", "Account ID is required.");
        RequireId(
            chartOfAccountsVersionId,
            "ACCOUNT_CHART_VERSION_REQUIRED",
            "Chart-of-accounts version ID is required.");

        if (!Enum.IsDefined(kind))
        {
            throw new AccountInvariantException("ACCOUNT_KIND_INVALID", "Account kind is invalid.");
        }

        if (version <= 0)
        {
            throw new AccountInvariantException(
                "ACCOUNT_SNAPSHOT_VERSION_INVALID",
                "Account snapshot version must be greater than zero.");
        }

        return new AccountPostingSnapshot(
            tenantId,
            companyId,
            accountId,
            chartOfAccountsVersionId,
            kind,
            isActive,
            version);
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new AccountInvariantException(code, message);
        }
    }
}
