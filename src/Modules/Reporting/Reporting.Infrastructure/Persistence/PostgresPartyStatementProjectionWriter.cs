using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Domain.PartyReports;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public sealed record PartyStatementProjectionPersistenceResult(Guid StatementId, bool Created);

public static class PostgresPartyStatementProjectionWriter
{
    public static async ValueTask<PartyStatementProjectionPersistenceResult> PersistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        ValidatedPartyStatement statement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(statement);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        scope.EnsureAllowed(statement.ReportSlice.TenantId, statement.ReportSlice.CompanyId);

        const string headerSql = """
            INSERT INTO reporting.party_statement_projection
                (tenant_id,company_id,projection_generation_id,statement_id,party_account_id,
                 control_account_id,balance_side,opening_exposure,closing_exposure,line_count)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)
            ON CONFLICT (tenant_id,company_id,statement_id) DO NOTHING
            RETURNING statement_id
            """;
        await using (var header = new NpgsqlCommand(headerSql, connection, transaction))
        {
            header.Parameters.AddWithValue(statement.ReportSlice.TenantId);
            header.Parameters.AddWithValue(statement.ReportSlice.CompanyId);
            header.Parameters.AddWithValue(statement.ReportSlice.ProjectionGenerationId);
            header.Parameters.AddWithValue(statement.StatementId);
            header.Parameters.AddWithValue(statement.PartyAccountId);
            header.Parameters.AddWithValue(statement.ControlAccountId);
            header.Parameters.AddWithValue((short)statement.BalanceSide);
            header.Parameters.AddWithValue(statement.OpeningExposure);
            header.Parameters.AddWithValue(statement.ClosingExposure);
            header.Parameters.AddWithValue(statement.Lines.Count);
            if (await header.ExecuteScalarAsync(cancellationToken) is Guid insertedId)
            {
                await InsertLinesAsync(connection, transaction, statement, cancellationToken);
                return new PartyStatementProjectionPersistenceResult(insertedId, true);
            }
        }

        await ValidateExistingAsync(connection, transaction, statement, cancellationToken);
        return new PartyStatementProjectionPersistenceResult(statement.StatementId, false);
    }

    private static async ValueTask InsertLinesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ValidatedPartyStatement statement,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO reporting.party_statement_projection_line
                (tenant_id,company_id,statement_id,line_number,event_id,event_kind,source_type,
                 source_event_id,due_schedule_line_id,payment_id,exposure_effect,running_exposure,
                 effective_date,sequence_key,recorded_at)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)
            """;
        for (var index = 0; index < statement.Lines.Count; index++)
        {
            PartyStatementLine line = statement.Lines[index];
            PartyStatementEventSnapshot snapshot = line.EventSnapshot;
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            object?[] values =
            [
                snapshot.TenantId, snapshot.CompanyId, statement.StatementId, index + 1,
                snapshot.EventId, (short)snapshot.Kind, snapshot.SourceType, snapshot.SourceEventId,
                snapshot.DueScheduleLineId, snapshot.PaymentId, snapshot.ExposureEffect,
                line.RunningExposure, snapshot.EffectiveDate, snapshot.SequenceKey, snapshot.RecordedAt,
            ];
            foreach (object? value in values)
            {
                command.Parameters.AddWithValue(value ?? DBNull.Value);
            }
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async ValueTask ValidateExistingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ValidatedPartyStatement statement,
        CancellationToken cancellationToken)
    {
        const string headerSql = """
            SELECT projection_generation_id,party_account_id,control_account_id,balance_side,
                   opening_exposure,closing_exposure,line_count
            FROM reporting.party_statement_projection
            WHERE tenant_id=$1 AND company_id=$2 AND statement_id=$3
            """;
        await using (var header = new NpgsqlCommand(headerSql, connection, transaction))
        {
            header.Parameters.AddWithValue(statement.ReportSlice.TenantId);
            header.Parameters.AddWithValue(statement.ReportSlice.CompanyId);
            header.Parameters.AddWithValue(statement.StatementId);
            await using NpgsqlDataReader reader = await header.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) ||
                reader.GetGuid(0) != statement.ReportSlice.ProjectionGenerationId ||
                reader.GetGuid(1) != statement.PartyAccountId || reader.GetGuid(2) != statement.ControlAccountId ||
                reader.GetInt16(3) != (short)statement.BalanceSide ||
                reader.GetDecimal(4) != statement.OpeningExposure ||
                reader.GetDecimal(5) != statement.ClosingExposure || reader.GetInt32(6) != statement.Lines.Count)
            {
                throw new PartyStatementProjectionPersistenceConflictException(statement.StatementId);
            }
        }

        const string lineSql = """
            SELECT event_id,event_kind,source_type,source_event_id,due_schedule_line_id,payment_id,
                   exposure_effect,running_exposure,effective_date,sequence_key,recorded_at
            FROM reporting.party_statement_projection_line
            WHERE tenant_id=$1 AND company_id=$2 AND statement_id=$3 ORDER BY line_number
            """;
        await using var lines = new NpgsqlCommand(lineSql, connection, transaction);
        lines.Parameters.AddWithValue(statement.ReportSlice.TenantId);
        lines.Parameters.AddWithValue(statement.ReportSlice.CompanyId);
        lines.Parameters.AddWithValue(statement.StatementId);
        await using NpgsqlDataReader lineReader = await lines.ExecuteReaderAsync(cancellationToken);
        var index = 0;
        while (await lineReader.ReadAsync(cancellationToken))
        {
            if (index >= statement.Lines.Count || !Matches(lineReader, statement.Lines[index]))
            {
                throw new PartyStatementProjectionPersistenceConflictException(statement.StatementId);
            }
            index++;
        }
        if (index != statement.Lines.Count)
        {
            throw new PartyStatementProjectionPersistenceConflictException(statement.StatementId);
        }
    }

    private static bool Matches(NpgsqlDataReader reader, PartyStatementLine line)
    {
        PartyStatementEventSnapshot snapshot = line.EventSnapshot;
        Guid? paymentId = reader.IsDBNull(5) ? null : reader.GetGuid(5);
        return reader.GetGuid(0) == snapshot.EventId && reader.GetInt16(1) == (short)snapshot.Kind &&
               reader.GetString(2) == snapshot.SourceType && reader.GetGuid(3) == snapshot.SourceEventId &&
               reader.GetGuid(4) == snapshot.DueScheduleLineId && paymentId == snapshot.PaymentId &&
               reader.GetDecimal(6) == snapshot.ExposureEffect && reader.GetDecimal(7) == line.RunningExposure &&
               reader.GetFieldValue<DateOnly>(8) == snapshot.EffectiveDate &&
               reader.GetInt64(9) == snapshot.SequenceKey &&
               reader.GetFieldValue<DateTimeOffset>(10) == snapshot.RecordedAt;
    }
}

public sealed class PartyStatementProjectionPersistenceConflictException(Guid statementId)
    : InvalidOperationException("The statement ID already has different immutable projection content.")
{
    public string Code { get; } = "PARTY_STATEMENT_PROJECTION_CONFLICT";
    public Guid StatementId { get; } = statementId;
}
