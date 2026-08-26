using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Treasury.Domain.Statements;
using Npgsql;

namespace KaguERP.Modules.Treasury.Infrastructure.Persistence;

public sealed record StatementLinePersistenceResult(Guid StatementLineId, bool Created);

public static class PostgresStatementLineWriter
{
    public static async ValueTask<StatementLinePersistenceResult> PersistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        ValidatedStatementLineDraft line,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(line);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        scope.EnsureAllowed(line.TenantId, line.CompanyId);

        const string insertSql = """
            INSERT INTO treasury.statement_line
                (tenant_id,company_id,statement_line_id,statement_import_id,treasury_account_id,
                 source_system,identity_kind,external_key,currency,signed_amount,booking_date,
                 value_date,recorded_at,recorded_by,raw_object_sha256,parser_version)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16)
            ON CONFLICT (tenant_id,company_id,treasury_account_id,source_system,identity_kind,external_key)
            DO NOTHING RETURNING statement_line_id
            """;
        await using (var insert = new NpgsqlCommand(insertSql, connection, transaction))
        {
            AddParameters(insert, scope, line);
            object? inserted = await insert.ExecuteScalarAsync(cancellationToken);
            if (inserted is Guid lineId)
            {
                return new StatementLinePersistenceResult(lineId, true);
            }
        }

        const string existingSql = """
            SELECT statement_line_id,statement_import_id,currency,signed_amount,booking_date,value_date,
                   recorded_at,raw_object_sha256,parser_version
            FROM treasury.statement_line
            WHERE tenant_id=$1 AND company_id=$2 AND treasury_account_id=$3 AND source_system=$4
              AND identity_kind=$5 AND external_key=$6
            """;
        StatementLineExternalIdentity identity = line.ExternalIdentity;
        await using var existing = new NpgsqlCommand(existingSql, connection, transaction);
        existing.Parameters.AddWithValue(line.TenantId);
        existing.Parameters.AddWithValue(line.CompanyId);
        existing.Parameters.AddWithValue(line.TreasuryAccountId);
        existing.Parameters.AddWithValue(identity.SourceSystem);
        existing.Parameters.AddWithValue(identity.IdentityKind);
        existing.Parameters.AddWithValue(identity.ExternalKey);
        await using NpgsqlDataReader reader = await existing.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Statement line is not visible after its identity conflict.");
        }
        Guid existingId = reader.GetGuid(0);
        if (existingId != line.StatementLineId || reader.GetGuid(1) != line.StatementImportId ||
            reader.GetString(2) != line.Currency.Value || reader.GetDecimal(3) != line.SignedAmount ||
            reader.GetFieldValue<DateOnly>(4) != line.BookingDate ||
            reader.GetFieldValue<DateOnly>(5) != line.ValueDate ||
            reader.GetFieldValue<DateTimeOffset>(6) != line.RecordedAt ||
            reader.GetString(7) != line.RawObjectSha256 || reader.GetInt64(8) != line.ParserVersion)
        {
            throw new StatementLinePersistenceConflictException(existingId);
        }
        return new StatementLinePersistenceResult(existingId, false);
    }

    private static void AddParameters(
        NpgsqlCommand command,
        ExecutionScope scope,
        ValidatedStatementLineDraft line)
    {
        StatementLineExternalIdentity identity = line.ExternalIdentity;
        object[] values =
        [
            line.TenantId, line.CompanyId, line.StatementLineId, line.StatementImportId,
            line.TreasuryAccountId, identity.SourceSystem, identity.IdentityKind, identity.ExternalKey,
            line.Currency.Value, line.SignedAmount, line.BookingDate, line.ValueDate, line.RecordedAt,
            scope.ActorId, line.RawObjectSha256, line.ParserVersion,
        ];
        foreach (object value in values)
        {
            command.Parameters.AddWithValue(value);
        }
    }
}

public sealed class StatementLinePersistenceConflictException(Guid existingStatementLineId)
    : InvalidOperationException("The canonical statement-line identity has different immutable content.")
{
    public string Code { get; } = "STATEMENT_LINE_IDENTITY_CONFLICT";
    public Guid ExistingStatementLineId { get; } = existingStatementLineId;
}
