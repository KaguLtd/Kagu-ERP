using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Accounting.Domain.Currencies;
using Npgsql;

namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

/// <summary>Internal scoped immutable evidence reader. The calling use case owns business permission and audit.</summary>
public static class PostgresRoundingPolicySnapshotLoader
{
    public static async ValueTask<RoundingPolicySnapshot> LoadAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, Guid companyId, Guid policyId,
        long expectedVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Rounding evidence requires the caller's ReadCommitted transaction.");
        if (policyId == Guid.Empty || expectedVersion <= 0)
            throw new ArgumentException("Rounding policy identity and positive expected version are required.");
        await using (var context = new NpgsqlCommand("""
            SELECT set_config('app.tenant_id',$1,true),set_config('app.actor_id',$2,true),set_config('app.company_ids',$3,true)
            """, connection, transaction))
        {
            context.Parameters.AddWithValue(scope.TenantId.ToString("D"));
            context.Parameters.AddWithValue(scope.ActorId.ToString("D"));
            context.Parameters.AddWithValue("{" + companyId.ToString("D") + "}");
            await context.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var sql = new NpgsqlCommand("""
            SELECT version,scale,rounding_mode FROM accounting.rounding_policy_snapshot
            WHERE tenant_id=$1 AND company_id=$2 AND policy_id=$3
            """, connection, transaction);
        sql.Parameters.AddWithValue(scope.TenantId);
        sql.Parameters.AddWithValue(companyId);
        sql.Parameters.AddWithValue(policyId);
        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.GetInt64(0) != expectedVersion)
            throw new AuthoritativeCurrencyEvidenceException("ROUNDING_POLICY_EVIDENCE_MISMATCH", "Exact scoped rounding policy version is unavailable.");
        return RoundingPolicySnapshot.Create(scope.TenantId, companyId, policyId, reader.GetInt64(0),
            reader.GetInt16(1), (RoundingMode)reader.GetInt16(2));
    }
}
