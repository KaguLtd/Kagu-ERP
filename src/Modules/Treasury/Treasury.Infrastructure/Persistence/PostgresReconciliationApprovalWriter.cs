using KaguERP.BuildingBlocks.Application.Approvals;
using KaguERP.Modules.Treasury.Application.Reconciliation;
using KaguERP.Modules.Treasury.Domain.Reconciliation;
using Npgsql;

namespace KaguERP.Modules.Treasury.Infrastructure.Persistence;

public sealed record ReconciliationApprovalPersistenceResult(
    Guid ReconciliationId,
    bool Created,
    DateTimeOffset ApprovedAt,
    DateTimeOffset RecordedAt);

public static class PostgresReconciliationApprovalWriter
{
    public static async ValueTask<ReconciliationApprovalPersistenceResult> PersistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuthorizedReconciliationApproval approval,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(approval);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (recordedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Recorded timestamp must use the UTC offset.", nameof(recordedAt));
        }

        ValidatedReconciliationProposal proposal = approval.Proposal;
        approval.Scope.EnsureAllowed(proposal.TenantId, proposal.CompanyId);
        LoadedReconciliationProposal persistedProposal =
            await PostgresReconciliationProposalLoader.LoadAsync(
                connection,
                transaction,
                approval.Scope,
                proposal.CompanyId,
                proposal.ReconciliationId,
                cancellationToken)
            ?? throw new ReconciliationApprovalProposalUnavailableException(proposal.ReconciliationId);
        if (persistedProposal.RecordedBy != approval.ProposalMakerId ||
            !Matches(persistedProposal.Proposal, proposal))
        {
            throw new ReconciliationApprovalProposalConflictException(proposal.ReconciliationId);
        }

        await ValidatePaymentsAsync(connection, transaction, proposal, cancellationToken);

        ApprovalCompletionEvidence evidence = approval.ApprovalEvidence;
        ApprovalDecisionEvidence decision = evidence.Decisions.Single();
        const string insertSql = """
            INSERT INTO treasury.reconciliation_approval
                (tenant_id,company_id,reconciliation_id,approval_instance_id,workflow_version_id,
                 decision_id,maker_id,approver_id,approved_at,recorded_at,recorded_by)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)
            ON CONFLICT (tenant_id,company_id,reconciliation_id) DO NOTHING
            RETURNING reconciliation_id
            """;
        var created = false;
        await using (var insert = new NpgsqlCommand(insertSql, connection, transaction))
        {
            AddHeaderParameters(insert, approval, decision, recordedAt);
            created = await insert.ExecuteScalarAsync(cancellationToken) is Guid;
        }

        if (created)
        {
            await InsertParticipantsAsync(connection, transaction, proposal, cancellationToken);
        }
        else
        {
            await ValidateExistingAsync(
                connection,
                transaction,
                approval,
                decision,
                recordedAt,
                cancellationToken);
        }

