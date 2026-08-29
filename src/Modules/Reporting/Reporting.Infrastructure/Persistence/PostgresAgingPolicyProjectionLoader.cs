using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Domain.PartyReports;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public static class PostgresAgingPolicyProjectionLoader
{
    public static async ValueTask<CalendarDayAgingPolicySnapshot?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        Guid projectionGenerationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (projectionGenerationId == Guid.Empty)
        {
            throw new ArgumentException("Projection generation ID is required.", nameof(projectionGenerationId));
        }
        scope.EnsureAllowed(scope.TenantId, companyId);

        const string headerSql = """
            SELECT policy_id,policy_version
            FROM reporting.aging_policy_projection_snapshot
            WHERE tenant_id=$1 AND company_id=$2 AND projection_generation_id=$3
            """;
        Guid policyId;
        long policyVersion;
        await using (var header = new NpgsqlCommand(headerSql, connection, transaction))
        {
            header.Parameters.AddWithValue(scope.TenantId);
            header.Parameters.AddWithValue(companyId);
            header.Parameters.AddWithValue(projectionGenerationId);
            await using NpgsqlDataReader reader = await header.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            policyId = reader.GetGuid(0);
            policyVersion = reader.GetInt64(1);
        }

        const string bucketSql = """
            SELECT bucket_code,minimum_days_overdue,maximum_days_overdue
            FROM reporting.aging_policy_projection_bucket
            WHERE tenant_id=$1 AND company_id=$2 AND projection_generation_id=$3
            ORDER BY bucket_ordinal
            """;
        var buckets = new List<CalendarDayAgingBucket>();
        await using (var command = new NpgsqlCommand(bucketSql, connection, transaction))
        {
            command.Parameters.AddWithValue(scope.TenantId);
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(projectionGenerationId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                buckets.Add(CalendarDayAgingBucket.Create(
                    reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2)));
            }
        }

        try
        {
            return CalendarDayAgingPolicySnapshot.Create(
                scope.TenantId, companyId, policyId, policyVersion, buckets);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new AgingPolicyProjectionCorruptException(projectionGenerationId, exception);
        }
    }
}

public sealed class AgingPolicyProjectionCorruptException : InvalidOperationException
{
    public AgingPolicyProjectionCorruptException(Guid projectionGenerationId, Exception innerException)
        : base("Persisted aging policy projection cannot be reconstructed safely.", innerException)
    {
        ProjectionGenerationId = projectionGenerationId;
    }

    public string Code { get; } = "AGING_POLICY_PROJECTION_CORRUPT";
    public Guid ProjectionGenerationId { get; }
}
