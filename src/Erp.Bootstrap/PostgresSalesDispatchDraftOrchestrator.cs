using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.Bootstrap;

/// <summary>Internal draft use case. Caller owns commit; no allocation, stock movement or GL is written.</summary>
public static class PostgresSalesDispatchDraftOrchestrator
{
    public static async ValueTask<SalesDispatchDraftOutcome> CreateAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, AuthorizedSalesDispatchDraftCommand command, RequestAuditContext audit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateTransaction(connection, transaction);
        Validate(command.Scope, audit);
        const string savepoint = "sales_dispatch_draft_audit";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            var result = await PostgresSalesDispatchDraftStore.CreateAsync(connection, transaction, command,
                AuthorizeWarehousesAsync, cancellationToken);
            await AppendAuditAsync(connection, transaction, command.CompanyId, command.DispatchId, audit,
                "dispatch.create", result.Created ? "SALES_DISPATCH_DRAFT_CREATED" : "SALES_DISPATCH_DRAFT_REPLAYED", cancellationToken);
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }

    public static async ValueTask<SalesDispatchDraftSnapshot> LoadAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, Guid companyId, Guid dispatchId,
        RequestAuditContext audit, CancellationToken cancellationToken = default)
    {
        ValidateTransaction(connection, transaction);
        Validate(scope, audit);
        var result = await PostgresSalesDispatchDraftStore.LoadAsync(connection, transaction, scope, companyId,
            dispatchId, AuthorizeWarehousesAsync, cancellationToken);
        await AppendAuditAsync(connection, transaction, companyId, dispatchId, audit,
            "dispatch.draft.view", "SALES_DISPATCH_DRAFT_VIEW_ALLOWED", cancellationToken);
        return result;
    }

    private static async ValueTask AuthorizeWarehousesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ExecutionScope scope, Guid companyId, IReadOnlyList<Guid> warehouseIds, CancellationToken cancellationToken)
    {
        var evidence = await PostgresInventoryWarehouseScopeLoader.LoadAsync(connection, transaction, scope, companyId, cancellationToken);
        evidence.EnsureMatches(scope.TenantId, companyId, scope.ActorId);
        if (warehouseIds.Any(warehouse => !evidence.WarehouseIds.Contains(warehouse)))
            throw new SalesOrderAuthorizationException("SALES_DISPATCH_WAREHOUSE_SCOPE_REQUIRED",
                "The active actor must be scoped to every selected dispatch warehouse.");
    }

    private static ValueTask AppendAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid companyId, Guid dispatchId, RequestAuditContext audit, string action, string code, CancellationToken cancellationToken) =>
        PostgresAuthorizationAuditWriter.AppendAsync(connection, transaction,
            audit with { CompanyIds = new HashSet<Guid> { companyId } }, Guid.CreateVersion7(),
            new AuthorizationAuditEvent(action, "sales-dispatch-draft", dispatchId.ToString("D"), "allowed", code), cancellationToken);

    private static void Validate(ExecutionScope scope, RequestAuditContext audit)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(audit);
        if (scope.TenantId != audit.TenantId || scope.ActorId != audit.ActorId || !scope.CompanyIds.SetEquals(audit.CompanyIds))
            throw new ArgumentException("Dispatch audit context must match the trusted execution scope.");
    }

    private static void ValidateTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Dispatch orchestration requires the caller's ReadCommitted transaction.");
    }
}
