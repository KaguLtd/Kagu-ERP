using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Parties.Domain.Allocations;
using KaguERP.Modules.Parties.Domain.DueSchedules;
using KaguERP.Modules.Parties.Domain.OpenItems;
using Npgsql;

namespace KaguERP.Modules.Parties.Infrastructure.Persistence;

public static class PostgresOpenItemSnapshotLoader
{
    public static async ValueTask<DerivedOpenItemSnapshot?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        Guid dueScheduleLineId,
        DateOnly asOfEffectiveDate,
        DateTimeOffset recordedCutoff,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (dueScheduleLineId == Guid.Empty)
        {
            throw new ArgumentException("Due-schedule line ID is required.", nameof(dueScheduleLineId));
        }
        scope.EnsureAllowed(scope.TenantId, companyId);

        const string lineSql = """
            SELECT party_account_id, source_event_id, currency, original_amount, due_date,
                   payment_term_snapshot_id, payment_term_version, control_account_id
            FROM party.due_schedule_line
            WHERE tenant_id=$1 AND company_id=$2 AND due_schedule_line_id=$3
            """;
        DueScheduleLine dueLine;
        await using (var command = new NpgsqlCommand(lineSql, connection, transaction))
        {
            command.Parameters.AddWithValue(scope.TenantId);
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(dueScheduleLineId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            dueLine = DueScheduleLine.Create(
                scope.TenantId, companyId, reader.GetGuid(0), reader.GetGuid(1), dueScheduleLineId,
                AllocationCurrencyCode.Create(reader.GetString(2)), reader.GetDecimal(3),
                reader.GetFieldValue<DateOnly>(4), reader.GetGuid(5), reader.GetInt64(6), reader.GetGuid(7));
        }

        const string eventSql = """
            SELECT event_id, payment_id, impact_kind, amount, effective_date, recorded_at,
                   reverses_event_id
            FROM party.open_item_impact_event
            WHERE tenant_id=$1 AND company_id=$2 AND due_schedule_line_id=$3
            ORDER BY effective_date, recorded_at, event_id
            """;
        var events = new List<OpenItemImpactEvent>();
        await using (var command = new NpgsqlCommand(eventSql, connection, transaction))
        {
            command.Parameters.AddWithValue(scope.TenantId);
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(dueScheduleLineId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                events.Add(OpenItemImpactEvent.Create(
                    reader.GetGuid(0), scope.TenantId, companyId, dueLine.PartyAccountId,
                    dueScheduleLineId, ReadNullableGuid(reader, 1), dueLine.Currency,
                    (OpenItemImpactKind)reader.GetInt16(2), reader.GetDecimal(3),
                    reader.GetFieldValue<DateOnly>(4), reader.GetFieldValue<DateTimeOffset>(5),
                    ReadNullableGuid(reader, 6)));
            }
        }

        return DerivedOpenItemSnapshot.Create(dueLine, asOfEffectiveDate, recordedCutoff, events);
    }

    private static Guid? ReadNullableGuid(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
}
