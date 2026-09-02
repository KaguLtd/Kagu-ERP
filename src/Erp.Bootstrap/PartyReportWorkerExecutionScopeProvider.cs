using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Application.PartyReports;
using Npgsql;

namespace KaguERP.Bootstrap;

internal sealed class PartyReportWorkerExecutionScopeProvider(
    NpgsqlDataSource dataSource,
    PartyReportRefreshWorkerSettings settings,
    TimeProvider timeProvider)
{
    public async ValueTask<ExecutionScope> LoadAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetCandidateContextAsync(connection, transaction, cancellationToken);

        const string sql = """
            SELECT permission.company_id
            FROM iam.service_identity identity
            JOIN org.tenant tenant
              ON tenant.id=identity.tenant_id AND tenant.is_active
            JOIN iam.service_identity_company_permission permission
              ON permission.tenant_id=identity.tenant_id
             AND permission.service_identity_id=identity.id
            JOIN org.company company
              ON company.tenant_id=permission.tenant_id
             AND company.id=permission.company_id
             AND company.is_active
            WHERE identity.tenant_id=$1
              AND identity.id=$2
              AND identity.is_active
              AND identity.valid_from <= $3
              AND (identity.valid_to IS NULL OR identity.valid_to > $3)
              AND permission.permission_code=$4
              AND permission.valid_from <= $3
              AND (permission.valid_to IS NULL OR permission.valid_to > $3)
              AND permission.company_id=ANY($5)
            ORDER BY permission.company_id
            """;
        var authorizedCompanies = new List<Guid>(settings.CompanyIds.Count);
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue(settings.TenantId);
            command.Parameters.AddWithValue(settings.ActorId);
            command.Parameters.AddWithValue(now);
            command.Parameters.AddWithValue(PartyReportRefreshPermissions.Refresh);
            command.Parameters.AddWithValue(settings.CompanyIds.ToArray());
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                authorizedCompanies.Add(reader.GetGuid(0));
            }
        }
        await transaction.CommitAsync(cancellationToken);

        Guid[] expected = settings.CompanyIds.Order().ToArray();
        Guid[] actual = authorizedCompanies.Distinct().Order().ToArray();
        if (!expected.SequenceEqual(actual))
        {
            throw new PartyReportWorkerIdentityException(
                "PARTY_REPORT_WORKER_SCOPE_NOT_AUTHORIZED",
                "The configured service identity is inactive or lacks an exact company refresh permission.");
        }
        return new ExecutionScope(
            settings.TenantId,
            settings.ActorId,
            actual.Select(companyId => new CompanyAccess(
                companyId,
                [PartyReportRefreshPermissions.Refresh])));
    }

    private async ValueTask SetCandidateContextAsync(
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
        command.Parameters.AddWithValue(settings.TenantId.ToString());
        command.Parameters.AddWithValue(settings.ActorId.ToString());
        command.Parameters.AddWithValue("{" + string.Join(',', settings.CompanyIds.Order()) + "}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed class PartyReportWorkerIdentityException(string code, string message)
    : InvalidOperationException(message), IPartyReportRefreshFailure
{
    public string Code { get; } = code;
}
