using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static class AuthSmokeFixture
{
    private const string MigratorConnectionVariable = "KAGU_ERP_MIGRATOR_CONNECTION_STRING";
    private const string IssuerVariable = "KAGU_ERP_AUTH_SMOKE_ISSUER";
    private const string SubjectVariable = "KAGU_ERP_AUTH_SMOKE_SUBJECT";
    private static readonly Guid TenantId = Guid.Parse("019cda00-0000-7000-8000-000000000001");
    private static readonly Guid CompanyId = Guid.Parse("019cda00-0000-7000-8000-000000000002");
    private static readonly Guid ActorId = Guid.Parse("019cda00-0000-7000-8000-000000000003");

    public static async Task<int> SeedAsync()
    {
        string connectionString = RequireEnvironment(MigratorConnectionVariable);
        string issuer = RequireEnvironment(IssuerVariable);
        string subject = RequireEnvironment(SubjectVariable);

        await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await SetSchemaOwnerRoleAsync(connection, transaction);
        await CleanupCoreAsync(connection, transaction);

        const string tenantSql = """
            INSERT INTO org.tenant (id, code, created_by, updated_by)
            VALUES ($1, 'AUTH-SMOKE', $2, $2)
            """;
        await using (var command = new NpgsqlCommand(tenantSql, connection, transaction))
        {
            command.Parameters.AddWithValue(TenantId);
            command.Parameters.AddWithValue(ActorId);
            await command.ExecuteNonQueryAsync();
        }

        const string companySql = """
            INSERT INTO org.company (id, tenant_id, code, created_by, updated_by)
            VALUES ($1, $2, 'AUTH-SMOKE', $3, $3)
            """;
        await using (var command = new NpgsqlCommand(companySql, connection, transaction))
        {
            command.Parameters.AddWithValue(CompanyId);
            command.Parameters.AddWithValue(TenantId);
            command.Parameters.AddWithValue(ActorId);
            await command.ExecuteNonQueryAsync();
        }

        const string profileSql = """
            INSERT INTO iam.user_profile
                (id, tenant_id, issuer, subject_id, created_by, updated_by)
            VALUES ($1, $2, $3, $4, $1, $1)
            """;
        await using (var command = new NpgsqlCommand(profileSql, connection, transaction))
        {
            command.Parameters.AddWithValue(ActorId);
            command.Parameters.AddWithValue(TenantId);
            command.Parameters.AddWithValue(issuer);
            command.Parameters.AddWithValue(subject);
            await command.ExecuteNonQueryAsync();
        }

        const string permissionSql = """
            INSERT INTO iam.user_company_permission
                (user_profile_id, tenant_id, company_id, permission_code, created_by)
            VALUES ($1, $2, $3, 'profile.read', $1)
            """;
        await using (var command = new NpgsqlCommand(permissionSql, connection, transaction))
        {
            command.Parameters.AddWithValue(ActorId);
            command.Parameters.AddWithValue(TenantId);
            command.Parameters.AddWithValue(CompanyId);
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        Console.WriteLine("Local authentication smoke fixture seeded.");
        return 0;
    }

    public static async Task<int> CleanupAsync()
    {
        string connectionString = RequireEnvironment(MigratorConnectionVariable);
        await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await SetSchemaOwnerRoleAsync(connection, transaction);
        await CleanupCoreAsync(connection, transaction);
        await transaction.CommitAsync();
        Console.WriteLine("Local authentication smoke fixture cleaned.");
        return 0;
    }

    private static async Task CleanupCoreAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await DeleteByIdAsync(connection, transaction,
            "DELETE FROM platform.outbox_message WHERE tenant_id = $1", TenantId);
        await DeleteByIdAsync(connection, transaction,
            "DELETE FROM platform.audit_event WHERE tenant_id = $1", TenantId);
        await DeleteByIdAsync(connection, transaction,
            "DELETE FROM iam.user_company_permission WHERE user_profile_id = $1", ActorId);
        await DeleteByIdAsync(connection, transaction,
            "DELETE FROM iam.user_profile WHERE id = $1", ActorId);
        await DeleteByIdAsync(connection, transaction,
            "DELETE FROM org.company WHERE id = $1", CompanyId);
        await DeleteByIdAsync(connection, transaction,
            "DELETE FROM org.tenant WHERE id = $1", TenantId);
    }

    private static async Task DeleteByIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        Guid id)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(id);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SetSchemaOwnerRoleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction)
    {
        await using var command = new NpgsqlCommand("SET LOCAL ROLE kagu_erp_schema_owner", connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static string RequireEnvironment(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{name} is required.")
            : value;
    }
}
