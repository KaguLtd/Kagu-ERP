using System.Security.Claims;
using KaguERP.BuildingBlocks.Application.Security;
using Npgsql;

namespace KaguERP.Bootstrap;

public sealed class PostgresExecutionScopeResolver(NpgsqlDataSource dataSource) : IExecutionScopeResolver
{
    public async ValueTask<ExecutionScope?> ResolveAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        string? issuer = GetSingleClaim(principal, "iss");
        string? subject = GetSingleClaim(principal, "sub");
        if (string.IsNullOrWhiteSpace(issuer) || issuer.Length > 320 ||
            string.IsNullOrWhiteSpace(subject) || subject.Length > 255)
        {
            return null;
        }

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetIdentityContextAsync(connection, transaction, issuer, subject, cancellationToken);

        const string sql = """
            SELECT profile.id, profile.tenant_id, assignment.company_id, assignment.permission_code
            FROM iam.user_profile profile
            JOIN iam.user_company_permission assignment
              ON assignment.user_profile_id = profile.id
             AND assignment.tenant_id = profile.tenant_id
            WHERE profile.is_active
              AND assignment.valid_from <= clock_timestamp()
              AND (assignment.valid_to IS NULL OR assignment.valid_to > clock_timestamp())
            ORDER BY assignment.company_id, assignment.permission_code
            """;

        Guid? actorId = null;
        Guid? tenantId = null;
        var permissionsByCompany = new Dictionary<Guid, HashSet<string>>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                Guid rowActorId = reader.GetGuid(0);
                Guid rowTenantId = reader.GetGuid(1);
                if ((actorId.HasValue && actorId.Value != rowActorId) ||
                    (tenantId.HasValue && tenantId.Value != rowTenantId))
                {
                    throw new InvalidOperationException("Identity resolved to inconsistent ERP membership rows.");
                }

                actorId = rowActorId;
                tenantId = rowTenantId;
                Guid companyId = reader.GetGuid(2);
                string permission = reader.GetString(3);
                if (!permissionsByCompany.TryGetValue(companyId, out HashSet<string>? permissions))
                {
                    permissions = new HashSet<string>(StringComparer.Ordinal);
                    permissionsByCompany.Add(companyId, permissions);
                }

                permissions.Add(permission);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        if (!actorId.HasValue || !tenantId.HasValue || permissionsByCompany.Count == 0)
        {
            return null;
        }

        return new ExecutionScope(
            tenantId.Value,
            actorId.Value,
            permissionsByCompany.Select(entry => new CompanyAccess(entry.Key, entry.Value)));
    }

    private static string? GetSingleClaim(ClaimsPrincipal principal, string claimType)
    {
        string[] values = principal.FindAll(claimType).Select(claim => claim.Value).ToArray();
        return values.Length == 1 ? values[0] : null;
    }

    private static async Task SetIdentityContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string issuer,
        string subject,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                set_config('app.identity_issuer', $1, true),
                set_config('app.identity_subject', $2, true)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(issuer);
        command.Parameters.AddWithValue(subject);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
