using KaguERP.Modules.Parties.Application.Openings;
using KaguERP.Modules.Parties.Domain.Accounts;
using KaguERP.Modules.Parties.Domain.Openings;
using Npgsql;

namespace KaguERP.Modules.Parties.Infrastructure.Persistence;

public sealed record PartyAccountOpeningPersistenceResult(
    Guid OpeningEventId,
    bool Created,
    long SourceVersion,
    PartyAccountBalanceSide BalanceSide,
    string Currency,
    Guid ControlAccountId,
    DateTimeOffset RecordedAt);

public static class PostgresPartyAccountOpeningWriter
{
    public static async ValueTask<PartyAccountOpeningPersistenceResult> PersistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuthorizedPartyAccountOpeningPreparation preparation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(preparation);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }

        PartyAccountOpeningDraft draft = preparation.Draft;
        PartyAccountPostingContext account = await LoadAccountContextAsync(
            connection,
            transaction,
            draft,
            cancellationToken);

        const string insertSql = """
            INSERT INTO party.party_account_opening_event
                (tenant_id, company_id, opening_event_id, source_version, party_account_id,
                 balance_side, currency, control_account_id, entry_side, original_amount,
                 effective_date, recorded_at, recorded_by)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)
            ON CONFLICT (tenant_id, company_id, opening_event_id) DO NOTHING
            RETURNING opening_event_id
            """;
        await using (var insert = new NpgsqlCommand(insertSql, connection, transaction))
        {
            AddParameters(insert, preparation, account);
            object? inserted = await insert.ExecuteScalarAsync(cancellationToken);
            if (inserted is Guid insertedId)
            {
                return CreateResult(insertedId, true, draft, account);
            }
        }

        const string existingSql = """
            SELECT source_version, party_account_id, balance_side, currency, control_account_id,
                   entry_side, original_amount, effective_date, recorded_at, recorded_by
            FROM party.party_account_opening_event
            WHERE tenant_id=$1 AND company_id=$2 AND opening_event_id=$3
            """;
        await using var existing = new NpgsqlCommand(existingSql, connection, transaction);
        existing.Parameters.AddWithValue(draft.TenantId);
        existing.Parameters.AddWithValue(draft.CompanyId);
        existing.Parameters.AddWithValue(draft.OpeningEventId);
        await using NpgsqlDataReader reader = await existing.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Opening event is not visible after its identity conflict.");
        }

        if (reader.GetInt64(0) != draft.SourceVersion ||
            reader.GetGuid(1) != draft.PartyAccountId ||
            reader.GetInt16(2) != (short)account.BalanceSide ||
            !string.Equals(reader.GetString(3), account.Currency, StringComparison.Ordinal) ||
            reader.GetGuid(4) != account.ControlAccountId ||
            reader.GetInt16(5) != (short)draft.EntrySide ||
            reader.GetDecimal(6) != draft.OriginalAmount ||
            reader.GetFieldValue<DateOnly>(7) != draft.EffectiveDate ||
            reader.GetFieldValue<DateTimeOffset>(8) != draft.RecordedAt ||
            reader.GetGuid(9) != preparation.ActorId)
        {
            throw new PartyAccountOpeningPersistenceConflictException(draft.OpeningEventId);
        }

        return CreateResult(draft.OpeningEventId, false, draft, account);
    }

    private static async ValueTask<PartyAccountPostingContext> LoadAccountContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PartyAccountOpeningDraft draft,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT balance_side, currency, control_account_id
            FROM party.party_account
            WHERE tenant_id=$1 AND company_id=$2 AND party_account_id=$3
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(draft.TenantId);
        command.Parameters.AddWithValue(draft.CompanyId);
        command.Parameters.AddWithValue(draft.PartyAccountId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new PartyAccountOpeningAccountUnavailableException(draft.PartyAccountId);
        }
        if (reader.IsDBNull(0))
        {
            throw new PartyAccountOpeningAccountUnclassifiedException(draft.PartyAccountId);
        }

        short persistedBalanceSide = reader.GetInt16(0);
        if (!Enum.IsDefined(typeof(PartyAccountBalanceSide), persistedBalanceSide))
        {
            throw new PartyAccountOpeningAccountUnclassifiedException(draft.PartyAccountId);
        }

        return new PartyAccountPostingContext(
            (PartyAccountBalanceSide)persistedBalanceSide,
            reader.GetString(1),
            reader.GetGuid(2));
    }

    private static void AddParameters(
        NpgsqlCommand command,
        AuthorizedPartyAccountOpeningPreparation preparation,
        PartyAccountPostingContext account)
    {
        PartyAccountOpeningDraft draft = preparation.Draft;
        command.Parameters.AddWithValue(draft.TenantId);
        command.Parameters.AddWithValue(draft.CompanyId);
        command.Parameters.AddWithValue(draft.OpeningEventId);
        command.Parameters.AddWithValue(draft.SourceVersion);
        command.Parameters.AddWithValue(draft.PartyAccountId);
        command.Parameters.AddWithValue((short)account.BalanceSide);
        command.Parameters.AddWithValue(account.Currency);
        command.Parameters.AddWithValue(account.ControlAccountId);
        command.Parameters.AddWithValue((short)draft.EntrySide);
        command.Parameters.AddWithValue(draft.OriginalAmount);
        command.Parameters.AddWithValue(draft.EffectiveDate);
        command.Parameters.AddWithValue(draft.RecordedAt);
        command.Parameters.AddWithValue(preparation.ActorId);
    }

    private static PartyAccountOpeningPersistenceResult CreateResult(
        Guid openingEventId,
        bool created,
        PartyAccountOpeningDraft draft,
        PartyAccountPostingContext account) =>
        new(
            openingEventId,
            created,
            draft.SourceVersion,
            account.BalanceSide,
            account.Currency,
            account.ControlAccountId,
            draft.RecordedAt);

    private sealed record PartyAccountPostingContext(
        PartyAccountBalanceSide BalanceSide,
        string Currency,
        Guid ControlAccountId);
}

public sealed class PartyAccountOpeningPersistenceConflictException(Guid openingEventId)
    : InvalidOperationException("The opening event identity already has different immutable content.")
{
    public string Code { get; } = "PARTY_OPENING_SOURCE_CONFLICT";
    public Guid OpeningEventId { get; } = openingEventId;
}

public sealed class PartyAccountOpeningAccountUnavailableException(Guid partyAccountId)
    : InvalidOperationException("The party account is not visible in the active execution scope.")
{
    public string Code { get; } = "PARTY_OPENING_ACCOUNT_UNAVAILABLE";
    public Guid PartyAccountId { get; } = partyAccountId;
}

public sealed class PartyAccountOpeningAccountUnclassifiedException(Guid partyAccountId)
    : InvalidOperationException("The party account has no usable receivable/payable classification.")
{
    public string Code { get; } = "PARTY_OPENING_ACCOUNT_UNCLASSIFIED";
    public Guid PartyAccountId { get; } = partyAccountId;
}
