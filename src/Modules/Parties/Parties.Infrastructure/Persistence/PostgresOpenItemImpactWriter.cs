using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Parties.Domain.OpenItems;
using Npgsql;
using NpgsqlTypes;

namespace KaguERP.Modules.Parties.Infrastructure.Persistence;

public sealed record OpenItemImpactPersistenceResult(Guid EventId, bool Created);

public static class PostgresOpenItemImpactWriter
{
    public static async ValueTask<OpenItemImpactPersistenceResult> PersistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        OpenItemImpactEvent impact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(impact);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        scope.EnsureAllowed(impact.TenantId, impact.CompanyId);
        await LockDueLineAsync(connection, transaction, impact, cancellationToken);

        const string insertSql = """
            INSERT INTO party.open_item_impact_event
                (tenant_id, company_id, event_id, party_account_id, due_schedule_line_id,
                 payment_id, currency, impact_kind, amount, effective_date, recorded_at,
                 recorded_by, reverses_event_id)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)
            ON CONFLICT (tenant_id, company_id, event_id) DO NOTHING
            RETURNING event_id
            """;
        await using (var insert = new NpgsqlCommand(insertSql, connection, transaction))
        {
            AddParameters(insert, scope, impact);
            object? inserted = await insert.ExecuteScalarAsync(cancellationToken);
            if (inserted is Guid insertedId)
            {
                return new OpenItemImpactPersistenceResult(insertedId, true);
            }
        }

        const string existingSql = """
            SELECT party_account_id, due_schedule_line_id, payment_id, currency, impact_kind,
                   amount, effective_date, recorded_at, reverses_event_id
            FROM party.open_item_impact_event
            WHERE tenant_id=$1 AND company_id=$2 AND event_id=$3
            """;
        await using var existing = new NpgsqlCommand(existingSql, connection, transaction);
        existing.Parameters.AddWithValue(impact.TenantId);
        existing.Parameters.AddWithValue(impact.CompanyId);
        existing.Parameters.AddWithValue(impact.EventId);
        await using NpgsqlDataReader reader = await existing.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Open-item impact is not visible after its identity conflict.");
        }
        if (reader.GetGuid(0) != impact.PartyAccountId || reader.GetGuid(1) != impact.DueScheduleLineId ||
            ReadNullableGuid(reader, 2) != impact.PaymentId ||
            !string.Equals(reader.GetString(3), impact.Currency.Value, StringComparison.Ordinal) ||
            reader.GetInt16(4) != (short)impact.Kind || reader.GetDecimal(5) != impact.Amount ||
            reader.GetFieldValue<DateOnly>(6) != impact.EffectiveDate ||
            reader.GetFieldValue<DateTimeOffset>(7) != impact.RecordedAt ||
            ReadNullableGuid(reader, 8) != impact.ReversesEventId)
        {
            throw new OpenItemImpactPersistenceConflictException(impact.EventId);
        }
        return new OpenItemImpactPersistenceResult(impact.EventId, false);
    }

    private static async ValueTask LockDueLineAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OpenItemImpactEvent impact,
        CancellationToken cancellationToken)
    {
        const string lockSql = "SELECT pg_advisory_xact_lock(hashtextextended($1::text, 20260826))";
        await using (var lockCommand = new NpgsqlCommand(lockSql, connection, transaction))
        {
            lockCommand.Parameters.AddWithValue(impact.DueScheduleLineId);
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string sql = """
            SELECT party_account_id, currency
            FROM party.due_schedule_line
            WHERE tenant_id=$1 AND company_id=$2 AND due_schedule_line_id=$3
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(impact.TenantId);
        command.Parameters.AddWithValue(impact.CompanyId);
        command.Parameters.AddWithValue(impact.DueScheduleLineId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.GetGuid(0) != impact.PartyAccountId ||
            !string.Equals(reader.GetString(1), impact.Currency.Value, StringComparison.Ordinal))
        {
            throw new OpenItemImpactDueLineConflictException(impact.DueScheduleLineId);
        }
    }

    private static void AddParameters(
        NpgsqlCommand command,
        ExecutionScope scope,
        OpenItemImpactEvent impact)
    {
        command.Parameters.AddWithValue(impact.TenantId);
        command.Parameters.AddWithValue(impact.CompanyId);
        command.Parameters.AddWithValue(impact.EventId);
        command.Parameters.AddWithValue(impact.PartyAccountId);
        command.Parameters.AddWithValue(impact.DueScheduleLineId);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = impact.PaymentId.HasValue ? impact.PaymentId.Value : DBNull.Value });
        command.Parameters.AddWithValue(impact.Currency.Value);
        command.Parameters.AddWithValue((short)impact.Kind);
        command.Parameters.AddWithValue(impact.Amount);
        command.Parameters.AddWithValue(impact.EffectiveDate);
        command.Parameters.AddWithValue(impact.RecordedAt);
        command.Parameters.AddWithValue(scope.ActorId);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = impact.ReversesEventId.HasValue ? impact.ReversesEventId.Value : DBNull.Value });
    }

    private static Guid? ReadNullableGuid(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
}

public sealed class OpenItemImpactPersistenceConflictException(Guid eventId)
    : InvalidOperationException("The open-item impact ID already has different immutable content.")
{
    public string Code { get; } = "OPEN_ITEM_IMPACT_CONFLICT";
    public Guid EventId { get; } = eventId;
}

public sealed class OpenItemImpactDueLineConflictException(Guid dueScheduleLineId)
    : InvalidOperationException("The due-schedule line is missing or conflicts with the open-item scope.")
{
    public string Code { get; } = "OPEN_ITEM_DUE_LINE_CONFLICT";
    public Guid DueScheduleLineId { get; } = dueScheduleLineId;
}
