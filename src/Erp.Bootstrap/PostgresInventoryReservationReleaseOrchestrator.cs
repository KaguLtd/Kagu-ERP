using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.Bootstrap;

/// <summary>Internal release plus audit transaction participant; no public gateway or privilege grant.</summary>
public static class PostgresInventoryReservationReleaseOrchestrator
{
    public static async ValueTask<InventoryReservationReleaseResult> ReleaseAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, InventoryReservationReleaseRequest request, RequestAuditContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Release audit requires the caller's ReadCommitted transaction.");
        if (context.TenantId != request.Scope.TenantId || context.ActorId != request.Scope.ActorId ||
            !context.CompanyIds.SetEquals(request.Scope.CompanyIds))
            throw new ArgumentException("Release audit context must match trusted execution scope.", nameof(context));
        const string savepoint = "inventory_release_with_audit";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            var result = await PostgresInventoryReservationReleaseWriter.ReleaseAsync(connection, transaction, request, cancellationToken);
            await PostgresAuthorizationAuditWriter.AppendAsync(connection, transaction,
                context with { CompanyIds = new HashSet<Guid> { request.CompanyId } }, Guid.CreateVersion7(),
                new AuthorizationAuditEvent("inventory.reservation.release", "inventory-reservation",
                    request.ReservationId.ToString("D"), "allowed", "INVENTORY_RESERVATION_RELEASE_RESOLVED"), cancellationToken);
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
