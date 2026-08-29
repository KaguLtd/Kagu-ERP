using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Treasury.Domain.Payments;
using KaguERP.Modules.Treasury.Domain.Statements;
using Npgsql;

namespace KaguERP.Modules.Treasury.Infrastructure.Persistence;

public static class PostgresStatementLineLoader
{
    public static async ValueTask<ValidatedStatementLineDraft?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        Guid statementLineId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (statementLineId == Guid.Empty)
        {
            throw new ArgumentException("Statement-line ID is required.", nameof(statementLineId));
        }
        scope.EnsureAllowed(scope.TenantId, companyId);

        const string sql = """
            SELECT statement_import_id,treasury_account_id,source_system,identity_kind,external_key,
                   currency,signed_amount,booking_date,value_date,recorded_at,raw_object_sha256,parser_version
            FROM treasury.statement_line
            WHERE tenant_id=$1 AND company_id=$2 AND statement_line_id=$3
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(scope.TenantId);
        command.Parameters.AddWithValue(companyId);
        command.Parameters.AddWithValue(statementLineId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        StatementLineExternalIdentity identity = StatementLineExternalIdentity.Create(
            scope.TenantId, companyId, reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4));
        return ValidatedStatementLineDraft.Create(
            statementLineId,
            reader.GetGuid(0),
            identity,
            TreasuryCurrencyCode.Create(reader.GetString(5)),
            reader.GetDecimal(6),
            reader.GetFieldValue<DateOnly>(7),
            reader.GetFieldValue<DateOnly>(8),
            reader.GetFieldValue<DateTimeOffset>(9),
            reader.GetString(10),
            reader.GetInt64(11));
    }
}
