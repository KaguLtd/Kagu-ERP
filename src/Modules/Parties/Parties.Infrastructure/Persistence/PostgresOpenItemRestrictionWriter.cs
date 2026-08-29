using KaguERP.Modules.Parties.Application.OpenItems;
using KaguERP.Modules.Parties.Domain.OpenItems;
using Npgsql;
using NpgsqlTypes;

namespace KaguERP.Modules.Parties.Infrastructure.Persistence;

public sealed record OpenItemRestrictionPersistenceResult(Guid EventId, bool Created);

public static class PostgresOpenItemRestrictionWriter
{
    public static async ValueTask<OpenItemRestrictionPersistenceResult> PersistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuthorizedOpenItemRestrictionChange change,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(change);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }

        OpenItemRestrictionEvent restrictionEvent = change.RestrictionEvent;
        const string insertSql = """
            INSERT INTO party.open_item_restriction_event
                (tenant_id, company_id, event_id, party_account_id, due_schedule_line_id,
                 restriction_kind, restriction_action, reason_code, effective_date,
                 recorded_at, recorded_by, releases_event_id)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
            ON CONFLICT (tenant_id, company_id, event_id) DO NOTHING
            RETURNING event_id
            """;
        await using (var insert = new NpgsqlCommand(insertSql, connection, transaction))
        {
            AddParameters(insert, change);
            object? inserted = await insert.ExecuteScalarAsync(cancellationToken);
            if (inserted is Guid insertedId)
            {
                return new OpenItemRestrictionPersistenceResult(insertedId, true);
            }
        }

        const string existingSql = """
            SELECT party_account_id, due_schedule_line_id, restriction_kind, restriction_action,
                   reason_code, effective_date, recorded_at, recorded_by, releases_event_id
            FROM party.open_item_restriction_event
            WHERE tenant_id=$1 AND company_id=$2 AND event_id=$3
            """;
        await using var existing = new NpgsqlCommand(existingSql, connection, transaction);
        existing.Parameters.AddWithValue(restrictionEvent.TenantId);
        existing.Parameters.AddWithValue(restrictionEvent.CompanyId);
        existing.Parameters.AddWithValue(restrictionEvent.EventId);
        await using NpgsqlDataReader reader = await existing.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Restriction event is not visible after its identity conflict.");
        }
        if (reader.GetGuid(0) != restrictionEvent.PartyAccountId ||
            reader.GetGuid(1) != restrictionEvent.DueScheduleLineId ||
            reader.GetInt16(2) != (short)restrictionEvent.Kind ||
            reader.GetInt16(3) != (short)restrictionEvent.Action ||
            !string.Equals(reader.GetString(4), restrictionEvent.ReasonCode, StringComparison.Ordinal) ||
            reader.GetFieldValue<DateOnly>(5) != restrictionEvent.EffectiveDate ||
            reader.GetFieldValue<DateTimeOffset>(6) != restrictionEvent.RecordedAt ||
            reader.GetGuid(7) != change.ActorId ||
            ReadNullableGuid(reader, 8) != restrictionEvent.ReleasesEventId)
        {
            throw new OpenItemRestrictionPersistenceConflictException(restrictionEvent.EventId);
        }
        return new OpenItemRestrictionPersistenceResult(restrictionEvent.EventId, false);
    }

    private static void AddParameters(NpgsqlCommand command, AuthorizedOpenItemRestrictionChange change)
    {
        OpenItemRestrictionEvent restrictionEvent = change.RestrictionEvent;
        command.Parameters.AddWithValue(restrictionEvent.TenantId);
        command.Parameters.AddWithValue(restrictionEvent.CompanyId);
        command.Parameters.AddWithValue(restrictionEvent.EventId);
        command.Parameters.AddWithValue(restrictionEvent.PartyAccountId);
        command.Parameters.AddWithValue(restrictionEvent.DueScheduleLineId);
        command.Parameters.AddWithValue((short)restrictionEvent.Kind);
        command.Parameters.AddWithValue((short)restrictionEvent.Action);
        command.Parameters.AddWithValue(restrictionEvent.ReasonCode);
        command.Parameters.AddWithValue(restrictionEvent.EffectiveDate);
        command.Parameters.AddWithValue(restrictionEvent.RecordedAt);
        command.Parameters.AddWithValue(change.ActorId);
        command.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Uuid,
            Value = restrictionEvent.ReleasesEventId.HasValue
                ? restrictionEvent.ReleasesEventId.Value
                : DBNull.Value,
        });
    }

    private static Guid? ReadNullableGuid(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
}

public sealed class OpenItemRestrictionPersistenceConflictException(Guid eventId)
    : InvalidOperationException("The restriction event ID already has different immutable content.")
{
    public string Code { get; } = "OPEN_ITEM_RESTRICTION_CONFLICT";
    public Guid EventId { get; } = eventId;
}
