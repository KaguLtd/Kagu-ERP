namespace KaguERP.Modules.Accounting.Domain.Journals;

public sealed record JournalPostingIdentity
{
    private JournalPostingIdentity(
        Guid tenantId,
        Guid companyId,
        string sourceType,
        Guid sourceEventId,
        string postingPurpose)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        SourceType = sourceType;
        SourceEventId = sourceEventId;
        PostingPurpose = postingPurpose;
    }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public string SourceType { get; }

    public Guid SourceEventId { get; }

    public string PostingPurpose { get; }

    public static JournalPostingIdentity Create(
        Guid tenantId,
        Guid companyId,
        string sourceType,
        Guid sourceEventId,
        string postingPurpose)
    {
        if (tenantId == Guid.Empty)
        {
            throw new JournalInvariantException("JOURNAL_TENANT_REQUIRED", "Tenant ID is required.");
        }

        if (companyId == Guid.Empty)
        {
            throw new JournalInvariantException("JOURNAL_COMPANY_REQUIRED", "Company ID is required.");
        }

        if (sourceEventId == Guid.Empty)
        {
            throw new JournalInvariantException("JOURNAL_SOURCE_REQUIRED", "Source event ID is required.");
        }

        if (string.IsNullOrWhiteSpace(sourceType))
        {
            throw new JournalInvariantException("JOURNAL_SOURCE_TYPE_REQUIRED", "Source type is required.");
        }

        if (string.IsNullOrWhiteSpace(postingPurpose))
        {
            throw new JournalInvariantException("JOURNAL_PURPOSE_REQUIRED", "Posting purpose is required.");
        }

        return new JournalPostingIdentity(
            tenantId,
            companyId,
            sourceType.Trim(),
            sourceEventId,
            postingPurpose.Trim());
    }
}
