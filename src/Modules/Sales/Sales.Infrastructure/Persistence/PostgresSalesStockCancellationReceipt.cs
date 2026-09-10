using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using Npgsql;

namespace KaguERP.Modules.Sales.Infrastructure.Persistence;

/// <summary>Participant in the cancellation transaction; also binds a zero-release result to its effective date.</summary>
public static class PostgresSalesStockCancellationReceipt
{
    public static async ValueTask EnsureAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        AuthorizedSalesOrderTransitionCommand command, DateOnly effectiveDate, bool transitionCreated,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(command);
        if (!ReferenceEquals(transaction.Connection, connection) ||
            transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted ||
            command.Transition != SalesOrderTransition.Cancel)
            throw new ArgumentException("Cancellation receipt requires the same ReadCommitted cancellation transaction.");
        command.Scope.EnsureAllowed(command.Scope.TenantId, command.CompanyId);
        await using (var context = new NpgsqlCommand("""
            SELECT set_config('app.tenant_id',$1,true),set_config('app.company_ids',$2,true),set_config('app.actor_id',$3,true)
            """, connection, transaction))
        {
            context.Parameters.AddWithValue(command.Scope.TenantId.ToString("D"));
            context.Parameters.AddWithValue("{" + command.CompanyId.ToString("D") + "}");
            context.Parameters.AddWithValue(command.Scope.ActorId.ToString("D"));
            await context.ExecuteNonQueryAsync(cancellationToken);
        }
        if (transitionCreated)
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO sales.stock_order_cancellation_receipt
                    (tenant_id,company_id,order_id,correlation_id,effective_date)
                VALUES ($1,$2,$3,$4,$5)
                """, connection, transaction);
            AddIdentity(insert, command);
            insert.Parameters.AddWithValue(effectiveDate);
            await insert.ExecuteNonQueryAsync(cancellationToken);
            return;
        }
        await using var load = new NpgsqlCommand("""
            SELECT effective_date FROM sales.stock_order_cancellation_receipt
            WHERE tenant_id=$1 AND company_id=$2 AND order_id=$3 AND correlation_id=$4
            """, connection, transaction);
        AddIdentity(load, command);
        await using var reader = await load.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.GetFieldValue<DateOnly>(0) != effectiveDate)
            throw new SalesStockCancellationReceiptConflictException();
    }

    private static void AddIdentity(NpgsqlCommand sql, AuthorizedSalesOrderTransitionCommand command)
    {
        sql.Parameters.AddWithValue(command.Scope.TenantId);
        sql.Parameters.AddWithValue(command.CompanyId);
        sql.Parameters.AddWithValue(command.OrderId);
        sql.Parameters.AddWithValue(command.CorrelationId);
    }
}

public sealed class SalesStockCancellationReceiptConflictException()
    : InvalidOperationException("Cancellation receipt is missing or has a different effective date; replay cannot create or change it.")
{
    public string Code { get; } = "SALES_CANCEL_RECEIPT_CONFLICT";
}
