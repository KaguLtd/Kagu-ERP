namespace KaguERP.Modules.Treasury.Domain.Statements;

public sealed record StatementLineExternalIdentity
{
    private StatementLineExternalIdentity(
        Guid tenantId,
        Guid companyId,
        Guid treasuryAccountId,
        string sourceSystem,
        string identityKind,
        string externalKey)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        TreasuryAccountId = treasuryAccountId;
        SourceSystem = sourceSystem;
        IdentityKind = identityKind;
        ExternalKey = externalKey;
    }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public Guid TreasuryAccountId { get; }

    public string SourceSystem { get; }

    public string IdentityKind { get; }

    public string ExternalKey { get; }

    public static StatementLineExternalIdentity Create(
        Guid tenantId,
        Guid companyId,
        Guid treasuryAccountId,
        string sourceSystem,
        string identityKind,
        string externalKey)
    {
        RequireId(tenantId, "STATEMENT_TENANT_REQUIRED", "Statement-line tenant ID is required.");
        RequireId(companyId, "STATEMENT_COMPANY_REQUIRED", "Statement-line company ID is required.");
        RequireId(
            treasuryAccountId,
            "STATEMENT_TREASURY_ACCOUNT_REQUIRED",
            "Statement-line treasury-account ID is required.");

        return new StatementLineExternalIdentity(
            tenantId,
            companyId,
            treasuryAccountId,
            RequireText(sourceSystem, "STATEMENT_SOURCE_SYSTEM_REQUIRED", "Statement source system is required."),
            RequireText(identityKind, "STATEMENT_IDENTITY_KIND_REQUIRED", "Statement identity kind is required."),
            RequireText(externalKey, "STATEMENT_EXTERNAL_KEY_REQUIRED", "Statement external key is required."));
    }

    private static string RequireText(string value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new StatementInvariantException(code, message);
        }

        return value.Trim();
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new StatementInvariantException(code, message);
        }
    }
}
