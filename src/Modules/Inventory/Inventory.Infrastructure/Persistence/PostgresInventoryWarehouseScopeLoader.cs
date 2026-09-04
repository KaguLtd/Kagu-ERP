using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Transfers;
using Npgsql;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public static class PostgresInventoryWarehouseScopeLoader
{
    public static async ValueTask<InventoryWarehouseScopeEvidence> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }

        scope.EnsureAllowed(scope.TenantId, companyId);
        const string contextSql = """
            SELECT
                set_config('app.tenant_id', $1, true),
                set_config('app.actor_id', $2, true),
                set_config('app.company_ids', $3, true)
            """;
        await using (var context = new NpgsqlCommand(contextSql, connection, transaction))
        {
            context.Parameters.AddWithValue(scope.TenantId.ToString());
            context.Parameters.AddWithValue(scope.ActorId.ToString());
            context.Parameters.AddWithValue("{" + companyId + "}");
            await context.ExecuteNonQueryAsync(cancellationToken);
        }

        const string sql = """
            SELECT warehouse_id
            FROM iam.user_warehouse_scope
            WHERE tenant_id=$1 AND company_id=$2 AND user_profile_id=$3
              AND valid_from <= clock_timestamp()
              AND (valid_to IS NULL OR valid_to > clock_timestamp())
            ORDER BY warehouse_id
            FOR SHARE
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(scope.TenantId);
        command.Parameters.AddWithValue(companyId);
        command.Parameters.AddWithValue(scope.ActorId);
        var warehouseIds = new List<Guid>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            warehouseIds.Add(reader.GetGuid(0));
        }

        return InventoryWarehouseScopeEvidence.Create(
            scope.TenantId,
            companyId,
            scope.ActorId,
            warehouseIds);
    }
}
