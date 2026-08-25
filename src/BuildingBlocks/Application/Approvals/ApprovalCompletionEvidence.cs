using System.Collections.ObjectModel;

namespace KaguERP.BuildingBlocks.Application.Approvals;

public sealed record ApprovalDecisionEvidence
{
    private ApprovalDecisionEvidence(Guid decisionId, Guid approverId, DateTimeOffset decidedAt)
    {
        DecisionId = decisionId;
        ApproverId = approverId;
        DecidedAt = decidedAt;
    }

    public Guid DecisionId { get; }
    public Guid ApproverId { get; }
    public DateTimeOffset DecidedAt { get; }

    public static ApprovalDecisionEvidence Create(Guid decisionId, Guid approverId, DateTimeOffset decidedAt)
    {
        RequireId(decisionId, "APPROVAL_DECISION_ID_REQUIRED");
        RequireId(approverId, "APPROVAL_APPROVER_REQUIRED");
        if (decidedAt.Offset != TimeSpan.Zero)
        {
            throw new ApprovalEvidenceException("APPROVAL_DECIDED_AT_NOT_UTC", "Approval decision time must be UTC.");
        }

        return new ApprovalDecisionEvidence(decisionId, approverId, decidedAt);
    }

    private static void RequireId(Guid value, string code)
    {
        if (value == Guid.Empty)
        {
            throw new ApprovalEvidenceException(code, "Approval decision identifiers are required.");
        }
    }
}

public sealed class ApprovalCompletionEvidence
{
    private ApprovalCompletionEvidence(
        Guid tenantId,
        Guid companyId,
        Guid approvalInstanceId,
        Guid workflowVersionId,
        string subjectType,
        Guid subjectId,
        long subjectVersion,
        Guid makerId,
        int requiredQuorum,
        ReadOnlyCollection<ApprovalDecisionEvidence> decisions)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        ApprovalInstanceId = approvalInstanceId;
        WorkflowVersionId = workflowVersionId;
        SubjectType = subjectType;
        SubjectId = subjectId;
        SubjectVersion = subjectVersion;
        MakerId = makerId;
        RequiredQuorum = requiredQuorum;
        Decisions = decisions;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid ApprovalInstanceId { get; }
    public Guid WorkflowVersionId { get; }
    public string SubjectType { get; }
    public Guid SubjectId { get; }
    public long SubjectVersion { get; }
    public Guid MakerId { get; }
    public int RequiredQuorum { get; }
    public IReadOnlyList<ApprovalDecisionEvidence> Decisions { get; }

    public static ApprovalCompletionEvidence Create(
        Guid tenantId,
        Guid companyId,
        Guid approvalInstanceId,
        Guid workflowVersionId,
        string subjectType,
        Guid subjectId,
        long subjectVersion,
        Guid makerId,
        int requiredQuorum,
        IEnumerable<ApprovalDecisionEvidence> decisions)
    {
        RequireId(tenantId, "APPROVAL_TENANT_REQUIRED");
        RequireId(companyId, "APPROVAL_COMPANY_REQUIRED");
        RequireId(approvalInstanceId, "APPROVAL_INSTANCE_REQUIRED");
        RequireId(workflowVersionId, "APPROVAL_WORKFLOW_VERSION_REQUIRED");
        RequireId(subjectId, "APPROVAL_SUBJECT_REQUIRED");
        RequireId(makerId, "APPROVAL_MAKER_REQUIRED");
        if (string.IsNullOrWhiteSpace(subjectType))
        {
            throw new ApprovalEvidenceException("APPROVAL_SUBJECT_TYPE_REQUIRED", "Approval subject type is required.");
        }
        if (subjectVersion <= 0)
        {
            throw new ApprovalEvidenceException("APPROVAL_SUBJECT_VERSION_INVALID", "Approval subject version must be positive.");
        }
        if (requiredQuorum <= 0)
        {
            throw new ApprovalEvidenceException("APPROVAL_QUORUM_INVALID", "Approval quorum must be positive.");
        }

        ArgumentNullException.ThrowIfNull(decisions);
        ApprovalDecisionEvidence[] snapshot = decisions.ToArray();
        if (snapshot.Any(decision => decision is null))
        {
            throw new ApprovalEvidenceException("APPROVAL_DECISION_REQUIRED", "Approval decisions cannot contain null values.");
        }
        if (snapshot.Select(decision => decision.DecisionId).Distinct().Count() != snapshot.Length)
        {
            throw new ApprovalEvidenceException("APPROVAL_DECISION_DUPLICATE", "Approval decision IDs must be unique.");
        }
        if (snapshot.Select(decision => decision.ApproverId).Distinct().Count() != snapshot.Length)
        {
            throw new ApprovalEvidenceException("APPROVAL_APPROVER_NOT_DISTINCT", "Each required approval vote must belong to a distinct person.");
        }
        if (snapshot.Any(decision => decision.ApproverId == makerId))
        {
            throw new ApprovalEvidenceException("APPROVAL_MAKER_CHECKER_CONFLICT", "The maker cannot approve the same critical subject.");
        }
        if (snapshot.Length < requiredQuorum)
        {
            throw new ApprovalEvidenceException("APPROVAL_QUORUM_NOT_MET", "The required distinct-person approval quorum is not met.");
        }

        Array.Sort(snapshot, static (left, right) => left.DecidedAt.CompareTo(right.DecidedAt));
        return new ApprovalCompletionEvidence(
            tenantId, companyId, approvalInstanceId, workflowVersionId, subjectType.Trim(), subjectId,
            subjectVersion, makerId, requiredQuorum, Array.AsReadOnly(snapshot));
    }

    public void EnsureSubject(Guid tenantId, Guid companyId, string subjectType, Guid subjectId, long subjectVersion)
    {
        if (TenantId != tenantId || CompanyId != companyId || SubjectId != subjectId ||
            SubjectVersion != subjectVersion || !string.Equals(SubjectType, subjectType?.Trim(), StringComparison.Ordinal))
        {
            throw new ApprovalEvidenceException("APPROVAL_SUBJECT_MISMATCH", "Approval evidence does not match the exact subject scope and version.");
        }
    }

    private static void RequireId(Guid value, string code)
    {
        if (value == Guid.Empty)
        {
            throw new ApprovalEvidenceException(code, "Approval evidence identifiers are required.");
        }
    }
}

public sealed class ApprovalEvidenceException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
