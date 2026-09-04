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
using KaguERP.Modules.Parties.Application.Openings;
using KaguERP.Modules.Parties.Application.OpenItems;
using KaguERP.Modules.Parties.Contracts.Reports;
using KaguERP.Modules.Parties.Domain.Accounts;
using KaguERP.Modules.Parties.Domain.Allocations;
using KaguERP.Modules.Parties.Domain.DueSchedules;
using KaguERP.Modules.Parties.Domain.Openings;
using KaguERP.Modules.Parties.Domain.OpenItems;
using KaguERP.Modules.Parties.Infrastructure.Persistence;
using KaguERP.Modules.Parties.Infrastructure.Reports;
using KaguERP.Modules.Reporting.Application.PartyReports;
using KaguERP.Modules.Reporting.Domain.ControlAccounts;
using KaguERP.Modules.Reporting.Domain.PartyReports;
using KaguERP.Modules.Reporting.Infrastructure.PartyReports;
using KaguERP.Modules.Reporting.Infrastructure.Persistence;
using KaguERP.Modules.Treasury.Domain.Payments;
using KaguERP.Modules.Treasury.Domain.Reconciliation;
using KaguERP.Modules.Treasury.Domain.Statements;
using KaguERP.Modules.Treasury.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static partial class DatabaseIntegrationCheck
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
            await AssertPartyReportRefreshWorkQueueAsync(
                migratorDataSource,
                appDataSource,
                migratorConnectionString,
                appConnectionString,
                tenantA,
                companyA1,
                companyA2,
                actorId);
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
            await AssertPartyAccountOpeningPersistenceAsync(
                migratorDataSource, appDataSource, tenantA, companyA1, companyA2, actorId);
            await AssertAuthoritativeAgingPolicySourceAsync(
                migratorDataSource, appDataSource, tenantA, companyA1, companyA2, actorId);
            await AssertAuthoritativePartyReportSourceAsync(
                migratorDataSource, appDataSource, appConnectionString, tenantA, companyA1, companyA2, actorId);
            await AssertPaymentEconomicEventPersistenceAsync(
                appDataSource, tenantA, companyA1, companyA2, actorId);
            await AssertStatementLinePersistenceAsync(
                migratorDataSource, appDataSource, tenantA, companyA1, companyA2, actorId);
            await AssertProjectionGenerationPersistenceAsync(
                migratorDataSource, appDataSource, tenantA, companyA1, companyA2, actorId);
            await AssertInventoryQuantityMovementFoundationAsync(
                migratorDataSource, appDataSource, tenantA, companyA1, companyA2, actorId);
            await AssertSalesOrderLifecycleFoundationAsync(
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

        DateOnly sinkAsOf = new(2026, 8, 27);
        DateTimeOffset sinkCutoff = new(2026, 8, 27, 18, 0, 0, TimeSpan.Zero);
        var sinkImpact = new PartyReportImpactFact(
            Guid.CreateVersion7(), PartyReportImpactKind.Allocation, Guid.CreateVersion7(), 10m,
            sinkAsOf.AddDays(-1), sinkCutoff.AddMinutes(-1), null);
        var sinkItem = new PartyOpenItemSourceFact(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "sales.invoice",
            50m, 40m, sinkAsOf.AddDays(-10), sinkAsOf.AddDays(10), sinkCutoff.AddMinutes(-2),
            PartyReportRestrictionEvidence.Clear, [sinkImpact]);
        PartyReportSourceBatch sinkSource = PartyReportSourceBatch.Create(
            tenantId, companyId, Guid.CreateVersion7(), Guid.CreateVersion7(),
            PartyReportBalanceSide.Receivable, "GBP", sinkAsOf, sinkCutoff, 0m,
            "party-event:1", "party-event:2", [sinkItem], []);
        CalendarDayAgingPolicySnapshot sinkPolicy = CalendarDayAgingPolicySnapshot.Create(
            tenantId, companyId, Guid.CreateVersion7(), 1,
            [CalendarDayAgingBucket.Create("all", int.MinValue, int.MaxValue)]);
        Guid sinkGenerationId = Guid.CreateVersion7();
        PartyReportProjectionBuilder.ProjectionPair sinkPair = PartyReportProjectionBuilder.BuildPair(
            sinkSource, sinkPolicy, "party.account.detail", Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), 1, sinkGenerationId, sinkCutoff.AddMinutes(1));
        ControlAccountBalanceSnapshot sinkSubledger = ControlAccountBalanceSnapshot.Create(
            Guid.CreateVersion7(), sinkSource.ControlAccountId, LedgerSide.Subledger,
            0m, 50m, 10m, 40m, 2, new string('5', 64), sinkPair.Statement.ReportSlice);
        ControlAccountBalanceSnapshot sinkGeneralLedger = ControlAccountBalanceSnapshot.Create(
            Guid.CreateVersion7(), sinkSource.ControlAccountId, LedgerSide.GeneralLedger,
            0m, 50m, 10m, 40m, 2, new string('6', 64), sinkPair.Statement.ReportSlice);
        var sinkCommand = new PartyReportProjectionJobCommand(
            new PartyReportSourceQuery(tenantId, companyId, sinkSource.PartyAccountId, sinkAsOf, sinkCutoff),
            "party.account.detail", 1, sinkGenerationId, sinkPair.Statement.StatementId,
            sinkPair.Aging.AgingReportId, sinkPair.CrossFoot.CrossFootId, Guid.CreateVersion7(),
            sinkCutoff.AddMinutes(1), "integration-refresh");
        var sinkPublication = new PartyReportProjectionPublication(
            sinkCommand, sinkSource, sinkPair,
            new PartyControlAccountEvidence(sinkSubledger, sinkGeneralLedger));
        var postgresSink = new PostgresPartyReportProjectionSink(appDataSource, scope);
        PartyReportProjectionJobResult sinkFirst = await postgresSink.PublishAsync(sinkPublication);
        PartyReportProjectionJobResult sinkReplay = await postgresSink.PublishAsync(sinkPublication);
        Assert(sinkFirst.Created && !sinkReplay.Created &&
               sinkReplay.ProjectionGenerationId == sinkGenerationId,
            "Transaction-owning PostgreSQL projection sink did not create then idempotently replay the generation.");
        PartyReportProjectionSinkException sinkContextMismatch =
            await ThrowsAsync<PartyReportProjectionSinkException>(() => postgresSink.PublishAsync(
                sinkPublication with
                {
                    Command = sinkCommand with { ReportCode = "party.account.other" },
                }).AsTask());
        Assert(sinkContextMismatch.Code == "PARTY_REPORT_PUBLICATION_CONTEXT_MISMATCH",
            "PostgreSQL sink accepted a job command that did not match the validated report slice.");
        var deniedSink = new PostgresPartyReportProjectionSink(
            appDataSource, new ExecutionScope(tenantId, actorId, [otherCompanyId]));
        await ThrowsAsync<ExecutionScopeDeniedException>(() => deniedSink.PublishAsync(sinkPublication).AsTask());

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

    private static async Task SeedServiceIdentityAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        Guid companyId,
        Guid serviceActorId,
        Guid createdBy)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
        const string identitySql = """
            INSERT INTO iam.service_identity
                (id, tenant_id, identity_code, created_by)
            VALUES ($1,$2,$3,$4)
            """;
        await using (var identity = new NpgsqlCommand(identitySql, connection, transaction))
        {
            identity.Parameters.AddWithValue(serviceActorId);
            identity.Parameters.AddWithValue(tenantId);
            identity.Parameters.AddWithValue($"party-report-worker-{serviceActorId:N}");
            identity.Parameters.AddWithValue(createdBy);
            await identity.ExecuteNonQueryAsync();
        }
        const string permissionSql = """
            INSERT INTO iam.service_identity_company_permission
                (service_identity_id, tenant_id, company_id, permission_code, created_by)
            VALUES ($1,$2,$3,$4,$5)
            """;
        await using (var permission = new NpgsqlCommand(permissionSql, connection, transaction))
        {
            permission.Parameters.AddWithValue(serviceActorId);
            permission.Parameters.AddWithValue(tenantId);
            permission.Parameters.AddWithValue(companyId);
            permission.Parameters.AddWithValue(PartyReportRefreshPermissions.Refresh);
            permission.Parameters.AddWithValue(createdBy);
            await permission.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    private static async Task SetServiceIdentityActiveAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        Guid serviceActorId,
        bool isActive)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
        const string sql = """
            UPDATE iam.service_identity
            SET is_active=$3
            WHERE tenant_id=$1 AND id=$2
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(serviceActorId);
        command.Parameters.AddWithValue(isActive);
        Assert(await command.ExecuteNonQueryAsync() == 1,
            "Service identity active-state fixture did not update exactly one row.");
        await transaction.CommitAsync();
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

    private static async Task AssertPartyReportRefreshWorkQueueAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        string migratorConnectionString,
        string appConnectionString,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid ownerActorId)
    {
        Guid serviceActorId = Guid.CreateVersion7();
        await SeedServiceIdentityAsync(
            migratorDataSource,
            tenantId,
            companyId,
            serviceActorId,
            ownerActorId);
        var serviceScope = new ExecutionScope(
            tenantId,
            serviceActorId,
            [new CompanyAccess(companyId, [PartyReportRefreshPermissions.Refresh])]);
        var store = new PostgresPartyReportRefreshWorkStore(appDataSource, serviceScope);
        DateTimeOffset now = ToPostgresTimestamp(DateTimeOffset.UtcNow);

        PartyReportRefreshRequest CreateRequest(
            Guid generationId,
            string reason,
            long reportVersion = 1) => PartyReportRefreshRequest.Create(
                tenantId,
                companyId,
                Guid.CreateVersion7(),
                PartyAccountDetailReportDefinition.ReportCode,
                reportVersion,
                new DateOnly(2026, 8, 30),
                now,
                generationId,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                now.AddSeconds(1),
                reason,
                now,
                "Europe/Nicosia",
                "kagu-default",
                "run-once");

        Guid workItemId = Guid.CreateVersion7();
        Guid generationId = Guid.CreateVersion7();
        PartyReportRefreshRequest request = CreateRequest(generationId, "integration-queue-concurrency");
        var enqueue = new PartyReportRefreshEnqueueCommand(
            workItemId,
            $"queue-concurrency:{workItemId:N}",
            request,
            2,
            now,
            now,
            serviceActorId);
        PartyReportRefreshEnqueueResult created = await store.EnqueueAsync(enqueue);
        PartyReportRefreshEnqueueResult replay = await store.EnqueueAsync(enqueue);
        Assert(created.Created && !replay.Created && created.WorkItemId == replay.WorkItemId,
            "Party report refresh enqueue did not preserve the first request on exact replay.");
        PartyReportRefreshQueueException changedPayload =
            await ThrowsAsync<PartyReportRefreshQueueException>(() => store.EnqueueAsync(
                enqueue with { Request = CreateRequest(generationId, "changed-payload", 2) }).AsTask());
        Assert(changedPayload.Code == "PARTY_REPORT_REFRESH_REQUEST_KEY_REUSED",
            "Party report refresh request key accepted a changed canonical payload.");

        var hiddenStore = new PostgresPartyReportRefreshWorkStore(
            appDataSource,
            new ExecutionScope(
                tenantId,
                serviceActorId,
                [new CompanyAccess(otherCompanyId, [PartyReportRefreshPermissions.Refresh])]));
        Assert(await hiddenStore.TryClaimAsync(now, TimeSpan.FromSeconds(30)) is null,
            "Party report refresh queue exposed another company's pending work.");

        var competingStore = new PostgresPartyReportRefreshWorkStore(appDataSource, serviceScope);
        Task<PartyReportRefreshLease?> firstClaim = store.TryClaimAsync(
            now,
            TimeSpan.FromSeconds(30)).AsTask();
        Task<PartyReportRefreshLease?> secondClaim = competingStore.TryClaimAsync(
            now,
            TimeSpan.FromSeconds(30)).AsTask();
        PartyReportRefreshLease?[] claims = await Task.WhenAll(firstClaim, secondClaim);
        PartyReportRefreshLease winner = claims.Single(item => item is not null)!;
        Assert(claims.Count(item => item is not null) == 1 && winner.AttemptNumber == 1,
            "SKIP LOCKED claim allowed two workers to lease the same refresh occurrence.");
        Assert(await store.FailAsync(
                winner,
                "INTEGRATION_RETRY",
                now.AddSeconds(1),
                TimeSpan.Zero),
            "First failed refresh attempt did not return to the bounded retry queue.");
        PartyReportRefreshLease retry = await store.TryClaimAsync(
            now.AddSeconds(2),
            TimeSpan.FromSeconds(30)) ?? throw new InvalidOperationException("Retry was not claimable.");
        Assert(retry.AttemptNumber == 2 && retry.WorkItemId == workItemId,
            "Retry did not preserve work identity or increment its attempt.");
        Assert(!await store.FailAsync(
                retry,
                "INTEGRATION_TERMINAL",
                now.AddSeconds(3),
                TimeSpan.Zero),
            "Final refresh attempt was scheduled beyond max attempts.");

        Guid reclaimWorkItemId = Guid.CreateVersion7();
        PartyReportRefreshRequest reclaimRequest = CreateRequest(
            Guid.CreateVersion7(),
            "integration-expired-lease-reclaim");
        await store.EnqueueAsync(new PartyReportRefreshEnqueueCommand(
            reclaimWorkItemId,
            $"expired-reclaim:{reclaimWorkItemId:N}",
            reclaimRequest,
            2,
            now.AddSeconds(10),
            now,
            serviceActorId));
        PartyReportRefreshLease firstLease = await store.TryClaimAsync(
            now.AddSeconds(10),
            TimeSpan.FromSeconds(5)) ?? throw new InvalidOperationException("Lease fixture was not claimable.");
        PartyReportRefreshLease reclaimedLease = await store.TryClaimAsync(
            now.AddSeconds(16),
            TimeSpan.FromSeconds(5)) ?? throw new InvalidOperationException("Expired lease was not reclaimed.");
        Assert(firstLease.WorkItemId == reclaimedLease.WorkItemId && reclaimedLease.AttemptNumber == 2 &&
               firstLease.LeaseToken != reclaimedLease.LeaseToken,
            "Expired refresh lease was not reclaimed with a new token and attempt.");
        Assert(!await store.FailAsync(
                reclaimedLease,
                "INTEGRATION_RECLAIM_TERMINAL",
                now.AddSeconds(17),
                TimeSpan.Zero),
            "Reclaimed final attempt was not terminal.");

        Guid crashWorkItemId = Guid.CreateVersion7();
        PartyReportRefreshRequest crashRequest = CreateRequest(
            Guid.CreateVersion7(),
            "integration-last-attempt-crash");
        await store.EnqueueAsync(new PartyReportRefreshEnqueueCommand(
            crashWorkItemId,
            $"last-attempt-crash:{crashWorkItemId:N}",
            crashRequest,
            1,
            now.AddSeconds(20),
            now,
            serviceActorId));
        _ = await store.TryClaimAsync(now.AddSeconds(20), TimeSpan.FromSeconds(5)) ??
            throw new InvalidOperationException("Last-attempt crash fixture was not claimable.");
        Assert(await store.TryClaimAsync(now.AddSeconds(26), TimeSpan.FromSeconds(5)) is null,
            "An expired last attempt was incorrectly reclaimed beyond max attempts.");

        await using (NpgsqlConnection verifyConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction verifyTransaction = await verifyConnection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(
                verifyConnection,
                verifyTransaction,
                tenantId,
                serviceActorId,
                companyId);
            const string statusSql = """
                SELECT status, last_error_code,
                       (SELECT count(*) FROM reporting.party_report_refresh_event event
                        WHERE event.tenant_id=item.tenant_id
                          AND event.company_id=item.company_id
                          AND event.work_item_id=item.work_item_id)
                FROM reporting.party_report_refresh_work_item item
                WHERE tenant_id=$1 AND company_id=$2 AND work_item_id=$3
                """;
            await using var status = new NpgsqlCommand(statusSql, verifyConnection, verifyTransaction);
            status.Parameters.AddWithValue(tenantId);
            status.Parameters.AddWithValue(companyId);
            status.Parameters.AddWithValue(crashWorkItemId);
            await using NpgsqlDataReader reader = await status.ExecuteReaderAsync();
            Assert(await reader.ReadAsync() && reader.GetString(0) == "failed" &&
                   reader.GetString(1) == "PARTY_REPORT_REFRESH_LEASE_EXPIRED" && reader.GetInt64(2) == 3,
                "Expired last attempt did not produce a terminal append-only failure trail.");
            await reader.CloseAsync();

            await using var tamper = new NpgsqlCommand(
                "UPDATE reporting.party_report_refresh_work_item SET status='pending' " +
                "WHERE tenant_id=$1 AND company_id=$2 AND work_item_id=$3",
                verifyConnection,
                verifyTransaction);
            tamper.Parameters.AddWithValue(tenantId);
            tamper.Parameters.AddWithValue(companyId);
            tamper.Parameters.AddWithValue(crashWorkItemId);
            PostgresException terminalTamper = await ThrowsAsync<PostgresException>(() => tamper.ExecuteNonQueryAsync());
            Assert(terminalTamper.SqlState == PostgresErrorCodes.ObjectNotInPrerequisiteState,
                "Terminal refresh work was mutable through the runtime role.");
            await verifyTransaction.RollbackAsync();
        }

        IConfiguration exactConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KAGU_ERP_MIGRATOR_CONNECTION_STRING"] = migratorConnectionString,
                ["KAGU_ERP_APP_CONNECTION_STRING"] = appConnectionString,
                ["KAGU_ERP_REPORT_WORKER_TENANT_ID"] = tenantId.ToString(),
                ["KAGU_ERP_REPORT_WORKER_ACTOR_ID"] = serviceActorId.ToString(),
                ["KAGU_ERP_REPORT_WORKER_COMPANY_IDS"] = companyId.ToString(),
            })
            .Build();
        var services = new ServiceCollection();
        services.AddKaguErpBootstrap(exactConfiguration);
        services.AddKaguErpPartyReportRefreshWorker(exactConfiguration);
        await using ServiceProvider provider = services.BuildServiceProvider();
        IPartyReportRefreshCycle cycle = provider.GetRequiredService<IPartyReportRefreshCycle>();
        Assert((await cycle.ProcessNextAsync()).Disposition == PartyReportRefreshCycleDisposition.Idle,
            "Production Worker composition did not validate its exact IAM scope and become idle.");

        IConfiguration overbroadConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KAGU_ERP_APP_CONNECTION_STRING"] = appConnectionString,
                ["KAGU_ERP_REPORT_WORKER_TENANT_ID"] = tenantId.ToString(),
                ["KAGU_ERP_REPORT_WORKER_ACTOR_ID"] = serviceActorId.ToString(),
                ["KAGU_ERP_REPORT_WORKER_COMPANY_IDS"] = $"{companyId},{otherCompanyId}",
            })
            .Build();
        var overbroadServices = new ServiceCollection();
        overbroadServices.AddKaguErpBootstrap(overbroadConfiguration);
        overbroadServices.AddKaguErpPartyReportRefreshWorker(overbroadConfiguration);
        await using ServiceProvider overbroadProvider = overbroadServices.BuildServiceProvider();
        PartyReportWorkerIdentityException overbroad = await ThrowsAsync<PartyReportWorkerIdentityException>(() =>
            overbroadProvider.GetRequiredService<IPartyReportRefreshCycle>().ProcessNextAsync().AsTask());
        Assert(overbroad.Code == "PARTY_REPORT_WORKER_SCOPE_NOT_AUTHORIZED",
            "Deployment company allow-list widened the authoritative service-identity scope.");

        await SetServiceIdentityActiveAsync(migratorDataSource, tenantId, serviceActorId, false);
        PartyReportWorkerIdentityException inactive = await ThrowsAsync<PartyReportWorkerIdentityException>(() =>
            cycle.ProcessNextAsync().AsTask());
        Assert(inactive.Code == "PARTY_REPORT_WORKER_SCOPE_NOT_AUTHORIZED",
            "Inactive Worker service identity still resolved an execution scope.");
        await SetServiceIdentityActiveAsync(migratorDataSource, tenantId, serviceActorId, true);
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
        bool hasPostingPermission,
        Guid? sourceEventId = null,
        string sourceType = "integration.invoice",
        string postingPurpose = "journal-preparation",
        DateOnly? effectiveDate = null,
        DateTimeOffset? recordedAt = null,
        Guid? debitAccountId = null,
        Guid? creditAccountId = null,
        string functionalCurrency = "GBP",
        decimal functionalUnitsNumerator = 1m)
    {
        Guid postingRuleVersionId = Guid.CreateVersion7();
        Guid chartVersionId = Guid.CreateVersion7();
        Guid resolvedDebitAccountId = debitAccountId ?? Guid.CreateVersion7();
        Guid resolvedCreditAccountId = creditAccountId ?? Guid.CreateVersion7();
        Guid dimensionId = Guid.CreateVersion7();
        CurrencyCode gbp = CurrencyCode.Create("GBP");
        CurrencyCode functional = CurrencyCode.Create(functionalCurrency);
        ExchangeRateSnapshot rate = ExchangeRateSnapshot.Create(
            tenantId, companyId, Guid.CreateVersion7(), 1, gbp, functional, "spot", "integration",
            new DateOnly(2026, 8, 24), functionalUnitsNumerator, 1m);
        RoundingPolicySnapshot rounding = RoundingPolicySnapshot.Create(
            tenantId, companyId, Guid.CreateVersion7(), 1, 4, RoundingMode.ToEven);
        JournalCurrencyAmountSnapshot debitCurrency = JournalCurrencyAmountSnapshot.Create(
            rate, rounding, JournalAmount.Create(amount, 0m));
        JournalCurrencyAmountSnapshot creditCurrency = JournalCurrencyAmountSnapshot.Create(
            rate, rounding, JournalAmount.Create(0m, amount));
        ValidatedJournalDraft draft = ValidatedJournalDraft.Create(
            tenantId,
            companyId,
            sourceEventId ?? Guid.CreateVersion7(),
            postingRuleVersionId,
            sourceType,
            postingPurpose,
            effectiveDate ?? new DateOnly(2026, 8, 24),
            recordedAt ?? new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero),
            functional,
            [
                JournalLineDraft.Create(resolvedDebitAccountId, null, debitCurrency.FunctionalAmount,
                    [DimensionAssignment.Create(dimensionId, Guid.CreateVersion7())], debitCurrency),
                JournalLineDraft.Create(resolvedCreditAccountId, null, creditCurrency.FunctionalAmount,
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
            PostedSourceEvidence? evidence = await PostgresPostedSourceEvidenceLoader.LoadActiveAsync(
                connection,
                transaction,
                request.Scope,
                companyId,
                request.Draft.SourceType,
                request.Draft.SourceEventId,
                1,
                request.Draft.PostingPurpose,
                request.Draft.EffectiveDate,
                postedAt.AddSeconds(1));
            Assert(evidence is not null && evidence.JournalId == journalId &&
                   evidence.SourceVersion == 1 && evidence.PostedAt == first.PostedAt &&
                   evidence.EffectiveDate == request.Draft.EffectiveDate,
                "Exact active posted-source evidence was not reproduced.");
            PostedSourceLifecycleEvidence activeLifecycle =
                await PostgresPostedSourceEvidenceLoader.LoadLifecycleAsync(
                    connection,
                    transaction,
                    request.Scope,
                    companyId,
                    request.Draft.SourceType,
                    request.Draft.SourceEventId,
                    1,
                    request.Draft.PostingPurpose,
                    request.Draft.EffectiveDate,
                    postedAt.AddSeconds(1));
            Assert(activeLifecycle.State == PostedSourceLifecycleState.Active &&
                   activeLifecycle.Posting?.JournalId == journalId && activeLifecycle.Reversal is null,
                "Posted-source lifecycle did not expose the active journal state.");
            PostedSourceEvidence? beforePosting = await PostgresPostedSourceEvidenceLoader.LoadActiveAsync(
                connection,
                transaction,
                request.Scope,
                companyId,
                request.Draft.SourceType,
                request.Draft.SourceEventId,
                1,
                request.Draft.PostingPurpose,
                request.Draft.EffectiveDate,
                first.PostedAt.AddMilliseconds(-1));
            Assert(beforePosting is null,
                "A posted journal leaked into a recorded cutoff before its posting timestamp.");
            PostedSourceEvidence? wrongVersion = await PostgresPostedSourceEvidenceLoader.LoadActiveAsync(
                connection,
                transaction,
                request.Scope,
                companyId,
                request.Draft.SourceType,
                request.Draft.SourceEventId,
                2,
                request.Draft.PostingPurpose,
                request.Draft.EffectiveDate,
                postedAt.AddSeconds(1));
            Assert(wrongVersion is null,
                "Posted-source evidence ignored the requested source version.");
            PostedSourceLifecycleEvidence unpostedLifecycle =
                await PostgresPostedSourceEvidenceLoader.LoadLifecycleAsync(
                    connection,
                    transaction,
                    request.Scope,
                    companyId,
                    request.Draft.SourceType,
                    request.Draft.SourceEventId,
                    2,
                    request.Draft.PostingPurpose,
                    request.Draft.EffectiveDate,
                    postedAt.AddSeconds(1));
            Assert(unpostedLifecycle.State == PostedSourceLifecycleState.NotPosted &&
                   unpostedLifecycle.Posting is null && unpostedLifecycle.Reversal is null,
                "Posted-source lifecycle guessed evidence for an unposted source version.");

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
            PostedSourceEvidence? activeBeforeLink = await PostgresPostedSourceEvidenceLoader.LoadActiveAsync(
                connection,
                transaction,
                original.Scope,
                companyId,
                original.Draft.SourceType,
                original.Draft.SourceEventId,
                1,
                original.Draft.PostingPurpose,
                original.Draft.EffectiveDate,
                first.LinkedAt.AddMilliseconds(-1));
            PostedSourceEvidence? activeAfterLink = await PostgresPostedSourceEvidenceLoader.LoadActiveAsync(
                connection,
                transaction,
                original.Scope,
                companyId,
                original.Draft.SourceType,
                original.Draft.SourceEventId,
                1,
                original.Draft.PostingPurpose,
                original.Draft.EffectiveDate,
                linkedAt.AddSeconds(1));
            Assert(activeBeforeLink?.JournalId == originalJournalId && activeAfterLink is null,
                "Active posted-source evidence did not honor the bitemporal reversal link.");
            PostedSourceLifecycleEvidence lifecycleBeforeLink =
                await PostgresPostedSourceEvidenceLoader.LoadLifecycleAsync(
                    connection,
                    transaction,
                    original.Scope,
                    companyId,
                    original.Draft.SourceType,
                    original.Draft.SourceEventId,
                    1,
                    original.Draft.PostingPurpose,
                    original.Draft.EffectiveDate,
                    first.LinkedAt.AddMilliseconds(-1));
            PostedSourceLifecycleEvidence lifecycleAfterLink =
                await PostgresPostedSourceEvidenceLoader.LoadLifecycleAsync(
                    connection,
                    transaction,
                    original.Scope,
                    companyId,
                    original.Draft.SourceType,
                    original.Draft.SourceEventId,
                    1,
                    original.Draft.PostingPurpose,
                    original.Draft.EffectiveDate,
                    linkedAt.AddSeconds(1));
            Assert(lifecycleBeforeLink.State == PostedSourceLifecycleState.Active &&
                   lifecycleBeforeLink.Posting?.JournalId == originalJournalId &&
                   lifecycleAfterLink.State == PostedSourceLifecycleState.Reversed &&
                   lifecycleAfterLink.Posting?.JournalId == originalJournalId &&
                   lifecycleAfterLink.Reversal?.OriginalJournalId == originalJournalId &&
                   lifecycleAfterLink.Reversal.ReversalJournalId == reversalJournalId,
                "Posted-source lifecycle did not preserve original and reversal evidence across the cut.");

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

    private static async Task AssertPartyAccountOpeningPersistenceAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        Guid partyId = Guid.CreateVersion7();
        Guid partyAccountId = Guid.CreateVersion7();
        Guid controlAccountId = Guid.CreateVersion7();
        Guid openingEventId = Guid.CreateVersion7();
        DateTimeOffset recordedAt = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using (var party = new NpgsqlCommand(
                "INSERT INTO party.party_identity (tenant_id,party_id,created_at,created_by) VALUES ($1,$2,$3,$4)",
                connection,
                transaction))
            {
                party.Parameters.AddWithValue(tenantId);
                party.Parameters.AddWithValue(partyId);
                party.Parameters.AddWithValue(recordedAt);
                party.Parameters.AddWithValue(actorId);
                await party.ExecuteNonQueryAsync();
            }
            await using (var account = new NpgsqlCommand(
                "INSERT INTO party.party_account (tenant_id,company_id,party_account_id,party_id,currency,balance_side,control_account_id,created_at,created_by) VALUES ($1,$2,$3,$4,'GBP',1,$5,$6,$7)",
                connection,
                transaction))
            {
                account.Parameters.AddWithValue(tenantId);
                account.Parameters.AddWithValue(companyId);
                account.Parameters.AddWithValue(partyAccountId);
                account.Parameters.AddWithValue(partyId);
                account.Parameters.AddWithValue(controlAccountId);
                account.Parameters.AddWithValue(recordedAt);
                account.Parameters.AddWithValue(actorId);
                await account.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }

        PartyAccountOpeningDraft draft = PartyAccountOpeningDraft.Create(
            tenantId,
            companyId,
            openingEventId,
            partyAccountId,
            PartyAccountOpeningEntrySide.Debit,
            125.5000m,
            new DateOnly(2026, 1, 1),
            recordedAt,
            Guid.CreateVersion7(),
            [PartyAccountOpeningDueLineDraft.Create(
                Guid.CreateVersion7(), 125.5000m, new DateOnly(2026, 1, 1), Guid.CreateVersion7(), 1)]);
        var scope = new ExecutionScope(
            tenantId,
            actorId,
            [new CompanyAccess(
                companyId,
                [AuthorizedPartyAccountOpeningPreparation.RequiredPermission])]);
        AuthorizedPartyAccountOpeningPreparation preparation =
            AuthorizedPartyAccountOpeningPreparation.Create(scope, draft);

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            PartyAccountOpeningPersistenceResult created = await PostgresPartyAccountOpeningWriter.PersistAsync(
                connection,
                transaction,
                preparation);
            Assert(created.Created && created.OpeningEventId == openingEventId && created.SourceVersion == 1 &&
                   created.DueScheduleId == draft.DueScheduleId &&
                   created.BalanceSide == PartyAccountBalanceSide.Receivable && created.Currency == "GBP" &&
                   created.ControlAccountId == controlAccountId && created.RecordedAt == recordedAt,
                "Opening writer did not snapshot the authoritative PartyAccount context.");
            Assert(await CountAsync(
                    connection,
                    transaction,
                    "SELECT count(*) FROM party.due_schedule WHERE due_schedule_id=$1",
                    draft.DueScheduleId) == 1 &&
                   await CountAsync(
                       connection,
                       transaction,
                       "SELECT count(*) FROM party.due_schedule_line WHERE due_schedule_id=$1",
                       draft.DueScheduleId) == 1,
                "Opening writer did not persist the allocatable due schedule in the same transaction.");

            PartyAccountOpeningPersistenceResult replay = await PostgresPartyAccountOpeningWriter.PersistAsync(
                connection,
                transaction,
                preparation);
            Assert(!replay.Created && replay == created with { Created = false },
                "Opening source idempotent replay did not return the immutable first result.");

            PartyAccountOpeningDraft changedDraft = PartyAccountOpeningDraft.Create(
                tenantId,
                companyId,
                openingEventId,
                partyAccountId,
                PartyAccountOpeningEntrySide.Debit,
                125.5100m,
                draft.EffectiveDate,
                recordedAt,
                draft.DueScheduleId,
                [PartyAccountOpeningDueLineDraft.Create(
                    draft.DueLines[0].DueScheduleLineId,
                    125.5100m,
                    draft.DueLines[0].DueDate,
                    draft.DueLines[0].PaymentTermSnapshotId,
                    draft.DueLines[0].PaymentTermVersion)]);
            PartyAccountOpeningPersistenceConflictException conflict =
                await ThrowsAsync<PartyAccountOpeningPersistenceConflictException>(() =>
                    PostgresPartyAccountOpeningWriter.PersistAsync(
                        connection,
                        transaction,
                        AuthorizedPartyAccountOpeningPreparation.Create(scope, changedDraft)).AsTask());
            Assert(conflict.OpeningEventId == openingEventId,
                "Opening source identity accepted different immutable content.");

            PartyAccountOpeningDraft oppositeSideDraft = PartyAccountOpeningDraft.Create(
                tenantId,
                companyId,
                Guid.CreateVersion7(),
                partyAccountId,
                PartyAccountOpeningEntrySide.Credit,
                1m,
                draft.EffectiveDate,
                recordedAt,
                Guid.CreateVersion7(),
                [PartyAccountOpeningDueLineDraft.Create(
                    Guid.CreateVersion7(), 1m, draft.EffectiveDate, Guid.CreateVersion7(), 1)]);
            PartyAccountOpeningEntrySideAccountMismatchException sideMismatch =
                await ThrowsAsync<PartyAccountOpeningEntrySideAccountMismatchException>(() =>
                    PostgresPartyAccountOpeningWriter.PersistAsync(
                        connection,
                        transaction,
                        AuthorizedPartyAccountOpeningPreparation.Create(scope, oppositeSideDraft)).AsTask());
            Assert(sideMismatch.PartyAccountId == partyAccountId &&
                   sideMismatch.BalanceSide == PartyAccountBalanceSide.Receivable &&
                   sideMismatch.EntrySide == PartyAccountOpeningEntrySide.Credit,
                "Opening writer accepted an entry opposite to the PartyAccount natural side.");
            await transaction.CommitAsync();
        }

        var otherCompanyScope = new ExecutionScope(
            tenantId,
            actorId,
            [new CompanyAccess(
                otherCompanyId,
                [AuthorizedPartyAccountOpeningPreparation.RequiredPermission])]);
        PartyAccountOpeningDraft crossCompanyDraft = PartyAccountOpeningDraft.Create(
            tenantId,
            otherCompanyId,
            Guid.CreateVersion7(),
            partyAccountId,
            PartyAccountOpeningEntrySide.Debit,
            1m,
            draft.EffectiveDate,
            recordedAt,
            Guid.CreateVersion7(),
            [PartyAccountOpeningDueLineDraft.Create(
                Guid.CreateVersion7(), 1m, draft.EffectiveDate, Guid.CreateVersion7(), 1)]);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, otherCompanyId);
            PartyAccountOpeningAccountUnavailableException hidden =
                await ThrowsAsync<PartyAccountOpeningAccountUnavailableException>(() =>
                    PostgresPartyAccountOpeningWriter.PersistAsync(
                        connection,
                        transaction,
                        AuthorizedPartyAccountOpeningPreparation.Create(otherCompanyScope, crossCompanyDraft)).AsTask());
            Assert(hidden.PartyAccountId == partyAccountId,
                "Cross-company opening request did not fail closed at the PartyAccount boundary.");
            Assert(await CountAsync(
                    connection,
                    transaction,
                    "SELECT count(*) FROM party.party_account_opening_event WHERE opening_event_id=$1",
                    openingEventId) == 0,
                "Cross-company scope could read an opening event from another company.");
            await transaction.CommitAsync();
        }

        await AssertJournalSourceMutationRejectedAsync(
            appDataSource,
            tenantId,
            companyId,
            actorId,
            "UPDATE party.party_account_opening_event SET original_amount=original_amount WHERE opening_event_id=$1",
            openingEventId,
            "Runtime role updated an append-only PartyAccount opening event.");
        await AssertJournalSourceMutationRejectedAsync(
            appDataSource,
            tenantId,
            companyId,
            actorId,
            "DELETE FROM party.party_account_opening_event WHERE opening_event_id=$1",
            openingEventId,
            "Runtime role deleted an append-only PartyAccount opening event.");

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using var mismatchedContext = new NpgsqlCommand(
                "INSERT INTO party.party_account_opening_event (tenant_id,company_id,opening_event_id,source_version,party_account_id,balance_side,currency,control_account_id,entry_side,original_amount,effective_date,recorded_at,recorded_by) VALUES ($1,$2,$3,1,$4,2,'GBP',$5,1,1,DATE '2026-01-01',$6,$7)",
                connection,
                transaction);
            mismatchedContext.Parameters.AddWithValue(tenantId);
            mismatchedContext.Parameters.AddWithValue(companyId);
            mismatchedContext.Parameters.AddWithValue(Guid.CreateVersion7());
            mismatchedContext.Parameters.AddWithValue(partyAccountId);
            mismatchedContext.Parameters.AddWithValue(controlAccountId);
            mismatchedContext.Parameters.AddWithValue(recordedAt);
            mismatchedContext.Parameters.AddWithValue(actorId);
            PostgresException mismatch = await ThrowsAsync<PostgresException>(() =>
                mismatchedContext.ExecuteNonQueryAsync());
            Assert(mismatch.SqlState == PostgresErrorCodes.ForeignKeyViolation &&
                   mismatch.ConstraintName == "fk_party_account_opening_context",
                "Database accepted an opening event with a forged PartyAccount role snapshot.");
            await transaction.RollbackAsync();
        }
    }

    private static async Task AssertAuthoritativeAgingPolicySourceAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        Guid policyId = Guid.CreateVersion7();
        DateTimeOffset firstRecordedAt = new(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);
        DateTimeOffset secondRecordedAt = new(2026, 8, 10, 8, 0, 0, TimeSpan.Zero);
        CalendarDayAgingBucket[] firstBuckets =
        [
            CalendarDayAgingBucket.Create("all", int.MinValue, int.MaxValue),
        ];
        CalendarDayAgingBucket[] currentBuckets =
        [
            CalendarDayAgingBucket.Create("future", int.MinValue, -1),
            CalendarDayAgingBucket.Create("due-now", 0, 0),
            CalendarDayAgingBucket.Create("1-30", 1, 30),
            CalendarDayAgingBucket.Create("31-60", 31, 60),
            CalendarDayAgingBucket.Create("61-90", 61, 90),
            CalendarDayAgingBucket.Create("91-120", 91, 120),
            CalendarDayAgingBucket.Create("121+", 121, int.MaxValue),
        ];
        await InsertAgingPolicyDefinitionAsync(
            migratorDataSource,
            tenantId,
            companyId,
            actorId,
            policyId,
            1,
            new DateOnly(2026, 1, 1),
            firstRecordedAt,
            firstBuckets);
        await InsertAgingPolicyDefinitionAsync(
            migratorDataSource,
            tenantId,
            companyId,
            actorId,
            policyId,
            2,
            new DateOnly(2026, 7, 1),
            secondRecordedAt,
            currentBuckets);

        var scope = new ExecutionScope(tenantId, actorId, [companyId, otherCompanyId]);
        var source = new PostgresPartyAgingPolicySource(appDataSource, scope);
        CalendarDayAgingPolicySnapshot? beforeEffective = await source.LoadAsync(
            tenantId,
            companyId,
            new DateOnly(2026, 6, 30),
            secondRecordedAt.AddDays(1));
        Assert(beforeEffective is not null && beforeEffective.PolicyId == policyId &&
               beforeEffective.Version == 1 && beforeEffective.Buckets.Single().Code == "all",
            "Aging policy source did not respect the effective-date cut.");

        CalendarDayAgingPolicySnapshot? beforeRecorded = await source.LoadAsync(
            tenantId,
            companyId,
            new DateOnly(2026, 12, 31),
            secondRecordedAt.AddTicks(-1));
        Assert(beforeRecorded is not null && beforeRecorded.Version == 1,
            "Aging policy source leaked a version recorded after the data cutoff.");

        CalendarDayAgingPolicySnapshot? current = await source.LoadAsync(
            tenantId,
            companyId,
            new DateOnly(2026, 12, 31),
            secondRecordedAt);
        Assert(current is not null && current.PolicyId == policyId && current.Version == 2 &&
               string.Join('|', current.Buckets.Select(bucket => bucket.Code)) ==
               "future|due-now|1-30|31-60|61-90|91-120|121+",
            "Aging policy source did not reconstruct the authoritative company policy.");
        DateTimeOffset thirdRecordedAt = secondRecordedAt.AddDays(1);
        await InsertRuntimeAgingPolicyDefinitionAsync(
            appDataSource,
            tenantId,
            companyId,
            actorId,
            policyId,
            3,
            new DateOnly(2027, 1, 1),
            thirdRecordedAt,
            currentBuckets);
        CalendarDayAgingPolicySnapshot? beforeFutureEffective = await source.LoadAsync(
            tenantId,
            companyId,
            new DateOnly(2026, 12, 31),
            thirdRecordedAt);
        Assert(beforeFutureEffective is not null && beforeFutureEffective.Version == 2,
            "A future-effective aging policy changed the current report cut.");
        Assert(await source.LoadAsync(
                   tenantId,
                   otherCompanyId,
                   new DateOnly(2026, 12, 31),
                   secondRecordedAt) is null,
            "A company without an aging policy did not fail closed with no source result.");

        var restrictedSource = new PostgresPartyAgingPolicySource(
            appDataSource,
            new ExecutionScope(tenantId, actorId, [companyId]));
        await ThrowsAsync<ExecutionScopeDeniedException>(() => restrictedSource.LoadAsync(
            tenantId,
            otherCompanyId,
            new DateOnly(2026, 12, 31),
            secondRecordedAt).AsTask());

        await using (NpgsqlConnection rlsConnection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction rlsTransaction = await rlsConnection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(rlsConnection, rlsTransaction, tenantId, actorId, otherCompanyId);
            Assert(await CountAsync(
                    rlsConnection,
                    rlsTransaction,
                    "SELECT count(*) FROM reporting.aging_policy_definition WHERE policy_id = $1",
                    policyId) == 0,
                "Aging policy definition leaked through a different company RLS scope.");
            await rlsTransaction.CommitAsync();
        }

        await using (NpgsqlConnection privilegeConnection = await appDataSource.OpenConnectionAsync())
        await using (var privilege = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user,'reporting.aging_policy_definition','SELECT'),has_table_privilege(current_user,'reporting.aging_policy_definition','INSERT'),has_table_privilege(current_user,'reporting.aging_policy_definition','UPDATE'),has_table_privilege(current_user,'reporting.aging_policy_definition_bucket','DELETE')",
            privilegeConnection))
        await using (NpgsqlDataReader reader = await privilege.ExecuteReaderAsync())
        {
            Assert(await reader.ReadAsync() && reader.GetBoolean(0) && reader.GetBoolean(1) &&
                   !reader.GetBoolean(2) && !reader.GetBoolean(3),
                "Runtime aging-policy definition privileges are not append-only.");
        }

        await using (NpgsqlConnection gapConnection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction gapTransaction = await gapConnection.BeginTransactionAsync())
        {
            await ExecuteAsync(gapConnection, gapTransaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using var gap = new NpgsqlCommand(
                "INSERT INTO reporting.aging_policy_definition (tenant_id,company_id,policy_id,policy_version,effective_from,recorded_at,recorded_by,bucket_count) VALUES ($1,$2,$3,5,$4,$5,$6,1)",
                gapConnection,
                gapTransaction);
            gap.Parameters.AddWithValue(tenantId);
            gap.Parameters.AddWithValue(companyId);
            gap.Parameters.AddWithValue(policyId);
            gap.Parameters.AddWithValue(new DateOnly(2027, 1, 1));
            gap.Parameters.AddWithValue(secondRecordedAt.AddDays(1));
            gap.Parameters.AddWithValue(actorId);
            PostgresException exception = await ThrowsAsync<PostgresException>(() => gap.ExecuteNonQueryAsync());
            Assert(exception.SqlState == PostgresErrorCodes.CheckViolation &&
                   exception.ConstraintName == "ck_aging_policy_definition_version_sequence",
                "Database accepted a non-contiguous aging policy version.");
            await gapTransaction.RollbackAsync();
        }

        await using (NpgsqlConnection identityConnection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction identityTransaction = await identityConnection.BeginTransactionAsync())
        {
            await ExecuteAsync(identityConnection, identityTransaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using var changedIdentity = new NpgsqlCommand(
                "INSERT INTO reporting.aging_policy_definition (tenant_id,company_id,policy_id,policy_version,effective_from,recorded_at,recorded_by,bucket_count) VALUES ($1,$2,$3,4,$4,$5,$6,1)",
                identityConnection,
                identityTransaction);
            changedIdentity.Parameters.AddWithValue(tenantId);
            changedIdentity.Parameters.AddWithValue(companyId);
            changedIdentity.Parameters.AddWithValue(Guid.CreateVersion7());
            changedIdentity.Parameters.AddWithValue(new DateOnly(2027, 1, 1));
            changedIdentity.Parameters.AddWithValue(secondRecordedAt.AddDays(1));
            changedIdentity.Parameters.AddWithValue(actorId);
            PostgresException exception = await ThrowsAsync<PostgresException>(() => changedIdentity.ExecuteNonQueryAsync());
            Assert(exception.SqlState == PostgresErrorCodes.CheckViolation &&
                   exception.ConstraintName == "ck_aging_policy_definition_policy_id_stable",
                "Database accepted a different policy identity in one company stream.");
            await identityTransaction.RollbackAsync();
        }

        await using (NpgsqlConnection coverageConnection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction coverageTransaction = await coverageConnection.BeginTransactionAsync())
        {
            await ExecuteAsync(coverageConnection, coverageTransaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            const string headerSql = "INSERT INTO reporting.aging_policy_definition (tenant_id,company_id,policy_id,policy_version,effective_from,recorded_at,recorded_by,bucket_count) VALUES ($1,$2,$3,1,$4,$5,$6,2)";
            await using (var header = new NpgsqlCommand(headerSql, coverageConnection, coverageTransaction))
            {
                header.Parameters.AddWithValue(tenantId);
                header.Parameters.AddWithValue(otherCompanyId);
                header.Parameters.AddWithValue(Guid.CreateVersion7());
                header.Parameters.AddWithValue(new DateOnly(2026, 1, 1));
                header.Parameters.AddWithValue(firstRecordedAt);
                header.Parameters.AddWithValue(actorId);
                await header.ExecuteNonQueryAsync();
            }
            const string bucketsSql = "INSERT INTO reporting.aging_policy_definition_bucket (tenant_id,company_id,policy_version,bucket_ordinal,bucket_code,minimum_days_overdue,maximum_days_overdue) VALUES ($1,$2,1,1,'before',-2147483648,0),($1,$2,1,2,'after',2,2147483647)";
            await using (var buckets = new NpgsqlCommand(bucketsSql, coverageConnection, coverageTransaction))
            {
                buckets.Parameters.AddWithValue(tenantId);
                buckets.Parameters.AddWithValue(otherCompanyId);
                await buckets.ExecuteNonQueryAsync();
            }
            PostgresException exception = await ThrowsAsync<PostgresException>(() => coverageTransaction.CommitAsync());
            Assert(exception.SqlState == PostgresErrorCodes.CheckViolation &&
                   exception.ConstraintName == "ck_aging_policy_definition_bucket_coverage",
                "Database accepted a gapped authoritative aging policy.");
        }
    }

    private static async Task InsertAgingPolicyDefinitionAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        Guid policyId,
        long policyVersion,
        DateOnly effectiveFrom,
        DateTimeOffset recordedAt,
        CalendarDayAgingBucket[] buckets)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
        const string headerSql = """
            INSERT INTO reporting.aging_policy_definition
                (tenant_id,company_id,policy_id,policy_version,effective_from,recorded_at,
                 recorded_by,bucket_count)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8)
            """;
        await using (var header = new NpgsqlCommand(headerSql, connection, transaction))
        {
            header.Parameters.AddWithValue(tenantId);
            header.Parameters.AddWithValue(companyId);
            header.Parameters.AddWithValue(policyId);
            header.Parameters.AddWithValue(policyVersion);
            header.Parameters.AddWithValue(effectiveFrom);
            header.Parameters.AddWithValue(recordedAt);
            header.Parameters.AddWithValue(actorId);
            header.Parameters.AddWithValue(buckets.Length);
            await header.ExecuteNonQueryAsync();
        }

        const string bucketSql = """
            INSERT INTO reporting.aging_policy_definition_bucket
                (tenant_id,company_id,policy_version,bucket_ordinal,bucket_code,
                 minimum_days_overdue,maximum_days_overdue)
            VALUES ($1,$2,$3,$4,$5,$6,$7)
            """;
        for (var index = 0; index < buckets.Length; index++)
        {
            CalendarDayAgingBucket bucket = buckets[index];
            await using var command = new NpgsqlCommand(bucketSql, connection, transaction);
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(policyVersion);
            command.Parameters.AddWithValue(index + 1);
            command.Parameters.AddWithValue(bucket.Code);
            command.Parameters.AddWithValue(bucket.MinimumDaysOverdue);
            command.Parameters.AddWithValue(bucket.MaximumDaysOverdue);
            await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    private static async Task InsertRuntimeAgingPolicyDefinitionAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        Guid policyId,
        long policyVersion,
        DateOnly effectiveFrom,
        DateTimeOffset recordedAt,
        CalendarDayAgingBucket[] buckets)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
        const string headerSql = """
            INSERT INTO reporting.aging_policy_definition
                (tenant_id,company_id,policy_id,policy_version,effective_from,recorded_at,
                 recorded_by,bucket_count)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8)
            """;
        await using (var header = new NpgsqlCommand(headerSql, connection, transaction))
        {
            header.Parameters.AddWithValue(tenantId);
            header.Parameters.AddWithValue(companyId);
            header.Parameters.AddWithValue(policyId);
            header.Parameters.AddWithValue(policyVersion);
            header.Parameters.AddWithValue(effectiveFrom);
            header.Parameters.AddWithValue(recordedAt);
            header.Parameters.AddWithValue(actorId);
            header.Parameters.AddWithValue(buckets.Length);
            await header.ExecuteNonQueryAsync();
        }

        const string bucketSql = """
            INSERT INTO reporting.aging_policy_definition_bucket
                (tenant_id,company_id,policy_version,bucket_ordinal,bucket_code,
                 minimum_days_overdue,maximum_days_overdue)
            VALUES ($1,$2,$3,$4,$5,$6,$7)
            """;
        for (var index = 0; index < buckets.Length; index++)
        {
            CalendarDayAgingBucket bucket = buckets[index];
            await using var command = new NpgsqlCommand(bucketSql, connection, transaction);
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(policyVersion);
            command.Parameters.AddWithValue(index + 1);
            command.Parameters.AddWithValue(bucket.Code);
            command.Parameters.AddWithValue(bucket.MinimumDaysOverdue);
            command.Parameters.AddWithValue(bucket.MaximumDaysOverdue);
            await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    private static async Task AssertAuthoritativePartyReportSourceAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        string appConnectionString,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        Guid partyId = Guid.CreateVersion7();
        Guid partyAccountId = Guid.CreateVersion7();
        Guid controlAccountId = Guid.CreateVersion7();
        Guid dueScheduleId = Guid.CreateVersion7();
        Guid dueSourceEventId = Guid.CreateVersion7();
        Guid dueScheduleLineId = Guid.CreateVersion7();
        Guid openingEventId = Guid.CreateVersion7();
        DateOnly dueEffectiveDate = new(2026, 8, 24);
        DateTimeOffset dueRecordedAt = new(2026, 8, 25, 8, 0, 0, TimeSpan.Zero);
        DateOnly openingEffectiveDate = new(2026, 8, 1);
        DateTimeOffset openingRecordedAt = new(2026, 8, 2, 8, 0, 0, TimeSpan.Zero);
        var reportScope = new ExecutionScope(tenantId, actorId, [companyId]);
        var currency = AllocationCurrencyCode.Create("GBP");
        ValidatedDueSchedule dueSchedule = ValidatedDueSchedule.Create(
            tenantId,
            companyId,
            partyAccountId,
            dueSourceEventId,
            currency,
            75m,
            [DueScheduleLine.Create(
                tenantId,
                companyId,
                partyAccountId,
                dueSourceEventId,
                dueScheduleLineId,
                currency,
                75m,
                new DateOnly(2026, 7, 15),
                Guid.CreateVersion7(),
                1,
                controlAccountId)]);
        var dueCommand = new DueSchedulePersistenceCommand(
            reportScope,
            partyId,
            PartyAccountBalanceSide.Receivable,
            dueScheduleId,
            "sales.invoice",
            1,
            dueEffectiveDate,
            "primary-receivable",
            controlAccountId,
            dueRecordedAt,
            dueSchedule);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            DueSchedulePersistenceResult persisted = await PostgresDueScheduleWriter.PersistAsync(
                connection,
                transaction,
                dueCommand);
            Assert(persisted.Created, "Party report source fixture did not create its due schedule.");
            await transaction.CommitAsync();
        }

        PartyAccountOpeningDraft openingDraft = PartyAccountOpeningDraft.Create(
            tenantId,
            companyId,
            openingEventId,
            partyAccountId,
            PartyAccountOpeningEntrySide.Debit,
            25m,
            openingEffectiveDate,
            openingRecordedAt,
            Guid.CreateVersion7(),
            [PartyAccountOpeningDueLineDraft.Create(
                Guid.CreateVersion7(), 25m, openingEffectiveDate, Guid.CreateVersion7(), 1)]);
        var openingScope = new ExecutionScope(
            tenantId,
            actorId,
            [new CompanyAccess(
                companyId,
                [AuthorizedPartyAccountOpeningPreparation.RequiredPermission])]);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            PartyAccountOpeningPersistenceResult persisted = await PostgresPartyAccountOpeningWriter.PersistAsync(
                connection,
                transaction,
                AuthorizedPartyAccountOpeningPreparation.Create(openingScope, openingDraft));
            Assert(persisted.Created, "Party report source fixture did not create its opening source.");
            await transaction.CommitAsync();
        }

        PartySourcePostingEvidenceLoader evidenceLoader = async (
            connection,
            transaction,
            activeScope,
            requestedCompanyId,
            sourceType,
            sourceEventId,
            sourceVersion,
            postingPurpose,
            effectiveAsOf,
            recordedCutoff,
            cancellationToken) =>
        {
            PostedSourceEvidence? evidence = await PostgresPostedSourceEvidenceLoader.LoadActiveAsync(
                connection,
                transaction,
                activeScope,
                requestedCompanyId,
                sourceType,
                sourceEventId,
                sourceVersion,
                postingPurpose,
                effectiveAsOf,
                recordedCutoff,
                cancellationToken);
            return evidence is null
                ? null
                : new PartySourcePostingEvidence(
                    evidence.JournalId,
                    evidence.SourceType,
                    evidence.SourceEventId,
                    evidence.SourceVersion,
                    evidence.PostingPurpose,
                    evidence.EffectiveDate,
                    evidence.RecordedAt,
                    evidence.PostedAt);
        };
        PartySourcePostingLifecycleEvidenceLoader lifecycleLoader = async (
            connection,
            transaction,
            activeScope,
            requestedCompanyId,
            sourceType,
            sourceEventId,
            sourceVersion,
            postingPurpose,
            effectiveAsOf,
            recordedCutoff,
            cancellationToken) =>
        {
            PostedSourceLifecycleEvidence lifecycle = await PostgresPostedSourceEvidenceLoader.LoadLifecycleAsync(
                connection,
                transaction,
                activeScope,
                requestedCompanyId,
                sourceType,
                sourceEventId,
                sourceVersion,
                postingPurpose,
                effectiveAsOf,
                recordedCutoff,
                cancellationToken);
            PartySourcePostingEvidence? posting = lifecycle.Posting is null
                ? null
                : new PartySourcePostingEvidence(
                    lifecycle.Posting.JournalId,
                    lifecycle.Posting.SourceType,
                    lifecycle.Posting.SourceEventId,
                    lifecycle.Posting.SourceVersion,
                    lifecycle.Posting.PostingPurpose,
                    lifecycle.Posting.EffectiveDate,
                    lifecycle.Posting.RecordedAt,
                    lifecycle.Posting.PostedAt);
            PartySourcePostingReversalEvidence? reversal = lifecycle.Reversal is null
                ? null
                : new PartySourcePostingReversalEvidence(
                    lifecycle.Reversal.OriginalJournalId,
                    lifecycle.Reversal.ReversalJournalId,
                    lifecycle.Reversal.EffectiveDate,
                    lifecycle.Reversal.RecordedAt,
                    lifecycle.Reversal.PostedAt,
                    lifecycle.Reversal.LinkedAt);
            return new PartySourcePostingLifecycleEvidence(
                (PartySourcePostingLifecycleState)lifecycle.State,
                posting,
                reversal);
        };
        var source = new PostgresPartyReportSource(
            appDataSource,
            reportScope,
            evidenceLoader,
            lifecycleLoader);
        var query = new PartyReportSourceQuery(
            tenantId,
            companyId,
            partyAccountId,
            new DateOnly(2026, 12, 31),
            DateTimeOffset.UtcNow.AddMinutes(1));
        PartyReportSourceBatch? beforePosting = await source.LoadAsync(query);
        Assert(beforePosting is not null && beforePosting.OpeningExposure == 0m &&
               beforePosting.OpenItems.Count == 0 &&
               beforePosting.SourceWatermarkFrom == "posted-journal:none",
            "Unposted Party sources leaked into the authoritative report batch.");

        JournalPreparationRequest dueJournalRequest = CreateJournalPreparationRequest(
            tenantId,
            companyId,
            actorId,
            75m,
            hasPostingPermission: true,
            dueSourceEventId,
            dueCommand.SourceType,
            dueCommand.SourcePostingPurpose,
            dueEffectiveDate,
            dueRecordedAt,
            debitAccountId: controlAccountId,
            functionalCurrency: "TRY",
            functionalUnitsNumerator: 40m);
        Guid dueJournalId = Guid.CreateVersion7();
        await PostJournalFixtureAsync(
            migratorDataSource,
            appDataSource,
            dueJournalRequest,
            dueJournalId,
            actorId);
        DateTimeOffset dueGoldenCutoff = ToPostgresTimestamp(DateTimeOffset.UtcNow.AddMinutes(1));
        PartyReportSourceQuery dueGoldenQuery = query with { RecordedCutoff = dueGoldenCutoff };
        PartyReportSourceBatch? dueGoldenSource = await source.LoadAsync(dueGoldenQuery);
        Assert(dueGoldenSource is not null && dueGoldenSource.OpeningExposure == 0m &&
               dueGoldenSource.OpenItems.Single().RemainingAmount == 75m &&
               dueGoldenSource.PostingLineage.Count == 1 &&
               dueGoldenSource.PostingLineage[0].JournalId == dueJournalId,
            "Posted due source did not expose its exact journal lineage for the golden report cut.");
        var postgresControlSource = new PostgresPartyControlAccountEvidenceSource(
            appDataSource,
            reportScope,
            LoadPartyGeneralLedgerEvidenceAsync);
        var dueGoldenJob = new PartyReportProjectionJob(
            source,
            new PostgresPartyAgingPolicySource(appDataSource, reportScope),
            postgresControlSource,
            new PostgresPartyReportProjectionSink(appDataSource, reportScope));
        Guid dueGoldenGenerationId = Guid.CreateVersion7();
        Guid dueGoldenStatementId = Guid.CreateVersion7();
        Guid dueGoldenAgingId = Guid.CreateVersion7();
        Guid dueGoldenCrossFootId = Guid.CreateVersion7();
        Guid dueGoldenReconciliationId = Guid.CreateVersion7();
        var dueGoldenCommand = new PartyReportProjectionJobCommand(
            dueGoldenQuery,
            "party.account.detail",
            1,
            dueGoldenGenerationId,
            dueGoldenStatementId,
            dueGoldenAgingId,
            dueGoldenCrossFootId,
            dueGoldenReconciliationId,
            dueGoldenCutoff.AddMinutes(1),
            "integration-golden-source-to-gl");
        PartyReportProjectionJobResult dueGoldenCreated = await dueGoldenJob.RunAsync(dueGoldenCommand);
        PartyReportProjectionJobResult dueGoldenReplay = await dueGoldenJob.RunAsync(dueGoldenCommand);
        Assert(dueGoldenCreated.Created && !dueGoldenReplay.Created &&
               dueGoldenReplay.ProjectionGenerationId == dueGoldenGenerationId,
            "Real Party source-to-GL projection did not create then idempotently replay one generation.");
        await AssertPersistedPartyGoldenAsync(
            appDataSource,
            reportScope,
            companyId,
            dueGoldenCrossFootId,
            dueGoldenStatementId,
            dueGoldenAgingId,
            dueGoldenReconciliationId,
            dueGoldenGenerationId,
            75m);

        Guid workerActorId = Guid.CreateVersion7();
        await SeedServiceIdentityAsync(
            migratorDataSource,
            tenantId,
            companyId,
            workerActorId,
            actorId);
        var workerScope = new ExecutionScope(
            tenantId,
            workerActorId,
            [new CompanyAccess(companyId, [PartyReportRefreshPermissions.Refresh])]);
        DateTimeOffset workerScheduledAt = ToPostgresTimestamp(DateTimeOffset.UtcNow);
        Guid workerGenerationId = Guid.CreateVersion7();
        Guid workerStatementId = Guid.CreateVersion7();
        Guid workerAgingId = Guid.CreateVersion7();
        Guid workerCrossFootId = Guid.CreateVersion7();
        Guid workerReconciliationId = Guid.CreateVersion7();
        PartyReportRefreshRequest workerRequest = PartyReportRefreshRequest.Create(
            tenantId,
            companyId,
            partyAccountId,
            PartyAccountDetailReportDefinition.ReportCode,
            PartyAccountDetailReportDefinition.Version,
            dueGoldenQuery.EffectiveAsOf,
            dueGoldenQuery.RecordedCutoff,
            workerGenerationId,
            workerStatementId,
            workerAgingId,
            workerCrossFootId,
            workerReconciliationId,
            dueGoldenCutoff.AddMinutes(2),
            "integration-production-worker",
            workerScheduledAt,
            "Europe/Nicosia",
            "kagu-default",
            "run-once");
        var workerStore = new PostgresPartyReportRefreshWorkStore(appDataSource, workerScope);
        Guid workerWorkItemId = Guid.CreateVersion7();
        await workerStore.EnqueueAsync(new PartyReportRefreshEnqueueCommand(
            workerWorkItemId,
            $"party-golden-worker:{workerWorkItemId:N}",
            workerRequest,
            3,
            workerScheduledAt,
            workerScheduledAt,
            workerActorId));
        IConfiguration workerConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KAGU_ERP_APP_CONNECTION_STRING"] = appConnectionString,
                ["KAGU_ERP_REPORT_WORKER_TENANT_ID"] = tenantId.ToString(),
                ["KAGU_ERP_REPORT_WORKER_ACTOR_ID"] = workerActorId.ToString(),
                ["KAGU_ERP_REPORT_WORKER_COMPANY_IDS"] = companyId.ToString(),
            })
            .Build();
        var workerServices = new ServiceCollection();
        workerServices.AddKaguErpBootstrap(workerConfiguration);
        workerServices.AddKaguErpPartyReportRefreshWorker(workerConfiguration);
        await using (ServiceProvider workerProvider = workerServices.BuildServiceProvider())
        {
            IPartyReportRefreshCycle workerCycle =
                workerProvider.GetRequiredService<IPartyReportRefreshCycle>();
            PartyReportRefreshCycleResult workerResult = await workerCycle.ProcessNextAsync();
            string workerPersistenceState;
            await using (NpgsqlConnection diagnosticConnection = await appDataSource.OpenConnectionAsync())
            await using (NpgsqlTransaction diagnosticTransaction = await diagnosticConnection.BeginTransactionAsync())
            {
                await SetAuditScopeAsync(
                    diagnosticConnection,
                    diagnosticTransaction,
                    tenantId,
                    workerActorId,
                    companyId);
                const string diagnosticSql = """
                    SELECT item.status, item.last_error_code,
                           EXISTS(SELECT 1 FROM reporting.projection_generation generation
                                  WHERE generation.tenant_id=item.tenant_id
                                    AND generation.company_id=item.company_id
                                    AND generation.projection_generation_id=item.projection_generation_id)
                    FROM reporting.party_report_refresh_work_item item
                    WHERE item.tenant_id=$1 AND item.company_id=$2 AND item.work_item_id=$3
                    """;
                await using var diagnostic = new NpgsqlCommand(
                    diagnosticSql,
                    diagnosticConnection,
                    diagnosticTransaction);
                diagnostic.Parameters.AddWithValue(tenantId);
                diagnostic.Parameters.AddWithValue(companyId);
                diagnostic.Parameters.AddWithValue(workerWorkItemId);
                await using NpgsqlDataReader diagnosticReader = await diagnostic.ExecuteReaderAsync();
                Assert(await diagnosticReader.ReadAsync(), "Worker diagnostic work item was not visible.");
                workerPersistenceState =
                    $"status={diagnosticReader.GetString(0)}, " +
                    $"storedError={(diagnosticReader.IsDBNull(1) ? "none" : diagnosticReader.GetString(1))}, " +
                    $"projectionExists={diagnosticReader.GetBoolean(2)}";
                await diagnosticReader.CloseAsync();
                await diagnosticTransaction.CommitAsync();
            }
            Assert(workerResult.Disposition == PartyReportRefreshCycleDisposition.Completed &&
                   workerResult.WorkItemId == workerWorkItemId && workerResult.AttemptNumber == 1,
                $"Production Worker composition did not complete the durable Party projection work item; " +
                $"disposition={workerResult.Disposition}, error={workerResult.ErrorCode ?? "none"}, " +
                $"{workerPersistenceState}.");
            Assert((await workerCycle.ProcessNextAsync()).Disposition == PartyReportRefreshCycleDisposition.Idle,
                "Completed durable Party projection work was claimed a second time.");
        }
        await AssertPersistedPartyGoldenAsync(
            appDataSource,
            workerScope,
            companyId,
            workerCrossFootId,
            workerStatementId,
            workerAgingId,
            workerReconciliationId,
            workerGenerationId,
            75m);
        JournalPreparationRequest openingJournalRequest = CreateJournalPreparationRequest(
            tenantId,
            companyId,
            actorId,
            25m,
            hasPostingPermission: true,
            openingEventId,
            PartyAccountOpeningDraft.SourceType,
            PartyAccountOpeningDraft.PostingPurpose,
            openingEffectiveDate,
            openingRecordedAt,
            debitAccountId: controlAccountId,
            functionalCurrency: "TRY",
            functionalUnitsNumerator: 40m);
        Guid openingJournalId = Guid.CreateVersion7();
        await PostJournalFixtureAsync(
            migratorDataSource,
            appDataSource,
            openingJournalRequest,
            openingJournalId,
            actorId);

        PartyReportSourceBatch? posted = await source.LoadAsync(
            query with { RecordedCutoff = DateTimeOffset.UtcNow.AddMinutes(1) });
        PartyOpenItemSourceFact? postedDue = posted?.OpenItems
            .SingleOrDefault(item => item.SourceEventId == dueSourceEventId);
        PartyOpenItemSourceFact? postedOpening = posted?.OpenItems
            .SingleOrDefault(item => item.SourceEventId == openingEventId);
        Assert(posted is not null && posted.OpeningExposure == 0m && posted.OpenItems.Count == 2 &&
               postedDue is not null && postedDue.OriginalAmount == 75m &&
               postedDue.RemainingAmount == 75m && postedDue.EffectiveDate == dueEffectiveDate &&
               postedDue.RestrictionEvidence == PartyReportRestrictionEvidence.Clear &&
               postedOpening is not null && postedOpening.OriginalAmount == 25m &&
               postedOpening.RemainingAmount == 25m &&
               postedOpening.DueDate == openingDraft.DueLines[0].DueDate &&
               posted.SourceWatermarkFrom.StartsWith("posted-journal:", StringComparison.Ordinal) &&
               posted.SourceWatermarkTo.StartsWith("posted-set-v1:", StringComparison.Ordinal) &&
               posted.SourceChecksumSha256.Length == 64,
            "Exact posted Party sources did not compose into the authoritative report contract.");

        var restrictionScope = new ExecutionScope(
            tenantId,
            actorId,
            [new CompanyAccess(
                companyId,
                [AuthorizedOpenItemRestrictionChange.RequiredPermission])]);
        DateTimeOffset disputeRecordedAt = DateTimeOffset.UtcNow;
        OpenItemRestrictionEvent dispute = OpenItemRestrictionEvent.Create(
            Guid.CreateVersion7(),
            tenantId,
            companyId,
            partyAccountId,
            dueScheduleLineId,
            OpenItemRestrictionKind.Dispute,
            OpenItemRestrictionAction.Applied,
            "invoice-under-review",
            new DateOnly(2026, 8, 28),
            disputeRecordedAt);
        AuthorizedOpenItemRestrictionChange disputeChange =
            AuthorizedOpenItemRestrictionChange.Create(restrictionScope, dispute);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            OpenItemRestrictionPersistenceResult created =
                await PostgresOpenItemRestrictionWriter.PersistAsync(connection, transaction, disputeChange);
            OpenItemRestrictionPersistenceResult replay =
                await PostgresOpenItemRestrictionWriter.PersistAsync(connection, transaction, disputeChange);
            Assert(created.Created && !replay.Created && replay.EventId == dispute.EventId,
                "Restriction writer did not preserve the immutable first event on retry.");
            OpenItemRestrictionEvent changedReason = OpenItemRestrictionEvent.Create(
                dispute.EventId,
                tenantId,
                companyId,
                partyAccountId,
                dueScheduleLineId,
                dispute.Kind,
                dispute.Action,
                "changed-reason",
                dispute.EffectiveDate,
                dispute.RecordedAt);
            OpenItemRestrictionPersistenceConflictException conflict =
                await ThrowsAsync<OpenItemRestrictionPersistenceConflictException>(() =>
                    PostgresOpenItemRestrictionWriter.PersistAsync(
                        connection,
                        transaction,
                        AuthorizedOpenItemRestrictionChange.Create(restrictionScope, changedReason)).AsTask());
            Assert(conflict.EventId == dispute.EventId,
                "Restriction event identity accepted different immutable content.");
            await transaction.CommitAsync();
        }
        PartyReportSourceBatch? disputed = await source.LoadAsync(
            query with { RecordedCutoff = DateTimeOffset.UtcNow.AddMinutes(1) });
        Assert(disputed?.OpenItems.Single(item => item.SourceEventId == dueSourceEventId).RestrictionEvidence ==
               PartyReportRestrictionEvidence.Disputed,
            "Authoritative Party source did not expose the active dispute evidence.");

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            OpenItemRestrictionEvent duplicateDispute = OpenItemRestrictionEvent.Create(
                Guid.CreateVersion7(),
                tenantId,
                companyId,
                partyAccountId,
                dueScheduleLineId,
                OpenItemRestrictionKind.Dispute,
                OpenItemRestrictionAction.Applied,
                "duplicate-review",
                dispute.EffectiveDate,
                disputeRecordedAt.AddMinutes(1));
            PostgresException duplicate = await ThrowsAsync<PostgresException>(() =>
                PostgresOpenItemRestrictionWriter.PersistAsync(
                    connection,
                    transaction,
                    AuthorizedOpenItemRestrictionChange.Create(restrictionScope, duplicateDispute)).AsTask());
            Assert(duplicate.SqlState == PostgresErrorCodes.CheckViolation &&
                   duplicate.ConstraintName == "ck_open_item_restriction_single_active",
                "Database accepted a second active dispute for the same due line.");
            await transaction.RollbackAsync();
        }

        OpenItemRestrictionEvent block = OpenItemRestrictionEvent.Create(
            Guid.CreateVersion7(),
            tenantId,
            companyId,
            partyAccountId,
            dueScheduleLineId,
            OpenItemRestrictionKind.CollectionBlock,
            OpenItemRestrictionAction.Applied,
            "legal-hold",
            new DateOnly(2026, 8, 29),
            disputeRecordedAt.AddMinutes(2));
        OpenItemRestrictionEvent disputeRelease = OpenItemRestrictionEvent.Create(
            Guid.CreateVersion7(),
            tenantId,
            companyId,
            partyAccountId,
            dueScheduleLineId,
            OpenItemRestrictionKind.Dispute,
            OpenItemRestrictionAction.Released,
            "review-resolved",
            new DateOnly(2026, 8, 30),
            disputeRecordedAt.AddMinutes(3),
            dispute.EventId);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            Assert((await PostgresOpenItemRestrictionWriter.PersistAsync(
                    connection,
                    transaction,
                    AuthorizedOpenItemRestrictionChange.Create(restrictionScope, block))).Created,
                "Collection-block restriction was not persisted.");
            Assert((await PostgresOpenItemRestrictionWriter.PersistAsync(
                    connection,
                    transaction,
                    AuthorizedOpenItemRestrictionChange.Create(restrictionScope, disputeRelease))).Created,
                "Dispute release was not persisted.");
            DerivedOpenItemRestrictionSnapshot? beforeRelease =
                await PostgresOpenItemRestrictionSnapshotLoader.LoadAsync(
                    connection,
                    transaction,
                    restrictionScope,
                    companyId,
                    dueScheduleLineId,
                    query.EffectiveAsOf,
                    disputeRecordedAt.AddMinutes(2));
            DerivedOpenItemRestrictionSnapshot? afterRelease =
                await PostgresOpenItemRestrictionSnapshotLoader.LoadAsync(
                    connection,
                    transaction,
                    restrictionScope,
                    companyId,
                    dueScheduleLineId,
                    query.EffectiveAsOf,
                    disputeRecordedAt.AddMinutes(4));
            Assert(beforeRelease is not null && beforeRelease.IsDisputed && beforeRelease.IsCollectionBlocked &&
                   afterRelease is not null && !afterRelease.IsDisputed && afterRelease.IsCollectionBlocked,
                "Restriction loader did not honor the effective/recorded release cut.");
            await transaction.CommitAsync();
        }
        PartyReportSourceBatch? blocked = await source.LoadAsync(
            query with { RecordedCutoff = DateTimeOffset.UtcNow.AddMinutes(5) });
        Assert(blocked?.OpenItems.Single(item => item.SourceEventId == dueSourceEventId).RestrictionEvidence ==
               PartyReportRestrictionEvidence.Blocked,
            "Authoritative Party source did not preserve the active collection block after dispute release.");

        var hiddenSource = new PostgresPartyReportSource(
            appDataSource,
            new ExecutionScope(tenantId, actorId, [otherCompanyId]),
            evidenceLoader,
            lifecycleLoader);
        PartyReportSourceBatch? hidden = await hiddenSource.LoadAsync(
            query with { CompanyId = otherCompanyId, RecordedCutoff = DateTimeOffset.UtcNow.AddMinutes(1) });
        Assert(hidden is null, "Authoritative Party report source leaked a cross-company PartyAccount.");
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            var hiddenScope = new ExecutionScope(tenantId, actorId, [otherCompanyId]);
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, otherCompanyId);
            DerivedOpenItemRestrictionSnapshot? hiddenRestriction =
                await PostgresOpenItemRestrictionSnapshotLoader.LoadAsync(
                    connection,
                    transaction,
                    hiddenScope,
                    otherCompanyId,
                    dueScheduleLineId,
                    query.EffectiveAsOf,
                    query.RecordedCutoff);
            Assert(hiddenRestriction is null,
                "Open-item restriction evidence leaked through a cross-company due-line ID.");
            await transaction.CommitAsync();
        }
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (var privilege = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user, 'party.open_item_restriction_event', 'SELECT'), has_table_privilege(current_user, 'party.open_item_restriction_event', 'INSERT'), has_table_privilege(current_user, 'party.open_item_restriction_event', 'UPDATE'), has_table_privilege(current_user, 'party.open_item_restriction_event', 'DELETE')",
            connection))
        await using (NpgsqlDataReader reader = await privilege.ExecuteReaderAsync())
        {
            Assert(await reader.ReadAsync() && reader.GetBoolean(0) && reader.GetBoolean(1) &&
                   !reader.GetBoolean(2) && !reader.GetBoolean(3),
                "Runtime open-item restriction privileges are not append-only.");
        }

        DateTimeOffset impactRecordedAt = DateTimeOffset.UtcNow;
        OpenItemImpactEvent impact = OpenItemImpactEvent.Create(
            Guid.CreateVersion7(),
            tenantId,
            companyId,
            partyAccountId,
            dueScheduleLineId,
            Guid.CreateVersion7(),
            currency,
            "party.payment-allocation",
            1,
            "party.payment-allocation.post",
            OpenItemImpactKind.Allocation,
            10m,
            new DateOnly(2026, 8, 26),
            impactRecordedAt);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            OpenItemImpactPersistenceResult persisted = await PostgresOpenItemImpactWriter.PersistAsync(
                connection,
                transaction,
                reportScope,
                impact);
            Assert(persisted.Created, "Party report impact fixture was not created.");
            await transaction.CommitAsync();
        }

        PartyReportSourceBatch? beforeImpactPosting = await source.LoadAsync(
            query with { RecordedCutoff = impactRecordedAt.AddMinutes(1) });
        Assert(beforeImpactPosting is not null &&
               beforeImpactPosting.OpenItems.Single(item => item.SourceEventId == dueSourceEventId).RemainingAmount == 75m &&
               beforeImpactPosting.OpenItems.Single(item => item.SourceEventId == dueSourceEventId).Impacts.Count == 0,
            "An unposted open-item impact changed the authoritative report batch.");

        JournalPreparationRequest impactJournalRequest = CreateJournalPreparationRequest(
            tenantId,
            companyId,
            actorId,
            impact.Amount,
            hasPostingPermission: true,
            impact.EventId,
            impact.SourceType,
            impact.SourcePostingPurpose,
            impact.EffectiveDate,
            impact.RecordedAt,
            creditAccountId: controlAccountId,
            functionalCurrency: "TRY",
            functionalUnitsNumerator: 40m);
        Guid impactJournalId = Guid.CreateVersion7();
        await PostJournalFixtureAsync(
            migratorDataSource,
            appDataSource,
            impactJournalRequest,
            impactJournalId,
            actorId);
        PartyReportSourceBatch? afterImpactPosting = await source.LoadAsync(
            query with { RecordedCutoff = DateTimeOffset.UtcNow.AddMinutes(1) });
        Assert(afterImpactPosting is not null &&
               afterImpactPosting.OpenItems.Single(item => item.SourceEventId == dueSourceEventId).RemainingAmount == 65m &&
               afterImpactPosting.OpenItems.Single(item => item.SourceEventId == dueSourceEventId).Impacts.Count == 1 &&
               afterImpactPosting.OpenItems.Single(item => item.SourceEventId == dueSourceEventId).Impacts[0].EventId == impact.EventId &&
               afterImpactPosting.OpenItems.Single(item => item.SourceEventId == dueSourceEventId).Impacts[0].Kind == PartyReportImpactKind.Allocation,
            "Exact posted allocation evidence did not reduce Party open-item remaining amount.");

        DateTimeOffset unallocationRecordedAt = DateTimeOffset.UtcNow;
        OpenItemImpactEvent unallocation = OpenItemImpactEvent.Create(
            Guid.CreateVersion7(),
            tenantId,
            companyId,
            partyAccountId,
            dueScheduleLineId,
            impact.PaymentId,
            currency,
            "party.payment-unallocation",
            1,
            "party.payment-unallocation.post",
            OpenItemImpactKind.Unallocation,
            impact.Amount,
            new DateOnly(2026, 8, 27),
            unallocationRecordedAt,
            impact.EventId);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            Assert((await PostgresOpenItemImpactWriter.PersistAsync(
                    connection,
                    transaction,
                    reportScope,
                    unallocation)).Created,
                "Party report counter-event fixture was not created.");
            await transaction.CommitAsync();
        }
        PartyReportSourceBatch? beforeCounterPosting = await source.LoadAsync(
            query with { RecordedCutoff = unallocationRecordedAt.AddMinutes(1) });
        Assert(beforeCounterPosting is not null &&
               beforeCounterPosting.OpenItems.Single(item => item.SourceEventId == dueSourceEventId).RemainingAmount == 65m &&
               beforeCounterPosting.OpenItems.Single(item => item.SourceEventId == dueSourceEventId).Impacts.Count == 1,
            "An unposted counter impact changed the authoritative report batch.");

        JournalPreparationRequest unallocationJournalRequest = CreateJournalPreparationRequest(
            tenantId,
            companyId,
            actorId,
            unallocation.Amount,
            hasPostingPermission: true,
            unallocation.EventId,
            unallocation.SourceType,
            unallocation.SourcePostingPurpose,
            unallocation.EffectiveDate,
            unallocation.RecordedAt,
            debitAccountId: controlAccountId,
            functionalCurrency: "TRY",
            functionalUnitsNumerator: 40m);
        Guid unallocationJournalId = Guid.CreateVersion7();
        await PostJournalFixtureAsync(
            migratorDataSource,
            appDataSource,
            unallocationJournalRequest,
            unallocationJournalId,
            actorId);
        PartyReportSourceBatch? afterCounterPosting = await source.LoadAsync(
            query with { RecordedCutoff = DateTimeOffset.UtcNow.AddMinutes(1) });
        Assert(afterCounterPosting is not null &&
               afterCounterPosting.OpenItems.Single(item => item.SourceEventId == dueSourceEventId).RemainingAmount == 75m &&
               afterCounterPosting.OpenItems.Single(item => item.SourceEventId == dueSourceEventId).Impacts.Count == 2 &&
               afterCounterPosting.OpenItems.Single(item => item.SourceEventId == dueSourceEventId).Impacts[1].EventId == unallocation.EventId &&
               afterCounterPosting.OpenItems.Single(item => item.SourceEventId == dueSourceEventId).Impacts[1].Kind == PartyReportImpactKind.Unallocation &&
               afterCounterPosting.PostingLineage.Count == 4 &&
               afterCounterPosting.PostingLineage.Select(item => item.JournalId).ToHashSet()
                   .SetEquals([dueJournalId, openingJournalId, impactJournalId, unallocationJournalId]),
            "Exact posted unallocation lifecycle did not restore the Party open-item remaining amount.");

        PartyReportSourceBatch combinedSource = afterCounterPosting ??
            throw new InvalidOperationException("Combined Party report source was not returned.");
        Guid combinedGenerationId = Guid.CreateVersion7();
        DateTimeOffset combinedGeneratedAt = combinedSource.RecordedCutoff.AddMinutes(1);
        ValidatedPartyStatement combinedStatement = PartyReportProjectionBuilder.BuildStatement(
            combinedSource,
            "party.account.detail",
            Guid.CreateVersion7(),
            1,
            combinedGenerationId,
            combinedGeneratedAt);
        PartyControlAccountEvidence combinedControl =
            await postgresControlSource.LoadAsync(combinedSource, combinedStatement.ReportSlice)
            ?? throw new InvalidOperationException("Combined Party control-account evidence was not returned.");
        ControlAccountReconciliationResult combinedReconciliation = ControlAccountReconciliationResult.Create(
            Guid.CreateVersion7(), combinedControl.Subledger, combinedControl.GeneralLedger);
        Assert(combinedReconciliation.IsReconciled &&
               combinedControl.Subledger.Debits == 110m && combinedControl.Subledger.Credits == 10m &&
               combinedControl.Subledger.ClosingBalance == 100m &&
               combinedControl.GeneralLedger.Debits == 110m && combinedControl.GeneralLedger.Credits == 10m &&
               combinedControl.GeneralLedger.ClosingBalance == 100m,
            "Opening, due, allocation and unallocation did not cross-foot to the exact GL control account.");
        var deniedControlSource = new PostgresPartyControlAccountEvidenceSource(
            appDataSource,
            new ExecutionScope(tenantId, actorId, [otherCompanyId]),
            LoadPartyGeneralLedgerEvidenceAsync);
        await ThrowsAsync<ExecutionScopeDeniedException>(() =>
            deniedControlSource.LoadAsync(combinedSource, combinedStatement.ReportSlice).AsTask());
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            PostedControlAccountEvidenceException missingControlLine =
                await ThrowsAsync<PostedControlAccountEvidenceException>(() =>
                    PostgresPostedControlAccountBalanceEvidenceLoader.LoadAsync(
                        connection,
                        transaction,
                        reportScope,
                        companyId,
                        Guid.CreateVersion7(),
                        combinedSource.Currency,
                        combinedSource.EffectiveAsOf,
                        combinedSource.RecordedCutoff,
                        combinedSource.PostingLineage.Select(item =>
                            new PostedControlAccountLineageReference(
                                item.JournalId,
                                item.SourceType,
                                item.SourceEventId,
                                item.SourceVersion,
                                item.PostingPurpose,
                                item.EffectiveDate,
                                item.RecordedAt,
                                item.PostedAt))).AsTask());
            Assert(missingControlLine.Code == "POSTED_CONTROL_ACCOUNT_LINEAGE_INCOMPLETE",
                "Exact GL evidence accepted Party journals without the selected control-account line.");
            PostedControlAccountEvidenceException wrongCurrency =
                await ThrowsAsync<PostedControlAccountEvidenceException>(() =>
                    PostgresPostedControlAccountBalanceEvidenceLoader.LoadAsync(
                        connection,
                        transaction,
                        reportScope,
                        companyId,
                        controlAccountId,
                        "USD",
                        combinedSource.EffectiveAsOf,
                        combinedSource.RecordedCutoff,
                        combinedSource.PostingLineage.Select(item =>
                            new PostedControlAccountLineageReference(
                                item.JournalId,
                                item.SourceType,
                                item.SourceEventId,
                                item.SourceVersion,
                                item.PostingPurpose,
                                item.EffectiveDate,
                                item.RecordedAt,
                                item.PostedAt))).AsTask());
            Assert(wrongCurrency.Code == "POSTED_CONTROL_ACCOUNT_CURRENCY_EVIDENCE_MISMATCH",
                "GL evidence silently interpreted a different transaction currency as the Party currency.");
            await transaction.RollbackAsync();
        }

        var impactJournalReversal = KaguERP.Modules.Accounting.Domain.Reversals.JournalReversalDraft.Create(
            impactJournalId,
            impactJournalRequest.Draft,
            Guid.CreateVersion7(),
            "accounting.party-impact-reversal",
            "party-impact-correction",
            impact.EffectiveDate,
            DateTimeOffset.UtcNow);
        JournalPreparationRequest impactReversalRequest = CreatePreparationRequestForDraft(
            impactJournalRequest,
            impactJournalReversal.ReversalJournalDraft);
        Guid impactReversalJournalId = Guid.CreateVersion7();
        await PostJournalFixtureAsync(
            migratorDataSource,
            appDataSource,
            impactReversalRequest,
            impactReversalJournalId,
            actorId,
            seedCurrencyEvidence: false);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            await PostgresPostedJournalReversalLinkWriter.PersistAsync(
                connection,
                transaction,
                reportScope,
                companyId,
                impactJournalId,
                impactReversalJournalId,
                DateTimeOffset.UtcNow);
            await transaction.CommitAsync();
        }
        AuthoritativePartyReportSourceException doubleReversalConflict =
            await ThrowsAsync<AuthoritativePartyReportSourceException>(() =>
                source.LoadAsync(
                    query with { RecordedCutoff = DateTimeOffset.UtcNow.AddMinutes(1) }).AsTask());
        Assert(doubleReversalConflict.Code == "PARTY_REPORT_IMPACT_COUNTER_ACTIVE_ORIGINAL_CONFLICT",
            "An active counter impact was accepted after its original Accounting source was reversed.");
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
            PartyAccountBalanceSide.Receivable,
            dueScheduleId,
            "sales.invoice",
            1,
            new DateOnly(2026, 9, 1),
            "primary-receivable",
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

        Guid payablePartyAccountId = Guid.CreateVersion7();
        Guid payableSourceEventId = Guid.CreateVersion7();
        Guid payableControlAccountId = Guid.CreateVersion7();
        ValidatedDueSchedule payableSchedule = ValidatedDueSchedule.Create(
            tenantId,
            companyId,
            payablePartyAccountId,
            payableSourceEventId,
            currency,
            15m,
            [DueScheduleLine.Create(
                tenantId,
                companyId,
                payablePartyAccountId,
                payableSourceEventId,
                Guid.CreateVersion7(),
                currency,
                15m,
                new DateOnly(2026, 12, 31),
                Guid.CreateVersion7(),
                1,
                payableControlAccountId)]);
        var payableCommand = command with
        {
            BalanceSide = PartyAccountBalanceSide.Payable,
            DueScheduleId = Guid.CreateVersion7(),
            SourceType = "purchasing.invoice",
            SourceEffectiveDate = new DateOnly(2026, 12, 1),
            SourcePostingPurpose = "primary-payable",
            DefaultControlAccountId = payableControlAccountId,
            Schedule = payableSchedule,
        };
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            DueSchedulePersistenceResult payable = await PostgresDueScheduleWriter.PersistAsync(
                connection,
                transaction,
                payableCommand);
            Assert(payable.Created &&
                   await CountAsync(
                       connection,
                       transaction,
                       "SELECT count(*) FROM party.party_account WHERE party_id=$1 AND currency='GBP' AND balance_side IN (1,2)",
                       partyId) == 2,
                "The same Party and currency did not retain separate receivable and payable accounts.");

            Guid duplicateReceivableAccountId = Guid.CreateVersion7();
            Guid duplicateReceivableSourceId = Guid.CreateVersion7();
            ValidatedDueSchedule duplicateReceivableSchedule = ValidatedDueSchedule.Create(
                tenantId,
                companyId,
                duplicateReceivableAccountId,
                duplicateReceivableSourceId,
                currency,
                5m,
                [DueScheduleLine.Create(
                    tenantId,
                    companyId,
                    duplicateReceivableAccountId,
                    duplicateReceivableSourceId,
                    Guid.CreateVersion7(),
                    currency,
                    5m,
                    new DateOnly(2027, 1, 31),
                    Guid.CreateVersion7(),
                    1,
                    controlAccountId)]);
            DueSchedulePartyAccountConflictException duplicateRole =
                await ThrowsAsync<DueSchedulePartyAccountConflictException>(() =>
                    PostgresDueScheduleWriter.PersistAsync(
                        connection,
                        transaction,
                        command with
                        {
                            DueScheduleId = Guid.CreateVersion7(),
                            Schedule = duplicateReceivableSchedule,
                        }).AsTask());
            Assert(duplicateRole.PartyAccountId == duplicateReceivableAccountId,
                "A Party accepted a second receivable account for the same company and currency.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using var unclassified = new NpgsqlCommand(
                "INSERT INTO party.party_account (tenant_id,company_id,party_account_id,party_id,currency,control_account_id,created_at,created_by) VALUES ($1,$2,$3,$4,'USD',$5,$6,$7)",
                connection,
                transaction);
            unclassified.Parameters.AddWithValue(tenantId);
            unclassified.Parameters.AddWithValue(companyId);
            unclassified.Parameters.AddWithValue(Guid.CreateVersion7());
            unclassified.Parameters.AddWithValue(partyId);
            unclassified.Parameters.AddWithValue(Guid.CreateVersion7());
            unclassified.Parameters.AddWithValue(DateTimeOffset.UtcNow);
            unclassified.Parameters.AddWithValue(actorId);
            PostgresException unclassifiedRejected =
                await ThrowsAsync<PostgresException>(() => unclassified.ExecuteNonQueryAsync());
            Assert(unclassifiedRejected.SqlState == "23514" &&
                   unclassifiedRejected.ConstraintName == "ck_party_account_balance_side_required",
                "The database accepted a newly-created PartyAccount without an explicit balance side.");
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using var unclassifiedSource = new NpgsqlCommand(
                "INSERT INTO party.due_schedule (tenant_id,company_id,due_schedule_id,party_account_id,source_type,source_event_id,source_version,currency,source_original_amount,recorded_at,recorded_by,line_count) VALUES ($1,$2,$3,$4,'sales.invoice',$5,1,'GBP',1,$6,$7,1)",
                connection,
                transaction);
            unclassifiedSource.Parameters.AddWithValue(tenantId);
            unclassifiedSource.Parameters.AddWithValue(companyId);
            unclassifiedSource.Parameters.AddWithValue(Guid.CreateVersion7());
            unclassifiedSource.Parameters.AddWithValue(partyAccountId);
            unclassifiedSource.Parameters.AddWithValue(Guid.CreateVersion7());
            unclassifiedSource.Parameters.AddWithValue(DateTimeOffset.UtcNow);
            unclassifiedSource.Parameters.AddWithValue(actorId);
            PostgresException sourceIdentityRejected =
                await ThrowsAsync<PostgresException>(() => unclassifiedSource.ExecuteNonQueryAsync());
            Assert(sourceIdentityRejected.SqlState == PostgresErrorCodes.CheckViolation &&
                   sourceIdentityRejected.ConstraintName ==
                   "ck_due_schedule_source_posting_identity_required",
                "The database accepted a new due schedule without exact source posting identity.");
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await ExecuteAsync(
                connection,
                transaction,
                "ALTER TABLE party.due_schedule DROP CONSTRAINT ck_due_schedule_source_posting_identity_required");
            Guid legacyScheduleId = Guid.CreateVersion7();
            await using var legacyHeader = new NpgsqlCommand(
                "INSERT INTO party.due_schedule (tenant_id,company_id,due_schedule_id,party_account_id,source_type,source_event_id,source_version,currency,source_original_amount,recorded_at,recorded_by,line_count) VALUES ($1,$2,$3,$4,'legacy.invoice',$5,1,'GBP',1,$6,$7,1)",
                connection,
                transaction);
            legacyHeader.Parameters.AddWithValue(tenantId);
            legacyHeader.Parameters.AddWithValue(companyId);
            legacyHeader.Parameters.AddWithValue(legacyScheduleId);
            legacyHeader.Parameters.AddWithValue(partyAccountId);
            legacyHeader.Parameters.AddWithValue(Guid.CreateVersion7());
            legacyHeader.Parameters.AddWithValue(DateTimeOffset.UtcNow);
            legacyHeader.Parameters.AddWithValue(actorId);
            await legacyHeader.ExecuteNonQueryAsync();

            DueSchedulePostingIdentityUnavailableException unavailable =
                await ThrowsAsync<DueSchedulePostingIdentityUnavailableException>(() =>
                    PostgresDueScheduleLoader.LoadAsync(
                        connection,
                        transaction,
                        command.Scope,
                        companyId,
                        legacyScheduleId).AsTask());
            Assert(unavailable.Code == "DUE_SCHEDULE_POSTING_IDENTITY_UNAVAILABLE" &&
                   unavailable.DueScheduleId == legacyScheduleId,
                "Authoritative due-schedule loader guessed posting identity for a legacy row.");
            await transaction.RollbackAsync();
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
                   loaded.SourceEffectiveDate == new DateOnly(2026, 9, 1) &&
                   loaded.SourcePostingPurpose == "primary-receivable" &&
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

            DueSchedulePersistenceConflictException sourceIdentityConflict =
                await ThrowsAsync<DueSchedulePersistenceConflictException>(() =>
                    PostgresDueScheduleWriter.PersistAsync(
                        connection,
                        transaction,
                        command with
                        {
                            DueScheduleId = Guid.CreateVersion7(),
                            SourceEffectiveDate = new DateOnly(2026, 9, 2),
                        }).AsTask());
            Assert(sourceIdentityConflict.ExistingDueScheduleId == dueScheduleId,
                "Due schedule retry accepted a different effective date for the same source version.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            Guid invalidScheduleId = Guid.CreateVersion7();
            await using (var header = new NpgsqlCommand(
                "INSERT INTO party.due_schedule (tenant_id,company_id,due_schedule_id,party_account_id,source_type,source_event_id,source_version,source_effective_date,source_posting_purpose,currency,source_original_amount,recorded_at,recorded_by,line_count) VALUES ($1,$2,$3,$4,'sales.invoice',$5,1,DATE '2026-12-01','primary-receivable','GBP',100,$6,$7,1)",
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
            paymentId, currency, "party.payment-allocation", 1, "party.payment-allocation.post",
            OpenItemImpactKind.Allocation, 20m,
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
                paymentId, currency, allocation.SourceType, allocation.SourceVersion,
                allocation.SourcePostingPurpose, OpenItemImpactKind.Allocation, 21m,
                allocation.EffectiveDate, allocation.RecordedAt);
            OpenItemImpactPersistenceConflictException conflict =
                await ThrowsAsync<OpenItemImpactPersistenceConflictException>(() =>
                    PostgresOpenItemImpactWriter.PersistAsync(
                        connection, transaction, command.Scope, changed).AsTask());
            Assert(conflict.EventId == allocation.EventId,
                "Open-item impact identity accepted different immutable content.");
            OpenItemImpactEvent changedSourceIdentity = OpenItemImpactEvent.Create(
                allocation.EventId, tenantId, companyId, partyAccountId, lines[0].DueScheduleLineId,
                paymentId, currency, allocation.SourceType, 2, allocation.SourcePostingPurpose,
                OpenItemImpactKind.Allocation, allocation.Amount,
                allocation.EffectiveDate, allocation.RecordedAt);
            OpenItemImpactPersistenceConflictException sourceIdentityConflict =
                await ThrowsAsync<OpenItemImpactPersistenceConflictException>(() =>
                    PostgresOpenItemImpactWriter.PersistAsync(
                        connection, transaction, command.Scope, changedSourceIdentity).AsTask());
            Assert(sourceIdentityConflict.EventId == allocation.EventId,
                "Open-item impact retry accepted a different source version.");
            OpenItemImpactEvent unallocation = OpenItemImpactEvent.Create(
                Guid.CreateVersion7(), tenantId, companyId, partyAccountId, lines[0].DueScheduleLineId,
                paymentId, currency, "party.payment-unallocation", 1, "party.payment-unallocation.post",
                OpenItemImpactKind.Unallocation, 20m,
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
                   afterCounter.RemainingAmount == 40m && afterCounter.ConsideredEvents.Count == 2 &&
                   afterCounter.ConsideredEvents.All(item => item.SourceVersion == 1) &&
                   afterCounter.ConsideredEvents[0].SourcePostingPurpose ==
                   "party.payment-allocation.post",
                "Open-item loader did not derive remaining from the exact counter history.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using var unclassifiedSource = new NpgsqlCommand(
                "INSERT INTO party.open_item_impact_event (tenant_id,company_id,event_id,party_account_id,due_schedule_line_id,payment_id,currency,impact_kind,amount,effective_date,recorded_at,recorded_by) VALUES ($1,$2,$3,$4,$5,$6,'GBP',1,1,DATE '2026-10-01',$7,$8)",
                connection,
                transaction);
            unclassifiedSource.Parameters.AddWithValue(tenantId);
            unclassifiedSource.Parameters.AddWithValue(companyId);
            unclassifiedSource.Parameters.AddWithValue(Guid.CreateVersion7());
            unclassifiedSource.Parameters.AddWithValue(partyAccountId);
            unclassifiedSource.Parameters.AddWithValue(lines[1].DueScheduleLineId);
            unclassifiedSource.Parameters.AddWithValue(Guid.CreateVersion7());
            unclassifiedSource.Parameters.AddWithValue(impactRecordedAt.AddMinutes(2));
            unclassifiedSource.Parameters.AddWithValue(actorId);
            PostgresException rejected =
                await ThrowsAsync<PostgresException>(() => unclassifiedSource.ExecuteNonQueryAsync());
            Assert(rejected.SqlState == PostgresErrorCodes.CheckViolation &&
                   rejected.ConstraintName == "ck_open_item_impact_source_identity_required",
                "The database accepted a new open-item impact without exact source identity.");
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await ExecuteAsync(
                connection,
                transaction,
                "ALTER TABLE party.open_item_impact_event DROP CONSTRAINT ck_open_item_impact_source_identity_required");
            Guid legacyImpactId = Guid.CreateVersion7();
            await using var legacyImpact = new NpgsqlCommand(
                "INSERT INTO party.open_item_impact_event (tenant_id,company_id,event_id,party_account_id,due_schedule_line_id,payment_id,currency,impact_kind,amount,effective_date,recorded_at,recorded_by) VALUES ($1,$2,$3,$4,$5,$6,'GBP',1,1,DATE '2026-10-01',$7,$8)",
                connection,
                transaction);
            legacyImpact.Parameters.AddWithValue(tenantId);
            legacyImpact.Parameters.AddWithValue(companyId);
            legacyImpact.Parameters.AddWithValue(legacyImpactId);
            legacyImpact.Parameters.AddWithValue(partyAccountId);
            legacyImpact.Parameters.AddWithValue(lines[1].DueScheduleLineId);
            legacyImpact.Parameters.AddWithValue(Guid.CreateVersion7());
            legacyImpact.Parameters.AddWithValue(impactRecordedAt.AddMinutes(2));
            legacyImpact.Parameters.AddWithValue(actorId);
            await legacyImpact.ExecuteNonQueryAsync();
            OpenItemImpactPostingIdentityUnavailableException unavailable =
                await ThrowsAsync<OpenItemImpactPostingIdentityUnavailableException>(() =>
                    PostgresOpenItemSnapshotLoader.LoadAsync(
                        connection,
                        transaction,
                        command.Scope,
                        companyId,
                        lines[1].DueScheduleLineId,
                        new DateOnly(2026, 12, 31),
                        impactRecordedAt.AddDays(1)).AsTask());
            Assert(unavailable.EventId == legacyImpactId,
                "Authoritative open-item loader guessed source identity for a legacy impact.");
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using var tamper = new NpgsqlCommand(
                "INSERT INTO party.open_item_impact_event (tenant_id,company_id,event_id,source_type,source_version,source_posting_purpose,party_account_id,due_schedule_line_id,payment_id,currency,impact_kind,amount,effective_date,recorded_at,recorded_by,reverses_event_id) VALUES ($1,$2,$3,'party.payment-unallocation',1,'party.payment-unallocation.post',$4,$5,$6,'GBP',2,19,DATE '2026-09-17',$7,$8,$9)",
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
            Guid.CreateVersion7(), currency, "party.payment-allocation", 1,
            "party.payment-allocation.post", OpenItemImpactKind.Allocation, 40m,
            new DateOnly(2026, 10, 1), impactRecordedAt.AddHours(1));
        OpenItemImpactEvent secondCapacityContender = OpenItemImpactEvent.Create(
            Guid.CreateVersion7(), tenantId, companyId, partyAccountId, lines[1].DueScheduleLineId,
            Guid.CreateVersion7(), currency, "party.payment-allocation", 1,
            "party.payment-allocation.post", OpenItemImpactKind.Allocation, 30m,
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
                "INSERT INTO party.open_item_impact_event (tenant_id,company_id,event_id,source_type,source_version,source_posting_purpose,party_account_id,due_schedule_line_id,payment_id,currency,impact_kind,amount,effective_date,recorded_at,recorded_by) VALUES ($1,$2,$3,'party.write-off',1,'party.write-off.post',$4,$5,NULL,'GBP',3,41,DATE '2026-10-02',$6,$7)",
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
        PaymentRateSnapshot rate = PaymentRateSnapshot.Create(
            tenantId, companyId, Guid.CreateVersion7(), 1, currency, currency,
            "identity", "company-base", new DateOnly(2026, 8, 26), 1m, 1m,
            Guid.CreateVersion7(), 1, 2);
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
        NpgsqlDataSource migratorDataSource,
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
            ValidatedStatementLineDraft? loaded = await PostgresStatementLineLoader.LoadAsync(
                connection, transaction, scope, companyId, line.StatementLineId);
            Assert(loaded == line,
                "Authoritative statement-line loader did not reconstruct the immutable domain snapshot.");
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
            ValidatedStatementLineDraft? hidden = await PostgresStatementLineLoader.LoadAsync(
                connection,
                transaction,
                new ExecutionScope(tenantId, actorId, [otherCompanyId]),
                otherCompanyId,
                line.StatementLineId);
            Assert(hidden is null, "Authoritative statement-line loader leaked a cross-company line.");
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

        InternalMovementCapacitySnapshot movement = InternalMovementCapacitySnapshot.Create(
            tenantId, companyId, treasuryAccountId, Guid.CreateVersion7(), 1,
            PaymentDirection.Incoming, line.Currency, 200m);
        ReconciliationMatchDraft match = ReconciliationMatchDraft.Create(line, movement, 100m);
        ValidatedReconciliationProposal proposal = ValidatedReconciliationProposal.Create(
            Guid.CreateVersion7(), tenantId, companyId, treasuryAccountId, line.Currency, [match]);
        DateTimeOffset proposalRecordedAt = line.RecordedAt.AddHours(1);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            ReconciliationProposalPersistenceResult first =
                await PostgresReconciliationProposalWriter.PersistAsync(
                    connection, transaction, scope, proposal, proposalRecordedAt);
            ReconciliationProposalPersistenceResult replay =
                await PostgresReconciliationProposalWriter.PersistAsync(
                    connection, transaction, scope, proposal, proposalRecordedAt);
            Assert(first.Created && !replay.Created && replay.ReconciliationId == proposal.ReconciliationId,
                "Reconciliation proposal retry did not return the immutable first result.");
            LoadedReconciliationProposal? loaded = await PostgresReconciliationProposalLoader.LoadAsync(
                connection, transaction, scope, companyId, proposal.ReconciliationId);
            Assert(loaded is not null && loaded.RecordedAt == proposalRecordedAt &&
                   loaded.Proposal.ReconciliationId == proposal.ReconciliationId &&
                   loaded.Proposal.TreasuryAccountId == treasuryAccountId &&
                   loaded.Proposal.Currency == line.Currency && loaded.Proposal.Matches.Count == 1 &&
                   loaded.Proposal.Matches[0] == match,
                "Authoritative reconciliation loader did not reconstruct the immutable proposal snapshot.");
            await transaction.CommitAsync();
        }

        ReconciliationMatchDraft changedMatch = ReconciliationMatchDraft.Create(line, movement, 90m);
        ValidatedReconciliationProposal changedProposal = ValidatedReconciliationProposal.Create(
            proposal.ReconciliationId, tenantId, companyId, treasuryAccountId, line.Currency, [changedMatch]);
        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            ReconciliationProposalPersistenceConflictException conflict =
                await ThrowsAsync<ReconciliationProposalPersistenceConflictException>(() =>
                    PostgresReconciliationProposalWriter.PersistAsync(
                        connection, transaction, scope, changedProposal, proposalRecordedAt).AsTask());
            Assert(conflict.ReconciliationId == proposal.ReconciliationId,
                "Reconciliation proposal ID accepted different immutable match content.");
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, otherCompanyId);
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM treasury.reconciliation_proposal WHERE reconciliation_id=$1",
                    proposal.ReconciliationId) == 0,
                "Reconciliation proposal leaked across company scope.");
            LoadedReconciliationProposal? hidden = await PostgresReconciliationProposalLoader.LoadAsync(
                connection,
                transaction,
                new ExecutionScope(tenantId, actorId, [otherCompanyId]),
                otherCompanyId,
                proposal.ReconciliationId);
            Assert(hidden is null, "Authoritative reconciliation loader leaked a cross-company proposal.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection proposalPrivilegeConnection = await appDataSource.OpenConnectionAsync())
        await using (var proposalPrivilege = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user,'treasury.reconciliation_proposal','SELECT'),has_table_privilege(current_user,'treasury.reconciliation_proposal','INSERT'),has_table_privilege(current_user,'treasury.reconciliation_proposal','UPDATE'),has_table_privilege(current_user,'treasury.reconciliation_proposal_match','DELETE')",
            proposalPrivilegeConnection))
        await using (NpgsqlDataReader proposalPrivilegeReader = await proposalPrivilege.ExecuteReaderAsync())
        {
            Assert(await proposalPrivilegeReader.ReadAsync() && proposalPrivilegeReader.GetBoolean(0) &&
                   proposalPrivilegeReader.GetBoolean(1) && !proposalPrivilegeReader.GetBoolean(2) &&
                   !proposalPrivilegeReader.GetBoolean(3),
                "Runtime reconciliation proposal privileges are not append-only.");
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            Guid invalidProposalId = Guid.CreateVersion7();
            await using (var header = new NpgsqlCommand(
                "INSERT INTO treasury.reconciliation_proposal (tenant_id,company_id,reconciliation_id,treasury_account_id,currency,match_count,recorded_at,recorded_by) VALUES ($1,$2,$3,$4,'GBP',1,$5,$6)",
                connection, transaction))
            {
                header.Parameters.AddWithValue(tenantId);
                header.Parameters.AddWithValue(companyId);
                header.Parameters.AddWithValue(invalidProposalId);
                header.Parameters.AddWithValue(treasuryAccountId);
                header.Parameters.AddWithValue(proposalRecordedAt);
                header.Parameters.AddWithValue(actorId);
                await header.ExecuteNonQueryAsync();
            }
            await using (var invalidMatch = new NpgsqlCommand(
                "INSERT INTO treasury.reconciliation_proposal_match (tenant_id,company_id,reconciliation_id,statement_line_id,movement_id,movement_version,movement_direction,movement_usable_amount,matched_amount) VALUES ($1,$2,$3,$4,$5,1,1,200,126)",
                connection, transaction))
            {
                invalidMatch.Parameters.AddWithValue(tenantId);
                invalidMatch.Parameters.AddWithValue(companyId);
                invalidMatch.Parameters.AddWithValue(invalidProposalId);
                invalidMatch.Parameters.AddWithValue(line.StatementLineId);
                invalidMatch.Parameters.AddWithValue(Guid.CreateVersion7());
                await invalidMatch.ExecuteNonQueryAsync();
            }
            PostgresException exception = await ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert(exception.SqlState == "23514" &&
                   exception.ConstraintName == "ck_reconciliation_proposal_snapshot",
                "Database accepted a reconciliation proposal above statement capacity.");
        }
    }

    private static async Task AssertProjectionGenerationPersistenceAsync(
        NpgsqlDataSource migratorDataSource,
        NpgsqlDataSource appDataSource,
        Guid tenantId,
        Guid companyId,
        Guid otherCompanyId,
        Guid actorId)
    {
        var scope = new ExecutionScope(tenantId, actorId, [companyId]);
        FinancialReportSlice slice = FinancialReportSlice.Create(
            tenantId, companyId, PartyAccountDetailReportDefinition.ReportCode,
            PartyAccountDetailReportDefinition.Version, new DateOnly(2026, 8, 25),
            new DateTimeOffset(2026, 8, 25, 20, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 25, 20, 1, 0, TimeSpan.Zero), Guid.CreateVersion7(),
            ReportCurrencyCode.Create("GBP"),
            ReportDimensionSlice.Create([ReportDimensionAssignment.Create("branch", "NIC")]));
        var command = new ProjectionGenerationPersistenceCommand(
            scope, slice, "scheduled-refresh", "event:100", "event:200", new string('c', 64));
        PartyStatementEventSnapshot statementEvent = PartyStatementEventSnapshot.Create(
            Guid.CreateVersion7(), tenantId, companyId, Guid.CreateVersion7(), Guid.CreateVersion7(),
            slice.Currency, PartyStatementEventKind.OpenItem, "party.due-schedule", Guid.CreateVersion7(),
            Guid.CreateVersion7(), null, 25m, slice.EffectiveAsOf, 1, slice.DataCutoffAt.AddMinutes(-1));
        ValidatedPartyStatement statement = ValidatedPartyStatement.Create(
            Guid.CreateVersion7(), statementEvent.PartyAccountId, statementEvent.ControlAccountId,
            PartyBalanceSide.Receivable, 10m, slice, [statementEvent]);
        CalendarDayAgingPolicySnapshot agingPolicy = CalendarDayAgingPolicySnapshot.Create(
            tenantId,
            companyId,
            Guid.CreateVersion7(),
            1,
            [
                CalendarDayAgingBucket.Create("future", int.MinValue, -1),
                CalendarDayAgingBucket.Create("current", 0, 0),
                CalendarDayAgingBucket.Create("overdue", 1, int.MaxValue),
            ]);
        OpenItemAgingSnapshot agingItem = OpenItemAgingSnapshot.Create(
            Guid.CreateVersion7(), tenantId, companyId, statement.PartyAccountId, statement.ControlAccountId,
            Guid.CreateVersion7(), Guid.CreateVersion7(), slice.Currency, 100m, 35m,
            slice.EffectiveAsOf.AddDays(-10), slice.EffectiveAsOf, slice.DataCutoffAt, false, false);
        ValidatedPartyAgingReport agingReport = ValidatedPartyAgingReport.Create(
            Guid.CreateVersion7(), statement.PartyAccountId, statement.ControlAccountId,
            PartyBalanceSide.Receivable, slice, agingPolicy, [agingItem]);
        Guid reportControlAccountId = statement.ControlAccountId;
        ControlAccountBalanceSnapshot subledgerBalance = ControlAccountBalanceSnapshot.Create(
            Guid.CreateVersion7(), reportControlAccountId, LedgerSide.Subledger,
            10m, 30m, 5m, 35m, 2, new string('1', 64), slice);
        ControlAccountBalanceSnapshot generalLedgerBalance = ControlAccountBalanceSnapshot.Create(
            Guid.CreateVersion7(), reportControlAccountId, LedgerSide.GeneralLedger,
            10m, 30m, 5m, 35m, 2, new string('2', 64), slice);

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            ProjectionGenerationPersistenceResult first =
                await PostgresProjectionGenerationWriter.PersistAsync(connection, transaction, command);
            ProjectionGenerationPersistenceResult replay =
                await PostgresProjectionGenerationWriter.PersistAsync(connection, transaction, command);
            Assert(first.Created && !replay.Created && replay.ProjectionGenerationId == slice.ProjectionGenerationId,
                "Projection generation retry did not return the immutable first manifest.");
            LoadedProjectionGeneration? loaded = await PostgresProjectionGenerationLoader.LoadAsync(
                connection, transaction, scope, companyId, slice.ProjectionGenerationId);
            Assert(loaded is not null && loaded.Slice.TenantId == slice.TenantId &&
                   loaded.Slice.CompanyId == slice.CompanyId && loaded.Slice.ReportCode == slice.ReportCode &&
                   loaded.Slice.ReportDefinitionVersion == slice.ReportDefinitionVersion &&
                   loaded.Slice.EffectiveAsOf == slice.EffectiveAsOf &&
                   loaded.Slice.DataCutoffAt == slice.DataCutoffAt && loaded.Slice.GeneratedAt == slice.GeneratedAt &&
                   loaded.Slice.ProjectionGenerationId == slice.ProjectionGenerationId &&
                   loaded.Slice.Currency == slice.Currency && loaded.Slice.Dimensions.Assignments.Count == 1 &&
                   loaded.Slice.Dimensions.Assignments[0] == slice.Dimensions.Assignments[0] &&
                   loaded.GenerationReason == command.GenerationReason &&
                   loaded.SourceWatermarkFrom == command.SourceWatermarkFrom &&
                   loaded.SourceWatermarkTo == command.SourceWatermarkTo &&
                   loaded.SourceChecksumSha256 == command.SourceChecksumSha256 &&
                   loaded.GeneratedBy == actorId,
                "Authoritative projection-generation loader did not reconstruct the immutable manifest.");
            PartyStatementProjectionPersistenceResult statementFirst =
                await PostgresPartyStatementProjectionWriter.PersistAsync(connection, transaction, scope, statement);
            PartyStatementProjectionPersistenceResult statementReplay =
                await PostgresPartyStatementProjectionWriter.PersistAsync(connection, transaction, scope, statement);
            Assert(statementFirst.Created && !statementReplay.Created && statementReplay.StatementId == statement.StatementId,
                "Party statement projection retry did not preserve the immutable first snapshot.");
            AgingPolicyProjectionPersistenceResult policyFirst = await PostgresAgingPolicyProjectionWriter.PersistAsync(
                connection, transaction, scope, slice, agingPolicy);
            AgingPolicyProjectionPersistenceResult policyReplay = await PostgresAgingPolicyProjectionWriter.PersistAsync(
                connection, transaction, scope, slice, agingPolicy);
            Assert(policyFirst.Created && !policyReplay.Created,
                "Aging policy projection retry did not preserve the immutable first snapshot.");
            CalendarDayAgingPolicySnapshot? loadedPolicy = await PostgresAgingPolicyProjectionLoader.LoadAsync(
                connection, transaction, scope, companyId, slice.ProjectionGenerationId);
            Assert(loadedPolicy is not null && loadedPolicy.TenantId == agingPolicy.TenantId &&
                   loadedPolicy.CompanyId == agingPolicy.CompanyId && loadedPolicy.PolicyId == agingPolicy.PolicyId &&
                   loadedPolicy.Version == agingPolicy.Version && loadedPolicy.Buckets.Count == agingPolicy.Buckets.Count &&
                   loadedPolicy.Buckets.Zip(agingPolicy.Buckets).All(pair => pair.First == pair.Second),
                "Authoritative aging-policy loader did not reconstruct the immutable snapshot.");
            PartyAgingProjectionPersistenceResult agingFirst = await PostgresPartyAgingProjectionWriter.PersistAsync(
                connection, transaction, scope, agingReport);
            PartyAgingProjectionPersistenceResult agingReplay = await PostgresPartyAgingProjectionWriter.PersistAsync(
                connection, transaction, scope, agingReport);
            Assert(agingFirst.Created && !agingReplay.Created,
                "Party aging projection retry did not preserve the immutable first snapshot.");
            ValidatedPartyAgingReport? loadedAging = await PostgresPartyAgingProjectionLoader.LoadAsync(
                connection, transaction, scope, companyId, agingReport.AgingReportId);
            Assert(loadedAging is not null && loadedAging.AgingReportId == agingReport.AgingReportId &&
                   loadedAging.PartyAccountId == agingReport.PartyAccountId &&
                   loadedAging.ControlAccountId == agingReport.ControlAccountId &&
                   loadedAging.BalanceSide == agingReport.BalanceSide &&
                   loadedAging.TotalRemaining == agingReport.TotalRemaining && loadedAging.Items.Count == 1 &&
                   loadedAging.Items[0] == agingItem &&
                   loadedAging.Policy.PolicyId == agingPolicy.PolicyId &&
                   loadedAging.Policy.Version == agingPolicy.Version &&
                   loadedAging.BucketSummaries.Count == agingReport.BucketSummaries.Count &&
                   loadedAging.BucketSummaries.Zip(agingReport.BucketSummaries).All(pair => pair.First == pair.Second),
                "Authoritative party-aging loader did not reconstruct the immutable projection.");
            Guid crossFootId = Guid.CreateVersion7();
            PartyStatementAgingCrossFoot? crossFoot = await PostgresPartyReportCrossFootLoader.LoadAsync(
                connection, transaction, scope, companyId, crossFootId, statement.StatementId, agingReport.AgingReportId);
            Assert(crossFoot is not null && crossFoot.CrossFootId == crossFootId &&
                   crossFoot.Statement.ClosingExposure == crossFoot.Aging.TotalRemaining &&
                   crossFoot.Statement.ReportSlice.ProjectionGenerationId == slice.ProjectionGenerationId,
                "Authoritative Party report composition did not exact-cross-foot the same projection slice.");
            var permittedReportScope = new ExecutionScope(
                tenantId,
                actorId,
                [new CompanyAccess(companyId, [PartyAccountDetailReportDefinition.ViewPermission])]);
            PartyStatementAgingCrossFoot? authorizedCrossFoot = await AuthorizedPartyReportQuery.ExecuteAsync(
                connection,
                transaction,
                new AuthorizedPartyReportQueryRequest(
                    permittedReportScope, companyId, PartyAccountDetailReportDefinition.ReportCode,
                    PartyAccountDetailReportDefinition.Version, PartyAccountDetailReportDefinition.ViewPermission,
                    Guid.CreateVersion7(), statement.StatementId, agingReport.AgingReportId));
            Assert(authorizedCrossFoot is not null,
                "Permission-first Party report query did not load an allowed authoritative projection.");
            PartyReportQueryDeniedException denied = await ThrowsAsync<PartyReportQueryDeniedException>(() =>
                AuthorizedPartyReportQuery.ExecuteAsync(
                    connection,
                    transaction,
                    new AuthorizedPartyReportQueryRequest(
                        scope, companyId, PartyAccountDetailReportDefinition.ReportCode,
                        PartyAccountDetailReportDefinition.Version, PartyAccountDetailReportDefinition.ViewPermission,
                        Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7())).AsTask());
            Assert(denied.Code == "PARTY_REPORT_QUERY_DENIED",
                "Party report query did not deny missing permission before resource lookup.");
            var reportAuditContext = new RequestAuditContext(
                Guid.CreateVersion7(), "trace-party-report", tenantId, actorId, new HashSet<Guid> { companyId }, null);
            Guid allowedAuditId = Guid.CreateVersion7();
            PartyStatementAgingCrossFoot? auditedCrossFoot = await AuditedPartyReportQuery.ExecuteAsync(
                connection,
                transaction,
                    new AuditedPartyReportQueryRequest(
                        new AuthorizedPartyReportQueryRequest(
                            permittedReportScope, companyId, PartyAccountDetailReportDefinition.ReportCode,
                            PartyAccountDetailReportDefinition.Version,
                            PartyAccountDetailReportDefinition.ViewPermission, Guid.CreateVersion7(),
                            statement.StatementId, agingReport.AgingReportId),
                    reportAuditContext,
                    allowedAuditId),
                PostgresAuthorizationAuditWriter.AppendAsync);
            Assert(auditedCrossFoot is not null,
                "Allowed Party report query did not complete after appending its audit fact.");
            Guid deniedAuditId = Guid.CreateVersion7();
            await ThrowsAsync<PartyReportQueryDeniedException>(() => AuditedPartyReportQuery.ExecuteAsync(
                connection,
                transaction,
                    new AuditedPartyReportQueryRequest(
                        new AuthorizedPartyReportQueryRequest(
                            scope, companyId, PartyAccountDetailReportDefinition.ReportCode,
                            PartyAccountDetailReportDefinition.Version,
                            PartyAccountDetailReportDefinition.ViewPermission, Guid.CreateVersion7(),
                            Guid.CreateVersion7(), Guid.CreateVersion7()),
                    reportAuditContext,
                    deniedAuditId),
                PostgresAuthorizationAuditWriter.AppendAsync).AsTask());
            await ThrowsAsync<InvalidOperationException>(() => AuditedPartyReportQuery.ExecuteAsync(
                connection,
                transaction,
                    new AuditedPartyReportQueryRequest(
                        new AuthorizedPartyReportQueryRequest(
                            permittedReportScope, companyId, PartyAccountDetailReportDefinition.ReportCode,
                            PartyAccountDetailReportDefinition.Version,
                            PartyAccountDetailReportDefinition.ViewPermission, Guid.CreateVersion7(),
                            statement.StatementId, agingReport.AgingReportId),
                    reportAuditContext,
                    Guid.CreateVersion7()),
                static (_, _, _, _, _, _) => ValueTask.FromException(
                    new InvalidOperationException("Forced audit failure."))).AsTask());
            PartyStatementAgingCrossFoot? wrongDefinition = await AuditedPartyReportQuery.ExecuteAsync(
                connection,
                transaction,
                new AuditedPartyReportQueryRequest(
                    new AuthorizedPartyReportQueryRequest(
                        permittedReportScope, companyId, "party.account.other",
                        PartyAccountDetailReportDefinition.Version,
                        PartyAccountDetailReportDefinition.ViewPermission, Guid.CreateVersion7(),
                        statement.StatementId, agingReport.AgingReportId),
                    reportAuditContext,
                    Guid.CreateVersion7()),
                PostgresAuthorizationAuditWriter.AppendAsync);
            Assert(wrongDefinition is null,
                "Party report query returned a projection for a different route definition.");
            await ThrowsAsync<PartyReportQueryDeniedException>(() => AuditedPartyReportQuery.ExecuteAsync(
                connection,
                transaction,
                new AuditedPartyReportQueryRequest(
                    new AuthorizedPartyReportQueryRequest(
                        permittedReportScope, otherCompanyId, PartyAccountDetailReportDefinition.ReportCode,
                        PartyAccountDetailReportDefinition.Version,
                        PartyAccountDetailReportDefinition.ViewPermission, Guid.CreateVersion7(),
                        statement.StatementId, agingReport.AgingReportId),
                    reportAuditContext,
                    Guid.CreateVersion7()),
                PostgresAuthorizationAuditWriter.AppendAsync).AsTask());
            ControlAccountBalanceProjectionPersistenceResult subledgerFirst =
                await PostgresControlAccountBalanceProjectionWriter.PersistAsync(
                    connection, transaction, scope, subledgerBalance);
            ControlAccountBalanceProjectionPersistenceResult subledgerReplay =
                await PostgresControlAccountBalanceProjectionWriter.PersistAsync(
                    connection, transaction, scope, subledgerBalance);
            await PostgresControlAccountBalanceProjectionWriter.PersistAsync(
                connection, transaction, scope, generalLedgerBalance);
            Assert(subledgerFirst.Created && !subledgerReplay.Created,
                "Control-account balance projection retry did not preserve the first snapshot.");
            ControlAccountReconciliationResult? reconciliation = await PostgresControlAccountReconciliationLoader.LoadAsync(
                connection, transaction, scope, companyId, Guid.CreateVersion7(),
                subledgerBalance.SnapshotId, generalLedgerBalance.SnapshotId);
            Assert(reconciliation is not null && reconciliation.IsReconciled && reconciliation.Difference == 0m,
                "Authoritative control-account loader did not exact-reconcile subledger and GL snapshots.");
            PartyReportProjectionPublicationResult publicationReplay =
                await PostgresPartyReportProjectionPublisher.PublishAsync(
                    connection,
                    transaction,
                    new PartyReportProjectionPublicationCommand(
                        command, statement, agingReport, subledgerBalance, generalLedgerBalance,
                        Guid.CreateVersion7(), Guid.CreateVersion7()));
            Assert(!publicationReplay.Created &&
                   publicationReplay.ProjectionGenerationId == slice.ProjectionGenerationId,
                "Atomic Party report publication replay did not preserve the complete immutable set.");
            Guid unrelatedControlAccountId = Guid.CreateVersion7();
            ControlAccountBalanceSnapshot unrelatedSubledger = ControlAccountBalanceSnapshot.Create(
                Guid.CreateVersion7(), unrelatedControlAccountId, LedgerSide.Subledger,
                10m, 30m, 5m, 35m, 2, new string('7', 64), slice);
            ControlAccountBalanceSnapshot unrelatedGeneralLedger = ControlAccountBalanceSnapshot.Create(
                Guid.CreateVersion7(), unrelatedControlAccountId, LedgerSide.GeneralLedger,
                10m, 30m, 5m, 35m, 2, new string('8', 64), slice);
            PartyReportProjectionPublicationException unrelatedAccount =
                await ThrowsAsync<PartyReportProjectionPublicationException>(() =>
                    PostgresPartyReportProjectionPublisher.PublishAsync(
                        connection,
                        transaction,
                        new PartyReportProjectionPublicationCommand(
                            command, statement, agingReport, unrelatedSubledger, unrelatedGeneralLedger,
                            Guid.CreateVersion7(), Guid.CreateVersion7())).AsTask());
            Assert(unrelatedAccount.Component == "control account" &&
                   await CountAsync(
                       connection, transaction,
                       "SELECT count(*) FROM reporting.control_account_balance_projection WHERE snapshot_id=$1",
                       unrelatedSubledger.SnapshotId) == 0 &&
                   await CountAsync(
                       connection, transaction,
                       "SELECT count(*) FROM reporting.control_account_balance_projection WHERE snapshot_id=$1",
                       unrelatedGeneralLedger.SnapshotId) == 0,
                "Unrelated control-account evidence wrote projection facts before rejection.");
            Guid foreignGenerationId = Guid.CreateVersion7();
            FinancialReportSlice foreignSlice = FinancialReportSlice.Create(
                tenantId, companyId, slice.ReportCode, slice.ReportDefinitionVersion, slice.EffectiveAsOf,
                slice.DataCutoffAt, slice.GeneratedAt, foreignGenerationId, slice.Currency, slice.Dimensions);
            ControlAccountBalanceSnapshot foreignGeneralLedger = ControlAccountBalanceSnapshot.Create(
                Guid.CreateVersion7(), reportControlAccountId, LedgerSide.GeneralLedger,
                10m, 30m, 5m, 35m, 2, new string('4', 64), foreignSlice);
            PartyReportProjectionPublicationException publicationMismatch =
                await ThrowsAsync<PartyReportProjectionPublicationException>(() =>
                    PostgresPartyReportProjectionPublisher.PublishAsync(
                        connection,
                        transaction,
                        new PartyReportProjectionPublicationCommand(
                            command, statement, agingReport, subledgerBalance, foreignGeneralLedger,
                            Guid.CreateVersion7(), Guid.CreateVersion7())).AsTask());
            Assert(publicationMismatch.Component == "general ledger" && await CountAsync(
                    connection, transaction,
                    "SELECT count(*) FROM reporting.projection_generation WHERE projection_generation_id=$1",
                    foreignGenerationId) == 0,
                "Invalid publication set wrote facts before slice validation completed.");
            ValidatedPartyStatement? loadedStatement = await PostgresPartyStatementProjectionLoader.LoadAsync(
                connection, transaction, scope, companyId, statement.StatementId);
            Assert(loadedStatement is not null && loadedStatement.StatementId == statement.StatementId &&
                   loadedStatement.PartyAccountId == statement.PartyAccountId &&
                   loadedStatement.ControlAccountId == statement.ControlAccountId &&
                   loadedStatement.BalanceSide == statement.BalanceSide &&
                   loadedStatement.OpeningExposure == statement.OpeningExposure &&
                   loadedStatement.ClosingExposure == statement.ClosingExposure &&
                   loadedStatement.ReportSlice.ProjectionGenerationId == slice.ProjectionGenerationId &&
                   loadedStatement.Lines.Count == 1 &&
                   loadedStatement.Lines[0].EventSnapshot == statementEvent &&
                   loadedStatement.Lines[0].RunningExposure == statement.Lines[0].RunningExposure,
                "Authoritative party-statement loader did not reconstruct the immutable projection.");
            PartyStatementDrillDownAnchor? drillDown = await PostgresPartyStatementDrillDownLoader.LoadAsync(
                connection, transaction, scope, companyId, slice.ProjectionGenerationId,
                statement.StatementId, statementEvent.EventId);
            Assert(drillDown is not null && drillDown.StatementId == statement.StatementId &&
                   drillDown.ReportSlice.ProjectionGenerationId == slice.ProjectionGenerationId &&
                   drillDown.EventSnapshot.EventId == statementEvent.EventId &&
                   drillDown.EventSnapshot.SourceEventId == statementEvent.SourceEventId &&
                   drillDown.EventSnapshot.DueScheduleLineId == statementEvent.DueScheduleLineId &&
                   drillDown.RunningExposure == statement.Lines[0].RunningExposure,
                "Party statement drill-down anchor did not preserve the exact projection and source lineage.");
            PartyStatementDrillDownAnchor? wrongGeneration = await PostgresPartyStatementDrillDownLoader.LoadAsync(
                connection, transaction, scope, companyId, Guid.CreateVersion7(),
                statement.StatementId, statementEvent.EventId);
            Assert(wrongGeneration is null,
                "Party statement drill-down reused a row outside the requested projection generation.");
            PartyStatementDrillDownAnchor? hiddenDrillDown = await PostgresPartyStatementDrillDownLoader.LoadAsync(
                connection, transaction, new ExecutionScope(tenantId, actorId, [otherCompanyId]),
                otherCompanyId, slice.ProjectionGenerationId, statement.StatementId, statementEvent.EventId);
            Assert(hiddenDrillDown is null,
                "Party statement drill-down exposed another company's projection row.");
            await transaction.CommitAsync();
        }

        var productionReportScope = new ExecutionScope(
            tenantId,
            actorId,
            [new CompanyAccess(companyId, [PartyAccountDetailReportDefinition.ViewPermission])]);
        var productionAuditContext = new RequestAuditContext(
            Guid.CreateVersion7(),
            "trace-party-report-executor",
            tenantId,
            actorId,
            new HashSet<Guid> { companyId },
            null);
        var executor = new PostgresAuditedPartyReportQueryExecutor(
            appDataSource,
            productionReportScope,
            productionAuditContext,
            PostgresAuthorizationAuditWriter.AppendAsync);
        Guid executorAuditId = Guid.CreateVersion7();
        PartyStatementAgingCrossFoot? executorResult = await executor.ExecuteAsync(
            new PostgresPartyReportQuery(
                companyId,
                PartyAccountDetailReportDefinition.ReportCode,
                PartyAccountDetailReportDefinition.Version,
                PartyAccountDetailReportDefinition.ViewPermission,
                Guid.CreateVersion7(),
                statement.StatementId,
                agingReport.AgingReportId,
                executorAuditId));
        Assert(executorResult is not null && executorResult.Statement.StatementId == statement.StatementId &&
               executorResult.Aging.AgingReportId == agingReport.AgingReportId,
            "Transaction-owning Party report query did not return the authorized persisted projection.");
        Guid deniedExecutorAuditId = Guid.CreateVersion7();
        var deniedExecutor = new PostgresAuditedPartyReportQueryExecutor(
            appDataSource,
            scope,
            productionAuditContext,
            PostgresAuthorizationAuditWriter.AppendAsync);
        await ThrowsAsync<PartyReportQueryDeniedException>(() => deniedExecutor.ExecuteAsync(
            new PostgresPartyReportQuery(
                companyId,
                PartyAccountDetailReportDefinition.ReportCode,
                PartyAccountDetailReportDefinition.Version,
                PartyAccountDetailReportDefinition.ViewPermission,
                Guid.CreateVersion7(),
                statement.StatementId,
                agingReport.AgingReportId,
                deniedExecutorAuditId)).AsTask());
        Guid missingExecutorAuditId = Guid.CreateVersion7();
        PartyStatementAgingCrossFoot? missingExecutorResult = await executor.ExecuteAsync(
            new PostgresPartyReportQuery(
                companyId,
                PartyAccountDetailReportDefinition.ReportCode,
                PartyAccountDetailReportDefinition.Version,
                PartyAccountDetailReportDefinition.ViewPermission,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                missingExecutorAuditId));
        Assert(missingExecutorResult is null,
            "Transaction-owning Party report query did not keep a missing projection fail-closed.");

        var productionQuery = new PostgresPartyAccountDetailReportQuery(
            appDataSource,
            new FixedExecutionScopeAccessor(productionReportScope),
            new FixedRequestAuditContextAccessor(productionAuditContext),
            PostgresAuthorizationAuditWriter.AppendAsync);
        PartyAccountDetailReportQueryResult productionResult = await productionQuery.ExecuteAsync(
            new PartyAccountDetailReportQueryRequest(
                companyId,
                statement.StatementId,
                agingReport.AgingReportId));
        Assert(productionResult.Outcome == PartyAccountDetailReportQueryOutcome.Allowed &&
               productionResult.Report?.StatementId == statement.StatementId &&
               productionResult.Report.AgingReportId == agingReport.AgingReportId &&
               productionResult.Report.ReportCode == PartyAccountDetailReportDefinition.ReportCode &&
               productionResult.Report.ClosingExposure == productionResult.Report.AgingTotalRemaining,
            "Production Party report application query did not map the authorized persisted projection.");
        var deniedProductionQuery = new PostgresPartyAccountDetailReportQuery(
            appDataSource,
            new FixedExecutionScopeAccessor(scope),
            new FixedRequestAuditContextAccessor(productionAuditContext),
            PostgresAuthorizationAuditWriter.AppendAsync);
        PartyAccountDetailReportQueryResult deniedProductionResult = await deniedProductionQuery.ExecuteAsync(
            new PartyAccountDetailReportQueryRequest(
                companyId,
                statement.StatementId,
                agingReport.AgingReportId));
        Assert(deniedProductionResult.Outcome == PartyAccountDetailReportQueryOutcome.Denied &&
               deniedProductionResult.Report is null,
            "Production Party report application query did not return a typed permission denial.");
        PartyAccountDetailReportQueryResult missingProductionResult = await productionQuery.ExecuteAsync(
            new PartyAccountDetailReportQueryRequest(
                companyId,
                Guid.CreateVersion7(),
                Guid.CreateVersion7()));
        Assert(missingProductionResult.Outcome == PartyAccountDetailReportQueryOutcome.NotFound &&
               missingProductionResult.Report is null,
            "Production Party report application query did not keep a missing projection fail-closed.");

        await using (NpgsqlConnection auditConnection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction auditTransaction = await auditConnection.BeginTransactionAsync())
        {
            await ExecuteAsync(auditConnection, auditTransaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            Assert(await CountAsync(
                       auditConnection,
                       auditTransaction,
                       "SELECT count(*) FROM platform.audit_event WHERE id=$1 AND outcome='allowed' AND reason_code='PARTY_REPORT_QUERY_ALLOWED'",
                       executorAuditId) == 1 &&
                   await CountAsync(
                       auditConnection,
                       auditTransaction,
                       "SELECT count(*) FROM platform.audit_event WHERE id=$1 AND outcome='denied' AND reason_code='PARTY_REPORT_QUERY_DENIED'",
                       deniedExecutorAuditId) == 1 &&
                   await CountAsync(
                       auditConnection,
                       auditTransaction,
                       "SELECT count(*) FROM platform.audit_event WHERE id=$1 AND outcome='denied' AND reason_code='PARTY_REPORT_NOT_FOUND'",
                       missingExecutorAuditId) == 1,
                "Transaction-owning Party report query did not atomically retain allowed/denied/not-found audit facts.");
            await auditTransaction.CommitAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, companyId);
            var changed = command with { SourceChecksumSha256 = new string('d', 64) };
            ProjectionGenerationPersistenceConflictException conflict =
                await ThrowsAsync<ProjectionGenerationPersistenceConflictException>(() =>
                    PostgresProjectionGenerationWriter.PersistAsync(connection, transaction, changed).AsTask());
            Assert(conflict.ProjectionGenerationId == slice.ProjectionGenerationId,
                "Projection generation ID accepted different immutable lineage.");
            ValidatedPartyStatement changedStatement = ValidatedPartyStatement.Create(
                statement.StatementId, statement.PartyAccountId, statement.ControlAccountId,
                statement.BalanceSide, 11m, slice, [statementEvent]);
            PartyStatementProjectionPersistenceConflictException statementConflict =
                await ThrowsAsync<PartyStatementProjectionPersistenceConflictException>(() =>
                    PostgresPartyStatementProjectionWriter.PersistAsync(
                        connection, transaction, scope, changedStatement).AsTask());
            Assert(statementConflict.StatementId == statement.StatementId,
                "Party statement ID accepted different immutable projection content.");
            CalendarDayAgingPolicySnapshot changedPolicy = CalendarDayAgingPolicySnapshot.Create(
                tenantId, companyId, agingPolicy.PolicyId, 2, agingPolicy.Buckets);
            AgingPolicyProjectionPersistenceConflictException policyConflict =
                await ThrowsAsync<AgingPolicyProjectionPersistenceConflictException>(() =>
                    PostgresAgingPolicyProjectionWriter.PersistAsync(
                        connection, transaction, scope, slice, changedPolicy).AsTask());
            Assert(policyConflict.ProjectionGenerationId == slice.ProjectionGenerationId,
                "Projection generation accepted a different aging policy version.");
            OpenItemAgingSnapshot changedAgingItem = OpenItemAgingSnapshot.Create(
                agingItem.OpenItemId, tenantId, companyId, agingItem.PartyAccountId, agingItem.ControlAccountId,
                agingItem.SourceEventId, agingItem.DueScheduleLineId, slice.Currency, 100m, 30m,
                agingItem.DueDate, slice.EffectiveAsOf, slice.DataCutoffAt, false, false);
            ValidatedPartyAgingReport changedAgingReport = ValidatedPartyAgingReport.Create(
                agingReport.AgingReportId, agingReport.PartyAccountId, agingReport.ControlAccountId,
                agingReport.BalanceSide, slice, agingPolicy, [changedAgingItem]);
            PartyAgingProjectionPersistenceConflictException agingConflict =
                await ThrowsAsync<PartyAgingProjectionPersistenceConflictException>(() =>
                    PostgresPartyAgingProjectionWriter.PersistAsync(
                        connection, transaction, scope, changedAgingReport).AsTask());
            Assert(agingConflict.AgingReportId == agingReport.AgingReportId,
                "Aging report ID accepted different immutable item content.");
            ControlAccountBalanceSnapshot changedBalance = ControlAccountBalanceSnapshot.Create(
                subledgerBalance.SnapshotId, reportControlAccountId, LedgerSide.Subledger,
                10m, 31m, 5m, 36m, 2, subledgerBalance.SourceChecksumSha256, slice);
            ControlAccountBalanceProjectionPersistenceConflictException balanceConflict =
                await ThrowsAsync<ControlAccountBalanceProjectionPersistenceConflictException>(() =>
                    PostgresControlAccountBalanceProjectionWriter.PersistAsync(
                        connection, transaction, scope, changedBalance).AsTask());
            Assert(balanceConflict.SnapshotId == subledgerBalance.SnapshotId,
                "Control-account snapshot ID accepted different immutable balance content.");
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection connection = await appDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await SetAuditScopeAsync(connection, transaction, tenantId, actorId, otherCompanyId);
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM reporting.projection_generation WHERE projection_generation_id=$1",
                    slice.ProjectionGenerationId) == 0,
                "Projection generation manifest leaked across company scope.");
            LoadedProjectionGeneration? hidden = await PostgresProjectionGenerationLoader.LoadAsync(
                connection,
                transaction,
                new ExecutionScope(tenantId, actorId, [otherCompanyId]),
                otherCompanyId,
                slice.ProjectionGenerationId);
            Assert(hidden is null, "Authoritative projection-generation loader leaked a cross-company manifest.");
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM reporting.party_statement_projection WHERE statement_id=$1",
                    statement.StatementId) == 0,
                "Party statement projection leaked across company scope.");
            ValidatedPartyStatement? hiddenStatement = await PostgresPartyStatementProjectionLoader.LoadAsync(
                connection,
                transaction,
                new ExecutionScope(tenantId, actorId, [otherCompanyId]),
                otherCompanyId,
                statement.StatementId);
            Assert(hiddenStatement is null, "Authoritative party-statement loader leaked a cross-company projection.");
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM reporting.aging_policy_projection_snapshot WHERE projection_generation_id=$1",
                    slice.ProjectionGenerationId) == 0,
                "Aging policy projection leaked across company scope.");
            CalendarDayAgingPolicySnapshot? hiddenPolicy = await PostgresAgingPolicyProjectionLoader.LoadAsync(
                connection,
                transaction,
                new ExecutionScope(tenantId, actorId, [otherCompanyId]),
                otherCompanyId,
                slice.ProjectionGenerationId);
            Assert(hiddenPolicy is null, "Authoritative aging-policy loader leaked a cross-company snapshot.");
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM reporting.party_aging_projection WHERE aging_report_id=$1",
                    agingReport.AgingReportId) == 0,
                "Party aging projection leaked across company scope.");
            ValidatedPartyAgingReport? hiddenAging = await PostgresPartyAgingProjectionLoader.LoadAsync(
                connection,
                transaction,
                new ExecutionScope(tenantId, actorId, [otherCompanyId]),
                otherCompanyId,
                agingReport.AgingReportId);
            Assert(hiddenAging is null, "Authoritative party-aging loader leaked a cross-company projection.");
            PartyStatementAgingCrossFoot? hiddenCrossFoot = await PostgresPartyReportCrossFootLoader.LoadAsync(
                connection, transaction, new ExecutionScope(tenantId, actorId, [otherCompanyId]), otherCompanyId,
                Guid.CreateVersion7(), statement.StatementId, agingReport.AgingReportId);
            Assert(hiddenCrossFoot is null, "Authoritative Party report cross-foot leaked across company scope.");
            Assert(await CountAsync(connection, transaction,
                    "SELECT count(*) FROM reporting.control_account_balance_projection WHERE snapshot_id=$1",
                    subledgerBalance.SnapshotId) == 0,
                "Control-account balance projection leaked across company scope.");
            await transaction.CommitAsync();
        }

        await using (NpgsqlConnection privilegeConnection = await appDataSource.OpenConnectionAsync())
        await using (var privilege = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user,'reporting.projection_generation','SELECT'),has_table_privilege(current_user,'reporting.projection_generation','INSERT'),has_table_privilege(current_user,'reporting.projection_generation','UPDATE'),has_table_privilege(current_user,'reporting.projection_generation_dimension','DELETE')",
            privilegeConnection))
        await using (NpgsqlDataReader reader = await privilege.ExecuteReaderAsync())
        {
            Assert(await reader.ReadAsync() && reader.GetBoolean(0) && reader.GetBoolean(1) &&
                   !reader.GetBoolean(2) && !reader.GetBoolean(3),
                "Runtime projection-generation privileges are not append-only.");
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using var invalidHeader = new NpgsqlCommand(
                "INSERT INTO reporting.projection_generation (tenant_id,company_id,projection_generation_id,report_code,report_definition_version,effective_as_of,data_cutoff_at,generated_at,currency,generation_reason,source_watermark_from,source_watermark_to,source_checksum_sha256,dimension_count,generated_by) VALUES ($1,$2,$3,'party-aging',1,$4,$5,$6,'GBP','scheduled-refresh','event:100','event:200',$7,1,$8)",
                connection, transaction);
            invalidHeader.Parameters.AddWithValue(tenantId);
            invalidHeader.Parameters.AddWithValue(companyId);
            invalidHeader.Parameters.AddWithValue(Guid.CreateVersion7());
            invalidHeader.Parameters.AddWithValue(slice.EffectiveAsOf);
            invalidHeader.Parameters.AddWithValue(slice.DataCutoffAt);
            invalidHeader.Parameters.AddWithValue(slice.GeneratedAt);
            invalidHeader.Parameters.AddWithValue(new string('e', 64));
            invalidHeader.Parameters.AddWithValue(actorId);
            await invalidHeader.ExecuteNonQueryAsync();
            PostgresException exception = await ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert(exception.SqlState == "23514" &&
                   exception.ConstraintName == "ck_projection_generation_dimension_count",
                "Database accepted a projection manifest with a mismatched dimension count.");
        }

        await using (NpgsqlConnection statementPrivilegeConnection = await appDataSource.OpenConnectionAsync())
        await using (var privilege = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user,'reporting.party_statement_projection','SELECT'),has_table_privilege(current_user,'reporting.party_statement_projection','INSERT'),has_table_privilege(current_user,'reporting.party_statement_projection','UPDATE'),has_table_privilege(current_user,'reporting.party_statement_projection_line','DELETE')",
            statementPrivilegeConnection))
        await using (NpgsqlDataReader reader = await privilege.ExecuteReaderAsync())
        {
            Assert(await reader.ReadAsync() && reader.GetBoolean(0) && reader.GetBoolean(1) &&
                   !reader.GetBoolean(2) && !reader.GetBoolean(3),
                "Runtime party-statement projection privileges are not append-only.");
        }

        await using (NpgsqlConnection policyPrivilegeConnection = await appDataSource.OpenConnectionAsync())
        await using (var privilege = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user,'reporting.aging_policy_projection_snapshot','SELECT'),has_table_privilege(current_user,'reporting.aging_policy_projection_snapshot','INSERT'),has_table_privilege(current_user,'reporting.aging_policy_projection_snapshot','UPDATE'),has_table_privilege(current_user,'reporting.aging_policy_projection_bucket','DELETE')",
            policyPrivilegeConnection))
        await using (NpgsqlDataReader reader = await privilege.ExecuteReaderAsync())
        {
            Assert(await reader.ReadAsync() && reader.GetBoolean(0) && reader.GetBoolean(1) &&
                   !reader.GetBoolean(2) && !reader.GetBoolean(3),
                "Runtime aging-policy projection privileges are not append-only.");
        }

        await using (NpgsqlConnection agingPrivilegeConnection = await appDataSource.OpenConnectionAsync())
        await using (var privilege = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user,'reporting.party_aging_projection','SELECT'),has_table_privilege(current_user,'reporting.party_aging_projection','INSERT'),has_table_privilege(current_user,'reporting.party_aging_projection','UPDATE'),has_table_privilege(current_user,'reporting.party_aging_projection_item','DELETE')",
            agingPrivilegeConnection))
        await using (NpgsqlDataReader reader = await privilege.ExecuteReaderAsync())
        {
            Assert(await reader.ReadAsync() && reader.GetBoolean(0) && reader.GetBoolean(1) &&
                   !reader.GetBoolean(2) && !reader.GetBoolean(3),
                "Runtime party-aging projection privileges are not append-only.");
        }

        await using (NpgsqlConnection balancePrivilegeConnection = await appDataSource.OpenConnectionAsync())
        await using (var privilege = new NpgsqlCommand(
            "SELECT has_table_privilege(current_user,'reporting.control_account_balance_projection','SELECT'),has_table_privilege(current_user,'reporting.control_account_balance_projection','INSERT'),has_table_privilege(current_user,'reporting.control_account_balance_projection','UPDATE'),has_table_privilege(current_user,'reporting.control_account_balance_projection','DELETE')",
            balancePrivilegeConnection))
        await using (NpgsqlDataReader reader = await privilege.ExecuteReaderAsync())
        {
            Assert(await reader.ReadAsync() && reader.GetBoolean(0) && reader.GetBoolean(1) &&
                   !reader.GetBoolean(2) && !reader.GetBoolean(3),
                "Runtime control-account projection privileges are not append-only.");
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using var invalidBalance = new NpgsqlCommand(
                "INSERT INTO reporting.control_account_balance_projection (tenant_id,company_id,projection_generation_id,snapshot_id,control_account_id,ledger_side,opening_balance,debits,credits,closing_balance,row_count,source_checksum_sha256) VALUES ($1,$2,$3,$4,$5,1,10,30,5,34,2,$6)",
                connection, transaction);
            invalidBalance.Parameters.AddWithValue(tenantId); invalidBalance.Parameters.AddWithValue(companyId);
            invalidBalance.Parameters.AddWithValue(slice.ProjectionGenerationId);
            invalidBalance.Parameters.AddWithValue(Guid.CreateVersion7()); invalidBalance.Parameters.AddWithValue(Guid.CreateVersion7());
            invalidBalance.Parameters.AddWithValue(new string('3', 64));
            PostgresException exception = await ThrowsAsync<PostgresException>(() => invalidBalance.ExecuteNonQueryAsync());
            Assert(exception.SqlState == "23514" && exception.ConstraintName == "ck_control_account_balance_projection",
                "Database accepted a control-account snapshot whose arithmetic does not cross-foot.");
            await transaction.RollbackAsync();
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using var tamper = new NpgsqlCommand(
                "DELETE FROM reporting.party_aging_projection_item WHERE tenant_id=$1 AND company_id=$2 AND aging_report_id=$3",
                connection, transaction);
            tamper.Parameters.AddWithValue(tenantId); tamper.Parameters.AddWithValue(companyId);
            tamper.Parameters.AddWithValue(agingReport.AgingReportId);
            await tamper.ExecuteNonQueryAsync();
            PostgresException exception = await ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert(exception.SqlState == "23514" && exception.ConstraintName == "ck_party_aging_projection_item_count",
                "Database accepted an aging projection with a missing item.");
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using var tamper = new NpgsqlCommand(
                "DELETE FROM reporting.aging_policy_projection_bucket WHERE tenant_id=$1 AND company_id=$2 AND projection_generation_id=$3 AND bucket_ordinal=2",
                connection,
                transaction);
            tamper.Parameters.AddWithValue(tenantId);
            tamper.Parameters.AddWithValue(companyId);
            tamper.Parameters.AddWithValue(slice.ProjectionGenerationId);
            await tamper.ExecuteNonQueryAsync();
            PostgresException exception = await ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert(exception.SqlState == "23514" &&
                   exception.ConstraintName == "ck_aging_policy_projection_bucket_count",
                "Database accepted an aging policy snapshot with a missing bucket.");
        }

        await using (NpgsqlConnection connection = await migratorDataSource.OpenConnectionAsync())
        await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, transaction, "SET LOCAL ROLE kagu_erp_schema_owner");
            await using var invalidStatement = new NpgsqlCommand(
                "INSERT INTO reporting.party_statement_projection (tenant_id,company_id,projection_generation_id,statement_id,party_account_id,control_account_id,balance_side,opening_exposure,closing_exposure,line_count) VALUES ($1,$2,$3,$4,$5,$6,1,10,35,1)",
                connection,
                transaction);
            invalidStatement.Parameters.AddWithValue(tenantId);
            invalidStatement.Parameters.AddWithValue(companyId);
            invalidStatement.Parameters.AddWithValue(slice.ProjectionGenerationId);
            invalidStatement.Parameters.AddWithValue(Guid.CreateVersion7());
            invalidStatement.Parameters.AddWithValue(Guid.CreateVersion7());
            invalidStatement.Parameters.AddWithValue(statement.ControlAccountId);
            await invalidStatement.ExecuteNonQueryAsync();
            PostgresException exception = await ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
            Assert(exception.SqlState == "23514" &&
                   exception.ConstraintName == "ck_party_statement_projection_line_count",
                "Database accepted a party statement projection with a mismatched line count.");
        }
    }

    private static async Task AssertPersistedPartyGoldenAsync(
        NpgsqlDataSource appDataSource,
        ExecutionScope scope,
        Guid companyId,
        Guid crossFootId,
        Guid statementId,
        Guid agingReportId,
        Guid reconciliationId,
        Guid projectionGenerationId,
        decimal expectedClosing)
    {
        await using NpgsqlConnection connection = await appDataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await SetAuditScopeAsync(connection, transaction, scope.TenantId, scope.ActorId, companyId);
        PartyStatementAgingCrossFoot? crossFoot = await PostgresPartyReportCrossFootLoader.LoadAsync(
            connection,
            transaction,
            scope,
            companyId,
            crossFootId,
            statementId,
            agingReportId);
        Assert(crossFoot is not null && crossFoot.Statement.ClosingExposure == expectedClosing &&
               crossFoot.Aging.TotalRemaining == expectedClosing,
            "Persisted Party statement and aging did not retain the golden cross-foot total.");

        var snapshotIds = new Dictionary<LedgerSide, Guid>();
        const string sql = """
            SELECT ledger_side, snapshot_id
            FROM reporting.control_account_balance_projection
            WHERE tenant_id=$1 AND company_id=$2 AND projection_generation_id=$3
            ORDER BY ledger_side
            """;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue(scope.TenantId);
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(projectionGenerationId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                snapshotIds.Add((LedgerSide)reader.GetInt16(0), reader.GetGuid(1));
            }
        }
        Assert(snapshotIds.Count == 2 && snapshotIds.ContainsKey(LedgerSide.Subledger) &&
               snapshotIds.ContainsKey(LedgerSide.GeneralLedger),
            "Golden generation did not persist both control-account ledger sides.");
        ControlAccountReconciliationResult? reconciliation =
            await PostgresControlAccountReconciliationLoader.LoadAsync(
                connection,
                transaction,
                scope,
                companyId,
                reconciliationId,
                snapshotIds[LedgerSide.Subledger],
                snapshotIds[LedgerSide.GeneralLedger]);
        Assert(reconciliation is not null && reconciliation.IsReconciled &&
               reconciliation.Subledger.ClosingBalance == expectedClosing &&
               reconciliation.GeneralLedger.ClosingBalance == expectedClosing,
            "Persisted Party subledger and exact GL control-account evidence did not reconcile.");
        await transaction.CommitAsync();
    }

    private static DateTimeOffset ToPostgresTimestamp(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("PostgreSQL test timestamps must use the UTC offset.", nameof(value));
        }
        return value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMicrosecond));
    }

    private static async ValueTask<PartyGeneralLedgerControlAccountEvidence>
        LoadPartyGeneralLedgerEvidenceAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            ExecutionScope scope,
            PartyReportSourceBatch source,
            CancellationToken cancellationToken)
    {
        PostedControlAccountBalanceEvidence evidence =
            await PostgresPostedControlAccountBalanceEvidenceLoader.LoadAsync(
                connection,
                transaction,
                scope,
                source.CompanyId,
                source.ControlAccountId,
                source.Currency,
                source.EffectiveAsOf,
                source.RecordedCutoff,
                source.PostingLineage.Select(item => new PostedControlAccountLineageReference(
                    item.JournalId,
                    item.SourceType,
                    item.SourceEventId,
                    item.SourceVersion,
                    item.PostingPurpose,
                    item.EffectiveDate,
                    item.RecordedAt,
                    item.PostedAt)),
                cancellationToken);
        return new PartyGeneralLedgerControlAccountEvidence(
            evidence.TenantId,
            evidence.CompanyId,
            evidence.ControlAccountId,
            evidence.Currency,
            evidence.EffectiveAsOf,
            evidence.RecordedCutoff,
            evidence.OpeningBalance,
            evidence.Debits,
            evidence.Credits,
            evidence.ClosingBalance,
            evidence.RowCount,
            evidence.SourceChecksumSha256);
    }

    private sealed class FixedPartyAgingPolicySource(CalendarDayAgingPolicySnapshot policy)
        : IPartyAgingPolicySource
    {
        public ValueTask<CalendarDayAgingPolicySnapshot?> LoadAsync(
            Guid tenantId,
            Guid companyId,
            DateOnly effectiveAsOf,
            DateTimeOffset recordedCutoff,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<CalendarDayAgingPolicySnapshot?>(policy);
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
        await using (var salesTransitionCommand = new NpgsqlCommand(
            "DELETE FROM sales.sales_order_transition_event WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            salesTransitionCommand.Parameters.AddWithValue(tenantA);
            salesTransitionCommand.Parameters.AddWithValue(tenantB);
            await salesTransitionCommand.ExecuteNonQueryAsync();
        }
        await using (var salesOrderCommand = new NpgsqlCommand(
            "DELETE FROM sales.sales_order WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            salesOrderCommand.Parameters.AddWithValue(tenantA);
            salesOrderCommand.Parameters.AddWithValue(tenantB);
            await salesOrderCommand.ExecuteNonQueryAsync();
        }
        await using (var stockMovementCommand = new NpgsqlCommand(
            "DELETE FROM inventory.stock_movement WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            stockMovementCommand.Parameters.AddWithValue(tenantA);
            stockMovementCommand.Parameters.AddWithValue(tenantB);
            await stockMovementCommand.ExecuteNonQueryAsync();
        }
        await using (var itemCompanyCommand = new NpgsqlCommand(
            "DELETE FROM inventory.item_company WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            itemCompanyCommand.Parameters.AddWithValue(tenantA);
            itemCompanyCommand.Parameters.AddWithValue(tenantB);
            await itemCompanyCommand.ExecuteNonQueryAsync();
        }
        await using (var itemCommand = new NpgsqlCommand(
            "DELETE FROM inventory.item WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            itemCommand.Parameters.AddWithValue(tenantA);
            itemCommand.Parameters.AddWithValue(tenantB);
            await itemCommand.ExecuteNonQueryAsync();
        }
        await using (var warehouseScopeCommand = new NpgsqlCommand(
            "DELETE FROM iam.user_warehouse_scope WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            warehouseScopeCommand.Parameters.AddWithValue(tenantA);
            warehouseScopeCommand.Parameters.AddWithValue(tenantB);
            await warehouseScopeCommand.ExecuteNonQueryAsync();
        }
        await using (var warehouseCommand = new NpgsqlCommand(
            "DELETE FROM org.warehouse WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            warehouseCommand.Parameters.AddWithValue(tenantA);
            warehouseCommand.Parameters.AddWithValue(tenantB);
            await warehouseCommand.ExecuteNonQueryAsync();
        }

        foreach (string table in new[]
                 {
                     "reporting.aging_policy_definition_bucket",
                     "reporting.aging_policy_definition",
                     "reporting.control_account_balance_projection",
                     "reporting.party_aging_projection_item",
                     "reporting.party_aging_projection",
                     "reporting.aging_policy_projection_bucket",
                     "reporting.aging_policy_projection_snapshot",
                     "reporting.party_statement_projection_line",
                     "reporting.party_statement_projection",
                     "reporting.projection_generation_dimension",
                     "reporting.projection_generation",
                     "treasury.reconciliation_proposal_match",
                     "treasury.reconciliation_proposal",
                     "treasury.statement_line",
                     "treasury.payment_economic_event",
                     "party.open_item_restriction_event",
                     "party.open_item_impact_event",
                     "party.party_account_opening_event",
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

        await using (var reportRefreshEventCommand = new NpgsqlCommand(
            "DELETE FROM reporting.party_report_refresh_event WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            reportRefreshEventCommand.Parameters.AddWithValue(tenantA);
            reportRefreshEventCommand.Parameters.AddWithValue(tenantB);
            await reportRefreshEventCommand.ExecuteNonQueryAsync();
        }

        await using (var reportRefreshWorkCommand = new NpgsqlCommand(
            "DELETE FROM reporting.party_report_refresh_work_item WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            reportRefreshWorkCommand.Parameters.AddWithValue(tenantA);
            reportRefreshWorkCommand.Parameters.AddWithValue(tenantB);
            await reportRefreshWorkCommand.ExecuteNonQueryAsync();
        }

        await using (var servicePermissionCommand = new NpgsqlCommand(
            "DELETE FROM iam.service_identity_company_permission WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            servicePermissionCommand.Parameters.AddWithValue(tenantA);
            servicePermissionCommand.Parameters.AddWithValue(tenantB);
            await servicePermissionCommand.ExecuteNonQueryAsync();
        }

        await using (var serviceIdentityCommand = new NpgsqlCommand(
            "DELETE FROM iam.service_identity WHERE tenant_id = $1 OR tenant_id = $2",
            connection,
            transaction))
        {
            serviceIdentityCommand.Parameters.AddWithValue(tenantA);
            serviceIdentityCommand.Parameters.AddWithValue(tenantB);
            await serviceIdentityCommand.ExecuteNonQueryAsync();
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

    private sealed class FixedExecutionScopeAccessor(ExecutionScope current) : IExecutionScopeAccessor
    {
        public ExecutionScope Current { get; } = current;
    }

    private sealed class FixedRequestAuditContextAccessor(RequestAuditContext current)
        : IRequestAuditContextAccessor
    {
        public RequestAuditContext Current { get; } = current;
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
