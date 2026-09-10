using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Domain;
using Npgsql;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

/// <summary>Internal append-only release participant. Runtime lifecycle INSERT and public commands remain closed.</summary>
public static class PostgresInventoryReservationReleaseWriter
{
    public static async ValueTask AcquireBatchLocksAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        IReadOnlyList<InventoryReservationReleaseRequest> requests, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(requests);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Release batch locks require the caller's ReadCommitted transaction.");
        if (requests.Count is < 1 or > 500 || requests.Any(request => request is null) ||
            requests.Select(request => request.ReservationId).Distinct().Count() != requests.Count)
            throw new ArgumentException("Release batches require 1–500 unique reservations.", nameof(requests));
        var first = requests[0];
        var warehouses = await PostgresInventoryWarehouseScopeLoader.LoadAsync(connection, transaction,
            first.Scope, first.CompanyId, cancellationToken);
        var sources = new List<InventoryDemandSourceIdentity>();
        var positions = new List<InventoryPositionLockTarget>();
        foreach (var request in requests)
        {
            if (request.Scope.TenantId != first.Scope.TenantId || request.Scope.ActorId != first.Scope.ActorId ||
                request.CompanyId != first.CompanyId || !request.Scope.CompanyIds.SetEquals(first.Scope.CompanyIds))
                throw new ArgumentException("Release batches require one trusted actor and company scope.", nameof(requests));
            request.EnsureWarehouseAccess(warehouses);
            await using var command = new NpgsqlCommand("""
                SELECT item_id,base_uom_code,source_type,source_id,source_line_id,source_version
                FROM inventory.reservation_creation
                WHERE tenant_id=$1 AND company_id=$2 AND reservation_id=$3 AND warehouse_id=$4
                """, connection, transaction);
            AddIdentity(command, request);
            command.Parameters.AddWithValue(request.WarehouseId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new InventoryReservationReleaseUnavailableException();
            sources.Add(InventoryDemandSourceIdentity.Create(reader.GetString(2), reader.GetGuid(3), reader.GetGuid(4), reader.GetInt64(5)));
            positions.Add(new(reader.GetGuid(0), request.WarehouseId, InventoryUomCode.Create(reader.GetString(1))));
        }
        await PostgresInventoryDemandLock.AcquireAsync(connection, transaction, first.Scope, first.CompanyId, sources, cancellationToken);
        await PostgresInventoryPositionLock.AcquireAsync(connection, transaction, first.Scope, first.CompanyId, positions, cancellationToken);
    }

    public static async ValueTask<InventoryReservationReleaseResult> ReleaseAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, InventoryReservationReleaseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(request);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Release requires the caller's ReadCommitted transaction.");
        request.EnsureWarehouseAccess(await PostgresInventoryWarehouseScopeLoader.LoadAsync(
            connection, transaction, request.Scope, request.CompanyId, cancellationToken));
        InventoryReservationState state;
        DateOnly previousDate;
        DateTimeOffset previousTime;
        await using (var creation = new NpgsqlCommand("""
            SELECT item_id,base_uom_code,source_type,source_id,source_line_id,source_version,
                   reserved_quantity,effective_date,recorded_at
            FROM inventory.reservation_creation
            WHERE tenant_id=$1 AND company_id=$2 AND reservation_id=$3 AND warehouse_id=$4
            """, connection, transaction))
        {
            AddIdentity(creation, request);
            creation.Parameters.AddWithValue(request.WarehouseId);
            await using var reader = await creation.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new InventoryReservationReleaseUnavailableException();
            state = InventoryReservationState.CreateActive(request.ReservationId, request.Scope.TenantId, request.CompanyId,
                reader.GetGuid(0), request.WarehouseId, InventoryUomCode.Create(reader.GetString(1)),
                InventoryDemandSourceIdentity.Create(reader.GetString(2), reader.GetGuid(3), reader.GetGuid(4), reader.GetInt64(5)),
                InventoryQuantity.Create(reader.GetDecimal(6)));
            previousDate = reader.GetFieldValue<DateOnly>(7);
            previousTime = reader.GetFieldValue<DateTimeOffset>(8);
        }
        await PostgresInventoryDemandLock.AcquireAsync(connection, transaction, request.Scope, request.CompanyId,
            [state.Source], cancellationToken);
        await PostgresInventoryPositionLock.AcquireAsync(connection, transaction, request.Scope, request.CompanyId,
            [new(state.ItemId, state.WarehouseId, state.BaseUom)], cancellationToken);
        request.EnsureWarehouseAccess(await PostgresInventoryWarehouseScopeLoader.LoadAsync(
            connection, transaction, request.Scope, request.CompanyId, cancellationToken));

