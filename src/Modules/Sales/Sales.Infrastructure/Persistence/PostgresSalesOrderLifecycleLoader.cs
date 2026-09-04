using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using Npgsql;

namespace KaguERP.Modules.Sales.Infrastructure.Persistence;

public static class PostgresSalesOrderLifecycleLoader
{
    public static async ValueTask<SalesOrderLifecycleView> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuthorizedSalesOrderLifecycleQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(query);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }

        await SetScopeAsync(connection, transaction, query.Scope, query.CompanyId, cancellationToken);
        SalesOrderLifecycleState state = await LoadStateAsync(connection, transaction, query, cancellationToken)
            ?? throw new SalesOrderNotFoundException();
        IReadOnlyList<SalesOrderTransitionEvent> transitions =
            await LoadTransitionsAsync(connection, transaction, query, cancellationToken);
        return new SalesOrderLifecycleView(state, transitions);
    }

    private static async ValueTask<SalesOrderLifecycleState?> LoadStateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuthorizedSalesOrderLifecycleQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT maker_id,version,status
            FROM sales.sales_order
            WHERE tenant_id=$1 AND company_id=$2 AND order_id=$3
            FOR SHARE
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(query.Scope.TenantId);
        command.Parameters.AddWithValue(query.CompanyId);
        command.Parameters.AddWithValue(query.OrderId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return SalesOrderLifecycleState.Rehydrate(
            query.Scope.TenantId,
            query.CompanyId,
            query.OrderId,
            reader.GetGuid(0),
            reader.GetInt64(1),
            (SalesOrderStatus)reader.GetInt16(2));
    }

    private static async ValueTask<IReadOnlyList<SalesOrderTransitionEvent>> LoadTransitionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuthorizedSalesOrderLifecycleQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT event_id,previous_version,new_version,previous_status,new_status,transition,
                   actor_id,correlation_id,occurred_at,reason
            FROM sales.sales_order_transition_event
            WHERE tenant_id=$1 AND company_id=$2 AND order_id=$3
            ORDER BY new_version
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(query.Scope.TenantId);
        command.Parameters.AddWithValue(query.CompanyId);
        command.Parameters.AddWithValue(query.OrderId);
        var transitions = new List<SalesOrderTransitionEvent>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            transitions.Add(new SalesOrderTransitionEvent(
                reader.GetGuid(0),
                query.Scope.TenantId,
                query.CompanyId,
                query.OrderId,
                reader.GetInt64(1),
                reader.GetInt64(2),
                (SalesOrderStatus)reader.GetInt16(3),
                (SalesOrderStatus)reader.GetInt16(4),
                (SalesOrderTransition)reader.GetInt16(5),
                reader.GetGuid(6),
                reader.GetGuid(7),
                reader.GetFieldValue<DateTimeOffset>(8),
                reader.IsDBNull(9) ? null : reader.GetString(9)));
        }

        return transitions;
    }

    private static async ValueTask SetScopeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        scope.EnsureAllowed(scope.TenantId, companyId);
        const string sql = """
            SELECT set_config('app.tenant_id',$1,true),
                   set_config('app.actor_id',$2,true),
                   set_config('app.company_ids',$3,true)
            """;
        await using var context = new NpgsqlCommand(sql, connection, transaction);
        context.Parameters.AddWithValue(scope.TenantId.ToString());
        context.Parameters.AddWithValue(scope.ActorId.ToString());
        context.Parameters.AddWithValue("{" + companyId + "}");
        await context.ExecuteNonQueryAsync(cancellationToken);
    }
}
