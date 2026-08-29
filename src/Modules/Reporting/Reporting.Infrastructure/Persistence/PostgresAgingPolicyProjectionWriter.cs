using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Domain.ControlAccounts;
using KaguERP.Modules.Reporting.Domain.PartyReports;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public sealed record AgingPolicyProjectionPersistenceResult(Guid ProjectionGenerationId, bool Created);

public static class PostgresAgingPolicyProjectionWriter
{
    public static async ValueTask<AgingPolicyProjectionPersistenceResult> PersistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        FinancialReportSlice slice,
        CalendarDayAgingPolicySnapshot policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(slice);
        ArgumentNullException.ThrowIfNull(policy);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        scope.EnsureAllowed(slice.TenantId, slice.CompanyId);
        if (policy.TenantId != slice.TenantId || policy.CompanyId != slice.CompanyId)
        {
            throw new ArgumentException("Aging policy and report slice scope must match.", nameof(policy));
        }

        const string headerSql = """
            INSERT INTO reporting.aging_policy_projection_snapshot
                (tenant_id,company_id,projection_generation_id,policy_id,policy_version,bucket_count)
            VALUES ($1,$2,$3,$4,$5,$6)
            ON CONFLICT (tenant_id,company_id,projection_generation_id) DO NOTHING
            RETURNING projection_generation_id
            """;
        await using (var header = new NpgsqlCommand(headerSql, connection, transaction))
        {
            header.Parameters.AddWithValue(slice.TenantId);
            header.Parameters.AddWithValue(slice.CompanyId);
            header.Parameters.AddWithValue(slice.ProjectionGenerationId);
            header.Parameters.AddWithValue(policy.PolicyId);
            header.Parameters.AddWithValue(policy.Version);
            header.Parameters.AddWithValue(policy.Buckets.Count);
            if (await header.ExecuteScalarAsync(cancellationToken) is Guid insertedId)
            {
                await InsertBucketsAsync(connection, transaction, slice, policy, cancellationToken);
                return new AgingPolicyProjectionPersistenceResult(insertedId, true);
            }
        }

        await ValidateExistingAsync(connection, transaction, slice, policy, cancellationToken);
        return new AgingPolicyProjectionPersistenceResult(slice.ProjectionGenerationId, false);
    }

    private static async ValueTask InsertBucketsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        FinancialReportSlice slice,
        CalendarDayAgingPolicySnapshot policy,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO reporting.aging_policy_projection_bucket
                (tenant_id,company_id,projection_generation_id,bucket_ordinal,bucket_code,
                 minimum_days_overdue,maximum_days_overdue)
            VALUES ($1,$2,$3,$4,$5,$6,$7)
            """;
        for (var index = 0; index < policy.Buckets.Count; index++)
        {
            CalendarDayAgingBucket bucket = policy.Buckets[index];
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue(slice.TenantId);
            command.Parameters.AddWithValue(slice.CompanyId);
            command.Parameters.AddWithValue(slice.ProjectionGenerationId);
            command.Parameters.AddWithValue(index + 1);
            command.Parameters.AddWithValue(bucket.Code);
            command.Parameters.AddWithValue(bucket.MinimumDaysOverdue);
            command.Parameters.AddWithValue(bucket.MaximumDaysOverdue);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async ValueTask ValidateExistingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        FinancialReportSlice slice,
        CalendarDayAgingPolicySnapshot policy,
        CancellationToken cancellationToken)
    {
        const string headerSql = """
            SELECT policy_id,policy_version,bucket_count
            FROM reporting.aging_policy_projection_snapshot
            WHERE tenant_id=$1 AND company_id=$2 AND projection_generation_id=$3
            """;
        await using (var header = new NpgsqlCommand(headerSql, connection, transaction))
        {
            header.Parameters.AddWithValue(slice.TenantId);
            header.Parameters.AddWithValue(slice.CompanyId);
            header.Parameters.AddWithValue(slice.ProjectionGenerationId);
            await using NpgsqlDataReader reader = await header.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.GetGuid(0) != policy.PolicyId ||
                reader.GetInt64(1) != policy.Version || reader.GetInt32(2) != policy.Buckets.Count)
            {
                throw new AgingPolicyProjectionPersistenceConflictException(slice.ProjectionGenerationId);
            }
        }

        const string bucketSql = """
            SELECT bucket_code,minimum_days_overdue,maximum_days_overdue
            FROM reporting.aging_policy_projection_bucket
            WHERE tenant_id=$1 AND company_id=$2 AND projection_generation_id=$3 ORDER BY bucket_ordinal
            """;
        await using var buckets = new NpgsqlCommand(bucketSql, connection, transaction);
        buckets.Parameters.AddWithValue(slice.TenantId);
        buckets.Parameters.AddWithValue(slice.CompanyId);
        buckets.Parameters.AddWithValue(slice.ProjectionGenerationId);
        await using NpgsqlDataReader bucketReader = await buckets.ExecuteReaderAsync(cancellationToken);
        var index = 0;
        while (await bucketReader.ReadAsync(cancellationToken))
        {
            if (index >= policy.Buckets.Count || bucketReader.GetString(0) != policy.Buckets[index].Code ||
                bucketReader.GetInt32(1) != policy.Buckets[index].MinimumDaysOverdue ||
                bucketReader.GetInt32(2) != policy.Buckets[index].MaximumDaysOverdue)
            {
                throw new AgingPolicyProjectionPersistenceConflictException(slice.ProjectionGenerationId);
            }
            index++;
        }
        if (index != policy.Buckets.Count)
        {
            throw new AgingPolicyProjectionPersistenceConflictException(slice.ProjectionGenerationId);
        }
    }
}

public sealed class AgingPolicyProjectionPersistenceConflictException(Guid projectionGenerationId)
    : InvalidOperationException("The projection generation already has a different immutable aging policy snapshot.")
{
    public string Code { get; } = "AGING_POLICY_PROJECTION_CONFLICT";
    public Guid ProjectionGenerationId { get; } = projectionGenerationId;
}
