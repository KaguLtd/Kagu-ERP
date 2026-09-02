namespace KaguERP.BuildingBlocks.Application.Approvals;

public sealed record ApprovalSubjectReference
{
    private ApprovalSubjectReference(
        Guid tenantId,
        Guid companyId,
        string subjectType,
        Guid subjectId,
        long subjectVersion)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        SubjectType = subjectType;
        SubjectId = subjectId;
        SubjectVersion = subjectVersion;
    }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public string SubjectType { get; }

    public Guid SubjectId { get; }

    public long SubjectVersion { get; }

    public static ApprovalSubjectReference Create(
        Guid tenantId,
        Guid companyId,
        string subjectType,
        Guid subjectId,
        long subjectVersion)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty || subjectId == Guid.Empty)
        {
            throw new ApprovalEvidenceException(
                "APPROVAL_SUBJECT_REFERENCE_ID_REQUIRED",
                "Approval subject reference identifiers are required.");
        }
        if (string.IsNullOrWhiteSpace(subjectType) || subjectType.Trim().Length > 120)
        {
            throw new ApprovalEvidenceException(
                "APPROVAL_SUBJECT_REFERENCE_TYPE_INVALID",
                "Approval subject reference type is required and cannot exceed 120 characters.");
        }
        if (subjectVersion <= 0)
        {
            throw new ApprovalEvidenceException(
                "APPROVAL_SUBJECT_REFERENCE_VERSION_INVALID",
                "Approval subject reference version must be positive.");
        }

        return new ApprovalSubjectReference(
            tenantId,
            companyId,
            subjectType.Trim(),
            subjectId,
            subjectVersion);
    }
}
