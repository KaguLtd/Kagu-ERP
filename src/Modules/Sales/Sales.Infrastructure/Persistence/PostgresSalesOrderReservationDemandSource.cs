using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Sales.Contracts.Reservations;
using Npgsql;

namespace KaguERP.Modules.Sales.Infrastructure.Persistence;

public sealed class PostgresSalesOrderReservationDemandSource : ISalesOrderReservationDemandSource
{
    public const string RequiredPermission = "sales.order.confirm";

    private readonly NpgsqlConnection connection;
    private readonly NpgsqlTransaction transaction;
    private readonly ExecutionScope scope;

    public PostgresSalesOrderReservationDemandSource(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException(
                "The transaction does not belong to the supplied connection.", nameof(transaction));
        }

        this.connection = connection;
        this.transaction = transaction;
        this.scope = scope;
    }

    public async ValueTask<SalesOrderReservationDemandSnapshot?> LoadAsync(
        SalesOrderReservationDemandQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        scope.EnsureAllowed(query.TenantId, query.CompanyId);
        if (!scope.HasPermission(query.CompanyId, RequiredPermission))
        {
            throw new SalesOrderReservationDemandAuthorizationException();
        }

        await SetScopeAsync(query.CompanyId, cancellationToken);
        const string headerSql = """
            SELECT 1
            FROM sales.sales_order
            WHERE tenant_id=$1 AND company_id=$2 AND order_id=$3
              AND version=$4 AND status=4
            FOR SHARE
            """;
        await using (var header = new NpgsqlCommand(headerSql, connection, transaction))
        {
            header.Parameters.AddWithValue(query.TenantId);
            header.Parameters.AddWithValue(query.CompanyId);
            header.Parameters.AddWithValue(query.OrderId);
            header.Parameters.AddWithValue(query.ConfirmedVersion);
            if (await header.ExecuteScalarAsync(cancellationToken) is null)
            {
                return null;
            }
        }

        const string lineSql = """
            SELECT order_line_id,item_id,base_uom_code,ordered_base_quantity
            FROM sales.sales_order_line
            WHERE tenant_id=$1 AND company_id=$2 AND order_id=$3
            ORDER BY order_line_id
            """;
        await using var linesCommand = new NpgsqlCommand(lineSql, connection, transaction);
        linesCommand.Parameters.AddWithValue(query.TenantId);
        linesCommand.Parameters.AddWithValue(query.CompanyId);
        linesCommand.Parameters.AddWithValue(query.OrderId);
        var lines = new List<SalesOrderReservationDemandLine>();
        await using NpgsqlDataReader reader = await linesCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            lines.Add(SalesOrderReservationDemandLine.Create(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetDecimal(3)));
        }

        return SalesOrderReservationDemandSnapshot.Create(
            query.TenantId,
            query.CompanyId,
            query.OrderId,
            query.ConfirmedVersion,
            lines);
    }

    private async ValueTask SetScopeAsync(Guid companyId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT set_config('app.tenant_id',$1,true),
                   set_config('app.actor_id',$2,true),
                   set_config('app.company_ids',$3,true)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(scope.TenantId.ToString());
        command.Parameters.AddWithValue(scope.ActorId.ToString());
        command.Parameters.AddWithValue("{" + companyId + "}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed class SalesOrderReservationDemandAuthorizationException()
    : InvalidOperationException("The active actor cannot load sales-order reservation demand.")
{
    public string Code { get; } = "SALES_RESERVATION_DEMAND_PERMISSION_REQUIRED";
}
