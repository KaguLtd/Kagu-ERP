using KaguERP.BuildingBlocks.Application.Security;
using Npgsql;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public sealed record InvoiceItemCheckLine(Guid ItemId, string BaseUomCode, decimal Quantity);

/// <summary>Current master check only; neither receipt availability nor stock posting eligibility.</summary>
public static class PostgresInvoiceItemCheck
{
    public static async ValueTask EnsureAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ExecutionScope scope, Guid companyId, IEnumerable<InvoiceItemCheckLine> lines, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection); ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope); ArgumentNullException.ThrowIfNull(lines);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, "purchasing.invoice.view")) throw new UnauthorizedAccessException("Invoice view permission is required.");
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Invoice item check requires the caller's ReadCommitted transaction.");
        var rows = lines.Take(501).ToArray();
        if (rows.Length is < 1 or > 500 || rows.Any(row => row is null || row.ItemId == Guid.Empty ||
            string.IsNullOrWhiteSpace(row.BaseUomCode) || row.Quantity is <= 0m or > 99999999999999.999999m ||
            decimal.Round(row.Quantity, 6) != row.Quantity))
            throw new ArgumentException("Bounded invoice item lines and exact positive quantities are required.", nameof(lines));
        await using (var context = new NpgsqlCommand("SELECT set_config('app.tenant_id',$1,true),set_config('app.actor_id',$2,true),set_config('app.company_ids',$3,true)", connection, transaction))
        {
            context.Parameters.AddWithValue(scope.TenantId.ToString("D")); context.Parameters.AddWithValue(scope.ActorId.ToString("D"));
            context.Parameters.AddWithValue("{" + companyId.ToString("D") + "}");
            await context.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var query = new NpgsqlCommand("""
            SELECT i.item_id,i.base_uom_code,i.quantity_scale
            FROM inventory.item i JOIN inventory.item_company c ON c.tenant_id=i.tenant_id AND c.item_id=i.item_id
            WHERE i.tenant_id=$1 AND c.company_id=$2 AND i.item_id=ANY($3) AND i.is_active AND c.is_active
            ORDER BY i.item_id FOR SHARE OF i,c
            """, connection, transaction);
        var ids = rows.Select(row => row.ItemId).Distinct().ToArray();
        query.Parameters.AddWithValue(scope.TenantId); query.Parameters.AddWithValue(companyId); query.Parameters.AddWithValue(ids);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        int found = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            Guid id = reader.GetGuid(0); string uom = reader.GetString(1); short scale = reader.GetInt16(2);
            if (scale is < 0 or > 6 || rows.Where(row => row.ItemId == id).Any(row =>
                row.BaseUomCode != uom || decimal.Round(row.Quantity, scale) != row.Quantity))
                throw new InvoiceItemUnavailableException();
            found++;
        }
        if (found != ids.Length) throw new InvoiceItemUnavailableException();
    }
}

public sealed class InvoiceItemUnavailableException() : InvalidOperationException("Active company items with matching base UOM and quantity scale are required.");
