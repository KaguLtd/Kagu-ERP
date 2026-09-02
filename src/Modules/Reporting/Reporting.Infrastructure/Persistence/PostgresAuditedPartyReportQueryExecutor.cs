using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Domain.PartyReports;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public sealed record PostgresPartyReportQuery(
    Guid CompanyId,
    string ExpectedReportCode,
    long ExpectedReportDefinitionVersion,
    string RequiredPermissionCode,
    Guid CrossFootId,
    Guid StatementId,
    Guid AgingReportId,
    Guid AuditEventId);

public sealed class PostgresAuditedPartyReportQueryExecutor(
    NpgsqlDataSource dataSource,
    ExecutionScope scope,
    RequestAuditContext auditContext,
    AppendPartyReportAudit appendAudit)
{
    public async ValueTask<PartyStatementAgingCrossFoot?> ExecuteAsync(
        PostgresPartyReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetExecutionContextAsync(connection, transaction, cancellationToken);
        var request = new AuditedPartyReportQueryRequest(
            new AuthorizedPartyReportQueryRequest(
                scope,
                query.CompanyId,
                query.ExpectedReportCode,
                query.ExpectedReportDefinitionVersion,
                query.RequiredPermissionCode,
                query.CrossFootId,
                query.StatementId,
                query.AgingReportId),
            auditContext,
            query.AuditEventId);
        try
        {
            PartyStatementAgingCrossFoot? result = await AuditedPartyReportQuery.ExecuteAsync(
                connection,
                transaction,
                request,
                appendAudit,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (PartyReportQueryDeniedException)
        {
            await transaction.CommitAsync(cancellationToken);
            throw;
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
}
