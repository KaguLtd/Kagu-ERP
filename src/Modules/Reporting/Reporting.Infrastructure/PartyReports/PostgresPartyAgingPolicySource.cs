using System.Data;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Application.PartyReports;
using KaguERP.Modules.Reporting.Domain.ControlAccounts;
using KaguERP.Modules.Reporting.Domain.PartyReports;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.PartyReports;

public sealed class PostgresPartyAgingPolicySource(
    NpgsqlDataSource dataSource,
    ExecutionScope scope) : IPartyAgingPolicySource
{
    public async ValueTask<CalendarDayAgingPolicySnapshot?> LoadAsync(
        Guid tenantId,
        Guid companyId,
        DateOnly effectiveAsOf,
        DateTimeOffset recordedCutoff,
        CancellationToken cancellationToken = default)
    {
        scope.EnsureAllowed(tenantId, companyId);
        if (recordedCutoff.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Recorded cutoff must use the UTC offset.", nameof(recordedCutoff));
        }

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);
        await SetExecutionContextAsync(connection, transaction, cancellationToken);

        const string headerSql = """
            SELECT policy_id, policy_version, bucket_count
            FROM reporting.aging_policy_definition
            WHERE tenant_id = $1
              AND company_id = $2
              AND effective_from <= $3
              AND recorded_at <= $4
            ORDER BY policy_version DESC
            LIMIT 1
            """;
        Guid policyId;
        long policyVersion;
        int bucketCount;
        bool found;
        await using (var header = new NpgsqlCommand(headerSql, connection, transaction))
        {
            header.Parameters.AddWithValue(tenantId);
            header.Parameters.AddWithValue(companyId);
            header.Parameters.AddWithValue(effectiveAsOf);
            header.Parameters.AddWithValue(recordedCutoff);
            await using NpgsqlDataReader reader = await header.ExecuteReaderAsync(cancellationToken);
            found = await reader.ReadAsync(cancellationToken);
            if (found)
            {
                policyId = reader.GetGuid(0);
                policyVersion = reader.GetInt64(1);
                bucketCount = reader.GetInt32(2);
            }
            else
            {
                policyId = Guid.Empty;
                policyVersion = 0;
                bucketCount = 0;
            }
        }
        if (!found)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        const string bucketSql = """
            SELECT bucket_code, minimum_days_overdue, maximum_days_overdue
            FROM reporting.aging_policy_definition_bucket
            WHERE tenant_id = $1
              AND company_id = $2
              AND policy_version = $3
            ORDER BY bucket_ordinal
            """;
        var buckets = new List<CalendarDayAgingBucket>(bucketCount);
        await using (var command = new NpgsqlCommand(bucketSql, connection, transaction))
        {
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(policyVersion);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                buckets.Add(CalendarDayAgingBucket.Create(
                    reader.GetString(0),
                    reader.GetInt32(1),
                    reader.GetInt32(2)));
            }
        }

        if (buckets.Count != bucketCount)
        {
            throw new PartyAgingPolicySourceException(
                "PARTY_AGING_POLICY_BUCKET_COUNT_MISMATCH",
                "The authoritative aging policy bucket count does not match its header.");
        }

        CalendarDayAgingPolicySnapshot policy;
        try
        {
            policy = CalendarDayAgingPolicySnapshot.Create(
                tenantId,
                companyId,
                policyId,
                policyVersion,
                buckets);
        }
        catch (ReportingInvariantException exception)
        {
            throw new PartyAgingPolicySourceException(
                "PARTY_AGING_POLICY_CORRUPT",
                "The authoritative aging policy cannot be reconstructed safely.",
                exception);
        }

        await transaction.CommitAsync(cancellationToken);
        return policy;
    }

    private async ValueTask SetExecutionContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                set_config('app.tenant_id', $1, true),
                set_config('app.actor_id', $2, true),
                set_config('app.company_ids', $3, true)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(scope.TenantId.ToString());
        command.Parameters.AddWithValue(scope.ActorId.ToString());
        command.Parameters.AddWithValue("{" + string.Join(',', scope.CompanyIds.Order()) + "}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed class PartyAgingPolicySourceException : InvalidOperationException, IPartyReportRefreshFailure
{
    public PartyAgingPolicySourceException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
