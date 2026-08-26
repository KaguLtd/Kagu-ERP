using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Treasury.Domain.Payments;
using Npgsql;

namespace KaguERP.Modules.Treasury.Infrastructure.Persistence;

public static class PostgresPaymentEconomicEventLoader
{
    public static async ValueTask<ValidatedPaymentEconomicEventDraft?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("Payment ID is required.", nameof(paymentId));
        }
        scope.EnsureAllowed(scope.TenantId, companyId);

        const string sql = """
            SELECT party_account_id,treasury_account_id,direction,transaction_amount,functional_amount,
                   transaction_currency,functional_currency,effective_date,recorded_at,source_type,
                   source_event_id,posting_purpose,rate_snapshot_id,rate_version,rate_type,rate_source,
                   rate_date,functional_units_numerator,transaction_units_denominator
            FROM treasury.payment_economic_event
            WHERE tenant_id=$1 AND company_id=$2 AND payment_id=$3
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(scope.TenantId);
        command.Parameters.AddWithValue(companyId);
        command.Parameters.AddWithValue(paymentId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var transactionCurrency = TreasuryCurrencyCode.Create(reader.GetString(5));
        var functionalCurrency = TreasuryCurrencyCode.Create(reader.GetString(6));
        SameCurrencyPaymentRateSnapshot rate = SameCurrencyPaymentRateSnapshot.Create(
            scope.TenantId,
            companyId,
            reader.GetGuid(12),
            reader.GetInt64(13),
            transactionCurrency,
            functionalCurrency,
            reader.GetString(14),
            reader.GetString(15),
            reader.GetFieldValue<DateOnly>(16),
            reader.GetDecimal(17),
            reader.GetDecimal(18));
        return ValidatedPaymentEconomicEventDraft.Create(
            paymentId,
            scope.TenantId,
            companyId,
            reader.GetGuid(0),
            reader.GetGuid(1),
            (PaymentDirection)reader.GetInt16(2),
            reader.GetDecimal(3),
            reader.GetDecimal(4),
            reader.GetFieldValue<DateOnly>(7),
            reader.GetFieldValue<DateTimeOffset>(8),
            reader.GetString(9),
            reader.GetGuid(10),
            reader.GetString(11),
            rate);
    }
}
