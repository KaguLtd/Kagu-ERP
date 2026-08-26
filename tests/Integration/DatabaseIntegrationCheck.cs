using System.Security.Claims;
using KaguERP.Bootstrap;
using KaguERP.BuildingBlocks.Application.Approvals;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Idempotency;
using KaguERP.BuildingBlocks.Application.Messaging;
using KaguERP.BuildingBlocks.Application.Observability;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Accounting.Application.Posting;
using KaguERP.Modules.Accounting.Domain.Accounts;
using KaguERP.Modules.Accounting.Domain.Currencies;
using KaguERP.Modules.Accounting.Domain.Dimensions;
using KaguERP.Modules.Accounting.Domain.Journals;
using KaguERP.Modules.Accounting.Domain.Periods;
using KaguERP.Modules.Accounting.Infrastructure.Persistence;
using KaguERP.Modules.Parties.Domain.Allocations;
using KaguERP.Modules.Parties.Domain.DueSchedules;
using KaguERP.Modules.Parties.Domain.OpenItems;
using KaguERP.Modules.Parties.Infrastructure.Persistence;
using KaguERP.Modules.Treasury.Domain.Payments;
using KaguERP.Modules.Treasury.Domain.Statements;
using KaguERP.Modules.Treasury.Infrastructure.Persistence;
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
        string testSubject = $"{TestSubject}-{tenantA:N}";

        await using NpgsqlDataSource migratorDataSource = NpgsqlDataSource.Create(migratorConnectionString);
        await using NpgsqlDataSource appDataSource = NpgsqlDataSource.Create(appConnectionString);

        try
        {
            await SeedAsync(migratorDataSource, tenantA, tenantB, companyA1, companyA2, companyB1, actorId, testSubject);
            await AssertRuntimeRoleAsync(appDataSource);
            await AssertDatabaseReadinessAsync(appDataSource);
            await AssertIdentityScopeResolutionAsync(appDataSource, tenantA, companyA1, companyA2, actorId, testSubject);
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
            await AssertApiIdempotencyPersistenceAsync(
                migratorDataSource,
                appDataSource,
                tenantA,
                companyA1,
                companyA2,
                actorId);
            await AssertAuthoritativeJournalAccountEvidenceAsync(
                migratorDataSource,
                appDataSource,
                tenantA,
                companyA1,
                companyA2,
                actorId);
            await AssertAuthoritativeJournalDimensionEvidenceAsync(
                migratorDataSource,
                appDataSource,
                tenantA,
                companyA1,
                companyA2,
                actorId);
            await AssertAuthoritativeJournalCurrencyEvidenceAsync(
                migratorDataSource,
                appDataSource,
                tenantA,
                companyA1,
                companyA2,
                actorId);
            await AssertAuthoritativeApprovalCompletionEvidenceAsync(
                migratorDataSource,
                appDataSource,
                tenantA,
                companyA1,
                companyA2,
                actorId);
            await AssertJournalSourceReservationAsync(
                migratorDataSource,
                appDataSource,
                tenantA,
                companyA1,
                companyA2,
                actorId);
            await AssertJournalSourceReservationWriterAsync(
                migratorDataSource,
                appDataSource,
                tenantA,
                companyA1,
                companyA2,
                actorId);
            await AssertAuthoritativePeriodPostingGateAsync(
                migratorDataSource,
                appDataSource,
                tenantA,
                companyA1,
                companyA2,
                actorId);
            await AssertJournalPreparationOrchestrationAsync(
                migratorDataSource,
                appDataSource,
                tenantA,
                companyA1,
                actorId);
            await AssertPostedJournalPersistenceAsync(
                migratorDataSource,
                appDataSource,
                tenantA,
                companyA1,
                companyA2,
                actorId);
            await AssertJournalPostingCompositionAsync(
                migratorDataSource,
                appDataSource,
                tenantA,
                companyA1,
                actorId);
            await AssertIdempotentJournalPostingCompositionAsync(
                migratorDataSource,
                appDataSource,
                tenantA,
                companyA1,
                actorId);
            await AssertPostedJournalReversalLinkAsync(
                migratorDataSource,
                appDataSource,
                tenantA,
                companyA1,
                companyA2,
                actorId);
            await AssertDueSchedulePersistenceAsync(
                migratorDataSource, appDataSource, tenantA, companyA1, companyA2, actorId);
            await AssertPaymentEconomicEventPersistenceAsync(
                appDataSource, tenantA, companyA1, companyA2, actorId);
            await AssertStatementLinePersistenceAsync(
                appDataSource, tenantA, companyA1, companyA2, actorId);
            await AssertJournalReservationAuditOutboxAtomicityAsync(
                migratorDataSource,
                appDataSource,
                tenantA,
                companyA1,
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

    private static async Task AssertApiIdempotencyPersistenceAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        const string commandName = "accounting.journal.prepare";
        string key = Guid.CreateVersion7().ToString("D");
        const string requestHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        Guid aggregateId = Guid.CreateVersion7();

        IdempotencyRecord[] results = await Task.WhenAll(
            ExecuteIdempotentProbeAsync(appDataSource, tenantId, companyId, actorId, commandName, key, requestHash, aggregateId),
            ExecuteIdempotentProbeAsync(appDataSource, tenantId, companyId, actorId, commandName, key, requestHash, aggregateId));
        Assert(results.Count(result => result.Created) == 1,
            "Parallel idempotency acquisition did not produce exactly one winner.");
        IdempotencyRecord replay = results.Single(result => !result.Created);
        Assert(replay.Status == IdempotencyRecordStatus.Completed && replay.ResponseStatus == 201 &&
               replay.AggregateId == aggregateId && replay.ResponseBodyJson == $"{{\"id\":\"{aggregateId:D}\",\"status\":\"prepared\"}}",
            "Idempotency replay did not return the completed response snapshot.");

        await using (NpgsqlConnection conflictConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction conflictTransaction = await conflictConnection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(conflictConnection, conflictTransaction, tenantId, actorId, companyId);
            IdempotencyKeyReusedException exception = await ThrowsAsync<IdempotencyKeyReusedException>(() =>
                PostgresIdempotencyWriter.AcquireAsync(
                    conflictConnection,
                    conflictTransaction,
                    new ExecutionScope(tenantId, actorId, [companyId]),
                    companyId,
                    Guid.CreateVersion7(),
                    commandName,
                    key,
                    "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb").AsTask());
            Assert(exception.Code == "IDEMPOTENCY_KEY_REUSED", "Idempotency payload conflict returned the wrong code.");
            await conflictTransaction.RollbackAsync();
        }

        Guid rolledBackRecordId = Guid.CreateVersion7();
        await using (NpgsqlConnection rollbackConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction rollbackTransaction = await rollbackConnection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(rollbackConnection, rollbackTransaction, tenantId, actorId, companyId);
            await PostgresIdempotencyWriter.AcquireAsync(
                rollbackConnection,
                rollbackTransaction,
                new ExecutionScope(tenantId, actorId, [companyId]),
                companyId,
                rolledBackRecordId,
                commandName,
                Guid.CreateVersion7().ToString("D"),
                requestHash);
            await rollbackTransaction.RollbackAsync();
        }

        await using (NpgsqlConnection ownerConnection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction ownerTransaction = await ownerConnection.BeginTransactionAsync())
        {
            await ExecuteAsync(ownerConnection, ownerTransaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            Assert(await CountAsync(
                    ownerConnection,
                    ownerTransaction,
                    "SELECT count(*) FROM platform.idempotency_record WHERE record_id = $1",
                    rolledBackRecordId) == 0,
                "Idempotency record escaped caller-owned rollback.");
            await ownerTransaction.CommitAsync();
        }

        await using (NpgsqlConnection scopeConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction scopeTransaction = await scopeConnection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(scopeConnection, scopeTransaction, tenantId, actorId, otherCompanyId);
            Assert(await CountAsync(
                    scopeConnection,
                    scopeTransaction,
                    "SELECT count(*) FROM platform.idempotency_record WHERE record_id = $1",
                    results[0].RecordId) == 0,
                "Idempotency response leaked across company scope.");
            await scopeTransaction.CommitAsync();
        }

        await using NpgsqlConnection privilegeConnection = await appDataSource.OpenConnectionAsync();
        await using var privilegeCommand = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user, 'platform.idempotency_record', 'SELECT'), has_table_privilege(current_user, 'platform.idempotency_record', 'INSERT'), has_table_privilege(current_user, 'platform.idempotency_record', 'UPDATE'), has_table_privilege(current_user, 'platform.idempotency_record', 'DELETE'), has_column_privilege(current_user, 'platform.idempotency_record', 'record_status', 'UPDATE'), has_column_privilege(current_user, 'platform.idempotency_record', 'request_hash', 'UPDATE')",
            privilegeConnection);
        await using NpgsqlDataReader privilegeReader = await privilegeCommand.ExecuteReaderAsync();
        Assert(await privilegeReader.ReadAsync(), "Idempotency privilege metadata was not returned.");
        Assert(privilegeReader.GetBoolean(0) && privilegeReader.GetBoolean(1) && !privilegeReader.GetBoolean(2) &&
               !privilegeReader.GetBoolean(3) && privilegeReader.GetBoolean(4) && !privilegeReader.GetBoolean(5),
            "Runtime idempotency privileges do not match select/insert/column-complete-only policy.");
    }

    private static async Task<IdempotencyRecord> ExecuteIdempotentProbeAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        string commandName,
        string key,
        string requestHash,
        Guid aggregateId)
    {
        var scope = new ExecutionScope(tenantId, actorId, [companyId]);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
        IdempotencyRecord acquired = await PostgresIdempotencyWriter.AcquireAsync(
            connection, transaction, scope, companyId, Guid.CreateVersion7(), commandName, key, requestHash);
        IdempotencyRecord result = acquired;
        if (acquired.Created)
        {
            result = await PostgresIdempotencyWriter.CompleteAsync(
                connection,
                transaction,
                scope,
                acquired,
                201,
                $"{{\"id\":\"{aggregateId:D}\",\"status\":\"prepared\"}}",
                aggregateId);
        }

        await transaction.CommitAsync();
        return result;
    }

    private static async Task AssertAuthoritativeJournalAccountEvidenceAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        Guid chartVersionId = Guid.CreateVersion7();
        Guid debitAccountId = Guid.CreateVersion7();
        Guid creditAccountId = Guid.CreateVersion7();
        Guid summaryAccountId = Guid.CreateVersion7();
        Guid inactiveAccountId = Guid.CreateVersion7();
        await SeedAccountPostingEvidenceAsync(
            migratorDataSource,
            tenantId,
            companyId,
            actorId,
            chartVersionId,
            [(debitAccountId, AccountKind.Posting, true), (creditAccountId, AccountKind.Posting, true),
             (summaryAccountId, AccountKind.Summary, true), (inactiveAccountId, AccountKind.Posting, false)]);

        var scope = new ExecutionScope(tenantId, actorId, [companyId]);
        ValidatedJournalDraft validDraft = CreateIntegrationJournalDraft(
            tenantId, companyId, Guid.CreateVersion7(), Guid.CreateVersion7(),
            debitAccountId, creditAccountId, 12m, reverseLineOrder: false);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            ValidatedJournalAccountSet loaded = await PostgresAuthoritativeJournalAccountLoader.LoadAsync(
                connection, transaction, scope, validDraft, chartVersionId);
            Assert(loaded.Accounts.Count == 2 && loaded.Accounts.All(account => account.IsActive && account.Kind == AccountKind.Posting),
                "Authoritative account loader did not return both active posting accounts.");
            await transaction.CommitAsync();
        }

        await AssertAccountEvidenceRejectedAsync(
            appDataSource,
            scope,
            CreateIntegrationJournalDraft(tenantId, companyId, Guid.CreateVersion7(), Guid.CreateVersion7(),
                summaryAccountId, creditAccountId, 13m, false),
            chartVersionId,
            tenantId,
            companyId,
            actorId,
            "JOURNAL_ACCOUNT_NOT_POSTABLE");
        await AssertAccountEvidenceRejectedAsync(
            appDataSource,
            scope,
            CreateIntegrationJournalDraft(tenantId, companyId, Guid.CreateVersion7(), Guid.CreateVersion7(),
                inactiveAccountId, creditAccountId, 14m, false),
            chartVersionId,
            tenantId,
            companyId,
            actorId,
            "JOURNAL_ACCOUNT_INACTIVE");

        ValidatedJournalDraft missingDraft = CreateIntegrationJournalDraft(
            tenantId, companyId, Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), creditAccountId, 15m, false);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            AuthoritativeAccountEvidenceException exception = await ThrowsAsync<AuthoritativeAccountEvidenceException>(() =>
                PostgresAuthoritativeJournalAccountLoader.LoadAsync(
                    connection, transaction, scope, missingDraft, chartVersionId).AsTask());
            Assert(exception.Code == "ACCOUNT_EVIDENCE_INCOMPLETE", "Missing account evidence returned the wrong code.");
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, otherCompanyId);
            AuthoritativeAccountEvidenceException exception = await ThrowsAsync<AuthoritativeAccountEvidenceException>(() =>
                PostgresAuthoritativeJournalAccountLoader.LoadAsync(
                    connection, transaction, scope, validDraft, chartVersionId).AsTask());
            Assert(exception.Code == "ACCOUNT_CHART_VERSION_NOT_FOUND", "Cross-company chart evidence did not fail closed.");
            await transaction.RollbackAsync();
        }

        await using NpgsqlConnection privilegeConnection = await appDataSource.OpenConnectionAsync();
        await using var privilegeCommand = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user, 'accounting.chart_of_accounts_version', 'SELECT'), has_table_privilege(current_user, 'accounting.chart_of_accounts_version', 'INSERT'), has_table_privilege(current_user, 'accounting.account_posting_snapshot', 'SELECT'), has_table_privilege(current_user, 'accounting.account_posting_snapshot', 'UPDATE')",
            privilegeConnection);
        await using NpgsqlDataReader reader = await privilegeCommand.ExecuteReaderAsync();
        Assert(await reader.ReadAsync(), "Account evidence privilege metadata was not returned.");
        Assert(reader.GetBoolean(0) && !reader.GetBoolean(1) && reader.GetBoolean(2) && !reader.GetBoolean(3),
            "Runtime account evidence privileges are not read-only.");
    }

    private static async Task AssertAccountEvidenceRejectedAsync(
        NpgsqlDataSource dataSource,
        ExecutionScope scope,
        ValidatedJournalDraft draft,
        Guid chartVersionId,
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        string expectedCode)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
        AccountInvariantException exception = await ThrowsAsync<AccountInvariantException>(() =>
            PostgresAuthoritativeJournalAccountLoader.LoadAsync(
                connection, transaction, scope, draft, chartVersionId).AsTask());
        Assert(exception.Code == expectedCode, "Account postability evidence returned the wrong invariant code.");
        await transaction.RollbackAsync();
    }

    private static async Task SeedAccountPostingEvidenceAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        Guid chartVersionId,
        IEnumerable<(Guid AccountId, AccountKind Kind, bool IsActive)> accounts)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
        await using (var chart = new NpgsqlCommand(
            "INSERT INTO accounting.chart_of_accounts_version (chart_version_id, tenant_id, company_id, version, created_by) SELECT $1, $2, $3, COALESCE(MAX(version), 0) + 1, $4 FROM accounting.chart_of_accounts_version WHERE tenant_id = $2 AND company_id = $3",
            connection,
            transaction))
        {
            chart.Parameters.AddWithValue(chartVersionId);
            chart.Parameters.AddWithValue(tenantId);
            chart.Parameters.AddWithValue(companyId);
            chart.Parameters.AddWithValue(actorId);
            await chart.ExecuteNonQueryAsync();
        }

        foreach ((Guid accountId, AccountKind kind, bool isActive) in accounts)
        {
            await using var account = new NpgsqlCommand(
                "INSERT INTO accounting.account_posting_snapshot (tenant_id, company_id, chart_version_id, account_id, account_kind, is_active, version, created_by) VALUES ($1, $2, $3, $4, $5, $6, 1, $7)",
                connection,
                transaction);
            account.Parameters.AddWithValue(tenantId);
            account.Parameters.AddWithValue(companyId);
            account.Parameters.AddWithValue(chartVersionId);
            account.Parameters.AddWithValue(accountId);
            account.Parameters.AddWithValue((short)kind);
            account.Parameters.AddWithValue(isActive);
            account.Parameters.AddWithValue(actorId);
            await account.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task AssertAuthoritativeJournalDimensionEvidenceAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        JournalPreparationRequest valid = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 21m, hasPostingPermission: true);
        Guid assignedDimensionId = valid.Draft.Lines[0].Dimensions.Single().DimensionId;
        await SeedDimensionRequirementAsync(
            migratorDataSource, tenantId, companyId, actorId,
            valid.Draft.PostingRuleVersionId, [assignedDimensionId]);
        var scope = new ExecutionScope(tenantId, actorId, [companyId]);

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            ValidatedJournalDimensions loaded = await PostgresAuthoritativeJournalDimensionLoader.LoadAsync(
                connection, transaction, scope, valid.Draft);
            Assert(loaded.RequirementSnapshot.RequiredDimensionIds.SequenceEqual([assignedDimensionId]),
                "Authoritative dimension loader returned the wrong requirement set.");
            await transaction.CommitAsync();
        }

        JournalPreparationRequest missing = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 22m, hasPostingPermission: true);
        await SeedDimensionRequirementAsync(
            migratorDataSource, tenantId, companyId, actorId,
            missing.Draft.PostingRuleVersionId, [Guid.CreateVersion7()]);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            DimensionInvariantException exception = await ThrowsAsync<DimensionInvariantException>(() =>
                PostgresAuthoritativeJournalDimensionLoader.LoadAsync(
                    connection, transaction, scope, missing.Draft).AsTask());
            Assert(exception.Code == "JOURNAL_DIMENSION_REQUIRED", "Missing required dimension returned the wrong code.");
            await transaction.RollbackAsync();
        }

        JournalPreparationRequest absent = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 23m, hasPostingPermission: true);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            AuthoritativeDimensionEvidenceException exception = await ThrowsAsync<AuthoritativeDimensionEvidenceException>(() =>
                PostgresAuthoritativeJournalDimensionLoader.LoadAsync(
                    connection, transaction, scope, absent.Draft).AsTask());
            Assert(exception.Code == "DIMENSION_REQUIREMENT_SET_NOT_FOUND", "Missing requirement set returned the wrong code.");
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, otherCompanyId);
            await ThrowsAsync<AuthoritativeDimensionEvidenceException>(() =>
                PostgresAuthoritativeJournalDimensionLoader.LoadAsync(
                    connection, transaction, scope, valid.Draft).AsTask());
            await transaction.RollbackAsync();
        }

        await using NpgsqlConnection privilegeConnection = await appDataSource.OpenConnectionAsync();
        await using var privilegeCommand = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user, 'accounting.posting_dimension_requirement_set', 'SELECT'), has_table_privilege(current_user, 'accounting.posting_dimension_requirement_set', 'INSERT'), has_table_privilege(current_user, 'accounting.posting_dimension_requirement', 'SELECT'), has_table_privilege(current_user, 'accounting.posting_dimension_requirement', 'UPDATE')",
            privilegeConnection);
        await using NpgsqlDataReader reader = await privilegeCommand.ExecuteReaderAsync();
        Assert(await reader.ReadAsync(), "Dimension evidence privilege metadata was not returned.");
        Assert(reader.GetBoolean(0) && !reader.GetBoolean(1) && reader.GetBoolean(2) && !reader.GetBoolean(3),
            "Runtime dimension evidence privileges are not read-only.");
    }

    private static async Task SeedDimensionRequirementAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        Guid postingRuleVersionId,
        IEnumerable<Guid> dimensionIds)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
        await using (var setCommand = new NpgsqlCommand(
            "INSERT INTO accounting.posting_dimension_requirement_set (tenant_id, company_id, posting_rule_version_id, version, created_by) VALUES ($1, $2, $3, 1, $4)",
            connection,
            transaction))
        {
            setCommand.Parameters.AddWithValue(tenantId);
            setCommand.Parameters.AddWithValue(companyId);
            setCommand.Parameters.AddWithValue(postingRuleVersionId);
            setCommand.Parameters.AddWithValue(actorId);
            await setCommand.ExecuteNonQueryAsync();
        }

        foreach (Guid dimensionId in dimensionIds)
        {
            await using var lineCommand = new NpgsqlCommand(
                "INSERT INTO accounting.posting_dimension_requirement (tenant_id, company_id, posting_rule_version_id, dimension_id) VALUES ($1, $2, $3, $4)",
                connection,
                transaction);
            lineCommand.Parameters.AddWithValue(tenantId);
            lineCommand.Parameters.AddWithValue(companyId);
            lineCommand.Parameters.AddWithValue(postingRuleVersionId);
            lineCommand.Parameters.AddWithValue(dimensionId);
            await lineCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task AssertAuthoritativeJournalCurrencyEvidenceAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        JournalPreparationRequest valid = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 31m, hasPostingPermission: true);
        await SeedCurrencyEvidenceAsync(migratorDataSource, valid, actorId, numeratorOverride: null);
        var scope = new ExecutionScope(tenantId, actorId, [companyId]);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            ValidatedJournalCurrencySet loaded = await PostgresAuthoritativeJournalCurrencyLoader.LoadAsync(
                connection, transaction, scope, valid.Draft);
            Assert(loaded.LineAmounts.Count == valid.Draft.Lines.Count,
                "Authoritative currency loader did not validate every journal line.");
            await transaction.CommitAsync();
        }

        JournalPreparationRequest tampered = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 32m, hasPostingPermission: true);
        await SeedCurrencyEvidenceAsync(migratorDataSource, tampered, actorId, numeratorOverride: 2m);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            AuthoritativeCurrencyEvidenceException exception = await ThrowsAsync<AuthoritativeCurrencyEvidenceException>(() =>
                PostgresAuthoritativeJournalCurrencyLoader.LoadAsync(
                    connection, transaction, scope, tampered.Draft).AsTask());
            Assert(exception.Code == "EXCHANGE_RATE_EVIDENCE_MISMATCH", "Changed rate evidence returned the wrong code.");
            await transaction.RollbackAsync();
        }

        JournalPreparationRequest absent = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 33m, hasPostingPermission: true);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            await ThrowsAsync<AuthoritativeCurrencyEvidenceException>(() =>
                PostgresAuthoritativeJournalCurrencyLoader.LoadAsync(
                    connection, transaction, scope, absent.Draft).AsTask());
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, otherCompanyId);
            await ThrowsAsync<AuthoritativeCurrencyEvidenceException>(() =>
                PostgresAuthoritativeJournalCurrencyLoader.LoadAsync(
                    connection, transaction, scope, valid.Draft).AsTask());
            await transaction.RollbackAsync();
        }

        await using NpgsqlConnection privilegeConnection = await appDataSource.OpenConnectionAsync();
        await using var privilegeCommand = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user, 'accounting.exchange_rate_snapshot', 'SELECT'), has_table_privilege(current_user, 'accounting.exchange_rate_snapshot', 'INSERT'), has_table_privilege(current_user, 'accounting.rounding_policy_snapshot', 'SELECT'), has_table_privilege(current_user, 'accounting.rounding_policy_snapshot', 'UPDATE')",
            privilegeConnection);
        await using NpgsqlDataReader reader = await privilegeCommand.ExecuteReaderAsync();
        Assert(await reader.ReadAsync(), "Currency evidence privilege metadata was not returned.");
        Assert(reader.GetBoolean(0) && !reader.GetBoolean(1) && reader.GetBoolean(2) && !reader.GetBoolean(3),
            "Runtime currency evidence privileges are not read-only.");
    }

    private static async Task SeedCurrencyEvidenceAsync(
        NpgsqlDataSource dataSource,
        JournalPreparationRequest request,
        Guid actorId,
        decimal? numeratorOverride)
    {
        JournalCurrencyAmountSnapshot amount = request.Draft.Lines[0].CurrencyAmount
            ?? throw new InvalidOperationException("Currency evidence fixture requires a line snapshot.");
        ExchangeRateSnapshot rate = amount.ExchangeRate;
        RoundingPolicySnapshot policy = amount.RoundingPolicy;
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
        await using (var rateCommand = new NpgsqlCommand(
            "INSERT INTO accounting.exchange_rate_snapshot (tenant_id, company_id, rate_snapshot_id, version, transaction_currency, functional_currency, rate_type, source, rate_date, functional_units_numerator, transaction_units_denominator, created_by) VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)",
            connection,
            transaction))
        {
            rateCommand.Parameters.AddWithValue(rate.TenantId);
            rateCommand.Parameters.AddWithValue(rate.CompanyId);
            rateCommand.Parameters.AddWithValue(rate.RateSnapshotId);
            rateCommand.Parameters.AddWithValue(rate.Version);
            rateCommand.Parameters.AddWithValue(rate.TransactionCurrency.Value);
            rateCommand.Parameters.AddWithValue(rate.FunctionalCurrency.Value);
            rateCommand.Parameters.AddWithValue(rate.RateType);
            rateCommand.Parameters.AddWithValue(rate.Source);
            rateCommand.Parameters.AddWithValue(rate.RateDate);
            rateCommand.Parameters.AddWithValue(numeratorOverride ?? rate.FunctionalUnitsNumerator);
            rateCommand.Parameters.AddWithValue(rate.TransactionUnitsDenominator);
            rateCommand.Parameters.AddWithValue(actorId);
            await rateCommand.ExecuteNonQueryAsync();
        }

        await using (var policyCommand = new NpgsqlCommand(
            "INSERT INTO accounting.rounding_policy_snapshot (tenant_id, company_id, policy_id, version, scale, rounding_mode, created_by) VALUES ($1,$2,$3,$4,$5,$6,$7)",
            connection,
            transaction))
        {
            policyCommand.Parameters.AddWithValue(policy.TenantId);
            policyCommand.Parameters.AddWithValue(policy.CompanyId);
            policyCommand.Parameters.AddWithValue(policy.PolicyId);
            policyCommand.Parameters.AddWithValue(policy.Version);
            policyCommand.Parameters.AddWithValue((short)policy.Scale);
            policyCommand.Parameters.AddWithValue((short)policy.Mode);
            policyCommand.Parameters.AddWithValue(actorId);
            await policyCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task AssertAuthoritativeApprovalCompletionEvidenceAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        Guid approvalInstanceId = Guid.CreateVersion7();
        Guid workflowVersionId = Guid.CreateVersion7();
        Guid subjectId = Guid.CreateVersion7();
        Guid makerId = Guid.CreateVersion7();
        Guid approverA = Guid.CreateVersion7();
        Guid approverB = Guid.CreateVersion7();
        const string subjectType = "accounting.journal-source";
        const long subjectVersion = 7;
        await SeedApprovalCompletionEvidenceAsync(
            migratorDataSource, tenantId, companyId, actorId, approvalInstanceId,
            workflowVersionId, subjectType, subjectId, subjectVersion, makerId,
            [(Guid.CreateVersion7(), approverA), (Guid.CreateVersion7(), approverB)]);

        var scope = new ExecutionScope(tenantId, actorId, [companyId]);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            ApprovalCompletionEvidence loaded = await PostgresAuthoritativeApprovalCompletionLoader.LoadAsync(
                connection, transaction, scope, tenantId, companyId, subjectType, subjectId, subjectVersion);
            Assert(loaded.ApprovalInstanceId == approvalInstanceId && loaded.WorkflowVersionId == workflowVersionId,
                "Authoritative approval loader returned the wrong completion snapshot.");
            Assert(loaded.Decisions.Count == 2 && loaded.Decisions.Select(item => item.ApproverId).Distinct().Count() == 2,
                "Authoritative approval loader did not preserve distinct-person quorum evidence.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            AuthoritativeApprovalEvidenceException missing = await ThrowsAsync<AuthoritativeApprovalEvidenceException>(() =>
                PostgresAuthoritativeApprovalCompletionLoader.LoadAsync(
                    connection, transaction, scope, tenantId, companyId, subjectType, subjectId, subjectVersion + 1).AsTask());
            Assert(missing.Code == "APPROVAL_COMPLETION_NOT_FOUND",
                "Stale approval subject version returned the wrong fail-closed code.");
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, otherCompanyId);
            await ThrowsAsync<AuthoritativeApprovalEvidenceException>(() =>
                PostgresAuthoritativeApprovalCompletionLoader.LoadAsync(
                    connection, transaction, scope, tenantId, companyId, subjectType, subjectId, subjectVersion).AsTask());
            await transaction.RollbackAsync();
        }

        await using NpgsqlConnection privilegeConnection = await appDataSource.OpenConnectionAsync();
        await using var privilegeCommand = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user, 'workflow.approval_completion_snapshot', 'SELECT'), has_table_privilege(current_user, 'workflow.approval_completion_snapshot', 'INSERT'), has_table_privilege(current_user, 'workflow.approval_decision_snapshot', 'SELECT'), has_table_privilege(current_user, 'workflow.approval_decision_snapshot', 'UPDATE')",
            privilegeConnection);
        await using NpgsqlDataReader reader = await privilegeCommand.ExecuteReaderAsync();
        Assert(await reader.ReadAsync(), "Approval evidence privilege metadata was not returned.");
        Assert(reader.GetBoolean(0) && !reader.GetBoolean(1) && reader.GetBoolean(2) && !reader.GetBoolean(3),
            "Runtime approval evidence privileges are not read-only.");
    }

    private static async Task SeedApprovalCompletionEvidenceAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        Guid approvalInstanceId,
        Guid workflowVersionId,
        string subjectType,
        Guid subjectId,
        long subjectVersion,
        Guid makerId,
        IReadOnlyCollection<(Guid DecisionId, Guid ApproverId)> decisions)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
        await using (var completion = new NpgsqlCommand(
            "INSERT INTO workflow.approval_completion_snapshot (tenant_id, company_id, approval_instance_id, workflow_version_id, subject_type, subject_id, subject_version, maker_id, required_quorum, completed_at, created_by) VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)",
            connection,
            transaction))
        {
            completion.Parameters.AddWithValue(tenantId);
            completion.Parameters.AddWithValue(companyId);
            completion.Parameters.AddWithValue(approvalInstanceId);
            completion.Parameters.AddWithValue(workflowVersionId);
            completion.Parameters.AddWithValue(subjectType);
            completion.Parameters.AddWithValue(subjectId);
            completion.Parameters.AddWithValue(subjectVersion);
            completion.Parameters.AddWithValue(makerId);
            completion.Parameters.AddWithValue(decisions.Count);
            completion.Parameters.AddWithValue(DateTimeOffset.UtcNow);
            completion.Parameters.AddWithValue(actorId);
            await completion.ExecuteNonQueryAsync();
        }

        int offset = 0;
        foreach ((Guid decisionId, Guid approverId) in decisions)
        {
            await using var decision = new NpgsqlCommand(
                "INSERT INTO workflow.approval_decision_snapshot (tenant_id, company_id, approval_instance_id, decision_id, approver_id, decided_at) VALUES ($1,$2,$3,$4,$5,$6)",
                connection,
                transaction);
            decision.Parameters.AddWithValue(tenantId);
            decision.Parameters.AddWithValue(companyId);
            decision.Parameters.AddWithValue(approvalInstanceId);
            decision.Parameters.AddWithValue(decisionId);
            decision.Parameters.AddWithValue(approverId);
            decision.Parameters.AddWithValue(DateTimeOffset.UtcNow.AddMinutes(offset++));
            await decision.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static Task SeedJournalApprovalEvidenceAsync(
        NpgsqlDataSource dataSource,
        JournalPreparationRequest request,
        Guid actorId)
    {
        JournalPostingIdentity identity = request.Draft.PostingIdentity;
        return SeedApprovalCompletionEvidenceAsync(
            dataSource,
            identity.TenantId,
            identity.CompanyId,
            actorId,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            identity.SourceType,
            identity.SourceEventId,
            1,
            Guid.CreateVersion7(),
            [(Guid.CreateVersion7(), Guid.CreateVersion7()), (Guid.CreateVersion7(), Guid.CreateVersion7())]);
    }

    private static async Task SeedAsync(
        NpgsqlDataSource dataSource,
        Guid tenantA,
        Guid tenantB,
        Guid companyA1,
        Guid companyA2,
        Guid companyB1,
        Guid actorId,
        string testSubject)
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
            command.Parameters.AddWithValue(testSubject);
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
        Guid actorId,
        string testSubject)
    {
        var resolver = new PostgresExecutionScopeResolver(dataSource);
        ExecutionScope? scope = await resolver.ResolveAsync(CreatePrincipal(TestIssuer, testSubject));

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
        Assert(await resolver.ResolveAsync(CreatePrincipal(null, testSubject)) is null,
            "Identity without an issuer unexpectedly resolved to an ERP scope.");

        ClaimsPrincipal duplicateSubject = CreatePrincipal(TestIssuer, testSubject);
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

    private static async Task AssertJournalSourceReservationAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        Guid sourceEventId = Guid.CreateVersion7();
        Guid firstReservationId = Guid.CreateVersion7();
        Guid secondReservationId = Guid.CreateVersion7();
        const string sourceType = "sales.invoice";
        const string postingPurpose = "party-receivable";
        const string draftHash = "1111111111111111111111111111111111111111111111111111111111111111";

        int[] raceResults = await Task.WhenAll(
            InsertJournalSourceReservationAsync(
                appDataSource,
                tenantId,
                companyId,
                actorId,
                firstReservationId,
                sourceType,
                sourceEventId,
                postingPurpose,
                draftHash,
                ignoreDuplicate: true),
            InsertJournalSourceReservationAsync(
                appDataSource,
                tenantId,
                companyId,
                actorId,
                secondReservationId,
                sourceType,
                sourceEventId,
                postingPurpose,
                draftHash,
                ignoreDuplicate: true));
        Assert(raceResults.Sum() == 1, "Parallel journal-source reservation did not produce exactly one insert.");

        await using (NpgsqlConnection ownerConnection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction ownerTransaction = await ownerConnection.BeginTransactionAsync())
        {
            await ExecuteAsync(ownerConnection, ownerTransaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            const string sql = """
                SELECT reservation_id, journal_draft_hash
                FROM accounting.journal_source_reservation
                WHERE tenant_id = $1
                  AND company_id = $2
                  AND source_type = $3
                  AND source_event_id = $4
                  AND posting_purpose = $5
                """;
            await using (var command = new NpgsqlCommand(sql, ownerConnection, ownerTransaction))
            {
                command.Parameters.AddWithValue(tenantId);
                command.Parameters.AddWithValue(companyId);
                command.Parameters.AddWithValue(sourceType);
                command.Parameters.AddWithValue(sourceEventId);
                command.Parameters.AddWithValue(postingPurpose);
                await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
                Assert(await reader.ReadAsync(), "Parallel journal-source reservation did not persist a row.");
                Guid storedReservationId = reader.GetGuid(0);
                Assert(storedReservationId == firstReservationId || storedReservationId == secondReservationId,
                    "Journal-source reservation persisted an unexpected identity.");
                Assert(reader.GetString(1) == draftHash, "Journal-source reservation hash changed.");
                Assert(!await reader.ReadAsync(), "Parallel journal-source reservation persisted multiple rows.");
            }

            await ownerTransaction.CommitAsync();
        }

        bool conflictingDraftRejected = false;
        try
        {
            await InsertJournalSourceReservationAsync(
                appDataSource,
                tenantId,
                companyId,
                actorId,
                Guid.CreateVersion7(),
                sourceType,
                sourceEventId,
                postingPurpose,
                "2222222222222222222222222222222222222222222222222222222222222222",
                ignoreDuplicate: false);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            conflictingDraftRejected = true;
        }

        Assert(conflictingDraftRejected, "Conflicting journal draft reused an existing source reservation.");

        Assert(await InsertJournalSourceReservationAsync(
                appDataSource,
                tenantId,
                otherCompanyId,
                actorId,
                Guid.CreateVersion7(),
                sourceType,
                sourceEventId,
                postingPurpose,
                draftHash,
                ignoreDuplicate: false) == 1,
            "The same source identity did not remain isolated by company.");

        bool crossCompanyRejected = false;
        await using (NpgsqlConnection crossCompanyConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction crossCompanyTransaction = await crossCompanyConnection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(crossCompanyConnection, crossCompanyTransaction, tenantId, actorId, companyId);
            try
            {
                await InsertJournalSourceReservationCommandAsync(
                    crossCompanyConnection,
                    crossCompanyTransaction,
                    tenantId,
                    otherCompanyId,
                    actorId,
                    Guid.CreateVersion7(),
                    sourceType,
                    Guid.CreateVersion7(),
                    postingPurpose,
                    draftHash,
                    ignoreDuplicate: false);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.InsufficientPrivilege)
            {
                crossCompanyRejected = true;
            }
        }

        Assert(crossCompanyRejected, "Journal-source reservation RLS accepted a company outside request scope.");

        await using (NpgsqlConnection privilegeConnection = await appDataSource.OpenConnectionAsync())
        await using (var privilegeCommand = new NpgsqlCommand(
            """
            SELECT
                has_table_privilege(current_user, 'accounting.journal_source_reservation', 'SELECT'),
                has_table_privilege(current_user, 'accounting.journal_source_reservation', 'INSERT'),
                has_table_privilege(current_user, 'accounting.journal_source_reservation', 'UPDATE'),
                has_table_privilege(current_user, 'accounting.journal_source_reservation', 'DELETE')
            """,
            privilegeConnection))
        await using (NpgsqlDataReader privilegeReader = await privilegeCommand.ExecuteReaderAsync())
        {
            Assert(await privilegeReader.ReadAsync(), "Journal-source reservation privilege metadata was not returned.");
            Assert(privilegeReader.GetBoolean(0) && privilegeReader.GetBoolean(1),
                "Runtime role cannot read and append journal-source reservations.");
            Assert(!privilegeReader.GetBoolean(2) && !privilegeReader.GetBoolean(3),
                "Runtime role can mutate or delete journal-source reservations.");
        }

        await AssertJournalSourceMutationRejectedAsync(
            appDataSource,
            tenantId,
            companyId,
            actorId,
            "UPDATE accounting.journal_source_reservation SET journal_draft_hash = journal_draft_hash WHERE source_event_id = $1",
            sourceEventId,
            "Runtime role updated an append-only journal-source reservation.");
        await AssertJournalSourceMutationRejectedAsync(
            appDataSource,
            tenantId,
            companyId,
            actorId,
            "DELETE FROM accounting.journal_source_reservation WHERE source_event_id = $1",
            sourceEventId,
            "Runtime role deleted an append-only journal-source reservation.");
    }

    private static async Task AssertJournalSourceMutationRejectedAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        string sql,
        Guid sourceEventId,
        string failureMessage)
    {
        bool rejected = false;
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(sourceEventId);
        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.InsufficientPrivilege)
        {
            rejected = true;
        }

        Assert(rejected, failureMessage);
    }

    private static async Task<int> InsertJournalSourceReservationAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        Guid reservationId,
        string sourceType,
        Guid sourceEventId,
        string postingPurpose,
        string draftHash,
        bool ignoreDuplicate)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
        int inserted = await InsertJournalSourceReservationCommandAsync(
            connection,
            transaction,
            tenantId,
            companyId,
            actorId,
            reservationId,
            sourceType,
            sourceEventId,
            postingPurpose,
            draftHash,
            ignoreDuplicate);
        await transaction.CommitAsync();
        return inserted;
    }

    private static async Task<int> InsertJournalSourceReservationCommandAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        Guid reservationId,
        string sourceType,
        Guid sourceEventId,
        string postingPurpose,
        string draftHash,
        bool ignoreDuplicate)
    {
        string sql = """
            INSERT INTO accounting.journal_source_reservation
                (reservation_id, tenant_id, company_id, source_type, source_event_id,
                 posting_purpose, journal_draft_hash, reserved_by)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
            """ + (ignoreDuplicate
                ? " ON CONFLICT (tenant_id, company_id, source_type, source_event_id, posting_purpose) DO NOTHING"
                : string.Empty);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(reservationId);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(companyId);
        command.Parameters.AddWithValue(sourceType);
        command.Parameters.AddWithValue(sourceEventId);
        command.Parameters.AddWithValue(postingPurpose);
        command.Parameters.AddWithValue(draftHash);
        command.Parameters.AddWithValue(actorId);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertJournalSourceReservationWriterAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        Guid sourceEventId = Guid.CreateVersion7();
        Guid postingRuleVersionId = Guid.CreateVersion7();
        Guid debitAccountId = Guid.CreateVersion7();
        Guid creditAccountId = Guid.CreateVersion7();
        Guid firstReservationId = Guid.CreateVersion7();
        var scope = new ExecutionScope(tenantId, actorId, [companyId]);
        ValidatedJournalDraft draft = CreateIntegrationJournalDraft(
            tenantId,
            companyId,
            sourceEventId,
            postingRuleVersionId,
            debitAccountId,
            creditAccountId,
            amount: 100m,
            reverseLineOrder: false);
        ValidatedJournalDraft reorderedRetry = CreateIntegrationJournalDraft(
            tenantId,
            companyId,
            sourceEventId,
            postingRuleVersionId,
            debitAccountId,
            creditAccountId,
            amount: 100.0000m,
            reverseLineOrder: true);

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            JournalSourceReservationResult created = await PostgresJournalSourceReservationWriter.ReserveAsync(
                connection,
                transaction,
                scope,
                firstReservationId,
                draft);
            JournalSourceReservationResult retried = await PostgresJournalSourceReservationWriter.ReserveAsync(
                connection,
                transaction,
                scope,
                Guid.CreateVersion7(),
                reorderedRetry);

            Assert(created.Created && created.ReservationId == firstReservationId,
                "Journal-source writer did not create the first reservation.");
            Assert(!retried.Created && retried.ReservationId == firstReservationId,
                "Equivalent reordered journal retry did not return the first reservation.");
            Assert(created.DraftHash == retried.DraftHash,
                "Equivalent reordered journal lines produced different V1 fingerprints.");

            bool changedDraftRejected = false;
            try
            {
                await PostgresJournalSourceReservationWriter.ReserveAsync(
                    connection,
                    transaction,
                    scope,
                    Guid.CreateVersion7(),
                    CreateIntegrationJournalDraft(
                        tenantId,
                        companyId,
                        sourceEventId,
                        postingRuleVersionId,
                        debitAccountId,
                        creditAccountId,
                        amount: 100.0001m,
                        reverseLineOrder: false));
            }
            catch (JournalSourceReservationConflictException exception)
                when (exception.ExistingReservationId == firstReservationId)
            {
                changedDraftRejected = true;
            }

            Assert(changedDraftRejected, "Changed journal content reused an existing source reservation.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection deniedConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction deniedTransaction = await deniedConnection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(deniedConnection, deniedTransaction, tenantId, actorId, companyId);
            bool deniedBeforeSql = false;
            try
            {
                await PostgresJournalSourceReservationWriter.ReserveAsync(
                    deniedConnection,
                    deniedTransaction,
                    scope,
                    Guid.CreateVersion7(),
                    CreateIntegrationJournalDraft(
                        tenantId,
                        otherCompanyId,
                        Guid.CreateVersion7(),
                        postingRuleVersionId,
                        debitAccountId,
                        creditAccountId,
                        amount: 1m,
                        reverseLineOrder: false));
            }
            catch (ExecutionScopeDeniedException)
            {
                deniedBeforeSql = true;
            }

            Assert(deniedBeforeSql, "Journal-source writer accepted a draft outside the execution scope.");
            await deniedTransaction.RollbackAsync();
        }

        Guid rolledBackReservationId = Guid.CreateVersion7();
        await using (NpgsqlConnection rollbackConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction rollbackTransaction = await rollbackConnection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(rollbackConnection, rollbackTransaction, tenantId, actorId, companyId);
            JournalSourceReservationResult rolledBack = await PostgresJournalSourceReservationWriter.ReserveAsync(
                rollbackConnection,
                rollbackTransaction,
                scope,
                rolledBackReservationId,
                CreateIntegrationJournalDraft(
                    tenantId,
                    companyId,
                    Guid.CreateVersion7(),
                    Guid.CreateVersion7(),
                    debitAccountId,
                    creditAccountId,
                    amount: 25m,
                    reverseLineOrder: false));
            Assert(rolledBack.Created, "Rollback probe did not create its reservation inside the transaction.");
            await rollbackTransaction.RollbackAsync();
        }

        await using NpgsqlConnection ownerConnection = await migratorDataSource.OpenConnectionAsync();
        await using NpgsqlTransaction ownerTransaction = await ownerConnection.BeginTransactionAsync();
        await ExecuteAsync(ownerConnection, ownerTransaction, "SET LOCAL ROLE kagu_erp_schema_owner");
        Assert(await CountAsync(
                ownerConnection,
                ownerTransaction,
                "SELECT count(*) FROM accounting.journal_source_reservation WHERE reservation_id = $1",
                rolledBackReservationId) == 0,
            "Journal-source reservation escaped its caller-owned rollback.");
        await ownerTransaction.CommitAsync();
    }

    private static ValidatedJournalDraft CreateIntegrationJournalDraft(
        Guid tenantId,
        Guid companyId,
        Guid sourceEventId,
        Guid postingRuleVersionId,
        Guid debitAccountId,
        Guid creditAccountId,
        decimal amount,
        bool reverseLineOrder)
    {
        JournalLineDraft debit = JournalLineDraft.Create(debitAccountId, null, JournalAmount.Create(amount, 0m));
        JournalLineDraft credit = JournalLineDraft.Create(creditAccountId, null, JournalAmount.Create(0m, amount));
        JournalLineDraft[] lines = reverseLineOrder ? [credit, debit] : [debit, credit];
        return ValidatedJournalDraft.Create(
            tenantId,
            companyId,
            sourceEventId,
            postingRuleVersionId,
            "integration.invoice",
            "party-receivable",
            new DateOnly(2026, 8, 24),
            new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero),
            CurrencyCode.Create("GBP"),
            lines);
    }

    private static async Task AssertAuthoritativePeriodPostingGateAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        Guid periodId = Guid.CreateVersion7();
        await InsertAccountingPeriodAsync(
            migratorDataSource,
            periodId,
            tenantId,
            companyId,
            "2026-08",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            actorId);

        var scope = new ExecutionScope(tenantId, actorId, [companyId]);
        ValidatedJournalDraft draft = CreateIntegrationJournalDraft(
            tenantId,
            companyId,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            10m,
            reverseLineOrder: false);

        await using (NpgsqlConnection appConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction appTransaction = await appConnection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(appConnection, appTransaction, tenantId, actorId, companyId);
            ValidatedPeriodLockSet loaded = await PostgresAuthoritativePeriodGateLoader.LoadForStandardPostingAsync(
                appConnection,
                appTransaction,
                scope,
                draft);
            Assert(loaded.PeriodId == periodId, "Authoritative period gate loaded the wrong period.");
            Assert(loaded.Locks.Count == 2, "Authoritative period gate did not load both required lock scopes.");

            await using NpgsqlConnection ownerConnection = await migratorDataSource.OpenConnectionAsync();
            await using NpgsqlTransaction ownerTransaction = await ownerConnection.BeginTransactionAsync();
            await ExecuteAsync(ownerConnection, ownerTransaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using (var lockCommand = new NpgsqlCommand(
                "SELECT pg_try_advisory_xact_lock(hashtextextended($1, 0))",
                ownerConnection,
                ownerTransaction))
            {
                lockCommand.Parameters.AddWithValue($"kagu-accounting-period:{periodId:D}");
                Assert(await lockCommand.ExecuteScalarAsync() is false,
                    "Concurrent period close acquired the posting transaction's period lock.");
            }

            await ownerTransaction.RollbackAsync();
            await appTransaction.CommitAsync();
        }

        ValidatedJournalDraft missingPeriodDraft = ValidatedJournalDraft.Create(
            draft.TenantId,
            draft.CompanyId,
            Guid.CreateVersion7(),
            draft.PostingRuleVersionId,
            draft.SourceType,
            draft.PostingPurpose,
            new DateOnly(2027, 1, 1),
            draft.RecordedAt,
            draft.FunctionalCurrency,
            draft.Lines);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            AuthoritativePeriodGateException exception = await ThrowsAsync<AuthoritativePeriodGateException>(() =>
                PostgresAuthoritativePeriodGateLoader.LoadForStandardPostingAsync(
                    connection,
                    transaction,
                    scope,
                    missingPeriodDraft).AsTask());
            Assert(exception.Code == "ACCOUNTING_PERIOD_NOT_FOUND", "Missing period returned an unexpected error code.");
            await transaction.RollbackAsync();
        }

        Guid overlappingPeriodId = Guid.CreateVersion7();
        await InsertAccountingPeriodAsync(
            migratorDataSource,
            overlappingPeriodId,
            tenantId,
            companyId,
            "2026-OVERLAP",
            new DateOnly(2026, 8, 24),
            new DateOnly(2026, 8, 25),
            actorId);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            AuthoritativePeriodGateException exception = await ThrowsAsync<AuthoritativePeriodGateException>(() =>
                PostgresAuthoritativePeriodGateLoader.LoadForStandardPostingAsync(
                    connection,
                    transaction,
                    scope,
                    draft).AsTask());
            Assert(exception.Code == "ACCOUNTING_PERIOD_AMBIGUOUS", "Overlapping periods did not fail closed.");
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection ownerConnection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction ownerTransaction = await ownerConnection.BeginTransactionAsync())
        {
            await ExecuteAsync(ownerConnection, ownerTransaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using (var deleteLocks = new NpgsqlCommand(
                "DELETE FROM accounting.period_lock_state WHERE period_id = $1",
                ownerConnection,
                ownerTransaction))
            {
                deleteLocks.Parameters.AddWithValue(overlappingPeriodId);
                await deleteLocks.ExecuteNonQueryAsync();
            }

            await using (var deletePeriod = new NpgsqlCommand(
                "DELETE FROM accounting.accounting_period WHERE period_id = $1",
                ownerConnection,
                ownerTransaction))
            {
                deletePeriod.Parameters.AddWithValue(overlappingPeriodId);
                await deletePeriod.ExecuteNonQueryAsync();
            }

            await using (var closePeriod = new NpgsqlCommand(
                "UPDATE accounting.period_lock_state SET close_stage = 1, version = version + 1 WHERE period_id = $1 AND lock_scope = 2",
                ownerConnection,
                ownerTransaction))
            {
                closePeriod.Parameters.AddWithValue(periodId);
                await closePeriod.ExecuteNonQueryAsync();
            }

            await ownerTransaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            PeriodInvariantException exception = await ThrowsAsync<PeriodInvariantException>(() =>
                PostgresAuthoritativePeriodGateLoader.LoadForStandardPostingAsync(
                    connection,
                    transaction,
                    scope,
                    draft).AsTask());
            Assert(exception.Code == "PERIOD_GL_LOCK_BLOCKS_POSTING", "Closed authoritative period did not fail closed.");
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection privilegeConnection = await appDataSource.OpenConnectionAsync())
        await using (var privilegeCommand = new NpgsqlCommand(
            """
            SELECT
                has_table_privilege(current_user, 'accounting.accounting_period', 'SELECT'),
                has_table_privilege(current_user, 'accounting.accounting_period', 'UPDATE'),
                has_table_privilege(current_user, 'accounting.period_lock_state', 'SELECT'),
                has_table_privilege(current_user, 'accounting.period_lock_state', 'UPDATE')
            """,
            privilegeConnection))
        await using (NpgsqlDataReader reader = await privilegeCommand.ExecuteReaderAsync())
        {
            Assert(await reader.ReadAsync(), "Period gate privilege metadata was not returned.");
            Assert(reader.GetBoolean(0) && reader.GetBoolean(2), "Runtime role cannot read period gate state.");
            Assert(!reader.GetBoolean(1) && !reader.GetBoolean(3), "Runtime role can mutate period gate state.");
        }

        await using NpgsqlConnection crossScopeConnection = await appDataSource.OpenConnectionAsync();
        await using NpgsqlTransaction crossScopeTransaction = await crossScopeConnection.BeginTransactionAsync();
        await SetAuditScopeAsync(crossScopeConnection, crossScopeTransaction, tenantId, actorId, otherCompanyId);
        Assert(await CountAsync(
                crossScopeConnection,
                crossScopeTransaction,
                "SELECT count(*) FROM accounting.accounting_period WHERE period_id = $1",
                periodId) == 0,
            "Accounting period leaked across company scope.");
        await crossScopeTransaction.CommitAsync();

        await using NpgsqlConnection resetConnection = await migratorDataSource.OpenConnectionAsync();
        await using NpgsqlTransaction resetTransaction = await resetConnection.BeginTransactionAsync();
        await ExecuteAsync(resetConnection, resetTransaction, "SET LOCAL ROLE kagu_erp_schema_owner");
        await using (var resetCommand = new NpgsqlCommand(
            "UPDATE accounting.period_lock_state SET close_stage = 0, version = version + 1, updated_at = now(), updated_by = $2 WHERE period_id = $1 AND lock_scope = 2",
            resetConnection,
            resetTransaction))
        {
            resetCommand.Parameters.AddWithValue(periodId);
            resetCommand.Parameters.AddWithValue(actorId);
            Assert(await resetCommand.ExecuteNonQueryAsync() == 1, "Authoritative period GL lock was not reset for later checks.");
        }

        await resetTransaction.CommitAsync();
    }

    private static async Task AssertJournalPreparationOrchestrationAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid actorId)
    {
        JournalPreparationRequest committedRequest = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 40m, hasPostingPermission: true);
        await SeedJournalPreparationEvidenceAsync(migratorDataSource, committedRequest, actorId);
        await SeedJournalApprovalEvidenceAsync(migratorDataSource, committedRequest, actorId);

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            JournalPreparationCommand command = ToJournalPreparationCommand(committedRequest);
            JournalPreparationResult result = await PostgresJournalPreparationOrchestrator.PrepareFromSourceAsync(
                connection,
                transaction,
                command,
                (_, _, _, _) => ValueTask.FromResult(new CanonicalJournalPreparationSource(
                    committedRequest.Draft, committedRequest.ChartOfAccountsVersionId, 1)),
                AppendJournalPreparationAuditAsync,
                AppendJournalPreparationOutboxAsync);
            Assert(result.ReservationCreated && result.DraftCreated,
                "Journal preparation did not create its reservation and non-posted draft.");
            await transaction.CommitAsync();
        }

        await AssertAtomicJournalFactCountsAsync(
            migratorDataSource,
            committedRequest.ReservationId,
            committedRequest.JournalDraftId,
            committedRequest.AuditEventId,
            committedRequest.OutboxEventId,
            1,
            "Committed journal preparation did not persist all four facts exactly once.");

        JournalPreparationRequest idempotentRequest = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 41m, hasPostingPermission: true);
        await SeedJournalPreparationEvidenceAsync(migratorDataSource, idempotentRequest, actorId);
        await SeedJournalApprovalEvidenceAsync(migratorDataSource, idempotentRequest, actorId);
        JournalPreparationCommand idempotentCommand = ToJournalPreparationCommand(idempotentRequest);
        Guid idempotencyRecordId = Guid.CreateVersion7();
        string idempotencyKey = $"journal-preparation-{Guid.CreateVersion7():D}";
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            IdempotentJournalPreparationResult first = await PostgresIdempotentJournalPreparationOrchestrator.PrepareAsync(
                connection,
                transaction,
                idempotentCommand,
                idempotencyRecordId,
                idempotencyKey,
                PostgresIdempotencyWriter.AcquireAsync,
                PostgresIdempotencyWriter.CompleteAsync,
                (_, _, _, _) => ValueTask.FromResult(new CanonicalJournalPreparationSource(
                    idempotentRequest.Draft, idempotentRequest.ChartOfAccountsVersionId, 1)),
                AppendJournalPreparationAuditAsync,
                AppendJournalPreparationOutboxAsync);
            Assert(!first.Replayed && first.Preparation.JournalDraftId == idempotentRequest.JournalDraftId,
                "First idempotent preparation did not create the expected result.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            IdempotentJournalPreparationResult replay = await PostgresIdempotentJournalPreparationOrchestrator.PrepareAsync(
                connection,
                transaction,
                idempotentCommand with
                {
                    ReservationId = Guid.CreateVersion7(),
                    JournalDraftId = Guid.CreateVersion7(),
                    AuditEventId = Guid.CreateVersion7(),
                    OutboxEventId = Guid.CreateVersion7(),
                },
                Guid.CreateVersion7(),
                idempotencyKey,
                PostgresIdempotencyWriter.AcquireAsync,
                PostgresIdempotencyWriter.CompleteAsync,
                (_, _, _, _) => throw new InvalidOperationException("Replay must not reload the canonical source."),
                AppendJournalPreparationAuditAsync,
                AppendJournalPreparationOutboxAsync);
            Assert(replay.Replayed && replay.Preparation.JournalDraftId == idempotentRequest.JournalDraftId,
                "Idempotent replay did not return the original preparation response.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            IdempotencyKeyReusedException exception = await ThrowsAsync<IdempotencyKeyReusedException>(() =>
                PostgresIdempotentJournalPreparationOrchestrator.PrepareAsync(
                    connection,
                    transaction,
                    idempotentCommand with { ExpectedSourceVersion = 2 },
                    Guid.CreateVersion7(),
                    idempotencyKey,
                    PostgresIdempotencyWriter.AcquireAsync,
                    PostgresIdempotencyWriter.CompleteAsync,
                    (_, _, _, _) => throw new InvalidOperationException("Changed payload must fail before source load."),
                    AppendJournalPreparationAuditAsync,
                    AppendJournalPreparationOutboxAsync).AsTask());
            Assert(exception.Code == "IDEMPOTENCY_KEY_REUSED",
                "Changed source version did not return the idempotency payload conflict.");
            await transaction.RollbackAsync();
        }

        await AssertAtomicJournalFactCountsAsync(
            migratorDataSource,
            idempotentRequest.ReservationId,
            idempotentRequest.JournalDraftId,
            idempotentRequest.AuditEventId,
            idempotentRequest.OutboxEventId,
            1,
            "Idempotent journal preparation produced duplicate or missing facts.",
            41m);

        JournalPreparationRequest idempotentRollbackRequest = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 48m, hasPostingPermission: true);
        await SeedJournalPreparationEvidenceAsync(migratorDataSource, idempotentRollbackRequest, actorId);
        await SeedJournalApprovalEvidenceAsync(migratorDataSource, idempotentRollbackRequest, actorId);
        JournalPreparationCommand rollbackCommand = ToJournalPreparationCommand(idempotentRollbackRequest);
        Guid rollbackIdempotencyRecordId = Guid.CreateVersion7();
        string rollbackIdempotencyKey = $"journal-preparation-rollback-{Guid.CreateVersion7():D}";
        JournalPreparationSourceLoader rollbackSourceLoader = (_, _, _, _) =>
            ValueTask.FromResult(new CanonicalJournalPreparationSource(
                idempotentRollbackRequest.Draft, idempotentRollbackRequest.ChartOfAccountsVersionId, 1));
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            _ = await PostgresIdempotentJournalPreparationOrchestrator.PrepareAsync(
                connection, transaction, rollbackCommand, rollbackIdempotencyRecordId, rollbackIdempotencyKey,
                PostgresIdempotencyWriter.AcquireAsync, PostgresIdempotencyWriter.CompleteAsync,
                rollbackSourceLoader, AppendJournalPreparationAuditAsync, AppendJournalPreparationOutboxAsync);
            await transaction.RollbackAsync();
        }

        await AssertAtomicJournalFactCountsAsync(
            migratorDataSource,
            idempotentRollbackRequest.ReservationId,
            idempotentRollbackRequest.JournalDraftId,
            idempotentRollbackRequest.AuditEventId,
            idempotentRollbackRequest.OutboxEventId,
            0,
            "Rolled-back idempotent preparation left a partial journal fact.");

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            IdempotentJournalPreparationResult retried = await PostgresIdempotentJournalPreparationOrchestrator.PrepareAsync(
                connection, transaction, rollbackCommand, rollbackIdempotencyRecordId, rollbackIdempotencyKey,
                PostgresIdempotencyWriter.AcquireAsync, PostgresIdempotencyWriter.CompleteAsync,
                rollbackSourceLoader, AppendJournalPreparationAuditAsync, AppendJournalPreparationOutboxAsync);
            Assert(!retried.Replayed, "Rolled-back idempotency record was incorrectly replayed.");
            await transaction.CommitAsync();
        }

        await AssertAtomicJournalFactCountsAsync(
            migratorDataSource,
            idempotentRollbackRequest.ReservationId,
            idempotentRollbackRequest.JournalDraftId,
            idempotentRollbackRequest.AuditEventId,
            idempotentRollbackRequest.OutboxEventId,
            1,
            "Retry after rollback did not atomically persist journal facts.",
            48m);

        JournalPreparationRequest rolledBackRequest = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 42m, hasPostingPermission: true);
        await SeedJournalPreparationEvidenceAsync(migratorDataSource, rolledBackRequest, actorId);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            await PostgresJournalPreparationOrchestrator.PrepareAsync(
                connection, transaction, rolledBackRequest, AppendJournalPreparationAuditAsync, AppendJournalPreparationOutboxAsync);
            await transaction.RollbackAsync();
        }

        await AssertAtomicJournalFactCountsAsync(
            migratorDataSource,
            rolledBackRequest.ReservationId,
            rolledBackRequest.JournalDraftId,
            rolledBackRequest.AuditEventId,
            rolledBackRequest.OutboxEventId,
            0,
            "Rolled-back journal preparation left a partial fact.");

        JournalPreparationRequest deniedRequest = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 43m, hasPostingPermission: false);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            JournalPostingCandidateException exception = await ThrowsAsync<JournalPostingCandidateException>(() =>
                PostgresJournalPreparationOrchestrator.PrepareAsync(
                connection, transaction, deniedRequest, AppendJournalPreparationAuditAsync, AppendJournalPreparationOutboxAsync).AsTask());
            Assert(exception.Code == "JOURNAL_POST_PERMISSION_REQUIRED",
                "Unauthorized journal preparation returned an unexpected error code.");
            await transaction.RollbackAsync();
        }

        await AssertAtomicJournalFactCountsAsync(
            migratorDataSource,
            deniedRequest.ReservationId,
            deniedRequest.JournalDraftId,
            deniedRequest.AuditEventId,
            deniedRequest.OutboxEventId,
            0,
            "Unauthorized journal preparation persisted a fact.");

        JournalPreparationRequest mismatchedRequest = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 45m, hasPostingPermission: true);
        JournalPreparationRequest differentSource = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 46m, hasPostingPermission: true);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            JournalPostingCandidateException exception = await ThrowsAsync<JournalPostingCandidateException>(() =>
                PostgresJournalPreparationOrchestrator.PrepareFromSourceAsync(
                    connection,
                    transaction,
                    ToJournalPreparationCommand(mismatchedRequest),
                    (_, _, _, _) => ValueTask.FromResult(new CanonicalJournalPreparationSource(
                        differentSource.Draft, differentSource.ChartOfAccountsVersionId, 1)),
                    AppendJournalPreparationAuditAsync,
                    AppendJournalPreparationOutboxAsync).AsTask());
            Assert(exception.Code == "JOURNAL_SOURCE_IDENTITY_MISMATCH",
                "Mismatched canonical journal source returned the wrong code.");
            await transaction.RollbackAsync();
        }

        await AssertAtomicJournalFactCountsAsync(
            migratorDataSource,
            mismatchedRequest.ReservationId,
            mismatchedRequest.JournalDraftId,
            mismatchedRequest.AuditEventId,
            mismatchedRequest.OutboxEventId,
            0,
            "Mismatched canonical journal source persisted a fact.");

        JournalPreparationRequest staleVersionRequest = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 47m, hasPostingPermission: true);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            JournalPostingCandidateException exception = await ThrowsAsync<JournalPostingCandidateException>(() =>
                PostgresJournalPreparationOrchestrator.PrepareFromSourceAsync(
                    connection,
                    transaction,
                    ToJournalPreparationCommand(staleVersionRequest),
                    (_, _, _, _) => ValueTask.FromResult(new CanonicalJournalPreparationSource(
                        staleVersionRequest.Draft, staleVersionRequest.ChartOfAccountsVersionId, 2)),
                    AppendJournalPreparationAuditAsync,
                    AppendJournalPreparationOutboxAsync).AsTask());
            Assert(exception.Code == "JOURNAL_SOURCE_VERSION_MISMATCH",
                "Changed canonical source version returned the wrong code.");
            await transaction.RollbackAsync();
        }

        await AssertAtomicJournalFactCountsAsync(
            migratorDataSource,
            staleVersionRequest.ReservationId,
            staleVersionRequest.JournalDraftId,
            staleVersionRequest.AuditEventId,
            staleVersionRequest.OutboxEventId,
            0,
            "Changed canonical source version persisted a fact.");

        JournalPreparationRequest missingApprovalRequest = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 49m, hasPostingPermission: true);
        await SeedJournalPreparationEvidenceAsync(migratorDataSource, missingApprovalRequest, actorId);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            AuthoritativeApprovalEvidenceException exception = await ThrowsAsync<AuthoritativeApprovalEvidenceException>(() =>
                PostgresJournalPreparationOrchestrator.PrepareFromSourceAsync(
                    connection,
                    transaction,
                    ToJournalPreparationCommand(missingApprovalRequest),
                    (_, _, _, _) => ValueTask.FromResult(new CanonicalJournalPreparationSource(
                        missingApprovalRequest.Draft, missingApprovalRequest.ChartOfAccountsVersionId, 1)),
                    AppendJournalPreparationAuditAsync,
                    AppendJournalPreparationOutboxAsync).AsTask());
            Assert(exception.Code == "APPROVAL_COMPLETION_NOT_FOUND",
                "Missing canonical source approval returned the wrong fail-closed code.");
            await transaction.RollbackAsync();
        }

        await AssertAtomicJournalFactCountsAsync(
            migratorDataSource,
            missingApprovalRequest.ReservationId,
            missingApprovalRequest.JournalDraftId,
            missingApprovalRequest.AuditEventId,
            missingApprovalRequest.OutboxEventId,
            0,
            "Missing source approval persisted a journal fact.");

        JournalPreparationRequest closedPeriodRequest = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 44m, hasPostingPermission: true);
        await SetGeneralLedgerPeriodStageAsync(
            migratorDataSource, tenantId, companyId, actorId, closeStage: 1);
        try
        {
            await using NpgsqlConnection connection = await appDataSource.OpenConnectionAsync();
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            PeriodInvariantException exception = await ThrowsAsync<PeriodInvariantException>(() =>
                PostgresJournalPreparationOrchestrator.PrepareAsync(
                    connection,
                    transaction,
                    closedPeriodRequest,
                    AppendJournalPreparationAuditAsync,
                    AppendJournalPreparationOutboxAsync).AsTask());
            Assert(exception.Code == "PERIOD_GL_LOCK_BLOCKS_POSTING",
                "Closed-period journal preparation returned an unexpected error code.");
            await transaction.RollbackAsync();
        }
        finally
        {
            await SetGeneralLedgerPeriodStageAsync(
                migratorDataSource, tenantId, companyId, actorId, closeStage: 0);
        }

        await AssertAtomicJournalFactCountsAsync(
            migratorDataSource,
            closedPeriodRequest.ReservationId,
            closedPeriodRequest.JournalDraftId,
            closedPeriodRequest.AuditEventId,
            closedPeriodRequest.OutboxEventId,
            0,
            "Closed-period journal preparation persisted a fact.");
    }

    private static async Task SetGeneralLedgerPeriodStageAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        short closeStage)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
        await using var command = new NpgsqlCommand(
            "UPDATE accounting.period_lock_state SET close_stage = $4, version = version + 1, updated_at = now(), updated_by = $3 WHERE tenant_id = $1 AND company_id = $2 AND lock_scope = 2",
            connection,
            transaction);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(companyId);
        command.Parameters.AddWithValue(actorId);
        command.Parameters.AddWithValue(closeStage);
        Assert(await command.ExecuteNonQueryAsync() == 1,
            "Journal preparation test could not change the authoritative GL period stage.");
        await transaction.CommitAsync();
    }

    private static JournalPreparationRequest CreateJournalPreparationRequest(
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        decimal amount,
        bool hasPostingPermission)
    {
        Guid postingRuleVersionId = Guid.CreateVersion7();
        Guid chartVersionId = Guid.CreateVersion7();
        Guid debitAccountId = Guid.CreateVersion7();
        Guid creditAccountId = Guid.CreateVersion7();
        Guid dimensionId = Guid.CreateVersion7();
        CurrencyCode gbp = CurrencyCode.Create("GBP");
        ExchangeRateSnapshot rate = ExchangeRateSnapshot.Create(
            tenantId, companyId, Guid.CreateVersion7(), 1, gbp, gbp, "spot", "integration", new DateOnly(2026, 8, 24), 1m, 1m);
        RoundingPolicySnapshot rounding = RoundingPolicySnapshot.Create(
            tenantId, companyId, Guid.CreateVersion7(), 1, 4, RoundingMode.ToEven);
        JournalCurrencyAmountSnapshot debitCurrency = JournalCurrencyAmountSnapshot.Create(
            rate, rounding, JournalAmount.Create(amount, 0m));
        JournalCurrencyAmountSnapshot creditCurrency = JournalCurrencyAmountSnapshot.Create(
            rate, rounding, JournalAmount.Create(0m, amount));
        ValidatedJournalDraft draft = ValidatedJournalDraft.Create(
            tenantId,
            companyId,
            Guid.CreateVersion7(),
            postingRuleVersionId,
            "integration.invoice",
            "journal-preparation",
            new DateOnly(2026, 8, 24),
            new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero),
            gbp,
            [
                JournalLineDraft.Create(debitAccountId, null, debitCurrency.FunctionalAmount,
                    [DimensionAssignment.Create(dimensionId, Guid.CreateVersion7())], debitCurrency),
                JournalLineDraft.Create(creditAccountId, null, creditCurrency.FunctionalAmount,
                    [DimensionAssignment.Create(dimensionId, Guid.CreateVersion7())], creditCurrency),
            ]);
        string[] permissions = hasPostingPermission ? [AuthorizedJournalPostingCandidate.RequiredPermission] : [];
        var scope = new ExecutionScope(tenantId, actorId, [new CompanyAccess(companyId, permissions)]);
        var auditContext = new RequestAuditContext(
            Guid.CreateVersion7(), "journal-preparation-integration-trace", tenantId, actorId,
            new HashSet<Guid> { companyId }, "synthetic-integration-session");
        return new JournalPreparationRequest(
            scope, auditContext, draft, chartVersionId,
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
    }

    private static async Task AssertPostedJournalPersistenceAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        JournalPreparationRequest request = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 51m, hasPostingPermission: true);
        await SeedJournalPreparationEvidenceAsync(migratorDataSource, request, actorId);
        await SeedJournalApprovalEvidenceAsync(migratorDataSource, request, actorId);
        Guid journalId = Guid.CreateVersion7();
        DateTimeOffset postedAt = DateTimeOffset.UtcNow;
        PostedJournalPersistenceResult first;

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            ValidatedPeriodLockSet periodLocks = await PostgresAuthoritativePeriodGateLoader.LoadForStandardPostingAsync(
                connection, transaction, request.Scope, request.Draft);
            ApprovalCompletionEvidence approval = await PostgresAuthoritativeApprovalCompletionLoader.LoadAsync(
                connection, transaction, request.Scope, tenantId, companyId,
                request.Draft.SourceType, request.Draft.SourceEventId, 1);
            JournalSourceReservationResult reservation = await PostgresJournalSourceReservationWriter.ReserveAsync(
                connection, transaction, request.Scope, request.ReservationId, request.Draft);
            ValidatedJournalDraftPersistenceResult draft = await PostgresValidatedJournalDraftWriter.PersistAsync(
                connection, transaction, request.Scope, request.JournalDraftId, reservation, request.Draft);
            first = await PostgresPostedJournalWriter.PersistAsync(
                connection, transaction, request.Scope, journalId, draft, request.Draft, 1,
                approval, periodLocks, postedAt);
            Assert(first.Created && first.JournalId == journalId, "Posted journal writer did not create the expected journal.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            ValidatedPeriodLockSet periodLocks = await PostgresAuthoritativePeriodGateLoader.LoadForStandardPostingAsync(
                connection, transaction, request.Scope, request.Draft);
            ApprovalCompletionEvidence approval = await PostgresAuthoritativeApprovalCompletionLoader.LoadAsync(
                connection, transaction, request.Scope, tenantId, companyId,
                request.Draft.SourceType, request.Draft.SourceEventId, 1);
            var persistedDraft = new ValidatedJournalDraftPersistenceResult(
                request.JournalDraftId, false, JournalDraftFingerprintV1.Compute(request.Draft));
            PostedJournalPersistenceResult replay = await PostgresPostedJournalWriter.PersistAsync(
                connection, transaction, request.Scope, Guid.CreateVersion7(), persistedDraft, request.Draft, 1,
                approval, periodLocks, DateTimeOffset.UtcNow);
            Assert(!replay.Created && replay.JournalId == journalId && replay.PostedAt == first.PostedAt,
                "Posted journal retry did not return the immutable first result.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            long headerCount = await CountAsync(
                connection, transaction,
                "SELECT count(*) FROM accounting.posted_journal WHERE journal_id = $1 AND total_debit = total_credit",
                journalId);
            long lineCount = await CountAsync(
                connection, transaction,
                "SELECT count(*) FROM accounting.posted_journal_line WHERE journal_id = $1",
                journalId);
            Assert(headerCount == 1 && lineCount == request.Draft.Lines.Count,
                "Posted journal header/line snapshot is incomplete or unbalanced.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using var tamper = new NpgsqlCommand(
                "UPDATE accounting.posted_journal_line SET credit = credit + 1 WHERE journal_id = $1 AND credit > 0",
                connection,
                transaction);
            tamper.Parameters.AddWithValue(journalId);
            Assert(await tamper.ExecuteNonQueryAsync() == 1, "Posted journal tamper fixture did not target one credit line.");
            PostgresException exception = await ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert(exception.SqlState == "23514" && exception.ConstraintName == "ck_posted_journal_cross_foot",
                "Database did not reject a posted journal line/header cross-foot mismatch.");
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, otherCompanyId);
            Assert(await CountAsync(
                    connection, transaction,
                    "SELECT count(*) FROM accounting.posted_journal WHERE journal_id = $1",
                    journalId) == 0,
                "Posted journal leaked across company scope.");
            await transaction.CommitAsync();
        }

        await using NpgsqlConnection privilegeConnection = await appDataSource.OpenConnectionAsync();
        await using var privilegeCommand = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user, 'accounting.posted_journal', 'SELECT'), has_table_privilege(current_user, 'accounting.posted_journal', 'INSERT'), has_table_privilege(current_user, 'accounting.posted_journal', 'UPDATE'), has_table_privilege(current_user, 'accounting.posted_journal_line', 'DELETE')",
            privilegeConnection);
        await using NpgsqlDataReader reader = await privilegeCommand.ExecuteReaderAsync();
        Assert(await reader.ReadAsync(), "Posted journal privilege metadata was not returned.");
        Assert(reader.GetBoolean(0) && reader.GetBoolean(1) && !reader.GetBoolean(2) && !reader.GetBoolean(3),
            "Runtime posted journal privileges are not append-only.");
    }

    private static async Task AssertJournalPostingCompositionAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid actorId)
    {
        JournalPreparationRequest request = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 52m, hasPostingPermission: true);
        await SeedJournalPreparationEvidenceAsync(migratorDataSource, request, actorId);
        await SeedJournalApprovalEvidenceAsync(migratorDataSource, request, actorId);
        var command = new JournalPostingCommand(
            ToJournalPreparationCommand(request), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            JournalPostingResult result = await PostgresJournalPostingOrchestrator.PostFromSourceAsync(
                connection,
                transaction,
                command,
                (_, _, _, _) => ValueTask.FromResult(new CanonicalJournalPreparationSource(
                    request.Draft, request.ChartOfAccountsVersionId, 1)),
                AppendJournalPreparationAuditAsync,
                AppendJournalPreparationOutboxAsync,
                AppendJournalPostedAuditAsync,
                AppendJournalPostedOutboxAsync);
            Assert(result.PostedJournal.Created && result.PostedJournal.JournalId == command.JournalId,
                "Journal posting composition did not create the expected posted result.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM accounting.posted_journal WHERE journal_id = $1", command.JournalId) == 1,
                "Committed posting composition did not persist its posted journal.");
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM platform.audit_event WHERE id = $1", command.PostedAuditEventId) == 1,
                "Committed posting composition did not persist its posted audit fact.");
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM platform.outbox_message WHERE event_id = $1", command.PostedOutboxEventId) == 1,
                "Committed posting composition did not persist its posted outbox fact.");
            await transaction.CommitAsync();
        }

        JournalPreparationRequest rollbackRequest = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 53m, hasPostingPermission: true);
        await SeedJournalPreparationEvidenceAsync(migratorDataSource, rollbackRequest, actorId);
        await SeedJournalApprovalEvidenceAsync(migratorDataSource, rollbackRequest, actorId);
        var rollbackCommand = new JournalPostingCommand(
            ToJournalPreparationCommand(rollbackRequest), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            await ThrowsAsync<InvalidOperationException>(() =>
                PostgresJournalPostingOrchestrator.PostFromSourceAsync(
                    connection,
                    transaction,
                    rollbackCommand,
                    (_, _, _, _) => ValueTask.FromResult(new CanonicalJournalPreparationSource(
                        rollbackRequest.Draft, rollbackRequest.ChartOfAccountsVersionId, 1)),
                    AppendJournalPreparationAuditAsync,
                    AppendJournalPreparationOutboxAsync,
                    AppendJournalPostedAuditAsync,
                    (_, _, _, _, _) => ValueTask.FromResult(false)).AsTask());
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM accounting.posted_journal WHERE journal_id = $1", rollbackCommand.JournalId) == 0,
                "Rolled-back posting composition left a posted journal.");
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM platform.audit_event WHERE id = $1", rollbackCommand.PostedAuditEventId) == 0,
                "Rolled-back posting composition left a posted audit fact.");
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM platform.outbox_message WHERE event_id = $1", rollbackCommand.PostedOutboxEventId) == 0,
                "Rolled-back posting composition left a posted outbox fact.");
            await transaction.CommitAsync();
        }
    }

    private static async Task AssertIdempotentJournalPostingCompositionAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid actorId)
    {
        JournalPreparationRequest request = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 54m, hasPostingPermission: true);
        await SeedJournalPreparationEvidenceAsync(migratorDataSource, request, actorId);
        await SeedJournalApprovalEvidenceAsync(migratorDataSource, request, actorId);
        var command = new JournalPostingCommand(
            ToJournalPreparationCommand(request), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        Guid idempotencyRecordId = Guid.CreateVersion7();
        string idempotencyKey = $"journal-post-{Guid.CreateVersion7():D}";
        int sourceLoadCount = 0;

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            IdempotentJournalPostingResult first = await PostgresIdempotentJournalPostingOrchestrator.PostAsync(
                connection, transaction, command, idempotencyRecordId, idempotencyKey,
                PostgresIdempotencyWriter.AcquireAsync, PostgresIdempotencyWriter.CompleteAsync,
                (_, _, _, _) =>
                {
                    sourceLoadCount++;
                    return ValueTask.FromResult(new CanonicalJournalPreparationSource(
                        request.Draft, request.ChartOfAccountsVersionId, 1));
                },
                AppendJournalPreparationAuditAsync, AppendJournalPreparationOutboxAsync,
                AppendJournalPostedAuditAsync, AppendJournalPostedOutboxAsync);
            Assert(!first.Replayed && first.Posting.PostedJournal.JournalId == command.JournalId,
                "Idempotent posting did not return its first posted result.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            JournalPostingCommand retryCommand = command with
            {
                JournalId = Guid.CreateVersion7(),
                PostedAuditEventId = Guid.CreateVersion7(),
                PostedOutboxEventId = Guid.CreateVersion7(),
                PostedAt = DateTimeOffset.UtcNow,
            };
            IdempotentJournalPostingResult replay = await PostgresIdempotentJournalPostingOrchestrator.PostAsync(
                connection, transaction, retryCommand, Guid.CreateVersion7(), idempotencyKey,
                PostgresIdempotencyWriter.AcquireAsync, PostgresIdempotencyWriter.CompleteAsync,
                (_, _, _, _) => throw new InvalidOperationException("Replay must not reload the source."),
                AppendJournalPreparationAuditAsync, AppendJournalPreparationOutboxAsync,
                AppendJournalPostedAuditAsync, AppendJournalPostedOutboxAsync);
            Assert(replay.Replayed && replay.Posting.PostedJournal.JournalId == command.JournalId && sourceLoadCount == 1,
                "Idempotent posting replay did not return the immutable first response.");

            JournalPostingCommand changedVersion = retryCommand with
            {
                Preparation = retryCommand.Preparation with { ExpectedSourceVersion = 2 },
            };
            IdempotencyKeyReusedException exception = await ThrowsAsync<IdempotencyKeyReusedException>(() =>
                PostgresIdempotentJournalPostingOrchestrator.PostAsync(
                    connection, transaction, changedVersion, Guid.CreateVersion7(), idempotencyKey,
                    PostgresIdempotencyWriter.AcquireAsync, PostgresIdempotencyWriter.CompleteAsync,
                    (_, _, _, _) => throw new InvalidOperationException("Conflict must precede source loading."),
                    AppendJournalPreparationAuditAsync, AppendJournalPreparationOutboxAsync,
                    AppendJournalPostedAuditAsync, AppendJournalPostedOutboxAsync).AsTask());
            Assert(exception.Code == "IDEMPOTENCY_KEY_REUSED",
                "Changed posting payload did not return the idempotency-key reuse conflict.");
            await transaction.CommitAsync();
        }

        JournalPreparationRequest rollbackRequest = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 55m, hasPostingPermission: true);
        await SeedJournalPreparationEvidenceAsync(migratorDataSource, rollbackRequest, actorId);
        await SeedJournalApprovalEvidenceAsync(migratorDataSource, rollbackRequest, actorId);
        var rollbackCommand = new JournalPostingCommand(
            ToJournalPreparationCommand(rollbackRequest), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        Guid rollbackRecordId = Guid.CreateVersion7();
        string rollbackKey = $"journal-post-rollback-{Guid.CreateVersion7():D}";
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            await ThrowsAsync<InvalidOperationException>(() =>
                PostgresIdempotentJournalPostingOrchestrator.PostAsync(
                    connection, transaction, rollbackCommand, rollbackRecordId, rollbackKey,
                    PostgresIdempotencyWriter.AcquireAsync, PostgresIdempotencyWriter.CompleteAsync,
                    (_, _, _, _) => ValueTask.FromResult(new CanonicalJournalPreparationSource(
                        rollbackRequest.Draft, rollbackRequest.ChartOfAccountsVersionId, 1)),
                    AppendJournalPreparationAuditAsync, AppendJournalPreparationOutboxAsync,
                    AppendJournalPostedAuditAsync, (_, _, _, _, _) => ValueTask.FromResult(false)).AsTask());
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM platform.idempotency_record WHERE record_id = $1", rollbackRecordId) == 0,
                "Rolled-back posting left an idempotency record.");
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM accounting.posted_journal WHERE journal_id = $1", rollbackCommand.JournalId) == 0,
                "Rolled-back idempotent posting left a posted journal.");
            await transaction.CommitAsync();
        }
    }

    private static async Task AssertPostedJournalReversalLinkAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        JournalPreparationRequest original = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 56m, hasPostingPermission: true);
        Guid originalJournalId = Guid.CreateVersion7();
        await PostJournalFixtureAsync(
            migratorDataSource, appDataSource, original, originalJournalId, actorId);

        var reversalDomain = KaguERP.Modules.Accounting.Domain.Reversals.JournalReversalDraft.Create(
            originalJournalId,
            original.Draft,
            Guid.CreateVersion7(),
            "accounting.alternate-posted-journal-reversal",
            "full-reversal",
            original.Draft.EffectiveDate,
            DateTimeOffset.UtcNow);
        JournalPreparationRequest reversal = CreatePreparationRequestForDraft(original, reversalDomain.ReversalJournalDraft);
        Guid reversalJournalId = Guid.CreateVersion7();
        await PostJournalFixtureAsync(
            migratorDataSource, appDataSource, reversal, reversalJournalId, actorId, seedCurrencyEvidence: false);

        DateTimeOffset linkedAt = DateTimeOffset.UtcNow;
        PostedJournalReversalLinkResult first;
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            first = await PostgresPostedJournalReversalLinkWriter.PersistAsync(
                connection, transaction, original.Scope, companyId,
                originalJournalId, reversalJournalId, linkedAt);
            Assert(first.Created && first.OriginalJournalId == originalJournalId &&
                   first.ReversalJournalId == reversalJournalId,
                "Posted reversal link writer did not create the expected link.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            PostedJournalReversalLinkResult replay = await PostgresPostedJournalReversalLinkWriter.PersistAsync(
                connection, transaction, original.Scope, companyId,
                originalJournalId, reversalJournalId, DateTimeOffset.UtcNow);
            Assert(!replay.Created && replay.LinkedAt == first.LinkedAt,
                "Posted reversal link retry did not return the immutable first result.");
            await transaction.CommitAsync();
        }

        var alternateReversalDomain = KaguERP.Modules.Accounting.Domain.Reversals.JournalReversalDraft.Create(
            originalJournalId,
            original.Draft,
            Guid.CreateVersion7(),
            "accounting.posted-journal-reversal",
            "alternate-full-reversal",
            original.Draft.EffectiveDate,
            DateTimeOffset.UtcNow);
        JournalPreparationRequest alternateReversal = CreatePreparationRequestForDraft(
            original, alternateReversalDomain.ReversalJournalDraft);
        Guid alternateReversalJournalId = Guid.CreateVersion7();
        await PostJournalFixtureAsync(
            migratorDataSource, appDataSource, alternateReversal, alternateReversalJournalId, actorId,
            seedCurrencyEvidence: false);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            PostedJournalReversalConflictException conflict = await ThrowsAsync<PostedJournalReversalConflictException>(() =>
                PostgresPostedJournalReversalLinkWriter.PersistAsync(
                    connection, transaction, original.Scope, companyId,
                    originalJournalId, alternateReversalJournalId, DateTimeOffset.UtcNow).AsTask());
            Assert(conflict.Code == "POSTED_JOURNAL_ALREADY_REVERSED" &&
                   conflict.ExistingReversalJournalId == reversalJournalId,
                "Second posted reversal returned the wrong conflict evidence.");
            await transaction.RollbackAsync();
        }

        JournalPreparationRequest unmatchedOriginal = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 57m, hasPostingPermission: true);
        Guid unmatchedOriginalJournalId = Guid.CreateVersion7();
        await PostJournalFixtureAsync(
            migratorDataSource, appDataSource, unmatchedOriginal, unmatchedOriginalJournalId, actorId);
        JournalPreparationRequest nonReversal = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 57m, hasPostingPermission: true);
        Guid nonReversalJournalId = Guid.CreateVersion7();
        await PostJournalFixtureAsync(
            migratorDataSource, appDataSource, nonReversal, nonReversalJournalId, actorId);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            PostgresException mismatch = await ThrowsAsync<PostgresException>(() =>
                PostgresPostedJournalReversalLinkWriter.PersistAsync(
                    connection, transaction, unmatchedOriginal.Scope, companyId,
                    unmatchedOriginalJournalId, nonReversalJournalId, DateTimeOffset.UtcNow).AsTask());
            Assert(mismatch.SqlState == "23514" && mismatch.ConstraintName == "ck_posted_journal_reversal_exact",
                "Database did not reject a non-opposite posted reversal.");
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, otherCompanyId);
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM accounting.posted_journal_reversal WHERE original_journal_id = $1",
                    originalJournalId) == 0,
                "Posted reversal link leaked across company scope.");
            await transaction.CommitAsync();
        }

        await using NpgsqlConnection privilegeConnection = await appDataSource.OpenConnectionAsync();
        await using var privilegeCommand = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user, 'accounting.posted_journal_reversal', 'SELECT'), has_table_privilege(current_user, 'accounting.posted_journal_reversal', 'INSERT'), has_table_privilege(current_user, 'accounting.posted_journal_reversal', 'UPDATE'), has_table_privilege(current_user, 'accounting.posted_journal_reversal', 'DELETE')",
            privilegeConnection);
        await using NpgsqlDataReader reader = await privilegeCommand.ExecuteReaderAsync();
        Assert(await reader.ReadAsync(), "Posted reversal privilege metadata was not returned.");
        Assert(reader.GetBoolean(0) && reader.GetBoolean(1) && !reader.GetBoolean(2) && !reader.GetBoolean(3),
            "Runtime posted reversal privileges are not append-only.");

        await AssertConcurrentPostedJournalReversalWinnerAsync(
            migratorDataSource, appDataSource, tenantId, companyId, actorId);
    }

    private static async Task AssertConcurrentPostedJournalReversalWinnerAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid actorId)
    {
        JournalPreparationRequest original = CreateJournalPreparationRequest(
            tenantId, companyId, actorId, 58m, hasPostingPermission: true);
        Guid originalJournalId = Guid.CreateVersion7();
        await PostJournalFixtureAsync(
            migratorDataSource, appDataSource, original, originalJournalId, actorId);

        JournalPreparationRequest firstReversal = CreatePreparationRequestForDraft(
            original,
            KaguERP.Modules.Accounting.Domain.Reversals.JournalReversalDraft.Create(
                originalJournalId,
                original.Draft,
                Guid.CreateVersion7(),
                "accounting.concurrent-reversal-a",
                "full-reversal-a",
                original.Draft.EffectiveDate,
                DateTimeOffset.UtcNow).ReversalJournalDraft);
        JournalPreparationRequest secondReversal = CreatePreparationRequestForDraft(
            original,
            KaguERP.Modules.Accounting.Domain.Reversals.JournalReversalDraft.Create(
                originalJournalId,
                original.Draft,
                Guid.CreateVersion7(),
                "accounting.concurrent-reversal-b",
                "full-reversal-b",
                original.Draft.EffectiveDate,
                DateTimeOffset.UtcNow).ReversalJournalDraft);
        Guid firstReversalJournalId = Guid.CreateVersion7();
        Guid secondReversalJournalId = Guid.CreateVersion7();
        await PostJournalFixtureAsync(
            migratorDataSource, appDataSource, firstReversal, firstReversalJournalId, actorId,
            seedCurrencyEvidence: false);
        await PostJournalFixtureAsync(
            migratorDataSource, appDataSource, secondReversal, secondReversalJournalId, actorId,
            seedCurrencyEvidence: false);

        await using NpgsqlConnection firstConnection = await appDataSource.OpenConnectionAsync();
        await using NpgsqlConnection secondConnection = await appDataSource.OpenConnectionAsync();
        await using NpgsqlTransaction firstTransaction = await firstConnection.BeginTransactionAsync();
        await using NpgsqlTransaction secondTransaction = await secondConnection.BeginTransactionAsync();
        await SetAuditScopeAsync(firstConnection, firstTransaction, tenantId, actorId, companyId);
        await SetAuditScopeAsync(secondConnection, secondTransaction, tenantId, actorId, companyId);

        PostedJournalReversalLinkResult winner = await PostgresPostedJournalReversalLinkWriter.PersistAsync(
            firstConnection,
            firstTransaction,
            original.Scope,
            companyId,
            originalJournalId,
            firstReversalJournalId,
            DateTimeOffset.UtcNow);
        Task<PostedJournalReversalLinkResult> blockedLoser =
            PostgresPostedJournalReversalLinkWriter.PersistAsync(
                secondConnection,
                secondTransaction,
                original.Scope,
                companyId,
                originalJournalId,
                secondReversalJournalId,
                DateTimeOffset.UtcNow).AsTask();
        await Task.Yield();
        Assert(!blockedLoser.IsCompleted,
            "Concurrent reversal contender did not wait on the original-journal uniqueness lock.");
        await firstTransaction.CommitAsync();

        PostedJournalReversalConflictException conflict = await ThrowsAsync<PostedJournalReversalConflictException>(
            () => blockedLoser);
        Assert(winner.Created && winner.ReversalJournalId == firstReversalJournalId &&
               conflict.ExistingReversalJournalId == firstReversalJournalId,
            "Concurrent reversal race did not preserve the single winning reversal.");
        await secondTransaction.RollbackAsync();
    }

    private static async Task AssertDueSchedulePersistenceAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        Guid partyId = Guid.CreateVersion7();
        Guid partyAccountId = Guid.CreateVersion7();
        Guid sourceEventId = Guid.CreateVersion7();
        Guid controlAccountId = Guid.CreateVersion7();
        var currency = AllocationCurrencyCode.Create("GBP");
        DueScheduleLine[] lines =
        [
            DueScheduleLine.Create(
                tenantId, companyId, partyAccountId, sourceEventId, Guid.CreateVersion7(), currency,
                40m, new DateOnly(2026, 9, 30), Guid.CreateVersion7(), 1, controlAccountId),
            DueScheduleLine.Create(
                tenantId, companyId, partyAccountId, sourceEventId, Guid.CreateVersion7(), currency,
                60m, new DateOnly(2026, 10, 31), Guid.CreateVersion7(), 1, controlAccountId),
        ];
        ValidatedDueSchedule schedule = ValidatedDueSchedule.Create(
            tenantId, companyId, partyAccountId, sourceEventId, currency, 100m, lines);
        Guid dueScheduleId = Guid.CreateVersion7();
        var command = new DueSchedulePersistenceCommand(
            new ExecutionScope(tenantId, actorId, [companyId]),
            partyId,
            dueScheduleId,
            "sales.invoice",
            1,
            controlAccountId,
            DateTimeOffset.UtcNow,
            schedule);
        DueSchedulePersistenceResult first;

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            first = await PostgresDueScheduleWriter.PersistAsync(connection, transaction, command);
            Assert(first.Created && first.DueScheduleId == dueScheduleId,
                "Due schedule writer did not create the expected immutable schedule.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            DueSchedulePersistenceResult replay = await PostgresDueScheduleWriter.PersistAsync(
                connection, transaction, command with { DueScheduleId = Guid.CreateVersion7() });
            Assert(!replay.Created && replay.DueScheduleId == dueScheduleId && replay.RecordedAt == first.RecordedAt,
                "Due schedule retry did not return the immutable first result.");
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM party.due_schedule_line WHERE due_schedule_id = $1", dueScheduleId) == 2,
                "Due schedule did not persist every installment line.");
            LoadedDueSchedule? loaded = await PostgresDueScheduleLoader.LoadAsync(
                connection, transaction, command.Scope, companyId, dueScheduleId);
            Assert(loaded is not null && loaded.DueScheduleId == dueScheduleId &&
                   loaded.SourceType == "sales.invoice" && loaded.SourceVersion == 1 &&
                   loaded.RecordedAt == first.RecordedAt && loaded.Schedule.SourceOriginalAmount == 100m &&
                   loaded.Schedule.Lines.Count == 2 && loaded.Schedule.Lines[0].OriginalAmount == 40m &&
                   loaded.Schedule.Lines[1].OriginalAmount == 60m,
                "Authoritative due schedule loader did not reconstruct the immutable domain snapshot.");

            DueScheduleLine[] changedLines =
            [
                DueScheduleLine.Create(
                    tenantId, companyId, partyAccountId, sourceEventId, lines[0].DueScheduleLineId, currency,
                    30m, lines[0].DueDate, lines[0].PaymentTermSnapshotId, 1, controlAccountId),
                DueScheduleLine.Create(
                    tenantId, companyId, partyAccountId, sourceEventId, lines[1].DueScheduleLineId, currency,
                    70m, lines[1].DueDate, lines[1].PaymentTermSnapshotId, 1, controlAccountId),
            ];
            ValidatedDueSchedule changedSchedule = ValidatedDueSchedule.Create(
                tenantId, companyId, partyAccountId, sourceEventId, currency, 100m, changedLines);
            DueSchedulePersistenceConflictException replayConflict =
                await ThrowsAsync<DueSchedulePersistenceConflictException>(() =>
                    PostgresDueScheduleWriter.PersistAsync(
                        connection,
                        transaction,
                        command with { DueScheduleId = Guid.CreateVersion7(), Schedule = changedSchedule }).AsTask());
            Assert(replayConflict.ExistingDueScheduleId == dueScheduleId,
                "Due schedule retry accepted different installment content for the same source version.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            Guid invalidScheduleId = Guid.CreateVersion7();
            await using (var header = new NpgsqlCommand(
                "INSERT INTO party.due_schedule (tenant_id,company_id,due_schedule_id,party_account_id,source_type,source_event_id,source_version,currency,source_original_amount,recorded_at,recorded_by,line_count) VALUES ($1,$2,$3,$4,'sales.invoice',$5,1,'GBP',100,$6,$7,1)",
                connection, transaction))
            {
                header.Parameters.AddWithValue(tenantId);
                header.Parameters.AddWithValue(companyId);
                header.Parameters.AddWithValue(invalidScheduleId);
                header.Parameters.AddWithValue(partyAccountId);
                header.Parameters.AddWithValue(Guid.CreateVersion7());
                header.Parameters.AddWithValue(DateTimeOffset.UtcNow);
                header.Parameters.AddWithValue(actorId);
                await header.ExecuteNonQueryAsync();
            }
            await using (var line = new NpgsqlCommand(
                "INSERT INTO party.due_schedule_line (tenant_id,company_id,due_schedule_id,due_schedule_line_id,party_account_id,source_event_id,currency,original_amount,due_date,payment_term_snapshot_id,payment_term_version,control_account_id) SELECT $1,$2,$3,$4,$5,source_event_id,'GBP',99,DATE '2026-12-31',$6,1,$7 FROM party.due_schedule WHERE due_schedule_id=$3",
                connection, transaction))
            {
                line.Parameters.AddWithValue(tenantId);
                line.Parameters.AddWithValue(companyId);
                line.Parameters.AddWithValue(invalidScheduleId);
                line.Parameters.AddWithValue(Guid.CreateVersion7());
                line.Parameters.AddWithValue(partyAccountId);
                line.Parameters.AddWithValue(Guid.CreateVersion7());
                line.Parameters.AddWithValue(controlAccountId);
                await line.ExecuteNonQueryAsync();
            }
            PostgresException exception = await ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert(exception.SqlState == "23514" && exception.ConstraintName == "ck_due_schedule_exact_total",
                "Database did not reject a due schedule whose installments fail to cross-foot.");
        }

        Guid concurrentSourceId = Guid.CreateVersion7();
        Guid concurrentLineId = Guid.CreateVersion7();
        ValidatedDueSchedule concurrentSchedule = ValidatedDueSchedule.Create(
            tenantId,
            companyId,
            partyAccountId,
            concurrentSourceId,
            currency,
            25m,
            [DueScheduleLine.Create(
                tenantId, companyId, partyAccountId, concurrentSourceId, concurrentLineId, currency,
                25m, new DateOnly(2026, 11, 30), Guid.CreateVersion7(), 1, controlAccountId)]);
        var concurrentCommand = command with
        {
            DueScheduleId = Guid.CreateVersion7(),
            SourceVersion = 1,
            Schedule = concurrentSchedule,
        };
        await using (NpgsqlConnection firstConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction firstTransaction = await firstConnection.BeginTransactionAsync())
        await using (NpgsqlConnection secondConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction secondTransaction = await secondConnection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(firstConnection, firstTransaction, tenantId, actorId, companyId);
            await SetAuditScopeAsync(secondConnection, secondTransaction, tenantId, actorId, companyId);
            DueSchedulePersistenceResult winner = await PostgresDueScheduleWriter.PersistAsync(
                firstConnection, firstTransaction, concurrentCommand);
            Task<DueSchedulePersistenceResult> blockedLoser = PostgresDueScheduleWriter.PersistAsync(
                secondConnection,
                secondTransaction,
                concurrentCommand with { DueScheduleId = Guid.CreateVersion7() }).AsTask();
            await Task.Yield();
            Assert(!blockedLoser.IsCompleted,
                "Concurrent due-schedule contender did not wait on the source-version uniqueness lock.");
            await firstTransaction.CommitAsync();
            DueSchedulePersistenceResult loser = await blockedLoser;
            Assert(winner.Created && !loser.Created && loser.DueScheduleId == winner.DueScheduleId,
                "Concurrent due-schedule race did not preserve the single immutable winner.");
            await secondTransaction.CommitAsync();
        }

        Guid paymentId = Guid.CreateVersion7();
        DateTimeOffset impactRecordedAt = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        OpenItemImpactEvent allocation = OpenItemImpactEvent.Create(
            Guid.CreateVersion7(), tenantId, companyId, partyAccountId, lines[0].DueScheduleLineId,
            paymentId, currency, OpenItemImpactKind.Allocation, 20m,
            new DateOnly(2026, 9, 15), impactRecordedAt);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            OpenItemImpactPersistenceResult created = await PostgresOpenItemImpactWriter.PersistAsync(
                connection, transaction, command.Scope, allocation);
            OpenItemImpactPersistenceResult replay = await PostgresOpenItemImpactWriter.PersistAsync(
                connection, transaction, command.Scope, allocation);
            Assert(created.Created && !replay.Created && created.EventId == replay.EventId,
                "Open-item impact writer did not return the immutable first event on retry.");
            OpenItemImpactEvent changed = OpenItemImpactEvent.Create(
                allocation.EventId, tenantId, companyId, partyAccountId, lines[0].DueScheduleLineId,
                paymentId, currency, OpenItemImpactKind.Allocation, 21m,
                allocation.EffectiveDate, allocation.RecordedAt);
            OpenItemImpactPersistenceConflictException conflict =
                await ThrowsAsync<OpenItemImpactPersistenceConflictException>(() =>
                    PostgresOpenItemImpactWriter.PersistAsync(
                        connection, transaction, command.Scope, changed).AsTask());
            Assert(conflict.EventId == allocation.EventId,
                "Open-item impact identity accepted different immutable content.");
            OpenItemImpactEvent unallocation = OpenItemImpactEvent.Create(
                Guid.CreateVersion7(), tenantId, companyId, partyAccountId, lines[0].DueScheduleLineId,
                paymentId, currency, OpenItemImpactKind.Unallocation, 20m,
                new DateOnly(2026, 9, 16), impactRecordedAt.AddMinutes(1), allocation.EventId);
            Assert((await PostgresOpenItemImpactWriter.PersistAsync(
                    connection, transaction, command.Scope, unallocation)).Created,
                "Exact open-item counter event was not persisted.");
            DerivedOpenItemSnapshot? beforeCounter = await PostgresOpenItemSnapshotLoader.LoadAsync(
                connection,
                transaction,
                command.Scope,
                companyId,
                lines[0].DueScheduleLineId,
                new DateOnly(2026, 9, 16),
                impactRecordedAt);
            Assert(beforeCounter is not null && beforeCounter.AllocatedAmount == 20m &&
                   beforeCounter.WrittenOffAmount == 0m && beforeCounter.RemainingAmount == 20m &&
                   beforeCounter.ConsideredEvents.Count == 1,
                "Open-item loader leaked a late-recorded counter into the historical cutoff.");
            DerivedOpenItemSnapshot? afterCounter = await PostgresOpenItemSnapshotLoader.LoadAsync(
                connection,
                transaction,
                command.Scope,
                companyId,
                lines[0].DueScheduleLineId,
                new DateOnly(2026, 9, 16),
                impactRecordedAt.AddMinutes(1));
            Assert(afterCounter is not null && afterCounter.AllocatedAmount == 0m &&
                   afterCounter.RemainingAmount == 40m && afterCounter.ConsideredEvents.Count == 2,
                "Open-item loader did not derive remaining from the exact counter history.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using var tamper = new NpgsqlCommand(
                "INSERT INTO party.open_item_impact_event (tenant_id,company_id,event_id,party_account_id,due_schedule_line_id,payment_id,currency,impact_kind,amount,effective_date,recorded_at,recorded_by,reverses_event_id) VALUES ($1,$2,$3,$4,$5,$6,'GBP',2,19,DATE '2026-09-17',$7,$8,$9)",
                connection, transaction);
            tamper.Parameters.AddWithValue(tenantId);
            tamper.Parameters.AddWithValue(companyId);
            tamper.Parameters.AddWithValue(Guid.CreateVersion7());
            tamper.Parameters.AddWithValue(partyAccountId);
            tamper.Parameters.AddWithValue(lines[0].DueScheduleLineId);
            tamper.Parameters.AddWithValue(paymentId);
            tamper.Parameters.AddWithValue(impactRecordedAt.AddMinutes(2));
            tamper.Parameters.AddWithValue(actorId);
            tamper.Parameters.AddWithValue(allocation.EventId);
            PostgresException exception = await ThrowsAsync<PostgresException>(() => tamper.ExecuteNonQueryAsync());
            Assert(exception.SqlState == "23514" && exception.ConstraintName == "ck_open_item_exact_counter",
                "Database accepted a counter event whose amount differs from its original.");
            await transaction.RollbackAsync();
        }

        OpenItemImpactEvent firstCapacityContender = OpenItemImpactEvent.Create(
            Guid.CreateVersion7(), tenantId, companyId, partyAccountId, lines[1].DueScheduleLineId,
            Guid.CreateVersion7(), currency, OpenItemImpactKind.Allocation, 40m,
            new DateOnly(2026, 10, 1), impactRecordedAt.AddHours(1));
        OpenItemImpactEvent secondCapacityContender = OpenItemImpactEvent.Create(
            Guid.CreateVersion7(), tenantId, companyId, partyAccountId, lines[1].DueScheduleLineId,
            Guid.CreateVersion7(), currency, OpenItemImpactKind.Allocation, 30m,
            new DateOnly(2026, 10, 1), impactRecordedAt.AddHours(1));
        await using (NpgsqlConnection firstCapacityConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction firstCapacityTransaction =
                     await firstCapacityConnection.BeginTransactionAsync())
        await using (NpgsqlConnection secondCapacityConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction secondCapacityTransaction =
                     await secondCapacityConnection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(
                firstCapacityConnection, firstCapacityTransaction, tenantId, actorId, companyId);
            await SetAuditScopeAsync(
                secondCapacityConnection, secondCapacityTransaction, tenantId, actorId, companyId);
            OpenItemImpactPersistenceResult capacityWinner = await PostgresOpenItemImpactWriter.PersistAsync(
                firstCapacityConnection, firstCapacityTransaction, command.Scope, firstCapacityContender);
            Task<OpenItemImpactPersistenceResult> blockedCapacityLoser =
                PostgresOpenItemImpactWriter.PersistAsync(
                    secondCapacityConnection,
                    secondCapacityTransaction,
                    command.Scope,
                    secondCapacityContender).AsTask();
            await Task.Yield();
            Assert(!blockedCapacityLoser.IsCompleted,
                "Concurrent open-item contender did not wait on the due-line capacity lock.");
            await firstCapacityTransaction.CommitAsync();
            PostgresException capacityConflict = await ThrowsAsync<PostgresException>(() => blockedCapacityLoser);
            Assert(capacityWinner.Created && capacityConflict.SqlState == "23514" &&
                   capacityConflict.ConstraintName == "ck_open_item_impact_capacity",
                "Concurrent open-item allocations exceeded the immutable due-line capacity.");
            await secondCapacityTransaction.RollbackAsync();
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using var capacityTamper = new NpgsqlCommand(
                "INSERT INTO party.open_item_impact_event (tenant_id,company_id,event_id,party_account_id,due_schedule_line_id,payment_id,currency,impact_kind,amount,effective_date,recorded_at,recorded_by) VALUES ($1,$2,$3,$4,$5,NULL,'GBP',3,41,DATE '2026-10-02',$6,$7)",
                connection, transaction);
            capacityTamper.Parameters.AddWithValue(tenantId);
            capacityTamper.Parameters.AddWithValue(companyId);
            capacityTamper.Parameters.AddWithValue(Guid.CreateVersion7());
            capacityTamper.Parameters.AddWithValue(partyAccountId);
            capacityTamper.Parameters.AddWithValue(lines[0].DueScheduleLineId);
            capacityTamper.Parameters.AddWithValue(impactRecordedAt.AddHours(2));
            capacityTamper.Parameters.AddWithValue(actorId);
            PostgresException capacityTamperException =
                await ThrowsAsync<PostgresException>(() => capacityTamper.ExecuteNonQueryAsync());
            Assert(capacityTamperException.SqlState == "23514" &&
                   capacityTamperException.ConstraintName == "ck_open_item_impact_capacity",
                "Database owner bypassed the immutable due-line capacity guard.");
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, otherCompanyId);
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM party.due_schedule WHERE due_schedule_id = $1", dueScheduleId) == 0,
                "Due schedule leaked across company scope.");
            LoadedDueSchedule? hidden = await PostgresDueScheduleLoader.LoadAsync(
                connection,
                transaction,
                new ExecutionScope(tenantId, actorId, [otherCompanyId]),
                otherCompanyId,
                dueScheduleId);
            Assert(hidden is null, "Authoritative due schedule loader leaked a cross-company schedule.");
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM party.open_item_impact_event WHERE event_id = $1", allocation.EventId) == 0,
                "Open-item impact leaked across company scope.");
            DerivedOpenItemSnapshot? hiddenOpenItem = await PostgresOpenItemSnapshotLoader.LoadAsync(
                connection,
                transaction,
                new ExecutionScope(tenantId, actorId, [otherCompanyId]),
                otherCompanyId,
                lines[0].DueScheduleLineId,
                new DateOnly(2026, 12, 31),
                impactRecordedAt.AddDays(1));
            Assert(hiddenOpenItem is null, "Open-item snapshot loader leaked a cross-company due line.");
            await transaction.CommitAsync();
        }

        await using NpgsqlConnection privilegeConnection = await appDataSource.OpenConnectionAsync();
        await using var privilegeCommand = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user, 'party.due_schedule', 'SELECT'), has_table_privilege(current_user, 'party.due_schedule', 'INSERT'), has_table_privilege(current_user, 'party.due_schedule', 'UPDATE'), has_table_privilege(current_user, 'party.due_schedule_line', 'DELETE')",
            privilegeConnection);
        await using NpgsqlDataReader reader = await privilegeCommand.ExecuteReaderAsync();
        Assert(await reader.ReadAsync(), "Due schedule privilege metadata was not returned.");
        Assert(reader.GetBoolean(0) && reader.GetBoolean(1) && !reader.GetBoolean(2) && !reader.GetBoolean(3),
            "Runtime due schedule privileges are not append-only.");
        await reader.DisposeAsync();
        await using var impactPrivilegeCommand = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user, 'party.open_item_impact_event', 'SELECT'), has_table_privilege(current_user, 'party.open_item_impact_event', 'INSERT'), has_table_privilege(current_user, 'party.open_item_impact_event', 'UPDATE'), has_table_privilege(current_user, 'party.open_item_impact_event', 'DELETE')",
            privilegeConnection);
        await using NpgsqlDataReader impactPrivilegeReader = await impactPrivilegeCommand.ExecuteReaderAsync();
        Assert(await impactPrivilegeReader.ReadAsync() && impactPrivilegeReader.GetBoolean(0) &&
               impactPrivilegeReader.GetBoolean(1) && !impactPrivilegeReader.GetBoolean(2) &&
               !impactPrivilegeReader.GetBoolean(3),
            "Runtime open-item impact privileges are not append-only.");
    }

    private static async Task AssertPaymentEconomicEventPersistenceAsync(
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        var currency = TreasuryCurrencyCode.Create("GBP");
        SameCurrencyPaymentRateSnapshot rate = SameCurrencyPaymentRateSnapshot.Create(
            tenantId, companyId, Guid.CreateVersion7(), 1, currency, currency,
            "identity", "company-base", new DateOnly(2026, 8, 26), 1m, 1m);
        ValidatedPaymentEconomicEventDraft payment = ValidatedPaymentEconomicEventDraft.Create(
            Guid.CreateVersion7(), tenantId, companyId, Guid.CreateVersion7(), Guid.CreateVersion7(),
            PaymentDirection.Incoming, 100m, 100m, new DateOnly(2026, 8, 26),
            new DateTimeOffset(2026, 8, 26, 13, 0, 0, TimeSpan.Zero),
            "cash.receipt", Guid.CreateVersion7(), "customer-receipt", rate);
        var scope = new ExecutionScope(tenantId, actorId, [companyId]);

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            PaymentEconomicEventPersistenceResult first = await PostgresPaymentEconomicEventWriter.PersistAsync(
                connection, transaction, scope, payment);
            PaymentEconomicEventPersistenceResult replay = await PostgresPaymentEconomicEventWriter.PersistAsync(
                connection, transaction, scope, payment);
            Assert(first.Created && !replay.Created && replay.PaymentId == payment.PaymentId,
                "Payment economic-event retry did not return the immutable first result.");
            ValidatedPaymentEconomicEventDraft? loaded = await PostgresPaymentEconomicEventLoader.LoadAsync(
                connection, transaction, scope, companyId, payment.PaymentId);
            Assert(loaded is not null && loaded.PaymentId == payment.PaymentId &&
                   loaded.PartyAccountId == payment.PartyAccountId &&
                   loaded.TreasuryAccountId == payment.TreasuryAccountId &&
                   loaded.TransactionAmount == 100m && loaded.FunctionalAmount == 100m &&
                   loaded.SourceIdentity == payment.SourceIdentity && loaded.RateSnapshot == payment.RateSnapshot,
                "Authoritative payment loader did not reconstruct the immutable domain snapshot.");
            await transaction.CommitAsync();
        }

        ValidatedPaymentEconomicEventDraft concurrentPayment = ValidatedPaymentEconomicEventDraft.Create(
            Guid.CreateVersion7(), tenantId, companyId, payment.PartyAccountId, payment.TreasuryAccountId,
            payment.Direction, 25m, 25m, payment.EffectiveDate, payment.RecordedAt.AddMinutes(1),
            payment.SourceIdentity.SourceType, Guid.CreateVersion7(), payment.SourceIdentity.PostingPurpose, rate);
        await using (NpgsqlConnection firstConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction firstTransaction = await firstConnection.BeginTransactionAsync())
        await using (NpgsqlConnection secondConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction secondTransaction = await secondConnection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(firstConnection, firstTransaction, tenantId, actorId, companyId);
            await SetAuditScopeAsync(secondConnection, secondTransaction, tenantId, actorId, companyId);
            PaymentEconomicEventPersistenceResult winner = await PostgresPaymentEconomicEventWriter.PersistAsync(
                firstConnection, firstTransaction, scope, concurrentPayment);
            Task<PaymentEconomicEventPersistenceResult> blockedLoser =
                PostgresPaymentEconomicEventWriter.PersistAsync(
                    secondConnection, secondTransaction, scope, concurrentPayment).AsTask();
            await Task.Yield();
            Assert(!blockedLoser.IsCompleted,
                "Concurrent payment contender did not wait on source uniqueness.");
            await firstTransaction.CommitAsync();
            PaymentEconomicEventPersistenceResult loser = await blockedLoser;
            Assert(winner.Created && !loser.Created && loser.PaymentId == winner.PaymentId,
                "Concurrent payment source race did not preserve the immutable first event.");
            await secondTransaction.CommitAsync();
        }

        ValidatedPaymentEconomicEventDraft changed = ValidatedPaymentEconomicEventDraft.Create(
            Guid.CreateVersion7(), tenantId, companyId, payment.PartyAccountId, payment.TreasuryAccountId,
            payment.Direction, 100m, 100m, payment.EffectiveDate, payment.RecordedAt,
            payment.SourceIdentity.SourceType, payment.SourceIdentity.SourceEventId,
            payment.SourceIdentity.PostingPurpose, rate);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            PaymentEconomicEventPersistenceConflictException conflict =
                await ThrowsAsync<PaymentEconomicEventPersistenceConflictException>(() =>
                    PostgresPaymentEconomicEventWriter.PersistAsync(
                        connection, transaction, scope, changed).AsTask());
            Assert(conflict.ExistingPaymentId == payment.PaymentId,
                "Payment source uniqueness accepted a second economic-event identity.");
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, otherCompanyId);
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM treasury.payment_economic_event WHERE payment_id=$1", payment.PaymentId) == 0,
                "Payment economic event leaked across company scope.");
            ValidatedPaymentEconomicEventDraft? hidden = await PostgresPaymentEconomicEventLoader.LoadAsync(
                connection,
                transaction,
                new ExecutionScope(tenantId, actorId, [otherCompanyId]),
                otherCompanyId,
                payment.PaymentId);
            Assert(hidden is null, "Authoritative payment loader leaked a cross-company event.");
            await transaction.CommitAsync();
        }

        await using NpgsqlConnection privilegeConnection = await appDataSource.OpenConnectionAsync();
        await using var privilege = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user,'treasury.payment_economic_event','SELECT'),has_table_privilege(current_user,'treasury.payment_economic_event','INSERT'),has_table_privilege(current_user,'treasury.payment_economic_event','UPDATE'),has_table_privilege(current_user,'treasury.payment_economic_event','DELETE')",
            privilegeConnection);
        await using NpgsqlDataReader reader = await privilege.ExecuteReaderAsync();
        Assert(await reader.ReadAsync() && reader.GetBoolean(0) && reader.GetBoolean(1) &&
               !reader.GetBoolean(2) && !reader.GetBoolean(3),
            "Runtime payment economic-event privileges are not append-only.");
    }

    private static async Task AssertStatementLinePersistenceAsync(
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        Guid treasuryAccountId = Guid.CreateVersion7();
        StatementLineExternalIdentity identity = StatementLineExternalIdentity.Create(
            tenantId, companyId, treasuryAccountId, "bank-profile-a", "bank-reference", "TX-2026-0001");
        ValidatedStatementLineDraft line = ValidatedStatementLineDraft.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), identity, TreasuryCurrencyCode.Create("GBP"),
            125.50m, new DateOnly(2026, 8, 25), new DateOnly(2026, 8, 26),
            new DateTimeOffset(2026, 8, 26, 14, 0, 0, TimeSpan.Zero), new string('a', 64), 1);
        var scope = new ExecutionScope(tenantId, actorId, [companyId]);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            StatementLinePersistenceResult first = await PostgresStatementLineWriter.PersistAsync(
                connection, transaction, scope, line);
            StatementLinePersistenceResult replay = await PostgresStatementLineWriter.PersistAsync(
                connection, transaction, scope, line);
            Assert(first.Created && !replay.Created && replay.StatementLineId == line.StatementLineId,
                "Statement-line retry did not return the immutable first result.");
            await transaction.CommitAsync();
        }

        ValidatedStatementLineDraft changed = ValidatedStatementLineDraft.Create(
            Guid.CreateVersion7(), line.StatementImportId, identity, line.Currency, line.SignedAmount,
            line.BookingDate, line.ValueDate, line.RecordedAt, line.RawObjectSha256, line.ParserVersion);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            StatementLinePersistenceConflictException conflict =
                await ThrowsAsync<StatementLinePersistenceConflictException>(() =>
                    PostgresStatementLineWriter.PersistAsync(connection, transaction, scope, changed).AsTask());
            Assert(conflict.ExistingStatementLineId == line.StatementLineId,
                "Statement external identity accepted a different immutable line identity.");
            await transaction.RollbackAsync();
        }

        ValidatedStatementLineDraft concurrentLine = ValidatedStatementLineDraft.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(),
            StatementLineExternalIdentity.Create(
                tenantId, companyId, treasuryAccountId, "bank-profile-a", "bank-reference", "TX-2026-0002"),
            TreasuryCurrencyCode.Create("GBP"), -25m, line.BookingDate, line.ValueDate,
            line.RecordedAt.AddMinutes(1), new string('b', 64), 1);
        await using (NpgsqlConnection firstConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction firstTransaction = await firstConnection.BeginTransactionAsync())
        await using (NpgsqlConnection secondConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction secondTransaction = await secondConnection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(firstConnection, firstTransaction, tenantId, actorId, companyId);
            await SetAuditScopeAsync(secondConnection, secondTransaction, tenantId, actorId, companyId);
            StatementLinePersistenceResult winner = await PostgresStatementLineWriter.PersistAsync(
                firstConnection, firstTransaction, scope, concurrentLine);
            Task<StatementLinePersistenceResult> blockedLoser = PostgresStatementLineWriter.PersistAsync(
                secondConnection, secondTransaction, scope, concurrentLine).AsTask();
            await Task.Yield();
            Assert(!blockedLoser.IsCompleted, "Concurrent statement-line contender did not wait on deduplication.");
            await firstTransaction.CommitAsync();
            StatementLinePersistenceResult loser = await blockedLoser;
            Assert(winner.Created && !loser.Created && loser.StatementLineId == winner.StatementLineId,
                "Concurrent statement-line race did not preserve the immutable first row.");
            await secondTransaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, otherCompanyId);
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM treasury.statement_line WHERE statement_line_id=$1", line.StatementLineId) == 0,
                "Statement line leaked across company scope.");
            await transaction.CommitAsync();
        }
        await using NpgsqlConnection privilegeConnection = await appDataSource.OpenConnectionAsync();
        await using var privilege = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user,'treasury.statement_line','SELECT'),has_table_privilege(current_user,'treasury.statement_line','INSERT'),has_table_privilege(current_user,'treasury.statement_line','UPDATE'),has_table_privilege(current_user,'treasury.statement_line','DELETE')",
            privilegeConnection);
        await using NpgsqlDataReader reader = await privilege.ExecuteReaderAsync();
        Assert(await reader.ReadAsync() && reader.GetBoolean(0) && reader.GetBoolean(1) &&
               !reader.GetBoolean(2) && !reader.GetBoolean(3),
            "Runtime statement-line privileges are not append-only.");
    }

    private static JournalPreparationRequest CreatePreparationRequestForDraft(
        JournalPreparationRequest template,
        ValidatedJournalDraft draft) =>
        new(
            template.Scope,
            template.AuditContext,
            draft,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7());

    private static async Task PostJournalFixtureAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        JournalPreparationRequest request,
        Guid journalId,
        Guid actorId,
        bool seedCurrencyEvidence = true)
    {
        await SeedJournalPreparationEvidenceAsync(
            migratorDataSource, request, actorId, seedCurrencyEvidence);
        await SeedJournalApprovalEvidenceAsync(migratorDataSource, request, actorId);
        var command = new JournalPostingCommand(
            ToJournalPreparationCommand(request), journalId, Guid.CreateVersion7(),
            Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        await using NpgsqlConnection connection = await appDataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await SetAuditScopeAsync(
            connection, transaction, request.Draft.TenantId, actorId, request.Draft.CompanyId);
        JournalPostingResult result = await PostgresJournalPostingOrchestrator.PostFromSourceAsync(
            connection,
            transaction,
            command,
            (_, _, _, _) => ValueTask.FromResult(new CanonicalJournalPreparationSource(
                request.Draft, request.ChartOfAccountsVersionId, 1)),
            AppendJournalPreparationAuditAsync,
            AppendJournalPreparationOutboxAsync,
            AppendJournalPostedAuditAsync,
            AppendJournalPostedOutboxAsync);
        Assert(result.PostedJournal.Created && result.PostedJournal.JournalId == journalId,
            "Posted journal fixture was not created.");
        await transaction.CommitAsync();
    }

    private static async Task SeedJournalPreparationEvidenceAsync(
        NpgsqlDataSource dataSource,
        JournalPreparationRequest request,
        Guid actorId,
        bool seedCurrencyEvidence = true)
    {
        await SeedAccountPostingEvidenceAsync(
            dataSource,
            request.Draft.TenantId,
            request.Draft.CompanyId,
            actorId,
            request.ChartOfAccountsVersionId,
            request.Draft.Lines.Select(line => (line.AccountId, AccountKind.Posting, true)));
        Guid[] dimensionIds = request.Draft.Lines
            .SelectMany(line => line.Dimensions)
            .Select(assignment => assignment.DimensionId)
            .Distinct()
            .ToArray();
        await SeedDimensionRequirementAsync(
            dataSource,
            request.Draft.TenantId,
            request.Draft.CompanyId,
            actorId,
            request.Draft.PostingRuleVersionId,
            dimensionIds);
        if (seedCurrencyEvidence)
        {
            await SeedCurrencyEvidenceAsync(dataSource, request, actorId, numeratorOverride: null);
        }
    }

    private static JournalPreparationCommand ToJournalPreparationCommand(JournalPreparationRequest request) =>
        new(
            request.Scope,
            request.AuditContext,
            request.Draft.PostingIdentity,
            1,
            request.ReservationId,
            request.JournalDraftId,
            request.AuditEventId,
            request.OutboxEventId);

    private static ValueTask AppendJournalPreparationAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        JournalPreparationRequest request,
        Guid persistedJournalDraftId,
        CancellationToken cancellationToken) =>
        PostgresAuthorizationAuditWriter.AppendAsync(
            connection,
            transaction,
            request.AuditContext,
            request.AuditEventId,
            new AuthorizationAuditEvent(
                "accounting.journal-draft.prepare",
                "validated-journal-draft",
                persistedJournalDraftId.ToString("D"),
                "allowed",
                "JOURNAL_DRAFT_PREPARED"),
            cancellationToken);

    private static ValueTask<bool> AppendJournalPreparationOutboxAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        JournalPreparationRequest request,
        Guid persistedJournalDraftId,
        Guid reservationId,
        Guid periodId,
        CancellationToken cancellationToken) =>
        PostgresOutboxWriter.EnqueueAsync(
            connection,
            transaction,
            request.Scope,
            new OutboxMessage(
                request.OutboxEventId,
                request.Draft.TenantId,
                request.Draft.CompanyId,
                "validated-journal-draft",
                persistedJournalDraftId,
                1,
                "accounting.journal-draft-prepared.v1",
                1,
                request.Draft.RecordedAt,
                $"{{\"journalDraftId\":\"{persistedJournalDraftId:D}\",\"reservationId\":\"{reservationId:D}\",\"periodId\":\"{periodId:D}\"}}"),
            cancellationToken);

    private static ValueTask AppendJournalPostedAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        JournalPostingCommand command,
        JournalPostingResult result,
        CancellationToken cancellationToken) =>
        PostgresAuthorizationAuditWriter.AppendAsync(
            connection,
            transaction,
            command.Preparation.AuditContext,
            command.PostedAuditEventId,
            new AuthorizationAuditEvent(
                "accounting.journal.post",
                "posted-journal",
                result.PostedJournal.JournalId.ToString("D"),
                "allowed",
                "JOURNAL_POSTED"),
            cancellationToken);

    private static ValueTask<bool> AppendJournalPostedOutboxAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        JournalPostingCommand command,
        JournalPostingResult result,
        CancellationToken cancellationToken) =>
        PostgresOutboxWriter.EnqueueAsync(
            connection,
            transaction,
            command.Preparation.Scope,
            new OutboxMessage(
                command.PostedOutboxEventId,
                command.Preparation.SourceIdentity.TenantId,
                command.Preparation.SourceIdentity.CompanyId,
                "posted-journal",
                result.PostedJournal.JournalId,
                1,
                "accounting.journal-posted.v1",
                1,
                result.PostedJournal.PostedAt,
                $"{{\"journalId\":\"{result.PostedJournal.JournalId:D}\",\"journalDraftId\":\"{result.Preparation.JournalDraftId:D}\",\"periodId\":\"{result.Preparation.PeriodId:D}\"}}"),
            cancellationToken);

    private static async Task InsertAccountingPeriodAsync(
        NpgsqlDataSource dataSource,
        Guid periodId,
        Guid tenantId,
        Guid companyId,
        string code,
        DateOnly startsOn,
        DateOnly endsOn,
        Guid actorId)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
        await using (var periodCommand = new NpgsqlCommand(
            """
            INSERT INTO accounting.accounting_period
                (period_id, tenant_id, company_id, period_code, starts_on, ends_on, created_by, updated_by)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $7)
            """,
            connection,
            transaction))
        {
            periodCommand.Parameters.AddWithValue(periodId);
            periodCommand.Parameters.AddWithValue(tenantId);
            periodCommand.Parameters.AddWithValue(companyId);
            periodCommand.Parameters.AddWithValue(code);
            periodCommand.Parameters.AddWithValue(startsOn);
            periodCommand.Parameters.AddWithValue(endsOn);
            periodCommand.Parameters.AddWithValue(actorId);
            await periodCommand.ExecuteNonQueryAsync();
        }

        const string lockSql = """
            INSERT INTO accounting.period_lock_state
                (tenant_id, company_id, period_id, lock_scope, close_stage, updated_by)
            VALUES ($1, $2, $3, 2, 0, $4), ($1, $2, $3, 4, 0, $4)
            """;
        await using (var lockCommand = new NpgsqlCommand(lockSql, connection, transaction))
        {
            lockCommand.Parameters.AddWithValue(tenantId);
            lockCommand.Parameters.AddWithValue(companyId);
            lockCommand.Parameters.AddWithValue(periodId);
            lockCommand.Parameters.AddWithValue(actorId);
            await lockCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task AssertJournalReservationAuditOutboxAtomicityAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid actorId)
    {
        var scope = new ExecutionScope(tenantId, actorId, [companyId]);
        var auditContext = new RequestAuditContext(
            Guid.CreateVersion7(),
            "journal-atomicity-integration-trace",
            tenantId,
            actorId,
            new HashSet<Guid> { companyId },
            "synthetic-integration-session");
        Guid reservationId = Guid.CreateVersion7();
        Guid journalDraftId = Guid.CreateVersion7();
        Guid auditEventId = Guid.CreateVersion7();
        Guid outboxEventId = Guid.CreateVersion7();
        ValidatedJournalDraft draft = CreateIntegrationJournalDraft(
            tenantId,
            companyId,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            amount: 40m,
            reverseLineOrder: false);

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            JournalSourceReservationResult reservation = await PostgresJournalSourceReservationWriter.ReserveAsync(
                connection,
                transaction,
                scope,
                reservationId,
                draft);
            Assert(reservation.Created, "Atomicity probe did not create its journal-source reservation.");
            ValidatedJournalDraftPersistenceResult persistedDraft =
                await PostgresValidatedJournalDraftWriter.PersistAsync(
                    connection,
                    transaction,
                    scope,
                    journalDraftId,
                    reservation,
                    draft);
            Assert(persistedDraft.Created, "Atomicity probe did not persist its validated journal draft.");

            ValidatedJournalDraftPersistenceResult retriedDraft =
                await PostgresValidatedJournalDraftWriter.PersistAsync(
                    connection,
                    transaction,
                    scope,
                    Guid.CreateVersion7(),
                    reservation,
                    draft);
            Assert(!retriedDraft.Created && retriedDraft.JournalDraftId == journalDraftId,
                "Validated journal draft retry was not idempotent.");

            await PostgresAuthorizationAuditWriter.AppendAsync(
                connection,
                transaction,
                auditContext,
                auditEventId,
                new AuthorizationAuditEvent(
                    "accounting.journal-draft.reserve",
                    "journal-source-reservation",
                    reservationId.ToString("D"),
                    "allowed",
                    "JOURNAL_SOURCE_RESERVED"));
            Assert(await PostgresOutboxWriter.EnqueueAsync(
                    connection,
                    transaction,
                    scope,
                    new OutboxMessage(
                        outboxEventId,
                        tenantId,
                        companyId,
                        "journal-source-reservation",
                        reservationId,
                        1,
                        "accounting.journal-draft-reserved.v1",
                        1,
                        draft.RecordedAt,
                        $"{{\"reservationId\":\"{reservationId:D}\"}}")),
                "Atomicity probe did not enqueue its outbox event.");
            await transaction.CommitAsync();
        }

        await AssertAtomicJournalFactCountsAsync(
            migratorDataSource,
            reservationId,
            journalDraftId,
            auditEventId,
            outboxEventId,
            expectedCount: 1,
            "Committed journal reservation/draft/audit/outbox facts were not all persisted exactly once.");

        await AssertValidatedJournalDraftRuntimeImmutabilityAsync(
            appDataSource,
            tenantId,
            companyId,
            actorId,
            journalDraftId);

        Guid rolledBackReservationId = Guid.CreateVersion7();
        Guid rolledBackJournalDraftId = Guid.CreateVersion7();
        Guid rolledBackAuditEventId = Guid.CreateVersion7();
        Guid rolledBackOutboxEventId = Guid.CreateVersion7();
        ValidatedJournalDraft rolledBackDraft = CreateIntegrationJournalDraft(
            tenantId,
            companyId,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            amount: 41m,
            reverseLineOrder: false);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            JournalSourceReservationResult rolledBackReservation = await PostgresJournalSourceReservationWriter.ReserveAsync(
                connection,
                transaction,
                scope,
                rolledBackReservationId,
                rolledBackDraft);
            await PostgresValidatedJournalDraftWriter.PersistAsync(
                connection,
                transaction,
                scope,
                rolledBackJournalDraftId,
                rolledBackReservation,
                rolledBackDraft);
            await PostgresAuthorizationAuditWriter.AppendAsync(
                connection,
                transaction,
                auditContext with { CorrelationId = Guid.CreateVersion7() },
                rolledBackAuditEventId,
                new AuthorizationAuditEvent(
                    "accounting.journal-draft.reserve",
                    "journal-source-reservation",
                    rolledBackReservationId.ToString("D"),
                    "allowed",
                    "JOURNAL_SOURCE_RESERVED"));
            await PostgresOutboxWriter.EnqueueAsync(
                connection,
                transaction,
                scope,
                new OutboxMessage(
                    rolledBackOutboxEventId,
                    tenantId,
                    companyId,
                    "journal-source-reservation",
                    rolledBackReservationId,
                    1,
                    "accounting.journal-draft-reserved.v1",
                    1,
                    rolledBackDraft.RecordedAt,
                    $"{{\"reservationId\":\"{rolledBackReservationId:D}\"}}"));
            await transaction.RollbackAsync();
        }

        await AssertAtomicJournalFactCountsAsync(
            migratorDataSource,
            rolledBackReservationId,
            rolledBackJournalDraftId,
            rolledBackAuditEventId,
            rolledBackOutboxEventId,
            expectedCount: 0,
            "Rolled-back journal reservation/draft/audit/outbox transaction persisted a partial fact.");
    }

    private static async Task AssertValidatedJournalDraftRuntimeImmutabilityAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        Guid journalDraftId)
    {
        await using (NpgsqlConnection privilegeConnection = await dataSource.OpenConnectionAsync())
        await using (var privilegeCommand = new NpgsqlCommand(
            """
            SELECT
                has_table_privilege(current_user, 'accounting.validated_journal_draft', 'SELECT'),
                has_table_privilege(current_user, 'accounting.validated_journal_draft', 'INSERT'),
                has_table_privilege(current_user, 'accounting.validated_journal_draft', 'UPDATE'),
                has_table_privilege(current_user, 'accounting.validated_journal_draft', 'DELETE'),
                has_table_privilege(current_user, 'accounting.validated_journal_line', 'SELECT'),
                has_table_privilege(current_user, 'accounting.validated_journal_line', 'INSERT'),
                has_table_privilege(current_user, 'accounting.validated_journal_line', 'UPDATE'),
                has_table_privilege(current_user, 'accounting.validated_journal_line', 'DELETE')
            """,
            privilegeConnection))
        await using (NpgsqlDataReader reader = await privilegeCommand.ExecuteReaderAsync())
        {
            Assert(await reader.ReadAsync(), "Validated journal privilege metadata was not returned.");
            Assert(reader.GetBoolean(0) && reader.GetBoolean(1) && reader.GetBoolean(4) && reader.GetBoolean(5),
                "Runtime role cannot read and append validated journal snapshots.");
            Assert(!reader.GetBoolean(2) && !reader.GetBoolean(3) && !reader.GetBoolean(6) && !reader.GetBoolean(7),
                "Runtime role can mutate or delete validated journal snapshots.");
        }

        await AssertJournalSourceMutationRejectedAsync(
            dataSource,
            tenantId,
            companyId,
            actorId,
            "UPDATE accounting.validated_journal_draft SET total_debit = total_debit WHERE journal_draft_id = $1",
            journalDraftId,
            "Runtime role updated an append-only validated journal draft.");
        await AssertJournalSourceMutationRejectedAsync(
            dataSource,
            tenantId,
            companyId,
            actorId,
            "DELETE FROM accounting.validated_journal_line WHERE journal_draft_id = $1",
            journalDraftId,
            "Runtime role deleted an append-only validated journal line.");

        await using NpgsqlConnection scopedConnection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction scopedTransaction = await scopedConnection.BeginTransactionAsync();
        await SetAuditScopeAsync(scopedConnection, scopedTransaction, tenantId, actorId, Guid.CreateVersion7());
        Assert(await CountAsync(
                scopedConnection,
                scopedTransaction,
                "SELECT count(*) FROM accounting.validated_journal_draft WHERE journal_draft_id = $1",
                journalDraftId) == 0,
            "Validated journal draft leaked outside the active company scope.");
        Assert(await CountAsync(
                scopedConnection,
                scopedTransaction,
                "SELECT count(*) FROM accounting.validated_journal_line WHERE journal_draft_id = $1",
                journalDraftId) == 0,
            "Validated journal lines leaked outside the active company scope.");
        await scopedTransaction.CommitAsync();
    }

    private static async Task AssertAtomicJournalFactCountsAsync(
        NpgsqlDataSource dataSource,
        Guid reservationId,
        Guid journalDraftId,
        Guid auditEventId,
        Guid outboxEventId,
        long expectedCount,
        string failureMessage,
        decimal expectedAmount = 40m)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
        long reservationCount = await CountAsync(
            connection,
            transaction,
            "SELECT count(*) FROM accounting.journal_source_reservation WHERE reservation_id = $1",
            reservationId);
        long draftCount = await CountAsync(
            connection,
            transaction,
            "SELECT count(*) FROM accounting.validated_journal_draft WHERE journal_draft_id = $1",
            journalDraftId);
        long lineCount = await CountAsync(
            connection,
            transaction,
            "SELECT count(*) FROM accounting.validated_journal_line WHERE journal_draft_id = $1",
            journalDraftId);
        long auditCount = await CountAsync(
            connection,
            transaction,
            "SELECT count(*) FROM platform.audit_event WHERE id = $1",
            auditEventId);
        long outboxCount = await CountAsync(
            connection,
            transaction,
            "SELECT count(*) FROM platform.outbox_message WHERE event_id = $1",
            outboxEventId);
        if (expectedCount == 1)
        {
            await using (var headerCommand = new NpgsqlCommand(
                """
                SELECT total_debit = $2::numeric
                   AND total_credit = $2::numeric
                   AND line_count = 2
                   AND functional_currency = 'GBP'
                FROM accounting.validated_journal_draft
                WHERE journal_draft_id = $1
                """,
                connection,
                transaction))
            {
                headerCommand.Parameters.AddWithValue(journalDraftId);
                headerCommand.Parameters.AddWithValue(expectedAmount);
                Assert(await headerCommand.ExecuteScalarAsync() is true,
                    "Validated journal header did not preserve its exact decimal totals and currency.");
            }

            await using var lineCommand = new NpgsqlCommand(
                """
                SELECT count(*) = 2
                   AND sum(debit) = $2::numeric
                   AND sum(credit) = $2::numeric
                   AND min(line_number) = 1
                   AND max(line_number) = 2
                FROM accounting.validated_journal_line
                WHERE journal_draft_id = $1
                """,
                connection,
                transaction);
            lineCommand.Parameters.AddWithValue(journalDraftId);
            lineCommand.Parameters.AddWithValue(expectedAmount);
            Assert(await lineCommand.ExecuteScalarAsync() is true,
                "Validated journal lines did not preserve exact amounts and ordering.");
        }
        Assert(
            reservationCount == expectedCount &&
                draftCount == expectedCount &&
                lineCount == expectedCount * 2 &&
                auditCount == expectedCount &&
                outboxCount == expectedCount,
            failureMessage);
        await transaction.CommitAsync();
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

        foreach (string table in new[]
                 {
                     "treasury.statement_line",
                     "treasury.payment_economic_event",
                     "party.open_item_impact_event",
                     "party.due_schedule_line",
                     "party.due_schedule",
                     "party.party_account",
                     "party.party_identity",
                 })
        {
            await using var partyCleanup = new NpgsqlCommand(
                $"DELETE FROM {table} WHERE tenant_id = $1 OR tenant_id = $2", connection, transaction);
            partyCleanup.Parameters.AddWithValue(tenantA);
            partyCleanup.Parameters.AddWithValue(tenantB);
            await partyCleanup.ExecuteNonQueryAsync();
        }

        await using (var reversalCommand = new NpgsqlCommand(
            "DELETE FROM accounting.posted_journal_reversal WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            reversalCommand.Parameters.AddWithValue(tenantA);
            reversalCommand.Parameters.AddWithValue(tenantB);
            await reversalCommand.ExecuteNonQueryAsync();
        }

        await using (var postedLineCommand = new NpgsqlCommand(
            "DELETE FROM accounting.posted_journal_line WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            postedLineCommand.Parameters.AddWithValue(tenantA);
            postedLineCommand.Parameters.AddWithValue(tenantB);
            await postedLineCommand.ExecuteNonQueryAsync();
        }

        await using (var postedJournalCommand = new NpgsqlCommand(
            "DELETE FROM accounting.posted_journal WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            postedJournalCommand.Parameters.AddWithValue(tenantA);
            postedJournalCommand.Parameters.AddWithValue(tenantB);
            await postedJournalCommand.ExecuteNonQueryAsync();
        }

        await using (var approvalDecisionCommand = new NpgsqlCommand(
            "DELETE FROM workflow.approval_decision_snapshot WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            approvalDecisionCommand.Parameters.AddWithValue(tenantA);
            approvalDecisionCommand.Parameters.AddWithValue(tenantB);
            await approvalDecisionCommand.ExecuteNonQueryAsync();
        }

        await using (var approvalCompletionCommand = new NpgsqlCommand(
            "DELETE FROM workflow.approval_completion_snapshot WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            approvalCompletionCommand.Parameters.AddWithValue(tenantA);
            approvalCompletionCommand.Parameters.AddWithValue(tenantB);
            await approvalCompletionCommand.ExecuteNonQueryAsync();
        }

        await using (var rateCommand = new NpgsqlCommand(
            "DELETE FROM accounting.exchange_rate_snapshot WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            rateCommand.Parameters.AddWithValue(tenantA);
            rateCommand.Parameters.AddWithValue(tenantB);
            await rateCommand.ExecuteNonQueryAsync();
        }

        await using (var roundingCommand = new NpgsqlCommand(
            "DELETE FROM accounting.rounding_policy_snapshot WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            roundingCommand.Parameters.AddWithValue(tenantA);
            roundingCommand.Parameters.AddWithValue(tenantB);
            await roundingCommand.ExecuteNonQueryAsync();
        }

        await using (var dimensionLineCommand = new NpgsqlCommand(
            "DELETE FROM accounting.posting_dimension_requirement WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            dimensionLineCommand.Parameters.AddWithValue(tenantA);
            dimensionLineCommand.Parameters.AddWithValue(tenantB);
            await dimensionLineCommand.ExecuteNonQueryAsync();
        }

        await using (var dimensionSetCommand = new NpgsqlCommand(
            "DELETE FROM accounting.posting_dimension_requirement_set WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            dimensionSetCommand.Parameters.AddWithValue(tenantA);
            dimensionSetCommand.Parameters.AddWithValue(tenantB);
            await dimensionSetCommand.ExecuteNonQueryAsync();
        }

        await using (var accountSnapshotCommand = new NpgsqlCommand(
            "DELETE FROM accounting.account_posting_snapshot WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            accountSnapshotCommand.Parameters.AddWithValue(tenantA);
            accountSnapshotCommand.Parameters.AddWithValue(tenantB);
            await accountSnapshotCommand.ExecuteNonQueryAsync();
        }

        await using (var chartVersionCommand = new NpgsqlCommand(
            "DELETE FROM accounting.chart_of_accounts_version WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            chartVersionCommand.Parameters.AddWithValue(tenantA);
            chartVersionCommand.Parameters.AddWithValue(tenantB);
            await chartVersionCommand.ExecuteNonQueryAsync();
        }

        await using (var idempotencyCommand = new NpgsqlCommand(
            "DELETE FROM platform.idempotency_record WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            idempotencyCommand.Parameters.AddWithValue(tenantA);
            idempotencyCommand.Parameters.AddWithValue(tenantB);
            await idempotencyCommand.ExecuteNonQueryAsync();
        }

        await using (var periodLockCommand = new NpgsqlCommand(
            "DELETE FROM accounting.period_lock_state WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            periodLockCommand.Parameters.AddWithValue(tenantA);
            periodLockCommand.Parameters.AddWithValue(tenantB);
            await periodLockCommand.ExecuteNonQueryAsync();
        }

        await using (var periodCommand = new NpgsqlCommand(
            "DELETE FROM accounting.accounting_period WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            periodCommand.Parameters.AddWithValue(tenantA);
            periodCommand.Parameters.AddWithValue(tenantB);
            await periodCommand.ExecuteNonQueryAsync();
        }

        await using (var lineCommand = new NpgsqlCommand(
            "DELETE FROM accounting.validated_journal_line WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            lineCommand.Parameters.AddWithValue(tenantA);
            lineCommand.Parameters.AddWithValue(tenantB);
            await lineCommand.ExecuteNonQueryAsync();
        }

        await using (var draftCommand = new NpgsqlCommand(
            "DELETE FROM accounting.validated_journal_draft WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            draftCommand.Parameters.AddWithValue(tenantA);
            draftCommand.Parameters.AddWithValue(tenantB);
            await draftCommand.ExecuteNonQueryAsync();
        }

        await using (var reservationCommand = new NpgsqlCommand(
            "DELETE FROM accounting.journal_source_reservation WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            reservationCommand.Parameters.AddWithValue(tenantA);
            reservationCommand.Parameters.AddWithValue(tenantB);
            await reservationCommand.ExecuteNonQueryAsync();
        }

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

    private static async Task<TException> ThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
    }
}
