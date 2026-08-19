using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static class DatabaseIntegrationCheck
{
    private const string MigratorConnectionVariable = "KAGU_ERP_MIGRATOR_CONNECTION_STRING";
    private const string AppConnectionVariable = "KAGU_ERP_APP_CONNECTION_STRING";

    public static async Task<int> RunAsync()
    {
        string? migratorConnectionString = Environment.GetEnvironmentVariable(MigratorConnectionVariable);
        string? appConnectionString = Environment.GetEnvironmentVariable(AppConnectionVariable);
        if (string.IsNullOrWhiteSpace(migratorConnectionString) || string.IsNullOrWhiteSpace(appConnectionString))
        {
            Console.Error.WriteLine($"{MigratorConnectionVariable} and {AppConnectionVariable} are required.");
            return 2;
        }

        Guid tenantA = Guid.CreateVersion7();
        Guid tenantB = Guid.CreateVersion7();
        Guid companyA1 = Guid.CreateVersion7();
        Guid companyA2 = Guid.CreateVersion7();
        Guid companyB1 = Guid.CreateVersion7();
        Guid actorId = Guid.CreateVersion7();

        await using NpgsqlDataSource migratorDataSource = NpgsqlDataSource.Create(migratorConnectionString);
        await using NpgsqlDataSource appDataSource = NpgsqlDataSource.Create(appConnectionString);

        try
        {
            await SeedAsync(migratorDataSource, tenantA, tenantB, companyA1, companyA2, companyB1, actorId);
            await AssertRuntimeRoleAsync(appDataSource);
            await AssertScopedReadsAsync(appDataSource, tenantA, tenantB, companyA1, companyA2);
            await AssertAuthorizedWriteAsync(appDataSource, tenantA, actorId);
            await AssertCrossTenantWriteRejectedAsync(appDataSource, tenantA, tenantB, Guid.CreateVersion7(), actorId);
            await AssertDeletePrivilegeRejectedAsync(appDataSource, tenantA, companyA1);
            await AssertTransactionContextDoesNotLeakAsync(appDataSource, tenantA, companyA1);
            Console.WriteLine("PostgreSQL tenant/company RLS integration checks passed.");
            return 0;
        }
        finally
        {
            await CleanupAsync(migratorDataSource, tenantA, tenantB);
        }
    }

    private static async Task SeedAsync(
        NpgsqlDataSource dataSource,
        Guid tenantA,
        Guid tenantB,
        Guid companyA1,
        Guid companyA2,
        Guid companyB1,
        Guid actorId)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");

        const string tenantSql = """
            INSERT INTO org.tenant (id, code, created_by, updated_by)
            VALUES ($1, $2, $3, $3), ($4, $5, $3, $3)
            """;
        await using (var command = new NpgsqlCommand(tenantSql, connection, transaction))
        {
            command.Parameters.AddWithValue(tenantA);
            command.Parameters.AddWithValue($"TENANT-{tenantA:N}");
            command.Parameters.AddWithValue(actorId);
            command.Parameters.AddWithValue(tenantB);
            command.Parameters.AddWithValue($"TENANT-{tenantB:N}");
            await command.ExecuteNonQueryAsync();
        }

        const string companySql = """
            INSERT INTO org.company (id, tenant_id, code, created_by, updated_by)
            VALUES
                ($1, $2, $3, $7, $7),
                ($4, $2, $5, $7, $7),
                ($6, $8, $9, $7, $7)
            """;
        await using (var command = new NpgsqlCommand(companySql, connection, transaction))
        {
            command.Parameters.AddWithValue(companyA1);
            command.Parameters.AddWithValue(tenantA);
            command.Parameters.AddWithValue($"COMPANY-{companyA1:N}");
            command.Parameters.AddWithValue(companyA2);
            command.Parameters.AddWithValue($"COMPANY-{companyA2:N}");
            command.Parameters.AddWithValue(companyB1);
            command.Parameters.AddWithValue(actorId);
            command.Parameters.AddWithValue(tenantB);
            command.Parameters.AddWithValue($"COMPANY-{companyB1:N}");
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task AssertRuntimeRoleAsync(NpgsqlDataSource dataSource)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        const string sql = """
            SELECT roles.rolsuper, roles.rolbypassrls, table_owner.tableowner = current_user
            FROM pg_catalog.pg_roles roles
            CROSS JOIN
            (
                SELECT tableowner
                FROM pg_catalog.pg_tables
                WHERE schemaname = 'org' AND tablename = 'company'
            ) table_owner
            WHERE roles.rolname = current_user
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        Assert(await reader.ReadAsync(), "Runtime role metadata was not found.");
        Assert(!reader.GetBoolean(0), "Runtime role must not be superuser.");
        Assert(!reader.GetBoolean(1), "Runtime role must not have BYPASSRLS.");
        Assert(!reader.GetBoolean(2), "Runtime role must not own business tables.");
    }

    private static async Task AssertScopedReadsAsync(
        NpgsqlDataSource dataSource,
        Guid tenantA,
        Guid tenantB,
        Guid companyA1,
        Guid companyA2)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await SetScopeAsync(connection, transaction, tenantA, companyA1);

        Assert(await CountAsync(connection, transaction, "SELECT count(*) FROM org.tenant WHERE id = $1", tenantA) == 1,
            "Authorized tenant was not visible.");
        Assert(await CountAsync(connection, transaction, "SELECT count(*) FROM org.tenant WHERE id = $1", tenantB) == 0,
            "Cross-tenant row was visible.");
        Assert(await CountAsync(connection, transaction, "SELECT count(*) FROM org.company WHERE id = $1", companyA1) == 1,
            "Authorized company was not visible.");
        Assert(await CountAsync(connection, transaction, "SELECT count(*) FROM org.company WHERE id = $1", companyA2) == 0,
            "Unauthorized company in the same tenant was visible.");

        await transaction.CommitAsync();
    }

    private static async Task AssertCrossTenantWriteRejectedAsync(
        NpgsqlDataSource dataSource,
        Guid tenantA,
        Guid tenantB,
        Guid scopeCompany,
        Guid actorId)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await SetScopeAsync(connection, transaction, tenantA, scopeCompany);

        const string sql = """
            INSERT INTO org.company (id, tenant_id, code, created_by, updated_by)
            VALUES ($1, $2, $3, $4, $4)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(scopeCompany);
        command.Parameters.AddWithValue(tenantB);
        command.Parameters.AddWithValue($"X-{Guid.CreateVersion7():N}");
        command.Parameters.AddWithValue(actorId);

        bool rejected = false;
        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.InsufficientPrivilege)
        {
            rejected = true;
        }

        Assert(rejected, "RLS accepted a cross-tenant write.");
    }

    private static async Task AssertAuthorizedWriteAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        Guid actorId)
    {
        Guid companyId = Guid.CreateVersion7();
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await SetScopeAsync(connection, transaction, tenantId, companyId);

        const string sql = """
            INSERT INTO org.company (id, tenant_id, code, created_by, updated_by)
            VALUES ($1, $2, $3, $4, $4)
            """;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue($"A-{companyId:N}");
            command.Parameters.AddWithValue(actorId);
            Assert(await command.ExecuteNonQueryAsync() == 1, "Authorized company insert did not affect one row.");
        }

        Assert(await CountAsync(connection, transaction, "SELECT count(*) FROM org.company WHERE id = $1", companyId) == 1,
            "Authorized company insert was not readable in the same scope.");
        await transaction.RollbackAsync();
    }

    private static async Task AssertDeletePrivilegeRejectedAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        Guid companyId)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await SetScopeAsync(connection, transaction, tenantId, companyId);
        await using var command = new NpgsqlCommand("DELETE FROM org.company WHERE id = $1", connection, transaction);
        command.Parameters.AddWithValue(companyId);

        bool rejected = false;
        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.InsufficientPrivilege)
        {
            rejected = true;
        }

        Assert(rejected, "Runtime role unexpectedly has DELETE permission.");
    }

    private static async Task AssertTransactionContextDoesNotLeakAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        Guid companyId)
    {
        await using (NpgsqlConnection scopedConnection = await dataSource.OpenConnectionAsync())
        {
            await using NpgsqlTransaction scopedTransaction = await scopedConnection.BeginTransactionAsync();
            await SetScopeAsync(scopedConnection, scopedTransaction, tenantId, companyId);
            Assert(await CountAsync(scopedConnection, scopedTransaction, "SELECT count(*) FROM org.company WHERE id = $1", companyId) == 1,
                "Scoped connection could not read its authorized row.");
            await scopedTransaction.CommitAsync();
        }

        await using NpgsqlConnection reusedConnection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction reusedTransaction = await reusedConnection.BeginTransactionAsync();
        Assert(await CountAsync(reusedConnection, reusedTransaction, "SELECT count(*) FROM org.company WHERE id = $1", companyId) == 0,
            "Tenant/company context leaked through the connection pool.");
        await reusedTransaction.CommitAsync();
    }

    private static async Task SetScopeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        params Guid[] companyIds)
    {
        const string sql = """
            SELECT
                set_config('app.tenant_id', $1, true),
                set_config('app.company_ids', $2, true)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(tenantId.ToString());
        command.Parameters.AddWithValue("{" + string.Join(',', companyIds) + "}");
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        Guid id)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(id);
        object? result = await command.ExecuteScalarAsync();
        return result is long count ? count : throw new InvalidOperationException("Expected a bigint count result.");
    }

    private static async Task CleanupAsync(NpgsqlDataSource dataSource, Guid tenantA, Guid tenantB)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");

        await using (var companyCommand = new NpgsqlCommand("DELETE FROM org.company WHERE tenant_id = $1 OR tenant_id = $2", connection, transaction))
        {
            companyCommand.Parameters.AddWithValue(tenantA);
            companyCommand.Parameters.AddWithValue(tenantB);
            await companyCommand.ExecuteNonQueryAsync();
        }

        await using (var tenantCommand = new NpgsqlCommand("DELETE FROM org.tenant WHERE id = $1 OR id = $2", connection, transaction))
        {
            tenantCommand.Parameters.AddWithValue(tenantA);
            tenantCommand.Parameters.AddWithValue(tenantB);
            await tenantCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
