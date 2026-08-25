using KaguERP.BuildingBlocks.Application.Audit;
using Npgsql;

namespace KaguERP.Bootstrap;

public sealed class PostgresAuthorizationAuditWriter(NpgsqlDataSource dataSource) : IAuthorizationAuditWriter
{
    public async Task WriteAsync(
        RequestAuditContext context,
        AuthorizationAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(auditEvent);

        Guid[] companyIds = context.CompanyIds.Order().ToArray();
        if (companyIds.Length == 0)
        {
            throw new InvalidOperationException("Authorization audit requires at least one trusted company scope.");
        }

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetAuditContextAsync(connection, transaction, context, companyIds, cancellationToken);

        await AppendAsync(
            connection,
            transaction,
            context,
            Guid.CreateVersion7(),
            auditEvent,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public static async ValueTask AppendAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RequestAuditContext context,
        Guid auditEventId,
        AuthorizationAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(auditEvent);

        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }

        if (auditEventId == Guid.Empty)
        {
            throw new ArgumentException("Audit event ID cannot be empty.", nameof(auditEventId));
        }

        Guid[] companyIds = context.CompanyIds.Order().ToArray();
        if (companyIds.Length == 0)
        {
            throw new InvalidOperationException("Authorization audit requires at least one trusted company scope.");
        }

        const string sql = """
            INSERT INTO platform.audit_event
                (id, tenant_id, actor_id, company_ids, correlation_id, trace_id, session_id,
                 action, target_type, target_id, outcome, reason_code)
            VALUES
                ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(auditEventId);
        command.Parameters.AddWithValue(context.TenantId);
        command.Parameters.AddWithValue(context.ActorId);
        command.Parameters.AddWithValue(companyIds);
        command.Parameters.AddWithValue(context.CorrelationId);
        command.Parameters.AddWithValue(context.TraceId);
        command.Parameters.AddWithValue((object?)context.SessionId ?? DBNull.Value);
        command.Parameters.AddWithValue(auditEvent.Action);
        command.Parameters.AddWithValue(auditEvent.TargetType);
        command.Parameters.AddWithValue((object?)auditEvent.TargetId ?? DBNull.Value);
        command.Parameters.AddWithValue(auditEvent.Outcome);
        command.Parameters.AddWithValue(auditEvent.ReasonCode);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SetAuditContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RequestAuditContext context,
        Guid[] companyIds,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                set_config('app.tenant_id', $1, true),
                set_config('app.actor_id', $2, true),
                set_config('app.company_ids', $3, true)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(context.TenantId.ToString());
        command.Parameters.AddWithValue(context.ActorId.ToString());
        command.Parameters.AddWithValue("{" + string.Join(',', companyIds) + "}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
