using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Treasury.Domain.Reconciliation;
using Npgsql;

namespace KaguERP.Modules.Treasury.Infrastructure.Persistence;

public sealed record ReconciliationProposalPersistenceResult(Guid ReconciliationId, bool Created);

public static class PostgresReconciliationProposalWriter
{
    public static async ValueTask<ReconciliationProposalPersistenceResult> PersistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        ValidatedReconciliationProposal proposal,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(proposal);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (recordedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Recorded timestamp must use the UTC offset.", nameof(recordedAt));
        }
        scope.EnsureAllowed(proposal.TenantId, proposal.CompanyId);

        const string headerSql = """
            INSERT INTO treasury.reconciliation_proposal
                (tenant_id,company_id,reconciliation_id,treasury_account_id,currency,match_count,recorded_at,recorded_by)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8)
            ON CONFLICT (tenant_id,company_id,reconciliation_id) DO NOTHING
            RETURNING reconciliation_id
            """;
        await using (var header = new NpgsqlCommand(headerSql, connection, transaction))
        {
            header.Parameters.AddWithValue(proposal.TenantId);
            header.Parameters.AddWithValue(proposal.CompanyId);
            header.Parameters.AddWithValue(proposal.ReconciliationId);
            header.Parameters.AddWithValue(proposal.TreasuryAccountId);
            header.Parameters.AddWithValue(proposal.Currency.Value);
            header.Parameters.AddWithValue(proposal.Matches.Count);
            header.Parameters.AddWithValue(recordedAt);
            header.Parameters.AddWithValue(scope.ActorId);
            if (await header.ExecuteScalarAsync(cancellationToken) is Guid insertedId)
            {
                await InsertMatchesAsync(connection, transaction, proposal, cancellationToken);
                return new ReconciliationProposalPersistenceResult(insertedId, true);
            }
        }

        const string existingSql = """
            SELECT treasury_account_id,currency,match_count,recorded_at
            FROM treasury.reconciliation_proposal
            WHERE tenant_id=$1 AND company_id=$2 AND reconciliation_id=$3
            """;
        await using (var existing = new NpgsqlCommand(existingSql, connection, transaction))
        {
            existing.Parameters.AddWithValue(proposal.TenantId);
            existing.Parameters.AddWithValue(proposal.CompanyId);
            existing.Parameters.AddWithValue(proposal.ReconciliationId);
            await using NpgsqlDataReader reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.GetGuid(0) != proposal.TreasuryAccountId ||
                reader.GetString(1) != proposal.Currency.Value || reader.GetInt32(2) != proposal.Matches.Count ||
                reader.GetFieldValue<DateTimeOffset>(3) != recordedAt)
            {
                throw new ReconciliationProposalPersistenceConflictException(proposal.ReconciliationId);
            }
        }
        await ValidateMatchesAsync(connection, transaction, proposal, cancellationToken);
        return new ReconciliationProposalPersistenceResult(proposal.ReconciliationId, false);
    }

    private static async ValueTask InsertMatchesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ValidatedReconciliationProposal proposal,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO treasury.reconciliation_proposal_match
                (tenant_id,company_id,reconciliation_id,statement_line_id,movement_id,movement_version,
                 movement_direction,movement_usable_amount,matched_amount)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9)
            """;
        foreach (ReconciliationMatchDraft match in proposal.Matches)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue(proposal.TenantId);
            command.Parameters.AddWithValue(proposal.CompanyId);
            command.Parameters.AddWithValue(proposal.ReconciliationId);
            command.Parameters.AddWithValue(match.StatementLine.StatementLineId);
            command.Parameters.AddWithValue(match.Movement.MovementId);
            command.Parameters.AddWithValue(match.Movement.Version);
            command.Parameters.AddWithValue((short)match.Movement.Direction);
            command.Parameters.AddWithValue(match.Movement.UsableAmount);
            command.Parameters.AddWithValue(match.MatchedAmount);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async ValueTask ValidateMatchesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ValidatedReconciliationProposal proposal,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT statement_line_id,movement_id,movement_version,movement_direction,
                   movement_usable_amount,matched_amount
            FROM treasury.reconciliation_proposal_match
            WHERE tenant_id=$1 AND company_id=$2 AND reconciliation_id=$3
            ORDER BY statement_line_id,movement_id
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(proposal.TenantId);
        command.Parameters.AddWithValue(proposal.CompanyId);
        command.Parameters.AddWithValue(proposal.ReconciliationId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        var index = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            if (index >= proposal.Matches.Count || !Matches(reader, proposal.Matches[index]))
            {
                throw new ReconciliationProposalPersistenceConflictException(proposal.ReconciliationId);
            }
            index++;
        }
        if (index != proposal.Matches.Count)
        {
            throw new ReconciliationProposalPersistenceConflictException(proposal.ReconciliationId);
        }
    }

    private static bool Matches(NpgsqlDataReader reader, ReconciliationMatchDraft match) =>
        reader.GetGuid(0) == match.StatementLine.StatementLineId &&
        reader.GetGuid(1) == match.Movement.MovementId &&
        reader.GetInt64(2) == match.Movement.Version &&
        reader.GetInt16(3) == (short)match.Movement.Direction &&
        reader.GetDecimal(4) == match.Movement.UsableAmount &&
        reader.GetDecimal(5) == match.MatchedAmount;
}

public sealed class ReconciliationProposalPersistenceConflictException(Guid reconciliationId)
    : InvalidOperationException("The reconciliation proposal ID already has different immutable content.")
{
    public string Code { get; } = "RECONCILIATION_PROPOSAL_CONFLICT";
    public Guid ReconciliationId { get; } = reconciliationId;
}
