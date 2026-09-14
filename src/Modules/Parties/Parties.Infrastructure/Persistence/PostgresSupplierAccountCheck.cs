using KaguERP.BuildingBlocks.Application.Security;
using Npgsql;

namespace KaguERP.Modules.Parties.Infrastructure.Persistence;

/// <summary>Published participant for invoice capture checks. Does not prove party activation or supplier-role lifecycle.</summary>
public static class PostgresSupplierAccountCheck
{
    public static async ValueTask EnsureAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ExecutionScope scope, Guid companyId, Guid accountId, string currency, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection); ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, "purchasing.invoice.view")) throw new UnauthorizedAccessException("Invoice view permission is required.");
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Supplier account check requires the caller's ReadCommitted transaction.");
        if (accountId == Guid.Empty || currency is not { Length: 3 } || currency.Any(c => c is < 'A' or > 'Z'))
            throw new ArgumentException("Account identity and currency are required.");
        await using (var context = new NpgsqlCommand("SELECT set_config('app.tenant_id',$1,true),set_config('app.actor_id',$2,true),set_config('app.company_ids',$3,true)", connection, transaction))
        {
            context.Parameters.AddWithValue(scope.TenantId.ToString("D")); context.Parameters.AddWithValue(scope.ActorId.ToString("D"));
            context.Parameters.AddWithValue("{" + companyId.ToString("D") + "}");
            await context.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var query = new NpgsqlCommand("""
            SELECT party_account_id FROM party.party_account
            WHERE tenant_id=$1 AND company_id=$2 AND party_account_id=$3 AND currency=$4 AND balance_side=2
            FOR SHARE
            """, connection, transaction);
        query.Parameters.AddWithValue(scope.TenantId); query.Parameters.AddWithValue(companyId);
        query.Parameters.AddWithValue(accountId); query.Parameters.AddWithValue(currency);
        if (await query.ExecuteScalarAsync(cancellationToken) is not Guid)
            throw new SupplierAccountUnavailableException();
    }
}

public sealed class SupplierAccountUnavailableException() : InvalidOperationException("A payable account in the invoice company and currency is required.");
