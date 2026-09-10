using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.Bootstrap;

/// <summary>Manual release batch foundation; does not cancel its source Sales order.</summary>
public static class PostgresInventoryReservationReleaseBatch
{
    public static async ValueTask<IReadOnlyList<InventoryReservationReleaseResult>> ReleaseAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<InventoryReservationReleaseRequest> requests,
        RequestAuditContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(context);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Release batches require the caller's ReadCommitted transaction.");
        var snapshot = requests.ToArray();
        if (snapshot.Any(request => request is null || context.TenantId != request.Scope.TenantId ||
            context.ActorId != request.Scope.ActorId || !context.CompanyIds.SetEquals(request.Scope.CompanyIds)))
            throw new ArgumentException("Release batch audit must match every trusted request scope.");
        const string savepoint = "inventory_release_batch";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            await PostgresInventoryReservationReleaseWriter.AcquireBatchLocksAsync(connection, transaction, snapshot, cancellationToken);
            var results = new Dictionary<Guid, InventoryReservationReleaseResult>();
            foreach (var request in snapshot.OrderBy(request => request.ReservationId))
                results.Add(request.ReservationId, await PostgresInventoryReservationReleaseOrchestrator.ReleaseAsync(
                    connection, transaction, request, context, cancellationToken));
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return Array.AsReadOnly(snapshot.Select(request => results[request.ReservationId]).ToArray());
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }
}
