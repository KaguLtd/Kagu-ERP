using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Treasury.Domain.Payments;
using KaguERP.Modules.Treasury.Domain.Reconciliation;
using KaguERP.Modules.Treasury.Domain.Statements;
using Npgsql;

namespace KaguERP.Modules.Treasury.Infrastructure.Persistence;

public sealed record LoadedReconciliationProposal(
    ValidatedReconciliationProposal Proposal,
    DateTimeOffset RecordedAt);

public static class PostgresReconciliationProposalLoader
{
    public static async ValueTask<LoadedReconciliationProposal?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        Guid reconciliationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (reconciliationId == Guid.Empty)
        {
            throw new ArgumentException("Reconciliation ID is required.", nameof(reconciliationId));
        }
        scope.EnsureAllowed(scope.TenantId, companyId);

        const string headerSql = """
            SELECT treasury_account_id,currency,recorded_at
            FROM treasury.reconciliation_proposal
            WHERE tenant_id=$1 AND company_id=$2 AND reconciliation_id=$3
            """;
        Guid treasuryAccountId;
        TreasuryCurrencyCode currency;
        DateTimeOffset recordedAt;
        await using (var header = new NpgsqlCommand(headerSql, connection, transaction))
        {
            header.Parameters.AddWithValue(scope.TenantId);
            header.Parameters.AddWithValue(companyId);
            header.Parameters.AddWithValue(reconciliationId);
            await using NpgsqlDataReader reader = await header.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            treasuryAccountId = reader.GetGuid(0);
            currency = TreasuryCurrencyCode.Create(reader.GetString(1));
            recordedAt = reader.GetFieldValue<DateTimeOffset>(2);
        }

        const string matchSql = """
            SELECT m.movement_id,m.movement_version,m.movement_direction,m.movement_usable_amount,
                   m.matched_amount,s.statement_line_id,s.statement_import_id,s.source_system,
                   s.identity_kind,s.external_key,s.signed_amount,s.booking_date,s.value_date,
                   s.recorded_at,s.raw_object_sha256,s.parser_version
            FROM treasury.reconciliation_proposal_match m
            JOIN treasury.statement_line s USING (tenant_id,company_id,statement_line_id)
            WHERE m.tenant_id=$1 AND m.company_id=$2 AND m.reconciliation_id=$3
            ORDER BY m.statement_line_id,m.movement_id
            """;
        var matches = new List<ReconciliationMatchDraft>();
        await using (var command = new NpgsqlCommand(matchSql, connection, transaction))
        {
            command.Parameters.AddWithValue(scope.TenantId);
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(reconciliationId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                StatementLineExternalIdentity identity = StatementLineExternalIdentity.Create(
                    scope.TenantId, companyId, treasuryAccountId,
                    reader.GetString(7), reader.GetString(8), reader.GetString(9));
                ValidatedStatementLineDraft statement = ValidatedStatementLineDraft.Create(
                    reader.GetGuid(5), reader.GetGuid(6), identity, currency, reader.GetDecimal(10),
                    reader.GetFieldValue<DateOnly>(11), reader.GetFieldValue<DateOnly>(12),
                    reader.GetFieldValue<DateTimeOffset>(13), reader.GetString(14), reader.GetInt64(15));
                InternalMovementCapacitySnapshot movement = InternalMovementCapacitySnapshot.Create(
                    scope.TenantId, companyId, treasuryAccountId, reader.GetGuid(0), reader.GetInt64(1),
                    (PaymentDirection)reader.GetInt16(2), currency, reader.GetDecimal(3));
                matches.Add(ReconciliationMatchDraft.Create(statement, movement, reader.GetDecimal(4)));
            }
        }
        return new LoadedReconciliationProposal(
            ValidatedReconciliationProposal.Create(
                reconciliationId, scope.TenantId, companyId, treasuryAccountId, currency, matches),
            recordedAt);
    }
}
