using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Accounting.Domain.Journals;
using KaguERP.Modules.Accounting.Domain.Periods;
using Npgsql;

namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

public static class PostgresAuthoritativePeriodGateLoader
{
    public static async ValueTask<ValidatedPeriodLockSet> LoadForStandardPostingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        ValidatedJournalDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(draft);

        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }

        scope.EnsureAllowed(draft.TenantId, draft.CompanyId);

        Guid periodId = RequireSinglePeriodId(
            await ReadMatchingPeriodIdsAsync(connection, transaction, draft, cancellationToken));
        await AcquirePeriodTransactionLockAsync(connection, transaction, periodId, cancellationToken);
        Guid revalidatedPeriodId = RequireSinglePeriodId(
            await ReadMatchingPeriodIdsAsync(connection, transaction, draft, cancellationToken));
        if (revalidatedPeriodId != periodId)
        {
            throw new AuthoritativePeriodGateException(
                "ACCOUNTING_PERIOD_CHANGED",
                "The accounting period changed while the posting gate was being acquired.");
        }

        const string lockSql = """
            SELECT lock_scope, close_stage, version
            FROM accounting.period_lock_state
            WHERE tenant_id = $1
              AND company_id = $2
              AND period_id = $3
              AND lock_scope IN ($4, $5)
            ORDER BY lock_scope
            """;
        var locks = new List<PeriodLockSnapshot>(2);
        await using (var command = new NpgsqlCommand(lockSql, connection, transaction))
        {
            command.Parameters.AddWithValue(draft.TenantId);
            command.Parameters.AddWithValue(draft.CompanyId);
            command.Parameters.AddWithValue(periodId);
            command.Parameters.AddWithValue((short)PeriodLockScope.GeneralLedger);
            command.Parameters.AddWithValue((short)PeriodLockScope.HardLegal);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                locks.Add(PeriodLockSnapshot.Create(
                    draft.TenantId,
                    draft.CompanyId,
                    periodId,
                    (PeriodLockScope)reader.GetInt16(0),
                    (PeriodCloseStage)reader.GetInt16(1),
                    reader.GetInt64(2)));
            }
        }

        ValidatedPeriodLockSet validated = ValidatedPeriodLockSet.Create(
            draft.TenantId,
            draft.CompanyId,
            periodId,
            locks);
        validated.EnsureStandardPostingAllowed();
        return validated;
    }

    private static async Task<List<Guid>> ReadMatchingPeriodIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ValidatedJournalDraft draft,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT period_id
            FROM accounting.accounting_period
            WHERE tenant_id = $1
              AND company_id = $2
              AND starts_on <= $3
              AND ends_on >= $3
            ORDER BY period_id
            """;
        var periodIds = new List<Guid>(2);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(draft.TenantId);
        command.Parameters.AddWithValue(draft.CompanyId);
        command.Parameters.AddWithValue(draft.EffectiveDate);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            periodIds.Add(reader.GetGuid(0));
        }

        return periodIds;
    }

    private static Guid RequireSinglePeriodId(List<Guid> periodIds)
    {
        if (periodIds.Count == 0)
        {
            throw new AuthoritativePeriodGateException(
                "ACCOUNTING_PERIOD_NOT_FOUND",
                "No accounting period contains the journal effective date.");
        }

        if (periodIds.Count != 1)
        {
            throw new AuthoritativePeriodGateException(
                "ACCOUNTING_PERIOD_AMBIGUOUS",
                "More than one accounting period contains the journal effective date.");
        }

        return periodIds.Single();
    }

    private static async Task AcquirePeriodTransactionLockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid periodId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended($1, 0))",
            connection,
            transaction);
        command.Parameters.AddWithValue(CreateLockKey(periodId));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static string CreateLockKey(Guid periodId) => $"kagu-accounting-period:{periodId:D}";
}

public sealed class AuthoritativePeriodGateException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
