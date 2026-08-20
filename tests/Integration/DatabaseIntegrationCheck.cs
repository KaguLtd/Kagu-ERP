using System.Security.Claims;
using KaguERP.Bootstrap;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Messaging;
using KaguERP.BuildingBlocks.Application.Observability;
using KaguERP.BuildingBlocks.Application.Security;
using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static class DatabaseIntegrationCheck
{
    private const string MigratorConnectionVariable = "KAGU_ERP_MIGRATOR_CONNECTION_STRING";
    private const string AppConnectionVariable = "KAGU_ERP_APP_CONNECTION_STRING";
    private const string TestIssuer = "https://issuer.example/realms/kagu-test";
    private const string TestSubject = "subject-a";

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
            await AssertDatabaseReadinessAsync(appDataSource);
            await AssertIdentityScopeResolutionAsync(appDataSource, tenantA, companyA1, companyA2, actorId);
            await AssertIdentityContextDoesNotLeakAsync(appDataSource, actorId);
            await AssertAuthorizationAuditPersistenceAsync(
                migratorDataSource,
                appDataSource,
                tenantA,
                companyA1,
                companyA2,
                actorId);
            await AssertTransactionalOutboxAsync(
                migratorDataSource,
                appDataSource,
                tenantA,
                companyA1,
                companyA2,
                actorId);
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

        const string profileSql = """
            INSERT INTO iam.user_profile
                (id, tenant_id, issuer, subject_id, created_by, updated_by)
            VALUES ($1, $2, $3, $4, $1, $1)
            """;
        await using (var command = new NpgsqlCommand(profileSql, connection, transaction))
        {
            command.Parameters.AddWithValue(actorId);
            command.Parameters.AddWithValue(tenantA);
            command.Parameters.AddWithValue(TestIssuer);
            command.Parameters.AddWithValue(TestSubject);
            await command.ExecuteNonQueryAsync();
        }

        const string permissionSql = """
            INSERT INTO iam.user_company_permission
                (user_profile_id, tenant_id, company_id, permission_code, valid_from, valid_to, created_by)
            VALUES
                ($1, $2, $3, 'profile.read', clock_timestamp() - interval '1 day', NULL, $1),
                ($1, $2, $4, 'invoice.read', clock_timestamp() - interval '1 day', NULL, $1),
                ($1, $2, $3, 'audit.export', clock_timestamp() - interval '2 days', clock_timestamp() - interval '1 day', $1)
            """;
        await using (var command = new NpgsqlCommand(permissionSql, connection, transaction))
        {
            command.Parameters.AddWithValue(actorId);
            command.Parameters.AddWithValue(tenantA);
            command.Parameters.AddWithValue(companyA1);
            command.Parameters.AddWithValue(companyA2);
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task AssertIdentityScopeResolutionAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        Guid companyA1,
        Guid companyA2,
        Guid actorId)
    {
        var resolver = new PostgresExecutionScopeResolver(dataSource);
        ExecutionScope? scope = await resolver.ResolveAsync(CreatePrincipal(TestIssuer, TestSubject));

        if (scope is null)
        {
            throw new InvalidOperationException("Known identity did not resolve to an ERP execution scope.");
        }

        Assert(scope.TenantId == tenantId, "Identity resolved to the wrong tenant.");
        Assert(scope.ActorId == actorId, "Identity resolved to the wrong actor.");
        Assert(scope.CompanyIds.SetEquals([companyA1, companyA2]), "Identity resolved to the wrong company set.");
        Assert(scope.HasPermission(companyA1, "profile.read"), "Active company permission was not resolved.");
        Assert(!scope.HasPermission(companyA2, "profile.read"), "Permission leaked across companies.");
        Assert(scope.HasPermission(companyA2, "invoice.read"), "Second company permission was not resolved.");
        Assert(!scope.HasPermission(companyA1, "audit.export"), "Expired permission was resolved as active.");

        Assert(await resolver.ResolveAsync(CreatePrincipal(TestIssuer, "unknown-subject")) is null,
            "Unknown identity unexpectedly resolved to an ERP scope.");
        Assert(await resolver.ResolveAsync(CreatePrincipal(null, TestSubject)) is null,
            "Identity without an issuer unexpectedly resolved to an ERP scope.");

        ClaimsPrincipal duplicateSubject = CreatePrincipal(TestIssuer, TestSubject);
        ((ClaimsIdentity)duplicateSubject.Identity!).AddClaim(new Claim("sub", "second-subject"));
        Assert(await resolver.ResolveAsync(duplicateSubject) is null,
            "Identity with multiple subject claims unexpectedly resolved to an ERP scope.");
    }

    private static async Task AssertIdentityContextDoesNotLeakAsync(NpgsqlDataSource dataSource, Guid actorId)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        Assert(await CountAsync(connection, transaction, "SELECT count(*) FROM iam.user_profile WHERE id = $1", actorId) == 0,
            "Identity bootstrap context leaked through the connection pool.");
        await transaction.CommitAsync();
    }

    private static ClaimsPrincipal CreatePrincipal(string? issuer, string subject)
    {
        var claims = new List<Claim>();
        if (issuer is not null)
        {
            claims.Add(new Claim("iss", issuer));
        }

        claims.Add(new Claim("sub", subject));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "integration-test"));
    }

    private static async Task AssertAuthorizationAuditPersistenceAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid allowedCompanyId,
        Guid disallowedCompanyId,
        Guid actorId)
    {
        Guid correlationId = Guid.CreateVersion7();
        var writer = new PostgresAuthorizationAuditWriter(appDataSource);
        var context = new RequestAuditContext(
            correlationId,
            "0123456789abcdef0123456789abcdef",
            tenantId,
            actorId,
            new HashSet<Guid> { allowedCompanyId },
            "opaque-test-session");
        await writer.WriteAsync(
            context,
            new AuthorizationAuditEvent(
                "iam.scope.read",
                "current-user-scope",
                actorId.ToString("D"),
                "allowed",
                "PROFILE_READ_GRANTED"));

        await using (NpgsqlConnection privilegeConnection = await appDataSource.OpenConnectionAsync())
        await using (var privilegeCommand = new NpgsqlCommand(
            """
            SELECT
                has_table_privilege(current_user, 'platform.audit_event', 'INSERT'),
                has_table_privilege(current_user, 'platform.audit_event', 'SELECT'),
                has_table_privilege(current_user, 'platform.audit_event', 'UPDATE'),
                has_table_privilege(current_user, 'platform.audit_event', 'DELETE')
            """,
            privilegeConnection))
        await using (NpgsqlDataReader privilegeReader = await privilegeCommand.ExecuteReaderAsync())
        {
            Assert(await privilegeReader.ReadAsync(), "Audit privilege metadata was not returned.");
            Assert(privilegeReader.GetBoolean(0), "Runtime role cannot append authorization audit events.");
            Assert(!privilegeReader.GetBoolean(1) && !privilegeReader.GetBoolean(2) && !privilegeReader.GetBoolean(3),
                "Runtime role has non-append audit table privileges.");
        }

        await using (NpgsqlConnection ownerConnection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction ownerTransaction = await ownerConnection.BeginTransactionAsync())
        {
            await ExecuteAsync(ownerConnection, ownerTransaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            const string sql = """
                SELECT tenant_id, actor_id, company_ids, action, outcome, reason_code
                FROM platform.audit_event
                WHERE correlation_id = $1
                """;
            await using (var command = new NpgsqlCommand(sql, ownerConnection, ownerTransaction))
            {
                command.Parameters.AddWithValue(correlationId);
                await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
                Assert(await reader.ReadAsync(), "Authorization audit event was not persisted.");
                Assert(reader.GetGuid(0) == tenantId && reader.GetGuid(1) == actorId,
                    "Authorization audit stored the wrong trusted identity scope.");
                Assert(reader.GetFieldValue<Guid[]>(2).SequenceEqual([allowedCompanyId]),
                    "Authorization audit stored the wrong company scope.");
                Assert(reader.GetString(3) == "iam.scope.read" && reader.GetString(4) == "allowed" &&
                    reader.GetString(5) == "PROFILE_READ_GRANTED",
                    "Authorization audit stored the wrong decision metadata.");
                Assert(!await reader.ReadAsync(), "Authorization decision unexpectedly produced duplicate audit rows.");
            }

            await ownerTransaction.CommitAsync();
        }

        await using (NpgsqlConnection appConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction appTransaction = await appConnection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(appConnection, appTransaction, tenantId, actorId, allowedCompanyId);
            bool selectRejected = false;
            try
            {
                await ExecuteAsync(appConnection, appTransaction, "SELECT count(*) FROM platform.audit_event");
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.InsufficientPrivilege)
            {
                selectRejected = true;
            }

            Assert(selectRejected, "Runtime role unexpectedly has audit SELECT permission.");
        }

        await using (NpgsqlConnection crossScopeConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction crossScopeTransaction = await crossScopeConnection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(crossScopeConnection, crossScopeTransaction, tenantId, actorId, allowedCompanyId);
            const string sql = """
                INSERT INTO platform.audit_event
                    (id, tenant_id, actor_id, company_ids, correlation_id, trace_id,
                     action, target_type, outcome, reason_code)
                VALUES ($1, $2, $3, $4, $5, 'trace', 'iam.scope.read', 'current-user-scope',
                        'allowed', 'PROFILE_READ_GRANTED')
                """;
            await using var command = new NpgsqlCommand(sql, crossScopeConnection, crossScopeTransaction);
            command.Parameters.AddWithValue(Guid.CreateVersion7());
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue(actorId);
            command.Parameters.AddWithValue(new[] { disallowedCompanyId });
            command.Parameters.AddWithValue(Guid.CreateVersion7());

            bool rejected = false;
            try
            {
                await command.ExecuteNonQueryAsync();
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.InsufficientPrivilege)
            {
                rejected = true;
            }

            Assert(rejected, "Audit RLS accepted a company outside the trusted request scope.");
        }
    }

    private static async Task SetAuditScopeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid actorId,
        params Guid[] companyIds)
    {
        const string sql = """
            SELECT
                set_config('app.tenant_id', $1, true),
                set_config('app.actor_id', $2, true),
                set_config('app.company_ids', $3, true)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(tenantId.ToString());
        command.Parameters.AddWithValue(actorId.ToString());
        command.Parameters.AddWithValue("{" + string.Join(',', companyIds) + "}");
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertTransactionalOutboxAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        var scope = new ExecutionScope(tenantId, actorId, [companyId]);
        Guid eventId = Guid.CreateVersion7();
        Guid aggregateId = Guid.CreateVersion7();
        var message = new OutboxMessage(
            eventId,
            tenantId,
            companyId,
            "integration-probe",
            aggregateId,
            1,
            "integration.probe.created",
            1,
            DateTimeOffset.UtcNow,
            "{\"kind\":\"synthetic\"}");

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetScopeAsync(connection, transaction, tenantId, companyId);
            Assert(await PostgresOutboxWriter.EnqueueAsync(connection, transaction, scope, message),
                "First outbox enqueue was not inserted.");
            Assert(!await PostgresOutboxWriter.EnqueueAsync(connection, transaction, scope, message),
                "Duplicate event ID produced a second outbox insert.");

            bool conflictingPayloadRejected = false;
            try
            {
                await PostgresOutboxWriter.EnqueueAsync(
                    connection,
                    transaction,
                    scope,
                    message with { PayloadJson = "{\"kind\":\"conflicting\"}" });
            }
            catch (OutboxEventConflictException)
            {
                conflictingPayloadRejected = true;
            }

            Assert(conflictingPayloadRejected, "Conflicting content reused an existing outbox event ID.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection ownerConnection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction ownerTransaction = await ownerConnection.BeginTransactionAsync())
        {
            await ExecuteAsync(ownerConnection, ownerTransaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            const string sql = """
                SELECT count(*), min(status), min(attempt_count), min(payload ->> 'kind')
                FROM platform.outbox_message
                WHERE event_id = $1
                """;
            await using (var command = new NpgsqlCommand(sql, ownerConnection, ownerTransaction))
            {
                command.Parameters.AddWithValue(eventId);
                await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
                Assert(await reader.ReadAsync(), "Outbox verification row was not returned.");
                Assert(reader.GetInt64(0) == 1 && reader.GetString(1) == "pending" && reader.GetInt32(2) == 0 &&
                    reader.GetString(3) == "synthetic",
                    "Outbox event was not stored exactly once in pending state.");
            }

            await ownerTransaction.CommitAsync();
        }

        await using (NpgsqlConnection sequenceConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction sequenceTransaction = await sequenceConnection.BeginTransactionAsync())
        {
            await SetScopeAsync(sequenceConnection, sequenceTransaction, tenantId, companyId);
            bool sequenceConflictRejected = false;
            try
            {
                await PostgresOutboxWriter.EnqueueAsync(
                    sequenceConnection,
                    sequenceTransaction,
                    scope,
                    message with { EventId = Guid.CreateVersion7() });
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                sequenceConflictRejected = true;
            }

            Assert(sequenceConflictRejected, "Duplicate aggregate sequence was accepted.");
        }

        await using (NpgsqlConnection crossScopeConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction crossScopeTransaction = await crossScopeConnection.BeginTransactionAsync())
        {
            await SetScopeAsync(crossScopeConnection, crossScopeTransaction, tenantId, companyId);
            bool crossScopeRejected = false;
            try
            {
                await PostgresOutboxWriter.EnqueueAsync(
                    crossScopeConnection,
                    crossScopeTransaction,
                    scope,
                    message with
                    {
                        EventId = Guid.CreateVersion7(),
                        CompanyId = otherCompanyId,
                        AggregateId = Guid.CreateVersion7(),
                    });
            }
            catch (ExecutionScopeDeniedException)
            {
                crossScopeRejected = true;
            }

            Assert(crossScopeRejected, "Outbox writer accepted a company outside the execution scope.");
        }

        Guid rolledBackCompanyId = Guid.CreateVersion7();
        Guid rolledBackEventId = Guid.CreateVersion7();
        var rollbackScope = new ExecutionScope(tenantId, actorId, [rolledBackCompanyId]);
        await using (NpgsqlConnection rollbackConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction rollbackTransaction = await rollbackConnection.BeginTransactionAsync())
        {
            await SetScopeAsync(rollbackConnection, rollbackTransaction, tenantId, rolledBackCompanyId);
            const string companySql = """
                INSERT INTO org.company (id, tenant_id, code, created_by, updated_by)
                VALUES ($1, $2, $3, $4, $4)
                """;
            await using (var command = new NpgsqlCommand(companySql, rollbackConnection, rollbackTransaction))
            {
                command.Parameters.AddWithValue(rolledBackCompanyId);
                command.Parameters.AddWithValue(tenantId);
                command.Parameters.AddWithValue($"RB-{rolledBackCompanyId:N}");
                command.Parameters.AddWithValue(actorId);
                await command.ExecuteNonQueryAsync();
            }

            await PostgresOutboxWriter.EnqueueAsync(
                rollbackConnection,
                rollbackTransaction,
                rollbackScope,
                message with
                {
                    EventId = rolledBackEventId,
                    CompanyId = rolledBackCompanyId,
                    AggregateId = Guid.CreateVersion7(),
                });
            await rollbackTransaction.RollbackAsync();
        }

        await using (NpgsqlConnection verifyConnection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction verifyTransaction = await verifyConnection.BeginTransactionAsync())
        {
            await ExecuteAsync(verifyConnection, verifyTransaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            Assert(await CountAsync(
                    verifyConnection,
                    verifyTransaction,
                    "SELECT count(*) FROM org.company WHERE id = $1",
                    rolledBackCompanyId) == 0,
                "Rolled-back business row was persisted.");
            Assert(await CountAsync(
                    verifyConnection,
                    verifyTransaction,
                    "SELECT count(*) FROM platform.outbox_message WHERE event_id = $1",
                    rolledBackEventId) == 0,
                "Outbox event escaped its rolled-back business transaction.");
            await verifyTransaction.CommitAsync();
        }
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

    private static async Task AssertDatabaseReadinessAsync(NpgsqlDataSource dataSource)
    {
        var probe = new PostgresReadinessProbe(dataSource);
        ReadinessResult result = await probe.CheckAsync();
        Assert(result == ReadinessResult.Ready, "Healthy PostgreSQL was reported as not ready.");

        var unavailableConnection = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString)
        {
            Port = 1,
            Timeout = 1,
            Pooling = false,
        };
        await using NpgsqlDataSource unavailableDataSource = NpgsqlDataSource.Create(unavailableConnection.ConnectionString);
        var unavailableProbe = new PostgresReadinessProbe(unavailableDataSource);
        Assert(await unavailableProbe.CheckAsync() == ReadinessResult.NotReady,
            "Unavailable PostgreSQL was reported as ready.");
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

        await using (var outboxCommand = new NpgsqlCommand(
            "DELETE FROM platform.outbox_message WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            outboxCommand.Parameters.AddWithValue(tenantA);
            outboxCommand.Parameters.AddWithValue(tenantB);
            await outboxCommand.ExecuteNonQueryAsync();
        }

        await using (var auditCommand = new NpgsqlCommand(
            "DELETE FROM platform.audit_event WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            auditCommand.Parameters.AddWithValue(tenantA);
            auditCommand.Parameters.AddWithValue(tenantB);
            await auditCommand.ExecuteNonQueryAsync();
        }

        await using (var permissionCommand = new NpgsqlCommand(
            "DELETE FROM iam.user_company_permission WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            permissionCommand.Parameters.AddWithValue(tenantA);
            permissionCommand.Parameters.AddWithValue(tenantB);
            await permissionCommand.ExecuteNonQueryAsync();
        }

        await using (var profileCommand = new NpgsqlCommand(
            "DELETE FROM iam.user_profile WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            profileCommand.Parameters.AddWithValue(tenantA);
            profileCommand.Parameters.AddWithValue(tenantB);
            await profileCommand.ExecuteNonQueryAsync();
        }

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
