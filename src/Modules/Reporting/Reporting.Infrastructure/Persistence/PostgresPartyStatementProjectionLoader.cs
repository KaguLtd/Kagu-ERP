using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Reporting.Domain.PartyReports;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.Persistence;

public static class PostgresPartyStatementProjectionLoader
{
    public static async ValueTask<ValidatedPartyStatement?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        Guid statementId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (statementId == Guid.Empty)
        {
            throw new ArgumentException("Statement ID is required.", nameof(statementId));
        }
        scope.EnsureAllowed(scope.TenantId, companyId);

        const string headerSql = """
            SELECT projection_generation_id,party_account_id,control_account_id,balance_side,opening_exposure
            FROM reporting.party_statement_projection
            WHERE tenant_id=$1 AND company_id=$2 AND statement_id=$3
            """;
        Guid generationId;
        Guid partyAccountId;
        Guid controlAccountId;
        PartyBalanceSide balanceSide;
        decimal openingExposure;
        await using (var header = new NpgsqlCommand(headerSql, connection, transaction))
        {
            header.Parameters.AddWithValue(scope.TenantId);
            header.Parameters.AddWithValue(companyId);
            header.Parameters.AddWithValue(statementId);
            await using NpgsqlDataReader reader = await header.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            generationId = reader.GetGuid(0);
            partyAccountId = reader.GetGuid(1);
            controlAccountId = reader.GetGuid(2);
            balanceSide = (PartyBalanceSide)reader.GetInt16(3);
            openingExposure = reader.GetDecimal(4);
        }

        LoadedProjectionGeneration manifest =
            await PostgresProjectionGenerationLoader.LoadAsync(
                connection, transaction, scope, companyId, generationId, cancellationToken)
            ?? throw new PartyStatementProjectionCorruptException(statementId);

        const string lineSql = """
            SELECT event_id,event_kind,source_type,source_event_id,due_schedule_line_id,payment_id,
                   exposure_effect,effective_date,sequence_key,recorded_at
            FROM reporting.party_statement_projection_line
            WHERE tenant_id=$1 AND company_id=$2 AND statement_id=$3 ORDER BY line_number
            """;
        var events = new List<PartyStatementEventSnapshot>();
        await using (var lines = new NpgsqlCommand(lineSql, connection, transaction))
        {
            lines.Parameters.AddWithValue(scope.TenantId);
            lines.Parameters.AddWithValue(companyId);
            lines.Parameters.AddWithValue(statementId);
            await using NpgsqlDataReader reader = await lines.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                events.Add(PartyStatementEventSnapshot.Create(
                    reader.GetGuid(0), scope.TenantId, companyId, partyAccountId, controlAccountId,
                    manifest.Slice.Currency, (PartyStatementEventKind)reader.GetInt16(1), reader.GetString(2),
                    reader.GetGuid(3), reader.GetGuid(4), ReadNullableGuid(reader, 5), reader.GetDecimal(6),
                    reader.GetFieldValue<DateOnly>(7), reader.GetInt64(8),
                    reader.GetFieldValue<DateTimeOffset>(9)));
            }
        }

        try
        {
            return ValidatedPartyStatement.Create(
                statementId, partyAccountId, controlAccountId, balanceSide,
                openingExposure, manifest.Slice, events);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new PartyStatementProjectionCorruptException(statementId, exception);
        }
    }

    private static Guid? ReadNullableGuid(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
}

public sealed class PartyStatementProjectionCorruptException : InvalidOperationException
{
    public PartyStatementProjectionCorruptException(Guid statementId, Exception? innerException = null)
        : base("Persisted party statement projection cannot be reconstructed safely.", innerException)
    {
        StatementId = statementId;
    }

    public string Code { get; } = "PARTY_STATEMENT_PROJECTION_CORRUPT";
    public Guid StatementId { get; }
}
