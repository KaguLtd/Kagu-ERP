using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Treasury.Contracts.Reconciliation;
using Npgsql;

namespace KaguERP.Modules.Treasury.Infrastructure.Persistence;

public static class PostgresApprovedReconciliationTransitPostingLoader
{
    public static async ValueTask<ApprovedReconciliationTransitPostingBatch?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        Guid reconciliationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
        if (reconciliationId == Guid.Empty)
        {
            throw new ArgumentException("Reconciliation ID is required.", nameof(reconciliationId));
        }
        scope.EnsureAllowed(scope.TenantId, companyId);

        const string sql = """
            SELECT proposal.treasury_account_id,proposal.currency,approval.recorded_at,
                   statement.statement_line_id,statement.booking_date,statement.signed_amount,
                   match.movement_id,match.matched_amount,payment.transaction_amount,
                   payment.functional_amount,payment.transaction_currency,payment.functional_currency,
                   payment.rate_snapshot_id,payment.rate_version,payment.rate_type,payment.rate_source,
                   payment.rate_date,payment.functional_units_numerator,payment.transaction_units_denominator,
                   payment.rounding_policy_id,payment.rounding_policy_version,payment.rounding_scale,
                   payment.rounding_mode
            FROM treasury.reconciliation_approval approval
            JOIN treasury.reconciliation_proposal proposal
              USING (tenant_id,company_id,reconciliation_id)
            JOIN treasury.reconciliation_proposal_match match
              USING (tenant_id,company_id,reconciliation_id)
            JOIN treasury.statement_line statement
              USING (tenant_id,company_id,statement_line_id)
            JOIN treasury.reconciliation_approved_statement approved_statement
              ON approved_statement.tenant_id=approval.tenant_id
             AND approved_statement.company_id=approval.company_id
             AND approved_statement.reconciliation_id=approval.reconciliation_id
             AND approved_statement.statement_line_id=statement.statement_line_id
            JOIN treasury.reconciliation_approved_movement approved_movement
              ON approved_movement.tenant_id=approval.tenant_id
             AND approved_movement.company_id=approval.company_id
             AND approved_movement.reconciliation_id=approval.reconciliation_id
             AND approved_movement.movement_id=match.movement_id
            JOIN treasury.payment_economic_event payment
              ON payment.tenant_id=approval.tenant_id
             AND payment.company_id=approval.company_id
             AND payment.payment_id=match.movement_id
            WHERE approval.tenant_id=$1 AND approval.company_id=$2 AND approval.reconciliation_id=$3
            ORDER BY statement.booking_date,statement.statement_line_id,match.movement_id
            """;

        Guid treasuryAccountId = default;
        string? currency = null;
        DateTimeOffset recordedAt = default;
        var rows = new List<TransitRow>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue(scope.TenantId);
            command.Parameters.AddWithValue(companyId);
            command.Parameters.AddWithValue(reconciliationId);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                treasuryAccountId = reader.GetGuid(0);
                currency = reader.GetString(1);
                recordedAt = reader.GetFieldValue<DateTimeOffset>(2);
                rows.Add(new TransitRow(
                    reader.GetGuid(3),
                    reader.GetFieldValue<DateOnly>(4),
                    reader.GetDecimal(5),
                    reader.GetGuid(6),
                    reader.GetDecimal(7),
                    reader.GetDecimal(8),
                    reader.GetDecimal(9),
                    ReconciliationTransitPaymentRateEvidence.Create(
                        scope.TenantId, companyId, reader.GetGuid(12), reader.GetInt64(13),
                        reader.GetString(10), reader.GetString(11), reader.GetString(14), reader.GetString(15),
                        reader.GetFieldValue<DateOnly>(16), reader.GetDecimal(17), reader.GetDecimal(18),
                        reader.GetGuid(19), reader.GetInt64(20), reader.GetInt16(21), reader.GetInt16(22))));
            }
        }

        if (rows.Count == 0)
        {
            return null;
        }

        ReconciliationTransitStatementFact[] statements = rows
            .GroupBy(row => new { row.StatementLineId, row.BookingDate, row.SignedAmount })
            .Select(group => ReconciliationTransitStatementFact.Create(
                group.Key.StatementLineId,
                group.Key.BookingDate,
                group.Key.SignedAmount > decimal.Zero
                    ? ReconciliationTransitDirection.Incoming
                    : ReconciliationTransitDirection.Outgoing,
                Math.Abs(group.Key.SignedAmount),
                group.Select(row => ReconciliationTransitPaymentMatch.Create(
                    row.PaymentId, row.MatchedAmount, row.PaymentTransactionAmount,
                    row.PaymentFunctionalAmount, row.RateEvidence))))
            .ToArray();

        return ApprovedReconciliationTransitPostingBatch.Create(
            scope.TenantId,
            companyId,
            reconciliationId,
            treasuryAccountId,
            currency!,
            recordedAt,
            statements);
    }

    private sealed record TransitRow(
        Guid StatementLineId,
        DateOnly BookingDate,
        decimal SignedAmount,
        Guid PaymentId,
        decimal MatchedAmount,
        decimal PaymentTransactionAmount,
        decimal PaymentFunctionalAmount,
        ReconciliationTransitPaymentRateEvidence RateEvidence);
}
