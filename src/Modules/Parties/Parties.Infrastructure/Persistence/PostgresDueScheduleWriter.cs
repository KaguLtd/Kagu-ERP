using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Parties.Domain.DueSchedules;
using Npgsql;

namespace KaguERP.Modules.Parties.Infrastructure.Persistence;

public sealed record DueSchedulePersistenceCommand(
    ExecutionScope Scope,
    Guid PartyId,
    Guid DueScheduleId,
    string SourceType,
    long SourceVersion,
    Guid DefaultControlAccountId,
    DateTimeOffset RecordedAt,
    ValidatedDueSchedule Schedule);

public sealed record DueSchedulePersistenceResult(
    Guid DueScheduleId,
    bool Created,
    DateTimeOffset RecordedAt);

public static class PostgresDueScheduleWriter
{
    public static async ValueTask<DueSchedulePersistenceResult> PersistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DueSchedulePersistenceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(command);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (command.PartyId == Guid.Empty || command.DueScheduleId == Guid.Empty ||
            command.DefaultControlAccountId == Guid.Empty)
        {
            throw new ArgumentException("Party, due schedule and default control-account IDs are required.", nameof(command));
        }
        if (command.SourceVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "Source version must be positive.");
        }
        if (command.RecordedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Recorded timestamp must use the UTC offset.", nameof(command));
        }
        string sourceType = command.SourceType.Trim();
        if (sourceType.Length == 0 || sourceType.Length > 120)
        {
            throw new ArgumentException("Source type is required and cannot exceed 120 characters.", nameof(command));
        }

        ValidatedDueSchedule schedule = command.Schedule;
        command.Scope.EnsureAllowed(schedule.TenantId, schedule.CompanyId);
        await EnsurePartyAndAccountAsync(connection, transaction, command, cancellationToken);

        const string headerSql = """
            INSERT INTO party.due_schedule
                (tenant_id, company_id, due_schedule_id, party_account_id, source_type,
                 source_event_id, source_version, currency, source_original_amount,
                 recorded_at, recorded_by, line_count)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
            ON CONFLICT (tenant_id, company_id, source_type, source_event_id, source_version) DO NOTHING
            RETURNING due_schedule_id, recorded_at
            """;
        await using (var header = new NpgsqlCommand(headerSql, connection, transaction))
        {
            header.Parameters.AddWithValue(schedule.TenantId);
            header.Parameters.AddWithValue(schedule.CompanyId);
            header.Parameters.AddWithValue(command.DueScheduleId);
            header.Parameters.AddWithValue(schedule.PartyAccountId);
            header.Parameters.AddWithValue(sourceType);
            header.Parameters.AddWithValue(schedule.SourceEventId);
            header.Parameters.AddWithValue(command.SourceVersion);
            header.Parameters.AddWithValue(schedule.Currency.Value);
            header.Parameters.AddWithValue(schedule.SourceOriginalAmount);
            header.Parameters.AddWithValue(command.RecordedAt);
            header.Parameters.AddWithValue(command.Scope.ActorId);
            header.Parameters.AddWithValue(schedule.Lines.Count);
            await using NpgsqlDataReader reader = await header.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                Guid insertedId = reader.GetGuid(0);
                DateTimeOffset insertedAt = reader.GetFieldValue<DateTimeOffset>(1);
                await reader.DisposeAsync();
                await InsertLinesAsync(connection, transaction, insertedId, schedule, cancellationToken);
                return new DueSchedulePersistenceResult(insertedId, true, insertedAt);
            }
        }

        const string existingSql = """
            SELECT due_schedule_id, party_account_id, currency, source_original_amount, line_count, recorded_at
            FROM party.due_schedule
            WHERE tenant_id=$1 AND company_id=$2 AND source_type=$3 AND source_event_id=$4 AND source_version=$5
            """;
        await using var existing = new NpgsqlCommand(existingSql, connection, transaction);
        existing.Parameters.AddWithValue(schedule.TenantId);
        existing.Parameters.AddWithValue(schedule.CompanyId);
        existing.Parameters.AddWithValue(sourceType);
        existing.Parameters.AddWithValue(schedule.SourceEventId);
        existing.Parameters.AddWithValue(command.SourceVersion);
        Guid existingId;
        DateTimeOffset existingRecordedAt;
        await using (NpgsqlDataReader existingReader = await existing.ExecuteReaderAsync(cancellationToken))
        {
            if (!await existingReader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Due schedule is not visible after its source uniqueness conflict.");
            }
            existingId = existingReader.GetGuid(0);
            existingRecordedAt = existingReader.GetFieldValue<DateTimeOffset>(5);
            if (existingReader.GetGuid(1) != schedule.PartyAccountId ||
                !string.Equals(existingReader.GetString(2), schedule.Currency.Value, StringComparison.Ordinal) ||
                existingReader.GetDecimal(3) != schedule.SourceOriginalAmount ||
                existingReader.GetInt32(4) != schedule.Lines.Count)
            {
                throw new DueSchedulePersistenceConflictException(existingId);
            }
        }
        await ValidateExistingLinesAsync(
            connection, transaction, existingId, schedule, cancellationToken);
        return new DueSchedulePersistenceResult(existingId, false, existingRecordedAt);
    }

    private static async ValueTask EnsurePartyAndAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DueSchedulePersistenceCommand command,
        CancellationToken cancellationToken)
    {
        ValidatedDueSchedule schedule = command.Schedule;
        const string partySql = """
            INSERT INTO party.party_identity (tenant_id, party_id, created_at, created_by)
            VALUES ($1,$2,$3,$4) ON CONFLICT (tenant_id, party_id) DO NOTHING
            """;
        await using (var partyCommand = new NpgsqlCommand(partySql, connection, transaction))
        {
            partyCommand.Parameters.AddWithValue(schedule.TenantId);
            partyCommand.Parameters.AddWithValue(command.PartyId);
            partyCommand.Parameters.AddWithValue(command.RecordedAt);
            partyCommand.Parameters.AddWithValue(command.Scope.ActorId);
            await partyCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string accountSql = """
            INSERT INTO party.party_account
                (tenant_id, company_id, party_account_id, party_id, currency, control_account_id, created_at, created_by)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8)
            ON CONFLICT (tenant_id, company_id, party_id, currency) DO NOTHING
            """;
        await using (var accountCommand = new NpgsqlCommand(accountSql, connection, transaction))
        {
            accountCommand.Parameters.AddWithValue(schedule.TenantId);
            accountCommand.Parameters.AddWithValue(schedule.CompanyId);
            accountCommand.Parameters.AddWithValue(schedule.PartyAccountId);
            accountCommand.Parameters.AddWithValue(command.PartyId);
            accountCommand.Parameters.AddWithValue(schedule.Currency.Value);
            accountCommand.Parameters.AddWithValue(command.DefaultControlAccountId);
            accountCommand.Parameters.AddWithValue(command.RecordedAt);
            accountCommand.Parameters.AddWithValue(command.Scope.ActorId);
            await accountCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string verifySql = """
            SELECT party_id, currency, control_account_id
            FROM party.party_account
            WHERE tenant_id=$1 AND company_id=$2 AND party_account_id=$3
            """;
        await using var verify = new NpgsqlCommand(verifySql, connection, transaction);
        verify.Parameters.AddWithValue(schedule.TenantId);
        verify.Parameters.AddWithValue(schedule.CompanyId);
        verify.Parameters.AddWithValue(schedule.PartyAccountId);
        await using NpgsqlDataReader reader = await verify.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.GetGuid(0) != command.PartyId ||
            !string.Equals(reader.GetString(1), schedule.Currency.Value, StringComparison.Ordinal) ||
            reader.GetGuid(2) != command.DefaultControlAccountId)
        {
            throw new DueSchedulePartyAccountConflictException(schedule.PartyAccountId);
        }
    }

    private static async ValueTask ValidateExistingLinesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid dueScheduleId,
        ValidatedDueSchedule schedule,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT due_schedule_line_id, party_account_id, source_event_id, currency,
                   original_amount, due_date, payment_term_snapshot_id, payment_term_version,
                   control_account_id
            FROM party.due_schedule_line
            WHERE tenant_id=$1 AND company_id=$2 AND due_schedule_id=$3
            ORDER BY due_date, due_schedule_line_id
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(schedule.TenantId);
        command.Parameters.AddWithValue(schedule.CompanyId);
        command.Parameters.AddWithValue(dueScheduleId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        var index = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            if (index >= schedule.Lines.Count || !Matches(reader, schedule.Lines[index]))
            {
                throw new DueSchedulePersistenceConflictException(dueScheduleId);
            }
            index++;
        }
        if (index != schedule.Lines.Count)
        {
            throw new DueSchedulePersistenceConflictException(dueScheduleId);
        }
    }

    private static bool Matches(NpgsqlDataReader reader, DueScheduleLine line) =>
        reader.GetGuid(0) == line.DueScheduleLineId &&
        reader.GetGuid(1) == line.PartyAccountId &&
        reader.GetGuid(2) == line.SourceEventId &&
        string.Equals(reader.GetString(3), line.Currency.Value, StringComparison.Ordinal) &&
        reader.GetDecimal(4) == line.OriginalAmount &&
        reader.GetFieldValue<DateOnly>(5) == line.DueDate &&
        reader.GetGuid(6) == line.PaymentTermSnapshotId &&
        reader.GetInt64(7) == line.PaymentTermVersion &&
        reader.GetGuid(8) == line.ControlAccountId;

    private static async ValueTask InsertLinesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid dueScheduleId,
        ValidatedDueSchedule schedule,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO party.due_schedule_line
                (tenant_id, company_id, due_schedule_id, due_schedule_line_id, party_account_id,
                 source_event_id, currency, original_amount, due_date, payment_term_snapshot_id,
                 payment_term_version, control_account_id)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
            """;
        foreach (DueScheduleLine line in schedule.Lines)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue(schedule.TenantId);
            command.Parameters.AddWithValue(schedule.CompanyId);
            command.Parameters.AddWithValue(dueScheduleId);
            command.Parameters.AddWithValue(line.DueScheduleLineId);
            command.Parameters.AddWithValue(line.PartyAccountId);
            command.Parameters.AddWithValue(line.SourceEventId);
            command.Parameters.AddWithValue(line.Currency.Value);
            command.Parameters.AddWithValue(line.OriginalAmount);
            command.Parameters.AddWithValue(line.DueDate);
            command.Parameters.AddWithValue(line.PaymentTermSnapshotId);
            command.Parameters.AddWithValue(line.PaymentTermVersion);
            command.Parameters.AddWithValue(line.ControlAccountId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}

public sealed class DueSchedulePersistenceConflictException(Guid existingDueScheduleId)
    : InvalidOperationException("The source version already has a different due schedule snapshot.")
{
    public string Code { get; } = "DUE_SCHEDULE_SOURCE_CONFLICT";
    public Guid ExistingDueScheduleId { get; } = existingDueScheduleId;
}

public sealed class DueSchedulePartyAccountConflictException(Guid partyAccountId)
    : InvalidOperationException("The party account identity or posting context conflicts with the existing account.")
{
    public string Code { get; } = "PARTY_ACCOUNT_CONFLICT";
    public Guid PartyAccountId { get; } = partyAccountId;
}
