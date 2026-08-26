using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Parties.Domain.Allocations;
using KaguERP.Modules.Parties.Domain.DueSchedules;
using Npgsql;

namespace KaguERP.Modules.Parties.Infrastructure.Persistence;

public sealed record LoadedDueSchedule(
    Guid DueScheduleId,
    string SourceType,
    long SourceVersion,
    DateTimeOffset RecordedAt,
    ValidatedDueSchedule Schedule);

public static class PostgresDueScheduleLoader
{
    public static async ValueTask<LoadedDueSchedule?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        Guid dueScheduleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (dueScheduleId == Guid.Empty)
        {
            throw new ArgumentException("Due schedule ID is required.", nameof(dueScheduleId));
        }
        scope.EnsureAllowed(scope.TenantId, companyId);

        const string headerSql = """
            SELECT party_account_id, source_type, source_event_id, source_version,
                   currency, source_original_amount, recorded_at
            FROM party.due_schedule
            WHERE tenant_id=$1 AND company_id=$2 AND due_schedule_id=$3
            """;
        Guid partyAccountId;
        string sourceType;
        Guid sourceEventId;
        long sourceVersion;
        AllocationCurrencyCode currency;
        decimal sourceAmount;
        DateTimeOffset recordedAt;
        await using (var header = new NpgsqlCommand(headerSql, connection, transaction))
        {
            header.Parameters.AddWithValue(scope.TenantId);
            header.Parameters.AddWithValue(companyId);
            header.Parameters.AddWithValue(dueScheduleId);
            await using NpgsqlDataReader reader = await header.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            partyAccountId = reader.GetGuid(0);
            sourceType = reader.GetString(1);
            sourceEventId = reader.GetGuid(2);
            sourceVersion = reader.GetInt64(3);
            currency = AllocationCurrencyCode.Create(reader.GetString(4));
            sourceAmount = reader.GetDecimal(5);
            recordedAt = reader.GetFieldValue<DateTimeOffset>(6);
        }

        const string linesSql = """
            SELECT due_schedule_line_id, original_amount, due_date, payment_term_snapshot_id,
                   payment_term_version, control_account_id
            FROM party.due_schedule_line
            WHERE tenant_id=$1 AND company_id=$2 AND due_schedule_id=$3
            ORDER BY due_date, due_schedule_line_id
            """;
        var lines = new List<DueScheduleLine>();
        await using (var command = new NpgsqlCommand(linesSql, connection, transaction))
        {
            command.Parameters.AddWithValue(scope.TenantId);
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(dueScheduleId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                lines.Add(DueScheduleLine.Create(
                    scope.TenantId, companyId, partyAccountId, sourceEventId, reader.GetGuid(0), currency,
                    reader.GetDecimal(1), reader.GetFieldValue<DateOnly>(2), reader.GetGuid(3),
                    reader.GetInt64(4), reader.GetGuid(5)));
            }
        }

        return new LoadedDueSchedule(
            dueScheduleId, sourceType, sourceVersion, recordedAt,
            ValidatedDueSchedule.Create(
                scope.TenantId, companyId, partyAccountId, sourceEventId, currency, sourceAmount, lines));
    }
}