        return new ReconciliationApprovalPersistenceResult(
            proposal.ReconciliationId,
            created,
            decision.DecidedAt,
            recordedAt);
    }

    private static async ValueTask ValidatePaymentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ValidatedReconciliationProposal proposal,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT treasury_account_id,direction,transaction_amount,transaction_currency
            FROM treasury.payment_economic_event
            WHERE tenant_id=$1 AND company_id=$2 AND payment_id=$3
            """;
        foreach (InternalMovementCapacitySnapshot movement in
                 proposal.Matches.Select(match => match.Movement).DistinctBy(item => item.MovementId))
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue(proposal.TenantId);
            command.Parameters.AddWithValue(proposal.CompanyId);
            command.Parameters.AddWithValue(movement.MovementId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new ReconciliationApprovalPaymentUnavailableException(movement.MovementId);
            }
            if (movement.Version != 1 || reader.GetGuid(0) != proposal.TreasuryAccountId ||
                reader.GetInt16(1) != (short)movement.Direction ||
                reader.GetDecimal(2) != movement.UsableAmount ||
                !string.Equals(reader.GetString(3), proposal.Currency.Value, StringComparison.Ordinal))
            {
                throw new ReconciliationApprovalPaymentConflictException(movement.MovementId);
            }
        }
    }

    private static async ValueTask InsertParticipantsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ValidatedReconciliationProposal proposal,
        CancellationToken cancellationToken)
    {
        const string statementSql = """
            INSERT INTO treasury.reconciliation_approved_statement
                (tenant_id,company_id,statement_line_id,reconciliation_id)
            VALUES ($1,$2,$3,$4)
            """;
        foreach (Guid statementLineId in proposal.Matches
                     .Select(match => match.StatementLine.StatementLineId)
                     .Distinct())
        {
            await using var command = new NpgsqlCommand(statementSql, connection, transaction);
            command.Parameters.AddWithValue(proposal.TenantId);
            command.Parameters.AddWithValue(proposal.CompanyId);
            command.Parameters.AddWithValue(statementLineId);
            command.Parameters.AddWithValue(proposal.ReconciliationId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        const string movementSql = """
            INSERT INTO treasury.reconciliation_approved_movement
                (tenant_id,company_id,movement_id,reconciliation_id)
            VALUES ($1,$2,$3,$4)
            """;
        foreach (Guid movementId in proposal.Matches.Select(match => match.Movement.MovementId).Distinct())
        {
            await using var command = new NpgsqlCommand(movementSql, connection, transaction);
            command.Parameters.AddWithValue(proposal.TenantId);
            command.Parameters.AddWithValue(proposal.CompanyId);
            command.Parameters.AddWithValue(movementId);
            command.Parameters.AddWithValue(proposal.ReconciliationId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async ValueTask ValidateExistingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuthorizedReconciliationApproval approval,
        ApprovalDecisionEvidence decision,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken)
    {
        ValidatedReconciliationProposal proposal = approval.Proposal;
        ApprovalCompletionEvidence evidence = approval.ApprovalEvidence;
        const string sql = """
            SELECT approval_instance_id,workflow_version_id,decision_id,maker_id,approver_id,
                   approved_at,recorded_at,recorded_by
            FROM treasury.reconciliation_approval
            WHERE tenant_id=$1 AND company_id=$2 AND reconciliation_id=$3
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(proposal.TenantId);
        command.Parameters.AddWithValue(proposal.CompanyId);
        command.Parameters.AddWithValue(proposal.ReconciliationId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) ||
            reader.GetGuid(0) != evidence.ApprovalInstanceId ||
            reader.GetGuid(1) != evidence.WorkflowVersionId ||
            reader.GetGuid(2) != decision.DecisionId ||
            reader.GetGuid(3) != approval.ProposalMakerId ||
            reader.GetGuid(4) != decision.ApproverId ||
            reader.GetFieldValue<DateTimeOffset>(5) != decision.DecidedAt ||
            reader.GetFieldValue<DateTimeOffset>(6) != recordedAt ||
            reader.GetGuid(7) != approval.Scope.ActorId)
        {
            throw new ReconciliationApprovalPersistenceConflictException(proposal.ReconciliationId);
        }
        await reader.DisposeAsync();

        await ValidateParticipantSetAsync(
            connection,
            transaction,
            proposal,
            "treasury.reconciliation_approved_statement",
            "statement_line_id",
            proposal.Matches.Select(match => match.StatementLine.StatementLineId),
            cancellationToken);
        await ValidateParticipantSetAsync(
            connection,
            transaction,
            proposal,
            "treasury.reconciliation_approved_movement",
            "movement_id",
            proposal.Matches.Select(match => match.Movement.MovementId),
            cancellationToken);
    }

    private static async ValueTask ValidateParticipantSetAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ValidatedReconciliationProposal proposal,
        string tableName,
        string idColumn,
        IEnumerable<Guid> expectedIds,
        CancellationToken cancellationToken)
    {
        string sql = $"""
            SELECT {idColumn}
            FROM {tableName}
            WHERE tenant_id=$1 AND company_id=$2 AND reconciliation_id=$3
            ORDER BY {idColumn}
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(proposal.TenantId);
        command.Parameters.AddWithValue(proposal.CompanyId);
        command.Parameters.AddWithValue(proposal.ReconciliationId);
        var actual = new List<Guid>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            actual.Add(reader.GetGuid(0));
        }
        if (!actual.SequenceEqual(expectedIds.Distinct().Order()))
        {
            throw new ReconciliationApprovalPersistenceConflictException(proposal.ReconciliationId);
        }
    }

    private static void AddHeaderParameters(
        NpgsqlCommand command,
        AuthorizedReconciliationApproval approval,
        ApprovalDecisionEvidence decision,
        DateTimeOffset recordedAt)
    {
        ValidatedReconciliationProposal proposal = approval.Proposal;
        ApprovalCompletionEvidence evidence = approval.ApprovalEvidence;
        command.Parameters.AddWithValue(proposal.TenantId);
        command.Parameters.AddWithValue(proposal.CompanyId);
        command.Parameters.AddWithValue(proposal.ReconciliationId);
        command.Parameters.AddWithValue(evidence.ApprovalInstanceId);
        command.Parameters.AddWithValue(evidence.WorkflowVersionId);
        command.Parameters.AddWithValue(decision.DecisionId);
        command.Parameters.AddWithValue(approval.ProposalMakerId);
        command.Parameters.AddWithValue(decision.ApproverId);
        command.Parameters.AddWithValue(decision.DecidedAt);
        command.Parameters.AddWithValue(recordedAt);
        command.Parameters.AddWithValue(approval.Scope.ActorId);
    }

    private static bool Matches(
        ValidatedReconciliationProposal persisted,
        ValidatedReconciliationProposal requested) =>
        persisted.ReconciliationId == requested.ReconciliationId &&
        persisted.TenantId == requested.TenantId &&
        persisted.CompanyId == requested.CompanyId &&
        persisted.TreasuryAccountId == requested.TreasuryAccountId &&
        persisted.Currency == requested.Currency &&
        persisted.Matches.SequenceEqual(requested.Matches);
}

public sealed class ReconciliationApprovalProposalUnavailableException(Guid reconciliationId)
    : InvalidOperationException("The reconciliation proposal is not visible in the active scope.")
{
    public string Code { get; } = "RECONCILIATION_APPROVAL_PROPOSAL_UNAVAILABLE";
    public Guid ReconciliationId { get; } = reconciliationId;
}

public sealed class ReconciliationApprovalProposalConflictException(Guid reconciliationId)
    : InvalidOperationException("The approved reconciliation proposal differs from persisted evidence.")
{
    public string Code { get; } = "RECONCILIATION_APPROVAL_PROPOSAL_CONFLICT";
    public Guid ReconciliationId { get; } = reconciliationId;
}

public sealed class ReconciliationApprovalPaymentUnavailableException(Guid paymentId)
    : InvalidOperationException("A reconciliation movement does not resolve to a visible payment economic event.")
{
    public string Code { get; } = "RECONCILIATION_APPROVAL_PAYMENT_UNAVAILABLE";
    public Guid PaymentId { get; } = paymentId;
}

public sealed class ReconciliationApprovalPaymentConflictException(Guid paymentId)
    : InvalidOperationException("A reconciliation movement conflicts with its immutable payment economic event.")
{
    public string Code { get; } = "RECONCILIATION_APPROVAL_PAYMENT_CONFLICT";
    public Guid PaymentId { get; } = paymentId;
}

public sealed class ReconciliationApprovalPersistenceConflictException(Guid reconciliationId)
    : InvalidOperationException("The approved reconciliation identity already has different immutable content.")
{
    public string Code { get; } = "RECONCILIATION_APPROVAL_CONFLICT";
    public Guid ReconciliationId { get; } = reconciliationId;
}
