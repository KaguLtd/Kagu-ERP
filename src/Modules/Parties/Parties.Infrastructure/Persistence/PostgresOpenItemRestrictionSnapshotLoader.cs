using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Parties.Domain.OpenItems;
using Npgsql;

namespace KaguERP.Modules.Parties.Infrastructure.Persistence;

public static class PostgresOpenItemRestrictionSnapshotLoader
{
    public static async ValueTask<DerivedOpenItemRestrictionSnapshot?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        Guid dueScheduleLineId,
        DateOnly effectiveAsOf,
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
        if (companyId == Guid.Empty || dueScheduleLineId == Guid.Empty || effectiveAsOf == default)
        {
            throw new ArgumentException("Company, due-line and effective as-of values are required.");
        }
        if (recordedCutoff == default || recordedCutoff.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Recorded cutoff is required and must use the UTC offset.", nameof(recordedCutoff));
        }
        scope.EnsureAllowed(scope.TenantId, companyId);

        const string dueSql = """
            SELECT party_account_id
            FROM party.due_schedule_line
            WHERE tenant_id=$1 AND company_id=$2 AND due_schedule_line_id=$3
            """;
        Guid partyAccountId;
        await using (var due = new NpgsqlCommand(dueSql, connection, transaction))
        {
            due.Parameters.AddWithValue(scope.TenantId);
            due.Parameters.AddWithValue(companyId);
            due.Parameters.AddWithValue(dueScheduleLineId);
            object? value = await due.ExecuteScalarAsync(cancellationToken);
            if (value is not Guid persistedPartyAccountId)
            {
                return null;
            }
            partyAccountId = persistedPartyAccountId;
        }

        const string eventSql = """
            SELECT event_id, restriction_kind, restriction_action, reason_code,
                   effective_date, recorded_at, releases_event_id
            FROM party.open_item_restriction_event
            WHERE tenant_id=$1 AND company_id=$2 AND due_schedule_line_id=$3
              AND effective_date <= $4 AND recorded_at <= $5
            ORDER BY effective_date, recorded_at, event_id
            """;
        var events = new List<OpenItemRestrictionEvent>();
        await using (var command = new NpgsqlCommand(eventSql, connection, transaction))
        {
            command.Parameters.AddWithValue(scope.TenantId);
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(dueScheduleLineId);
            command.Parameters.AddWithValue(effectiveAsOf);
            command.Parameters.AddWithValue(recordedCutoff);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                events.Add(OpenItemRestrictionEvent.Create(
                    reader.GetGuid(0),
                    scope.TenantId,
                    companyId,
                    partyAccountId,
                    dueScheduleLineId,
                    (OpenItemRestrictionKind)reader.GetInt16(1),
                    (OpenItemRestrictionAction)reader.GetInt16(2),
                    reader.GetString(3),
                    reader.GetFieldValue<DateOnly>(4),
                    reader.GetFieldValue<DateTimeOffset>(5),
                    reader.IsDBNull(6) ? null : reader.GetGuid(6)));
            }
        }
        return DerivedOpenItemRestrictionSnapshot.Create(
            scope.TenantId,
            companyId,
            partyAccountId,
            dueScheduleLineId,
            effectiveAsOf,
            recordedCutoff,
            events);
    }
}
