using System.Security.Cryptography;
using System.Text;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using KaguERP.Modules.Sales.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.Bootstrap;

public sealed record SalesOrderCancellationResult(SalesOrderLifecyclePersistenceResult Cancellation,
    IReadOnlyList<InventoryReservationReleaseResult> Releases);

/// <summary>Internal cancel + release composition. Existing cancel and release permissions are both required; no elevation.</summary>
public static class PostgresSalesOrderCancellationOrchestrator
{
    private static Guid ReleaseCorrelation(Guid cancellationId, Guid reservationId)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"kagu.sales.cancel-release.v1/{cancellationId:D}/{reservationId:D}"));
        hash[6] = (byte)((hash[6] & 0x0f) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }

    public static async ValueTask<SalesOrderCancellationResult> CancelAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, AuthorizedSalesOrderTransitionCommand command, DateOnly effectiveDate,
        RequestAuditContext auditContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(auditContext);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Cancellation requires the caller's ReadCommitted transaction.");
        string reason = command.Reason?.Trim() ?? string.Empty;
        if (command.Transition != SalesOrderTransition.Cancel || reason.Length is < 1 or > 500 ||
            auditContext.TenantId != command.Scope.TenantId || auditContext.ActorId != command.Scope.ActorId ||
            !auditContext.CompanyIds.SetEquals(command.Scope.CompanyIds))
            throw new ArgumentException("Cancellation requires a reason and trusted matching audit scope.");
        if (!command.Scope.HasPermission(command.CompanyId, InventoryReservationReleaseRequest.RequiredPermission))
            throw new InventoryReservationAuthorizationException("INVENTORY_RESERVATION_RELEASE_PERMISSION_REQUIRED",
                "Combined cancellation requires reservation release permission as well as sales cancellation permission.");
        const string savepoint = "sales_cancel_with_releases";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            // The producer row is exclusively locked before Inventory discovery. New confirmed-demand reads
            // cannot race in another reservation; manual release never acquires a Sales row lock.
            var cancelled = await PostgresSalesOrderLifecycleWriter.TransitionAsync(connection, transaction, command, cancellationToken);
            var heads = await PostgresInventorySourceReservationLoader.LoadAsync(connection, transaction, command.Scope,
                command.CompanyId, "sales.order", command.OrderId, cancellationToken);
            if (!cancelled.Created && heads.Any(head => head.RemainingQuantity.IsPositive))
                throw new SalesOrderCancellationReservationConflictException();
            await PostgresSalesStockCancellationReceipt.EnsureAsync(connection, transaction, command,
                effectiveDate, cancelled.Created, cancellationToken);
            var requests = new List<InventoryReservationReleaseRequest>();
            foreach (var head in heads)
            {
                Guid correlation = ReleaseCorrelation(command.CorrelationId, head.ReservationId);
                bool replay = head.LastTransition == InventoryReservationTransition.Release && head.LastCorrelationId == correlation;
                if (head.RemainingQuantity.IsPositive || replay)
                    requests.Add(new(command.Scope, command.CompanyId, head.WarehouseId, head.ReservationId,
                        replay ? head.Version - 1 : head.Version, correlation, effectiveDate, reason));
            }
            IReadOnlyList<InventoryReservationReleaseResult> releases = requests.Count == 0
                ? Array.Empty<InventoryReservationReleaseResult>()
                : await PostgresInventoryReservationReleaseBatch.ReleaseAsync(connection, transaction, requests, auditContext, cancellationToken);
            await PostgresAuthorizationAuditWriter.AppendAsync(connection, transaction,
                auditContext with { CompanyIds = new HashSet<Guid> { command.CompanyId } }, Guid.CreateVersion7(),
                new AuthorizationAuditEvent("sales.order.cancel", "sales-order", command.OrderId.ToString("D"), "allowed",
                    cancelled.Created ? "SALES_ORDER_CANCELLED_WITH_RELEASES" : "SALES_ORDER_CANCEL_RELEASES_REPLAYED"), cancellationToken);
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return new(cancelled, releases);
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }
}

public sealed class SalesOrderCancellationReservationConflictException()
    : InvalidOperationException("The original cancellation still has active reservations; replay cannot retrofit their release.")
{
    public string Code { get; } = "SALES_CANCEL_RESERVATION_RESULT_MISSING";
}
