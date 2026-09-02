using KaguERP.BuildingBlocks.Application.Approvals;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Treasury.Domain.Reconciliation;

namespace KaguERP.Modules.Treasury.Application.Reconciliation;

public sealed class AuthorizedReconciliationApproval
{
    public const string RequiredPermission = "treasury.reconciliation.approve";
    public const string ApprovalSubjectType = "treasury.reconciliation-proposal";
    public const long ApprovalSubjectVersion = 1;

    private AuthorizedReconciliationApproval(
        ExecutionScope scope,
        ValidatedReconciliationProposal proposal,
        Guid proposalMakerId,
        ApprovalCompletionEvidence approvalEvidence)
    {
        Scope = scope;
        Proposal = proposal;
        ProposalMakerId = proposalMakerId;
        ApprovalEvidence = approvalEvidence;
    }

    public ExecutionScope Scope { get; }

    public ValidatedReconciliationProposal Proposal { get; }

    public Guid ProposalMakerId { get; }

    public ApprovalCompletionEvidence ApprovalEvidence { get; }

    public static AuthorizedReconciliationApproval Create(
        ExecutionScope scope,
        ValidatedReconciliationProposal proposal,
        Guid proposalMakerId,
        ApprovalCompletionEvidence approvalEvidence)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(approvalEvidence);
        if (proposalMakerId == Guid.Empty)
        {
            throw new ReconciliationApprovalInvariantException(
                "RECONCILIATION_PROPOSAL_MAKER_REQUIRED",
                "Reconciliation proposal maker ID is required.");
        }

        scope.EnsureAllowed(proposal.TenantId, proposal.CompanyId);
        if (!scope.HasPermission(proposal.CompanyId, RequiredPermission))
        {
            throw new ReconciliationApprovalAuthorizationException();
        }

        approvalEvidence.EnsureSubject(
            proposal.TenantId,
            proposal.CompanyId,
            ApprovalSubjectType,
            proposal.ReconciliationId,
            ApprovalSubjectVersion);
        if (approvalEvidence.MakerId != proposalMakerId)
        {
            throw new ReconciliationApprovalInvariantException(
                "RECONCILIATION_APPROVAL_MAKER_MISMATCH",
                "Approval evidence must identify the immutable proposal maker.");
        }
        if (approvalEvidence.RequiredQuorum != 1 || approvalEvidence.Decisions.Count != 1)
        {
            throw new ReconciliationApprovalInvariantException(
                "RECONCILIATION_APPROVAL_QUORUM_INVALID",
                "The initial reconciliation policy requires exactly one distinct manager approval.");
        }

        proposal.EnsureZeroTolerance();
        return new AuthorizedReconciliationApproval(scope, proposal, proposalMakerId, approvalEvidence);
    }
}

public sealed class ReconciliationApprovalAuthorizationException()
    : Exception("The active actor cannot approve a reconciliation for this company.")
{
    public string Code { get; } = "RECONCILIATION_APPROVAL_PERMISSION_REQUIRED";
}

public sealed class ReconciliationApprovalInvariantException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
