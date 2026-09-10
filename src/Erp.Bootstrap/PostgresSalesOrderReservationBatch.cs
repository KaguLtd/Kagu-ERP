using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Sales.Contracts.Reservations;
using KaguERP.Modules.Sales.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.Bootstrap;

/// <summary>
/// Internal, bounded single-order batch. Use instead of looping over the single-line writer:
/// all request gates, then all demands, then all positions must be locked before the first write.
/// Caller still owns the transaction and must supply any future posting-date policy approval.
/// </summary>
public static class PostgresSalesOrderReservationBatch
{
    public static async ValueTask<IReadOnlyList<InventoryReservationRequestResult>> CreateAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<InventoryReservationRequest> requests,
        RequestAuditContext auditContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(auditContext);
        if (!ReferenceEquals(transaction.Connection, connection) ||
            transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Reservation batches require the caller's ReadCommitted transaction.");
        var snapshot = requests.ToArray();
        if (snapshot.Length is < 1 or > 500 || snapshot.Any(request => request is null))
            throw new ArgumentException("A reservation batch requires 1–500 requests.", nameof(requests));
        var first = snapshot[0];
        foreach (var request in snapshot)
        {
            if (request.Scope.TenantId != first.Scope.TenantId || request.Scope.ActorId != first.Scope.ActorId ||
                request.CompanyId != first.CompanyId || request.Source.SourceType != "sales.order" ||
                request.Source.SourceId != first.Source.SourceId || request.Source.SourceVersion != first.Source.SourceVersion ||
                request.EffectiveDate != first.EffectiveDate ||
                auditContext.TenantId != request.Scope.TenantId || auditContext.ActorId != request.Scope.ActorId ||
                !auditContext.CompanyIds.SetEquals(request.Scope.CompanyIds))
                throw new ArgumentException("A batch must share trusted scope, order version, effective date and audit context.", nameof(requests));
            if (!request.Scope.HasPermission(request.CompanyId, PostgresSalesOrderReservationDemandSource.RequiredPermission))
                throw new SalesOrderReservationDemandAuthorizationException();
        }
        if (snapshot.Select(request => request.RequestId).Distinct().Count() != snapshot.Length ||
            snapshot.Select(request => request.Source.SourceLineId).Distinct().Count() != snapshot.Length)
            throw new ArgumentException("A batch cannot repeat request or order-line identities.", nameof(requests));

        const string savepoint = "sales_order_reservation_batch";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            var ordered = snapshot.OrderBy(request => request.RequestId).ToArray();
            var pending = new List<InventoryReservationRequest>();
            foreach (var request in ordered)
            {
                if (await PostgresInventoryReservationRequestGate.AcquireAsync(connection, transaction, request, cancellationToken) is null)
                    pending.Add(request);
            }
            if (pending.Count > 0)
            {
                var producer = new PostgresSalesOrderReservationDemandSource(connection, transaction, first.Scope);
                var adapter = new SalesOrderReservationDemandEvidenceAdapter(producer);
                var evidence = await adapter.LoadAsync(SalesOrderReservationDemandQuery.Create(
                    first.Scope.TenantId, first.CompanyId, first.Source.SourceId, first.Source.SourceVersion), cancellationToken);
                var byLine = evidence.ToDictionary(line => line.Source.SourceLineId);
                var positions = new List<InventoryPositionLockTarget>();
                foreach (var request in pending)
                {
                    if (!byLine.TryGetValue(request.Source.SourceLineId, out var line))
                        throw new InventoryReservationDemandLineUnavailableException();
                    positions.Add(new(line.ItemId, request.WarehouseId, line.BaseUom));
                }
                await PostgresInventoryDemandLock.AcquireAsync(connection, transaction, first.Scope, first.CompanyId,
                    pending.Select(request => request.Source), cancellationToken);
                await PostgresInventoryPositionLock.AcquireAsync(connection, transaction, first.Scope, first.CompanyId,
                    positions, cancellationToken);
            }
            var results = new Dictionary<Guid, InventoryReservationRequestResult>();
            foreach (var request in ordered)
                results.Add(request.RequestId, await PostgresSalesOrderReservationOrchestrator.CreateAsync(
                    connection, transaction, request, auditContext, cancellationToken));
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return Array.AsReadOnly(snapshot.Select(request => results[request.RequestId]).ToArray());
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }
}
