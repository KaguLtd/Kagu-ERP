using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Sales.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.Bootstrap;

/// <summary>
/// Same-transaction sales demand, inventory reservation and audit composition.
/// Not registered as a public gateway while runtime reservation writes remain closed.
/// The caller owns commit/rollback, allowing order confirmation to join the transaction.
/// </summary>
public static class PostgresSalesOrderReservationOrchestrator
{
    public static async ValueTask<InventoryReservationRequestResult> CreateAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, InventoryReservationRequest request,
        RequestAuditContext auditContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(auditContext);
        if (auditContext.TenantId != request.Scope.TenantId || auditContext.ActorId != request.Scope.ActorId ||
            !auditContext.CompanyIds.SetEquals(request.Scope.CompanyIds))
            throw new ArgumentException("Reservation audit context must match the trusted execution scope.", nameof(auditContext));
        if (!request.Scope.HasPermission(request.CompanyId, PostgresSalesOrderReservationDemandSource.RequiredPermission))
            throw new SalesOrderReservationDemandAuthorizationException();
        var source = new PostgresSalesOrderReservationDemandSource(connection, transaction, request.Scope);
        const string savepoint = "sales_inventory_reservation_audit";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            var result = await PostgresInventoryReservationWriter.CreateAsync(
                connection, transaction, request, source, cancellationToken);
            await PostgresAuthorizationAuditWriter.AppendAsync(connection, transaction,
                auditContext with { CompanyIds = new HashSet<Guid> { request.CompanyId } }, Guid.CreateVersion7(),
                new AuthorizationAuditEvent("inventory.reservation.create", "inventory-reservation-request",
                    request.RequestId.ToString("D"), "allowed", "INVENTORY_RESERVATION_REQUEST_RESOLVED"), cancellationToken);
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
}
