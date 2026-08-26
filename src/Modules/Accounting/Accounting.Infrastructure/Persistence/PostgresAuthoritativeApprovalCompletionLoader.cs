using KaguERP.BuildingBlocks.Application.Approvals;
using KaguERP.BuildingBlocks.Application.Security;
using Npgsql;

namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

public static class PostgresAuthoritativeApprovalCompletionLoader
{
    public static async ValueTask<ApprovalCompletionEvidence> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid tenantId,
        Guid companyId,
        string subjectType,
        Guid subjectId,
        long subjectVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }

        scope.EnsureAllowed(tenantId, companyId);
        if (string.IsNullOrWhiteSpace(subjectType))
        {
            throw new ApprovalEvidenceException("APPROVAL_SUBJECT_TYPE_REQUIRED", "Approval subject type is required.");
        }
        string normalizedSubjectType = subjectType.Trim();
        const string sql = """
            SELECT c.approval_instance_id, c.workflow_version_id, c.maker_id, c.required_quorum,
                   d.decision_id, d.approver_id, d.decided_at
            FROM workflow.approval_completion_snapshot c
            JOIN workflow.approval_decision_snapshot d
              ON d.tenant_id = c.tenant_id
             AND d.company_id = c.company_id
             AND d.approval_instance_id = c.approval_instance_id
            WHERE c.tenant_id = $1 AND c.company_id = $2
              AND c.subject_type = $3 AND c.subject_id = $4 AND c.subject_version = $5
            ORDER BY d.decided_at, d.decision_id
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(companyId);
        command.Parameters.AddWithValue(normalizedSubjectType);
        command.Parameters.AddWithValue(subjectId);
        command.Parameters.AddWithValue(subjectVersion);

        Guid approvalInstanceId = Guid.Empty;
        Guid workflowVersionId = Guid.Empty;
        Guid makerId = Guid.Empty;
        int requiredQuorum = 0;
        var decisions = new List<ApprovalDecisionEvidence>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            approvalInstanceId = reader.GetGuid(0);
            workflowVersionId = reader.GetGuid(1);
            makerId = reader.GetGuid(2);
            requiredQuorum = reader.GetInt32(3);
            decisions.Add(ApprovalDecisionEvidence.Create(
                reader.GetGuid(4), reader.GetGuid(5), reader.GetFieldValue<DateTimeOffset>(6)));
        }

        if (approvalInstanceId == Guid.Empty)
        {
            throw new AuthoritativeApprovalEvidenceException(
                "APPROVAL_COMPLETION_NOT_FOUND",
                "Completed approval evidence was not found for the exact subject scope and version.");
        }

        ApprovalCompletionEvidence evidence = ApprovalCompletionEvidence.Create(
            tenantId, companyId, approvalInstanceId, workflowVersionId, normalizedSubjectType,
            subjectId, subjectVersion, makerId, requiredQuorum, decisions);
        evidence.EnsureSubject(tenantId, companyId, normalizedSubjectType, subjectId, subjectVersion);
        return evidence;
    }
}

public sealed class AuthoritativeApprovalEvidenceException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
