using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Treasury.Domain.Payments;
using Npgsql;

namespace KaguERP.Modules.Treasury.Infrastructure.Persistence;

public sealed record PaymentEconomicEventPersistenceResult(Guid PaymentId, bool Created);

public static class PostgresPaymentEconomicEventWriter
{
    public static async ValueTask<PaymentEconomicEventPersistenceResult> PersistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        ValidatedPaymentEconomicEventDraft payment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(payment);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        scope.EnsureAllowed(payment.TenantId, payment.CompanyId);

        const string insertSql = """
            INSERT INTO treasury.payment_economic_event
                (tenant_id,company_id,payment_id,party_account_id,treasury_account_id,direction,
                 transaction_amount,functional_amount,transaction_currency,functional_currency,
                 effective_date,recorded_at,recorded_by,source_type,source_event_id,posting_purpose,
                 rate_snapshot_id,rate_version,rate_type,rate_source,rate_date,
                 functional_units_numerator,transaction_units_denominator,rounding_policy_id,
                 rounding_policy_version,rounding_scale,rounding_mode,unrounded_functional_amount,
                 rounding_difference)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,$23,
                   $24,$25,$26,$27,$28,$29)
            ON CONFLICT (tenant_id,company_id,source_type,source_event_id,posting_purpose) DO NOTHING
            RETURNING payment_id
            """;
        await using (var insert = new NpgsqlCommand(insertSql, connection, transaction))
        {
            AddParameters(insert, scope, payment);
            object? inserted = await insert.ExecuteScalarAsync(cancellationToken);
            if (inserted is Guid paymentId)
            {
                return new PaymentEconomicEventPersistenceResult(paymentId, true);
            }
        }

        const string existingSql = """
            SELECT payment_id,party_account_id,treasury_account_id,direction,transaction_amount,
                   functional_amount,transaction_currency,functional_currency,effective_date,recorded_at,
                   rate_snapshot_id,rate_version,rate_type,rate_source,rate_date,
                   functional_units_numerator,transaction_units_denominator,rounding_policy_id,
                   rounding_policy_version,rounding_scale,rounding_mode,unrounded_functional_amount,
                   rounding_difference
            FROM treasury.payment_economic_event
            WHERE tenant_id=$1 AND company_id=$2 AND source_type=$3 AND source_event_id=$4
              AND posting_purpose=$5
            """;
        await using var existing = new NpgsqlCommand(existingSql, connection, transaction);
        existing.Parameters.AddWithValue(payment.TenantId);
        existing.Parameters.AddWithValue(payment.CompanyId);
        existing.Parameters.AddWithValue(payment.SourceIdentity.SourceType);
        existing.Parameters.AddWithValue(payment.SourceIdentity.SourceEventId);
        existing.Parameters.AddWithValue(payment.SourceIdentity.PostingPurpose);
        await using NpgsqlDataReader reader = await existing.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Payment is not visible after its source uniqueness conflict.");
        }
        Guid existingPaymentId = reader.GetGuid(0);
        PaymentRateSnapshot rate = payment.RateSnapshot;
        if (existingPaymentId != payment.PaymentId || reader.GetGuid(1) != payment.PartyAccountId ||
            reader.GetGuid(2) != payment.TreasuryAccountId || reader.GetInt16(3) != (short)payment.Direction ||
            reader.GetDecimal(4) != payment.TransactionAmount || reader.GetDecimal(5) != payment.FunctionalAmount ||
            reader.GetString(6) != rate.TransactionCurrency.Value ||
            reader.GetString(7) != rate.FunctionalCurrency.Value ||
            reader.GetFieldValue<DateOnly>(8) != payment.EffectiveDate ||
            reader.GetFieldValue<DateTimeOffset>(9) != payment.RecordedAt || reader.GetGuid(10) != rate.RateSnapshotId ||
            reader.GetInt64(11) != rate.Version || reader.GetString(12) != rate.RateType ||
            reader.GetString(13) != rate.Source || reader.GetFieldValue<DateOnly>(14) != rate.RateDate ||
            reader.GetDecimal(15) != rate.FunctionalUnitsNumerator ||
            reader.GetDecimal(16) != rate.TransactionUnitsDenominator || reader.GetGuid(17) != rate.RoundingPolicyId ||
            reader.GetInt64(18) != rate.RoundingPolicyVersion || reader.GetInt16(19) != rate.RoundingScale ||
            reader.GetInt16(20) != 2 || reader.GetDecimal(21) != payment.UnroundedFunctionalAmount ||
            reader.GetDecimal(22) != payment.RoundingDifference)
        {
            throw new PaymentEconomicEventPersistenceConflictException(existingPaymentId);
        }
        return new PaymentEconomicEventPersistenceResult(existingPaymentId, false);
    }

    private static void AddParameters(
        NpgsqlCommand command,
        ExecutionScope scope,
        ValidatedPaymentEconomicEventDraft payment)
    {
        PaymentRateSnapshot rate = payment.RateSnapshot;
        object[] values =
        [
            payment.TenantId, payment.CompanyId, payment.PaymentId, payment.PartyAccountId,
            payment.TreasuryAccountId, (short)payment.Direction, payment.TransactionAmount,
            payment.FunctionalAmount, rate.TransactionCurrency.Value, rate.FunctionalCurrency.Value,
            payment.EffectiveDate, payment.RecordedAt, scope.ActorId, payment.SourceIdentity.SourceType,
            payment.SourceIdentity.SourceEventId, payment.SourceIdentity.PostingPurpose, rate.RateSnapshotId,
            rate.Version, rate.RateType, rate.Source, rate.RateDate, rate.FunctionalUnitsNumerator,
            rate.TransactionUnitsDenominator, rate.RoundingPolicyId, rate.RoundingPolicyVersion,
            (short)rate.RoundingScale, (short)2, payment.UnroundedFunctionalAmount, payment.RoundingDifference,
        ];
        foreach (object value in values)
        {
            command.Parameters.AddWithValue(value);
        }
    }
}

public sealed class PaymentEconomicEventPersistenceConflictException(Guid existingPaymentId)
    : InvalidOperationException("The canonical payment source already has different immutable content.")
{
    public string Code { get; } = "PAYMENT_SOURCE_CONFLICT";
    public Guid ExistingPaymentId { get; } = existingPaymentId;
}