        InventoryReservationReleaseResult? replay = null;
        await using (var history = new NpgsqlCommand("""
            SELECT event_id,version,transition,quantity,consumed_quantity,remaining_quantity,effective_date,
                   occurred_at,recorded_at,actor_id,correlation_id,reason
            FROM inventory.reservation_lifecycle_event
            WHERE tenant_id=$1 AND company_id=$2 AND reservation_id=$3 ORDER BY version
            """, connection, transaction))
        {
            AddIdentity(history, request);
            await using var reader = await history.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                long version = reader.GetInt64(1);
                var transition = (InventoryReservationTransition)reader.GetInt16(2);
                DateOnly date = reader.GetFieldValue<DateOnly>(6);
                DateTimeOffset occurredAt = reader.GetFieldValue<DateTimeOffset>(7);
                Guid actor = reader.GetGuid(9), correlation = reader.GetGuid(10);
                string? reason = reader.IsDBNull(11) ? null : reader.GetString(11);
                if (version != state.Version + 1 || date < previousDate || occurredAt < previousTime)
                    throw new InventoryReservationReleaseUnavailableException();
                var next = InventoryReservationLifecycle.Apply(state, transition, state.Version,
                    InventoryQuantity.Create(reader.GetDecimal(3)), actor, correlation, occurredAt, reason).State;
                if (next.ConsumedQuantity.Value != reader.GetDecimal(4) || next.RemainingQuantity.Value != reader.GetDecimal(5))
                    throw new InventoryReservationReleaseUnavailableException();
                if (correlation == request.CorrelationId)
                {
                    if (transition != InventoryReservationTransition.Release || version != request.ExpectedVersion + 1 ||
                        actor != request.Scope.ActorId || date != request.EffectiveDate || reason != request.Reason)
                        throw new InventoryReservationReleaseConflictException();
                    replay = new(request.ReservationId, reader.GetGuid(0), version, state.RemainingQuantity,
                        next.ConsumedQuantity, reader.GetFieldValue<DateTimeOffset>(8));
                }
                state = next;
                previousDate = date;
                previousTime = occurredAt;
            }
        }
        if (replay is not null) return replay;
        if (request.EffectiveDate < previousDate)
            throw new InventoryReservationReleaseConflictException();
        DateTimeOffset now;
        await using (var clock = new NpgsqlCommand("SELECT clock_timestamp()", connection, transaction))
        await using (var reader = await clock.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            now = reader.GetFieldValue<DateTimeOffset>(0);
        }
        if (now < previousTime) throw new InventoryReservationReleaseConflictException();
        var released = InventoryReservationLifecycle.Apply(state, InventoryReservationTransition.Release,
            request.ExpectedVersion, InventoryQuantity.Create(0m), request.Scope.ActorId, request.CorrelationId, now, request.Reason);
        const string savepoint = "inventory_reservation_release";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO inventory.reservation_lifecycle_event
                    (tenant_id,company_id,reservation_id,event_id,version,transition,quantity,consumed_quantity,
                     remaining_quantity,effective_date,occurred_at,recorded_at,actor_id,correlation_id,reason)
                VALUES ($1,$2,$3,$4,$5,2,0,$6,0,$7,$8,$8,$9,$10,$11)
                """, connection, transaction);
            AddIdentity(insert, request);
            insert.Parameters.AddWithValue(released.Event.EventId);
            insert.Parameters.AddWithValue(released.State.Version);
            insert.Parameters.AddWithValue(released.State.ConsumedQuantity.Value);
            insert.Parameters.AddWithValue(request.EffectiveDate);
            insert.Parameters.AddWithValue(now);
            insert.Parameters.AddWithValue(request.Scope.ActorId);
            insert.Parameters.AddWithValue(request.CorrelationId);
            insert.Parameters.AddWithValue(request.Reason);
            await insert.ExecuteNonQueryAsync(cancellationToken);
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return new(request.ReservationId, released.Event.EventId, released.State.Version,
                state.RemainingQuantity, released.State.ConsumedQuantity, now);
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }

    private static void AddIdentity(NpgsqlCommand command, InventoryReservationReleaseRequest request)
    {
        command.Parameters.AddWithValue(request.Scope.TenantId);
        command.Parameters.AddWithValue(request.CompanyId);
        command.Parameters.AddWithValue(request.ReservationId);
    }
}

public sealed class InventoryReservationReleaseUnavailableException()
    : InvalidOperationException("Reservation is unavailable or its lifecycle evidence is inconsistent.")
{
    public string Code { get; } = "INVENTORY_RESERVATION_RELEASE_UNAVAILABLE";
}

public sealed class InventoryReservationReleaseConflictException()
    : InvalidOperationException("Reservation release conflicts with its original request or temporal history.")
{
    public string Code { get; } = "INVENTORY_RESERVATION_RELEASE_CONFLICT";
}
