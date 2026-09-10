using KaguERP.Bootstrap;
using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.DatabaseIntegrationChecks;

internal static partial class DatabaseIntegrationCheck
{
    private static async Task AssertReservationReleaseBatchAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ExecutionScope scope, Guid companyId, Guid firstWarehouse, Guid secondWarehouse, Guid firstReservation,
        Guid secondReservation, DateOnly date)
    {
        var requests = new[] {
            new InventoryReservationReleaseRequest(scope, companyId, firstWarehouse, firstReservation, 1, Guid.CreateVersion7(), date, "batch release"),
            new InventoryReservationReleaseRequest(scope, companyId, secondWarehouse, secondReservation, 1, Guid.CreateVersion7(), date, "batch release")
        }.OrderBy(request => request.ReservationId).ToArray();
        var audit = new RequestAuditContext(Guid.CreateVersion7(), "release-batch-fixture", scope.TenantId, scope.ActorId, scope.CompanyIds, null);
        var invalid = new InventoryReservationReleaseRequest(scope, companyId, requests[1].WarehouseId,
            requests[1].ReservationId, 2, requests[1].CorrelationId, date, requests[1].Reason);
        await ThrowsAsync<InventoryInvariantException>(async () =>
            await PostgresInventoryReservationReleaseBatch.ReleaseAsync(connection, transaction, [requests[0], invalid], audit));
        await using (var verify = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM inventory.reservation_lifecycle_event WHERE tenant_id=$1 AND company_id=$2 AND reservation_id=ANY($3)),
                   (SELECT count(*) FROM platform.audit_event WHERE tenant_id=$1 AND correlation_id=$4)
            """, connection, transaction))
        {
            verify.Parameters.AddWithValue(scope.TenantId);
            verify.Parameters.AddWithValue(companyId);
            verify.Parameters.AddWithValue(requests.Select(request => request.ReservationId).ToArray());
            verify.Parameters.AddWithValue(audit.CorrelationId);
            await using var reader = await verify.ExecuteReaderAsync();
            Assert(await reader.ReadAsync() && reader.GetInt64(0) == 0 && reader.GetInt64(1) == 0,
                "Failed second release left the first lifecycle event or audit behind.");
        }
        var results = await PostgresInventoryReservationReleaseBatch.ReleaseAsync(connection, transaction,
            requests.Reverse().ToArray(), audit);
        Assert(results.Count == 2 && results[0].ReservationId == requests[1].ReservationId &&
            results.Sum(result => result.ReleasedQuantity.Value) == 10m && results.All(result => result.ConsumedQuantity.IsZero),
            "Release batch failed to preserve input order and exact released quantities across warehouses.");
        var replay = await PostgresInventoryReservationReleaseBatch.ReleaseAsync(connection, transaction, requests, audit);
        Assert(replay[0] == results[1] && replay[1] == results[0], "Reordered release batch changed immutable results.");
        await ThrowsAsync<ArgumentException>(async () =>
            await PostgresInventoryReservationReleaseBatch.ReleaseAsync(connection, transaction, [requests[0], requests[0]], audit));
    }

    private static async Task AssertReservationReleaseWriterAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ExecutionScope scope, Guid companyId, Guid warehouseId, Guid reservationId, DateOnly effectiveDate)
    {
        Guid correlation = Guid.CreateVersion7();
        var request = new InventoryReservationReleaseRequest(scope, companyId, warehouseId, reservationId,
            2, correlation, effectiveDate, "  remaining demand withdrawn  ");
        var audit = new RequestAuditContext(Guid.CreateVersion7(), "release-fixture", scope.TenantId, scope.ActorId, scope.CompanyIds, null);
        var noPermission = new ExecutionScope(scope.TenantId, scope.ActorId,
            [new CompanyAccess(companyId, [AuthorizedInventoryReservationCandidate.RequiredPermission])]);
        await ThrowsAsync<InventoryReservationAuthorizationException>(() =>
        {
            _ = new InventoryReservationReleaseRequest(noPermission, companyId, warehouseId, reservationId,
                2, correlation, effectiveDate, "fixture");
            return Task.CompletedTask;
        });
        await ThrowsAsync<ArgumentException>(() =>
        {
            _ = new InventoryReservationReleaseRequest(scope, companyId, warehouseId, reservationId,
                2, correlation, effectiveDate, " ");
            return Task.CompletedTask;
        });
        var stale = new InventoryReservationReleaseRequest(scope, companyId, warehouseId, reservationId,
            1, correlation, effectiveDate, request.Reason);
        var versionError = await ThrowsAsync<InventoryInvariantException>(async () =>
            await PostgresInventoryReservationReleaseWriter.ReleaseAsync(connection, transaction, stale));
        Assert(versionError.Code == "INVENTORY_RESERVATION_VERSION_CONFLICT", "Stale release was not rejected.");
        await ThrowsAsync<InventoryReservationReleaseConflictException>(async () =>
            await PostgresInventoryReservationReleaseWriter.ReleaseAsync(connection, transaction,
                new InventoryReservationReleaseRequest(scope, companyId, warehouseId, reservationId,
                    2, correlation, effectiveDate.AddDays(-1), request.Reason)));
        var auditError = await ThrowsAsync<PostgresException>(async () =>
            await PostgresInventoryReservationReleaseOrchestrator.ReleaseAsync(connection, transaction, request,
                audit with { TraceId = new string('x', 65) }));
        Assert(auditError.SqlState == "22001", "Release audit failure did not reach the DB length constraint.");
        Assert(await CountAsync(connection, transaction,
            "SELECT count(*) FROM inventory.reservation_lifecycle_event WHERE reservation_id=$1 AND version=3", reservationId) == 0,
            "Audit failure left an unaudited release event behind.");
        var released = await PostgresInventoryReservationReleaseOrchestrator.ReleaseAsync(connection, transaction, request, audit);
        Assert(released.Version == 3 && released.ReleasedQuantity.Value == 4m && released.ConsumedQuantity.Value == 6m,
            "Release must free only the four remaining units and preserve the six consumed units.");
        var replay = await PostgresInventoryReservationReleaseOrchestrator.ReleaseAsync(connection, transaction, request, audit);
        Assert(replay == released, "Release retry did not return the immutable original event/time/quantity.");
        await ThrowsAsync<InventoryReservationReleaseConflictException>(async () =>
            await PostgresInventoryReservationReleaseWriter.ReleaseAsync(connection, transaction,
                new InventoryReservationReleaseRequest(scope, companyId, warehouseId, reservationId,
                    2, correlation, effectiveDate, "changed reason")));
        await ThrowsAsync<InventoryInvariantException>(async () =>
            await PostgresInventoryReservationReleaseWriter.ReleaseAsync(connection, transaction,
                new InventoryReservationReleaseRequest(scope, companyId, warehouseId, reservationId,
                    3, Guid.CreateVersion7(), effectiveDate, request.Reason)));
        var outsider = new ExecutionScope(scope.TenantId, Guid.CreateVersion7(),
            [new CompanyAccess(companyId, [InventoryReservationReleaseRequest.RequiredPermission])]);
        await ThrowsAsync<InventoryReservationAuthorizationException>(async () =>
            await PostgresInventoryReservationReleaseWriter.ReleaseAsync(connection, transaction,
                new InventoryReservationReleaseRequest(outsider, companyId, warehouseId, reservationId,
                    2, correlation, effectiveDate, request.Reason)));
        await ThrowsAsync<InventoryReservationReleaseUnavailableException>(async () =>
            await PostgresInventoryReservationReleaseWriter.ReleaseAsync(connection, transaction,
                new InventoryReservationReleaseRequest(scope, companyId, warehouseId, Guid.CreateVersion7(),
                    1, Guid.CreateVersion7(), effectiveDate, request.Reason)));
        _ = await PostgresInventoryWarehouseScopeLoader.LoadAsync(connection, transaction, scope, companyId);
        Assert(await CountAsync(connection, transaction,
            "SELECT count(*) FROM inventory.reservation_lifecycle_event WHERE reservation_id=$1 AND version=3", reservationId) == 1,
            "Release retries or rejected requests created extra lifecycle events.");
    }
}
