using System.Data;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using KaguERP.Modules.Sales.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.Bootstrap;

/// <summary>Owns draft/read transactions; not registered for HTTP before MP-04 verification.</summary>
public sealed class PostgresSalesDispatchDraftGateway(NpgsqlDataSource dataSource) : ISalesDispatchDraftGateway
{
    public async ValueTask<SalesDispatchDraftOutcome> CreateAsync(AuthorizedSalesDispatchDraftCommand command,
        RequestAuditContext audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateContext(command.Scope, command.CompanyId, audit);
        return await ExecuteAsync((connection, transaction, token) =>
            PostgresSalesDispatchDraftOrchestrator.CreateAsync(connection, transaction, command, audit, token), cancellationToken);
    }

    public async ValueTask<SalesDispatchDraftSnapshot> LoadAsync(AuthorizedSalesDispatchDraftQuery query,
        RequestAuditContext audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateContext(query.Scope, query.CompanyId, audit);
        return await ExecuteAsync((connection, transaction, token) => PostgresSalesDispatchDraftOrchestrator.LoadAsync(
            connection, transaction, query.Scope, query.CompanyId, query.DispatchId, audit, token), cancellationToken);
    }

    public async ValueTask<SalesDispatchReservationPreviewOutcome> PreviewAsync(AuthorizedSalesDispatchDraftQuery query,
        RequestAuditContext audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateContext(query.Scope, query.CompanyId, audit);
        return await ExecuteAsync(async (connection, transaction, token) =>
        {
            var preview = await PostgresSalesDispatchReservationPreview.LoadAsync(connection, transaction,
                query.Scope, query.CompanyId, query.DispatchId, audit, token);
            return Map(preview);
        }, cancellationToken);
    }

    public async ValueTask<SalesDispatchDraftPreparationOutcome> PrepareAsync(AuthorizedSalesDispatchDraftCommand command,
        RequestAuditContext audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateContext(command.Scope, command.CompanyId, audit);
        return await ExecuteAsync(async (connection, transaction, token) =>
        {
            var created = await PostgresSalesDispatchDraftOrchestrator.CreateAsync(connection, transaction, command, audit, token);
            var preview = await PostgresSalesDispatchReservationPreview.LoadAsync(connection, transaction,
                command.Scope, command.CompanyId, command.DispatchId, audit, token);
            return new SalesDispatchDraftPreparationOutcome(created.Created, Map(preview));
        }, cancellationToken);
    }

    private static SalesDispatchReservationPreviewOutcome Map(SalesDispatchReservationPreview preview)
    {
        var lines = preview.Lines.Select(line => new SalesDispatchReservationLineOutcome(
                line.OrderLineId, line.WarehouseId, line.Plan.RequestedQuantity.Value,
                line.Plan.ReservedQuantity.Value, line.Plan.UnreservedQuantity.Value,
                Array.AsReadOnly(line.Plan.Consumptions.Select(part => new SalesDispatchReservationConsumption(
                    part.ReservationId, part.ExpectedVersion, part.Quantity.Value)).ToArray()))).ToArray();
        return new SalesDispatchReservationPreviewOutcome(preview.Draft, Array.AsReadOnly(lines));
    }

    private async ValueTask<T> ExecuteAsync<T>(
        Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, ValueTask<T>> operation, CancellationToken token)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(token);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            T result = await operation(connection, transaction, token);
            // Never return a result before its audit transaction commits. Commit failure can be ambiguous.
            await transaction.CommitAsync(token);
            return result;
        }
        catch (Exception exception) when (MapFailure(exception) is not null)
        {
            // No raw SQL, server detail or inner persistence exception crosses this boundary.
            throw MapFailure(exception)!;
        }
    }

    internal static Exception? MapFailure(Exception exception) => exception switch
    {
        SalesOrderAuthorizationException or InventoryReservationAuthorizationException => new SalesDispatchAccessException(),
        SalesOrderNotFoundException => new SalesDispatchDraftNotFoundException(),
        SalesOrderLifecycleException value => new SalesDispatchPreparationConflictException(value.Code),
        InventoryStockMasterUnavailableException value => new SalesDispatchPreparationConflictException(value.Code),
        InventoryDispatchPreviewConflictException value => new SalesDispatchPreparationConflictException(value.Code),
        InventoryDispatchPreviewLimitException value => new SalesDispatchPreparationConflictException(value.Code),
        InventoryInvariantException value => new SalesDispatchPreparationConflictException(value.Code),
        NpgsqlException => new SalesDispatchUnavailableException(),
        _ => null
    };

    private static void ValidateContext(ExecutionScope scope, Guid companyId, RequestAuditContext audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        if (audit.TenantId != scope.TenantId || audit.ActorId != scope.ActorId ||
            !audit.CompanyIds.SetEquals(scope.CompanyIds))
            throw new SalesDispatchAccessException();
        AuthorizedSalesDispatchDraftCommand.EnsurePermission(scope, companyId);
    }
}
