using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Application.PartyReports;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public sealed class PostgresPartyReportRefreshWorkStore(
    NpgsqlDataSource dataSource,
    ExecutionScope scope) : IPartyReportRefreshWorkStore
{
    public async ValueTask<PartyReportRefreshEnqueueResult> EnqueueAsync(
        PartyReportRefreshEnqueueCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Request);
        RequireId(command.WorkItemId, nameof(command));
        RequireId(command.CreatedBy, nameof(command));
        if (command.CreatedBy != scope.ActorId)
        {
            throw new PartyReportRefreshQueueException(
                "PARTY_REPORT_REFRESH_ACTOR_MISMATCH",
                "The enqueue actor must match the trusted execution scope.");
        }
        if (command.MaxAttempts is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "Max attempts must be between 1 and 20.");
        }
        RequireUtc(command.AvailableAt, nameof(command));
        RequireUtc(command.CreatedAt, nameof(command));
        string requestKey = RequireText(command.RequestKey, 160, nameof(command));
        PartyReportRefreshRequest request = command.Request;
        scope.EnsureAllowed(request.TenantId, request.CompanyId);
        EnsureRefreshPermission(request.CompanyId);
        string fingerprint = request.ComputeFingerprintSha256();

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetExecutionContextAsync(connection, transaction, cancellationToken);

        const string insertSql = """
            INSERT INTO reporting.party_report_refresh_work_item
                (tenant_id, company_id, work_item_id, request_key, request_fingerprint_sha256,
                 party_account_id, report_code, report_definition_version, effective_as_of,
                 recorded_cutoff, projection_generation_id, statement_id, aging_report_id,
                 party_cross_foot_id, control_account_reconciliation_id, generated_at,
                 generation_reason, scheduled_for, timezone_name, business_calendar_code,
                 missed_run_policy, max_attempts, available_at, created_at, created_by)
            VALUES
                ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,$23,$24,$25)
            ON CONFLICT (tenant_id, company_id, request_key) DO NOTHING
            RETURNING work_item_id
            """;
        Guid? insertedId;
        await using (var insert = new NpgsqlCommand(insertSql, connection, transaction))
        {
            AddRequestParameters(insert, command, requestKey, fingerprint);
            object? value = await insert.ExecuteScalarAsync(cancellationToken);
            insertedId = value is Guid id ? id : null;
        }

        if (insertedId is Guid createdId)
        {
            await InsertEventAsync(
                connection,
                transaction,
                request.TenantId,
                request.CompanyId,
                createdId,
                "enqueued",
                0,
                command.CreatedAt,
                null,
                null,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PartyReportRefreshEnqueueResult(createdId, true);
        }

        const string existingSql = """
            SELECT work_item_id, request_fingerprint_sha256
            FROM reporting.party_report_refresh_work_item
            WHERE tenant_id=$1 AND company_id=$2 AND request_key=$3
            """;
        Guid existingId;
        string existingFingerprint;
        await using (var existing = new NpgsqlCommand(existingSql, connection, transaction))
        {
            existing.Parameters.AddWithValue(request.TenantId);
            existing.Parameters.AddWithValue(request.CompanyId);
            existing.Parameters.AddWithValue(requestKey);
            await using NpgsqlDataReader reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new PartyReportRefreshQueueException(
                    "PARTY_REPORT_REFRESH_REPLAY_NOT_VISIBLE",
                    "The existing refresh request is not visible after its uniqueness conflict.");
            }
            existingId = reader.GetGuid(0);
            existingFingerprint = reader.GetString(1);
        }

        if (!string.Equals(existingFingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new PartyReportRefreshQueueException(
                "PARTY_REPORT_REFRESH_REQUEST_KEY_REUSED",
                "The refresh request key is already bound to a different canonical payload.");
        }

        await transaction.CommitAsync(cancellationToken);
        return new PartyReportRefreshEnqueueResult(existingId, false);
    }

    public async ValueTask<PartyReportRefreshLease?> TryClaimAsync(
        DateTimeOffset claimedAt,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        RequireUtc(claimedAt, nameof(claimedAt));
        if (leaseDuration < TimeSpan.FromSeconds(5) || leaseDuration > TimeSpan.FromMinutes(15))
        {
            throw new ArgumentOutOfRangeException(
                nameof(leaseDuration),
                "Lease duration must be between five seconds and fifteen minutes.");
        }
        Guid[] companyIds = scope.CompanyIds
            .Where(companyId => scope.HasPermission(companyId, PartyReportRefreshPermissions.Refresh))
            .Order()
            .ToArray();
        if (companyIds.Length == 0)
        {
            throw new ExecutionScopeDeniedException();
        }

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetExecutionContextAsync(connection, transaction, cancellationToken);
        await FinalizeExpiredLastAttemptAsync(
            connection,
            transaction,
            companyIds,
            claimedAt,
            cancellationToken);

        Guid leaseToken = Guid.CreateVersion7();
        const string claimSql = """
            WITH candidate AS
            (
                SELECT tenant_id, company_id, work_item_id
                FROM reporting.party_report_refresh_work_item
                WHERE tenant_id=$1
                  AND company_id=ANY($2)
                  AND scheduled_for <= $3
                  AND attempt_count < max_attempts
                  AND
                  (
                      (status='pending' AND available_at <= $3)
                      OR (status='processing' AND lease_expires_at <= $3)
                  )
                ORDER BY scheduled_for, available_at, work_item_id
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            UPDATE reporting.party_report_refresh_work_item item
            SET status='processing',
                attempt_count=item.attempt_count + 1,
                lease_token=$4,
                lease_expires_at=$3 + $5,
                completed_at=NULL,
                last_error_code=NULL,
                last_error_at=NULL
            FROM candidate
            WHERE item.tenant_id=candidate.tenant_id
              AND item.company_id=candidate.company_id
              AND item.work_item_id=candidate.work_item_id
            RETURNING item.tenant_id, item.company_id, item.work_item_id,
                      item.party_account_id, item.report_code, item.report_definition_version,
                      item.effective_as_of, item.recorded_cutoff, item.projection_generation_id,
                      item.statement_id, item.aging_report_id, item.party_cross_foot_id,
                      item.control_account_reconciliation_id, item.generated_at,
                      item.generation_reason, item.scheduled_for, item.timezone_name,
                      item.business_calendar_code, item.missed_run_policy,
                      item.attempt_count, item.max_attempts
            """;
        PartyReportRefreshLease? lease = null;
        Guid claimedTenant = Guid.Empty;
        Guid claimedCompany = Guid.Empty;
        await using (var claim = new NpgsqlCommand(claimSql, connection, transaction))
        {
            claim.Parameters.AddWithValue(scope.TenantId);
            claim.Parameters.AddWithValue(companyIds);
            claim.Parameters.AddWithValue(claimedAt);
            claim.Parameters.AddWithValue(leaseToken);
            claim.Parameters.AddWithValue(leaseDuration);
            await using NpgsqlDataReader reader = await claim.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                claimedTenant = reader.GetGuid(0);
                claimedCompany = reader.GetGuid(1);
                var request = PartyReportRefreshRequest.Create(
                    claimedTenant,
                    claimedCompany,
                    reader.GetGuid(3),
                    reader.GetString(4),
                    reader.GetInt64(5),
                    reader.GetFieldValue<DateOnly>(6),
                    reader.GetFieldValue<DateTimeOffset>(7),
                    reader.GetGuid(8),
                    reader.GetGuid(9),
                    reader.GetGuid(10),
                    reader.GetGuid(11),
                    reader.GetGuid(12),
                    reader.GetFieldValue<DateTimeOffset>(13),
                    reader.GetString(14),
                    reader.GetFieldValue<DateTimeOffset>(15),
                    reader.GetString(16),
                    reader.GetString(17),
                    reader.GetString(18));
                lease = new PartyReportRefreshLease(
                    reader.GetGuid(2),
                    request,
                    leaseToken,
                    reader.GetInt32(19),
                    reader.GetInt32(20));
            }
        }

        if (lease is not null)
        {
            await InsertEventAsync(
                connection,
                transaction,
                claimedTenant,
                claimedCompany,
                lease.WorkItemId,
                "claimed",
                lease.AttemptNumber,
                claimedAt,
                lease.LeaseToken,
                null,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return lease;
    }

    public async ValueTask CompleteAsync(
        PartyReportRefreshLease lease,
        PartyReportProjectionJobResult result,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(result);
        RequireUtc(completedAt, nameof(completedAt));
        EnsureLeaseScopeAndPermission(lease);
        if (result.ProjectionGenerationId != lease.Request.ProjectionGenerationId ||
            result.StatementId != lease.Request.StatementId ||
            result.AgingReportId != lease.Request.AgingReportId)
        {
            throw new PartyReportRefreshQueueException(
                "PARTY_REPORT_REFRESH_RESULT_MISMATCH",
                "The projection result does not match the leased refresh request.");
        }

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetExecutionContextAsync(connection, transaction, cancellationToken);
        const string sql = """
            UPDATE reporting.party_report_refresh_work_item
            SET status='completed', lease_token=NULL, lease_expires_at=NULL,
                completed_at=$5, last_error_code=NULL, last_error_at=NULL
            WHERE tenant_id=$1 AND company_id=$2 AND work_item_id=$3
              AND status='processing' AND lease_token=$4 AND lease_expires_at > $5
            """;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue(lease.Request.TenantId);
            command.Parameters.AddWithValue(lease.Request.CompanyId);
            command.Parameters.AddWithValue(lease.WorkItemId);
            command.Parameters.AddWithValue(lease.LeaseToken);
            command.Parameters.AddWithValue(completedAt);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw LostLease();
            }
        }
        await InsertEventAsync(
            connection,
            transaction,
            lease.Request.TenantId,
            lease.Request.CompanyId,
            lease.WorkItemId,
            "completed",
            lease.AttemptNumber,
            completedAt,
            lease.LeaseToken,
            null,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async ValueTask<bool> FailAsync(
        PartyReportRefreshLease lease,
        string errorCode,
        DateTimeOffset failedAt,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        RequireUtc(failedAt, nameof(failedAt));
        if (retryDelay < TimeSpan.Zero || retryDelay > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay));
        }
        string normalizedError = RequireText(errorCode, 160, nameof(errorCode));
        EnsureLeaseScopeAndPermission(lease);
        bool willRetry = lease.AttemptNumber < lease.MaxAttempts;

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetExecutionContextAsync(connection, transaction, cancellationToken);
        const string sql = """
            UPDATE reporting.party_report_refresh_work_item
            SET status=$5, lease_token=NULL, lease_expires_at=NULL, completed_at=NULL,
                available_at=$6, last_error_code=$7, last_error_at=$8
            WHERE tenant_id=$1 AND company_id=$2 AND work_item_id=$3
              AND status='processing' AND lease_token=$4 AND lease_expires_at > $8
            """;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue(lease.Request.TenantId);
            command.Parameters.AddWithValue(lease.Request.CompanyId);
            command.Parameters.AddWithValue(lease.WorkItemId);
            command.Parameters.AddWithValue(lease.LeaseToken);
            command.Parameters.AddWithValue(willRetry ? "pending" : "failed");
            command.Parameters.AddWithValue(willRetry ? failedAt + retryDelay : failedAt);
            command.Parameters.AddWithValue(normalizedError);
            command.Parameters.AddWithValue(failedAt);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw LostLease();
            }
        }
        await InsertEventAsync(
            connection,
            transaction,
            lease.Request.TenantId,
            lease.Request.CompanyId,
            lease.WorkItemId,
            willRetry ? "retry-scheduled" : "failed",
            lease.AttemptNumber,
            failedAt,
            lease.LeaseToken,
            normalizedError,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return willRetry;
    }

    private async ValueTask FinalizeExpiredLastAttemptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid[] companyIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string selectSql = """
            SELECT tenant_id, company_id, work_item_id, attempt_count, lease_token
            FROM reporting.party_report_refresh_work_item
            WHERE tenant_id=$1 AND company_id=ANY($2)
              AND status='processing' AND lease_expires_at <= $3
              AND attempt_count >= max_attempts
            ORDER BY lease_expires_at, work_item_id
            FOR UPDATE SKIP LOCKED
            LIMIT 1
            """;
        (Guid TenantId, Guid CompanyId, Guid WorkItemId, int Attempt, Guid LeaseToken)? expired = null;
        await using (var select = new NpgsqlCommand(selectSql, connection, transaction))
        {
            select.Parameters.AddWithValue(scope.TenantId);
            select.Parameters.AddWithValue(companyIds);
            select.Parameters.AddWithValue(now);
            await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                expired = (
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetInt32(3),
                    reader.GetGuid(4));
            }
        }
        if (expired is null)
        {
            return;
        }

        const string updateSql = """
            UPDATE reporting.party_report_refresh_work_item
            SET status='failed', lease_token=NULL, lease_expires_at=NULL,
                completed_at=NULL, available_at=$5,
                last_error_code='PARTY_REPORT_REFRESH_LEASE_EXPIRED', last_error_at=$5
            WHERE tenant_id=$1 AND company_id=$2 AND work_item_id=$3
              AND status='processing' AND lease_token=$4
            """;
        await using (var update = new NpgsqlCommand(updateSql, connection, transaction))
        {
            update.Parameters.AddWithValue(expired.Value.TenantId);
            update.Parameters.AddWithValue(expired.Value.CompanyId);
            update.Parameters.AddWithValue(expired.Value.WorkItemId);
            update.Parameters.AddWithValue(expired.Value.LeaseToken);
            update.Parameters.AddWithValue(now);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw LostLease();
            }
        }
        await InsertEventAsync(
            connection,
            transaction,
            expired.Value.TenantId,
            expired.Value.CompanyId,
            expired.Value.WorkItemId,
            "failed",
            expired.Value.Attempt,
            now,
            expired.Value.LeaseToken,
            "PARTY_REPORT_REFRESH_LEASE_EXPIRED",
            cancellationToken);
    }

    private static void AddRequestParameters(
        NpgsqlCommand command,
        PartyReportRefreshEnqueueCommand enqueue,
        string requestKey,
        string fingerprint)
    {
        PartyReportRefreshRequest request = enqueue.Request;
        command.Parameters.AddWithValue(request.TenantId);
        command.Parameters.AddWithValue(request.CompanyId);
        command.Parameters.AddWithValue(enqueue.WorkItemId);
        command.Parameters.AddWithValue(requestKey);
        command.Parameters.AddWithValue(fingerprint);
        command.Parameters.AddWithValue(request.PartyAccountId);
        command.Parameters.AddWithValue(request.ReportCode);
        command.Parameters.AddWithValue(request.ReportDefinitionVersion);
        command.Parameters.AddWithValue(request.EffectiveAsOf);
        command.Parameters.AddWithValue(request.RecordedCutoff);
        command.Parameters.AddWithValue(request.ProjectionGenerationId);
        command.Parameters.AddWithValue(request.StatementId);
        command.Parameters.AddWithValue(request.AgingReportId);
        command.Parameters.AddWithValue(request.PartyCrossFootId);
        command.Parameters.AddWithValue(request.ControlAccountReconciliationId);
        command.Parameters.AddWithValue(request.GeneratedAt);
        command.Parameters.AddWithValue(request.GenerationReason);
        command.Parameters.AddWithValue(request.ScheduledFor);
        command.Parameters.AddWithValue(request.TimezoneName);
        command.Parameters.AddWithValue(request.BusinessCalendarCode);
        command.Parameters.AddWithValue(request.MissedRunPolicy);
        command.Parameters.AddWithValue(enqueue.MaxAttempts);
        command.Parameters.AddWithValue(enqueue.AvailableAt);
        command.Parameters.AddWithValue(enqueue.CreatedAt);
        command.Parameters.AddWithValue(enqueue.CreatedBy);
    }

    private async ValueTask InsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid companyId,
        Guid workItemId,
        string eventType,
        int attemptNumber,
        DateTimeOffset occurredAt,
        Guid? leaseToken,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO reporting.party_report_refresh_event
                (tenant_id, company_id, event_id, work_item_id, event_type,
                 attempt_number, occurred_at, actor_id, lease_token, error_code)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(companyId);
        command.Parameters.AddWithValue(Guid.CreateVersion7());
        command.Parameters.AddWithValue(workItemId);
        command.Parameters.AddWithValue(eventType);
        command.Parameters.AddWithValue(attemptNumber);
        command.Parameters.AddWithValue(occurredAt);
        command.Parameters.AddWithValue(scope.ActorId);
        command.Parameters.AddWithValue(leaseToken is Guid token ? token : DBNull.Value);
        command.Parameters.AddWithValue(errorCode is not null ? errorCode : DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private void EnsureLeaseScopeAndPermission(PartyReportRefreshLease lease)
    {
        scope.EnsureAllowed(lease.Request.TenantId, lease.Request.CompanyId);
        EnsureRefreshPermission(lease.Request.CompanyId);
    }

    private void EnsureRefreshPermission(Guid companyId)
    {
        if (!scope.HasPermission(companyId, PartyReportRefreshPermissions.Refresh))
        {
            throw new ExecutionScopeDeniedException();
        }
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

    private static PartyReportRefreshQueueException LostLease() => new(
        "PARTY_REPORT_REFRESH_LEASE_LOST",
        "The Party report refresh lease is missing, expired or owned by another worker.");

    private static void RequireId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A non-empty ID is required.", parameterName);
        }
    }

    private static void RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The timestamp must use the UTC offset.", parameterName);
        }
    }

    private static string RequireText(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-blank value is required.", parameterName);
        }
        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"The value cannot exceed {maximumLength} characters.", parameterName);
        }
        return normalized;
    }
}

public sealed class PartyReportRefreshQueueException(string code, string message)
    : InvalidOperationException(message), IPartyReportRefreshFailure
{
    public string Code { get; } = code;
}
