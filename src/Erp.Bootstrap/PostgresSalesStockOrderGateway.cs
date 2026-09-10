using System.Data;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using KaguERP.Modules.Sales.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.Bootstrap;

/// <summary>
/// Composition-root adapter; deliberately not registered for HTTP until the MP-04 runtime gate.
/// Never substitutes an owner connection or elevates the supplied actor's permissions.
/// </summary>
public sealed class PostgresSalesStockOrderGateway(NpgsqlDataSource dataSource) : ISalesStockOrderGateway
{
    public async ValueTask<SalesStockOrderConfirmationOutcome> ConfirmAsync(
        SalesStockOrderConfirmationCommand command, RequestAuditContext auditContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateContext(command.Transition, auditContext, "inventory.reservation.create");
        var transition = command.Transition;
        var requests = command.Lines.Select(line => new InventoryReservationRequest(
            transition.Scope, transition.CompanyId,
            PostgresSalesOrderConfirmationOrchestrator.ReservationRequestId(transition.CorrelationId, line.OrderLineId),
            line.WarehouseId, InventoryDemandSourceIdentity.Create("sales.order", transition.OrderId,
                line.OrderLineId, checked(transition.ExpectedVersion + 1)),
            InventoryQuantity.Create(line.Quantity), command.EffectiveDate)).ToArray();

        return await ExecuteAsync(async (connection, transaction, token) =>
        {
            var result = await PostgresSalesOrderConfirmationOrchestrator.ConfirmAsync(
                connection, transaction, transition, requests, auditContext, token);
            // Match by stable identity, never by database iteration order.
            var byRequest = result.Reservations.ToDictionary(reservation => reservation.RequestId);
            var reservations = command.Lines.Select(line =>
            {
                var reservation = byRequest[PostgresSalesOrderConfirmationOrchestrator.ReservationRequestId(
                    transition.CorrelationId, line.OrderLineId)];
                return new SalesStockOrderReservationOutcome(line.OrderLineId, line.WarehouseId,
                    reservation.RequestId, reservation.ReservationId, reservation.RequestedQuantity.Value,
                    reservation.ReservedQuantity.Value, reservation.RecordedAt);
            }).ToArray();
            return new SalesStockOrderConfirmationOutcome(Map(result.Confirmation), Array.AsReadOnly(reservations));
        }, cancellationToken);
    }

    public async ValueTask<SalesStockOrderCancellationOutcome> CancelAsync(
        SalesStockOrderCancellationCommand command, RequestAuditContext auditContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateContext(command.Transition, auditContext, InventoryReservationReleaseRequest.RequiredPermission);
        return await ExecuteAsync(async (connection, transaction, token) =>
        {
            var result = await PostgresSalesOrderCancellationOrchestrator.CancelAsync(
                connection, transaction, command.Transition, command.EffectiveDate, auditContext, token);
            return new SalesStockOrderCancellationOutcome(Map(result.Cancellation), Array.AsReadOnly(
                result.Releases.Select(release => new SalesStockOrderReleaseOutcome(release.ReservationId,
                    release.EventId, release.Version, release.ReleasedQuantity.Value,
                    release.ConsumedQuantity.Value, release.RecordedAt)).ToArray()));
        }, cancellationToken);
    }

    private async ValueTask<T> ExecuteAsync<T>(
        Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, ValueTask<T>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            T result = await operation(connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (MapFailure(exception) is not null)
        {
            // No SQL detail, document text or credentials cross the application boundary.
            // A connection loss during COMMIT is ambiguous: callers must retry the SAME key.
            throw MapFailure(exception)!;
        }
    }

    internal static Exception? MapFailure(Exception exception) => exception switch
    {
        InventoryReservationAuthorizationException => new SalesStockOrderAccessException(),
        SalesOrderNotFoundException => new SalesOrderGatewayNotFoundException(),
        SalesOrderPersistenceConflictException value => Conflict(value.Code),
        SalesStockCancellationReceiptConflictException value => Conflict(value.Code),
        SalesOrderLifecycleException value => Conflict(value.Code),
        InventoryInvariantException value => Conflict(value.Code),
        InventoryReservationRequestConflictException value => Conflict(value.Code),
        InventoryReservationReleaseConflictException value => Conflict(value.Code),
        InventoryReservationReleaseUnavailableException value => Conflict(value.Code),
        InventoryReservationDemandUnavailableException value => Conflict(value.Code),
        InventoryReservationDemandLineUnavailableException value => Conflict(value.Code),
        InventoryStockMasterUnavailableException value => Conflict(value.Code),
        InventorySourceReservationLimitException value => Conflict(value.Code),
        SalesOrderConfirmationReservationConflictException value => Conflict(value.Code),
        SalesOrderCancellationReservationConflictException value => Conflict(value.Code),
        InventoryReservationDemandContractMismatchException => new SalesStockOrderUnavailableException(),
        NpgsqlException => new SalesStockOrderUnavailableException(),
        _ => null,
    };

    private static SalesOrderGatewayConflictException Conflict(string code) =>
        new(code, "The stock-order operation conflicts with its current state or immutable request. No new result was returned.");

    private static SalesOrderLifecyclePersistenceOutcome Map(SalesOrderLifecyclePersistenceResult result) =>
        new(result.State, result.Commitment, result.Event, result.Created);

    private static void ValidateContext(AuthorizedSalesOrderTransitionCommand transition,
        RequestAuditContext auditContext, string inventoryPermission)
    {
        ArgumentNullException.ThrowIfNull(auditContext);
        if (auditContext.TenantId != transition.Scope.TenantId || auditContext.ActorId != transition.Scope.ActorId ||
            !auditContext.CompanyIds.SetEquals(transition.Scope.CompanyIds) ||
            !transition.Scope.HasPermission(transition.CompanyId, inventoryPermission))
            throw new SalesStockOrderAccessException();
    }
}
