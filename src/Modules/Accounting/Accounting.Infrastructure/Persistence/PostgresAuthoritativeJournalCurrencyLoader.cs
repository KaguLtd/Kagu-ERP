using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Accounting.Domain.Currencies;
using KaguERP.Modules.Accounting.Domain.Journals;
using Npgsql;

namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

public static class PostgresAuthoritativeJournalCurrencyLoader
{
    public static async ValueTask<ValidatedJournalCurrencySet> LoadAsync(
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
        ValidatedJournalCurrencySet validated = ValidatedJournalCurrencySet.Create(draft);
        Guid[] rateIds = validated.LineAmounts.Select(item => item.ExchangeRate.RateSnapshotId).Distinct().Order().ToArray();
        Guid[] policyIds = validated.LineAmounts.Select(item => item.RoundingPolicy.PolicyId).Distinct().Order().ToArray();
        Dictionary<Guid, ExchangeRateSnapshot> rates = await LoadRatesAsync(
            connection, transaction, draft, rateIds, cancellationToken);
        Dictionary<Guid, RoundingPolicySnapshot> policies = await LoadPoliciesAsync(
            connection, transaction, draft, policyIds, cancellationToken);

        foreach (JournalCurrencyAmountSnapshot amount in validated.LineAmounts)
        {
            if (!rates.TryGetValue(amount.ExchangeRate.RateSnapshotId, out ExchangeRateSnapshot? rate) ||
                rate != amount.ExchangeRate)
            {
                throw new AuthoritativeCurrencyEvidenceException(
                    "EXCHANGE_RATE_EVIDENCE_MISMATCH",
                    "The journal exchange-rate snapshot does not exactly match authoritative evidence.");
            }

            if (!policies.TryGetValue(amount.RoundingPolicy.PolicyId, out RoundingPolicySnapshot? policy) ||
                policy != amount.RoundingPolicy)
            {
                throw new AuthoritativeCurrencyEvidenceException(
                    "ROUNDING_POLICY_EVIDENCE_MISMATCH",
                    "The journal rounding-policy snapshot does not exactly match authoritative evidence.");
            }
        }

        return validated;
    }

    private static async Task<Dictionary<Guid, ExchangeRateSnapshot>> LoadRatesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ValidatedJournalDraft draft,
        Guid[] ids,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT rate_snapshot_id, version, transaction_currency, functional_currency,
                   rate_type, source, rate_date, functional_units_numerator, transaction_units_denominator
            FROM accounting.exchange_rate_snapshot
            WHERE tenant_id = $1 AND company_id = $2 AND rate_snapshot_id = ANY($3)
            ORDER BY rate_snapshot_id
            """;
        var result = new Dictionary<Guid, ExchangeRateSnapshot>();
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(draft.TenantId);
        command.Parameters.AddWithValue(draft.CompanyId);
        command.Parameters.AddWithValue(ids);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            Guid id = reader.GetGuid(0);
            result.Add(id, ExchangeRateSnapshot.Create(
                draft.TenantId, draft.CompanyId, id, reader.GetInt64(1),
                CurrencyCode.Create(reader.GetString(2).Trim()), CurrencyCode.Create(reader.GetString(3).Trim()),
                reader.GetString(4), reader.GetString(5), reader.GetFieldValue<DateOnly>(6),
                reader.GetDecimal(7), reader.GetDecimal(8)));
        }

        return result;
    }

    private static async Task<Dictionary<Guid, RoundingPolicySnapshot>> LoadPoliciesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ValidatedJournalDraft draft,
        Guid[] ids,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT policy_id, version, scale, rounding_mode
            FROM accounting.rounding_policy_snapshot
            WHERE tenant_id = $1 AND company_id = $2 AND policy_id = ANY($3)
            ORDER BY policy_id
            """;
        var result = new Dictionary<Guid, RoundingPolicySnapshot>();
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(draft.TenantId);
        command.Parameters.AddWithValue(draft.CompanyId);
        command.Parameters.AddWithValue(ids);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            Guid id = reader.GetGuid(0);
            result.Add(id, RoundingPolicySnapshot.Create(
                draft.TenantId, draft.CompanyId, id, reader.GetInt64(1), reader.GetInt16(2),
                (RoundingMode)reader.GetInt16(3)));
        }

        return result;
    }
}

public sealed class AuthoritativeCurrencyEvidenceException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
