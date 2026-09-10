using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Sales.Contracts.Reservations;
using Npgsql;
using NpgsqlTypes;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

/// <summary>
/// Transaction participant, not a public command gateway. The composition root must bind the
/// demand producer to this same connection and transaction. Runtime INSERT remains disabled
/// until all stock-decreasing writers enforce the shared capacity protocol.
/// </summary>
public static class PostgresInventoryReservationWriter
{
    public static async ValueTask<InventoryReservationRequestResult> CreateAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, InventoryReservationRequest request,
        ISalesOrderReservationDemandSource demandSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(demandSource);
        var replay = await PostgresInventoryReservationRequestGate.AcquireAsync(
            connection, transaction, request, cancellationToken);
        if (replay is not null) return replay;
        if (request.Source.SourceType != SalesOrderReservationDemandEvidenceAdapter.DemandSourceType)
            throw new InventoryReservationDemandUnavailableException();

        var adapter = new SalesOrderReservationDemandEvidenceAdapter(demandSource);
        var lines = await adapter.LoadAsync(SalesOrderReservationDemandQuery.Create(
            request.Scope.TenantId, request.CompanyId, request.Source.SourceId,
            request.Source.SourceVersion), cancellationToken);
        var demand = lines.SingleOrDefault(line => line.Source.SourceLineId == request.Source.SourceLineId)
            ?? throw new InventoryReservationDemandLineUnavailableException();
        var balances = await PostgresInventoryReservationBalanceLoader.LoadAsync(
            connection, transaction, request, demand, cancellationToken);
        short precision = await EnsureMastersAsync(connection, transaction, request, demand, cancellationToken);
        var capacity = await PostgresInventoryPositionCapacityLoader.LoadAsync(connection, transaction,
            request.Scope, request.CompanyId, new(demand.ItemId, request.WarehouseId, demand.BaseUom),
            request.EffectiveDate, cancellationToken);

        decimal remainingDemand = Math.Max(0m, demand.MaximumReservableQuantity.Value - balances.CommittedQuantityForDemand);
        decimal available = Math.Max(0m, capacity.MinimumAvailable);
        var quantity = InventoryQuantity.Create(Math.Min(request.RequestedQuantity.Value, Math.Min(remainingDemand, available)));
        if (decimal.Round(quantity.Value, precision) != quantity.Value)
            throw new InventoryReservationAuthorizationException("INVENTORY_RESERVATION_BALANCE_SCALE_INVALID",
                "The calculated reservation does not match the authoritative item's quantity scale.");
        Guid? reservationId = quantity.IsPositive ? Guid.CreateVersion7() : null;

        // Roll back both rows if the second statement fails, even when the caller catches the exception.
        const string savepoint = "inventory_reservation_create";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            if (reservationId is not null)
            {
                await using var creation = new NpgsqlCommand("""
                    INSERT INTO inventory.reservation_creation
                        (tenant_id,company_id,request_id,warehouse_id,requested_quantity,reserved_quantity,
                         reservation_id,recorded_by,recorded_at,item_id,base_uom_code,source_type,source_id,
                         source_line_id,source_version,effective_date,correlation_id,policy_version)
                    VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$3,$17)
                    """, connection, transaction);
                AddCommon(creation, request, quantity, reservationId, capacity.RecordedCutoff);
                creation.Parameters.AddWithValue(demand.ItemId);
                creation.Parameters.AddWithValue(demand.BaseUom.Value);
                creation.Parameters.AddWithValue(demand.Source.SourceType);
                creation.Parameters.AddWithValue(demand.Source.SourceId);
                creation.Parameters.AddWithValue(demand.Source.SourceLineId);
                creation.Parameters.AddWithValue(demand.Source.SourceVersion);
                creation.Parameters.AddWithValue(request.EffectiveDate);
                creation.Parameters.AddWithValue(InventoryReservationRequest.PolicyVersion);
                await creation.ExecuteNonQueryAsync(cancellationToken);
            }
            await using var result = new NpgsqlCommand("""
                INSERT INTO inventory.reservation_request_result
                    (tenant_id,company_id,request_id,warehouse_id,requested_quantity,reserved_quantity,
                     reservation_id,recorded_by,recorded_at,request_fingerprint)
                VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)
                """, connection, transaction);
            AddCommon(result, request, quantity, reservationId, capacity.RecordedCutoff);
            result.Parameters.AddWithValue(request.Fingerprint);
            await result.ExecuteNonQueryAsync(cancellationToken);
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return new(request.RequestId, reservationId, request.RequestedQuantity, quantity, capacity.RecordedCutoff);
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }

    private static void AddCommon(NpgsqlCommand command, InventoryReservationRequest request,
        InventoryQuantity quantity, Guid? reservationId, DateTimeOffset recordedAt)
    {
        command.Parameters.AddWithValue(request.Scope.TenantId);
        command.Parameters.AddWithValue(request.CompanyId);
        command.Parameters.AddWithValue(request.RequestId);
        command.Parameters.AddWithValue(request.WarehouseId);
        command.Parameters.AddWithValue(request.RequestedQuantity.Value);
        command.Parameters.AddWithValue(quantity.Value);
        command.Parameters.AddWithValue(NpgsqlDbType.Uuid, (object?)reservationId ?? DBNull.Value);
        command.Parameters.AddWithValue(request.Scope.ActorId);
        command.Parameters.AddWithValue(recordedAt);
    }

    private static async ValueTask<short> EnsureMastersAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        InventoryReservationRequest request, InventoryReservationDemandEvidence demand, CancellationToken cancellationToken)
    {
        try
        {
            return await PostgresInventoryStockMasterLoader.EnsureAsync(connection, transaction, request.Scope,
                request.CompanyId, demand.ItemId, demand.BaseUom, [request.WarehouseId], request.RequestedQuantity, cancellationToken);
        }
        catch (InventoryStockMasterUnavailableException exception)
        {
            throw new InventoryReservationAuthorizationException("INVENTORY_RESERVATION_MASTER_UNAVAILABLE",
                exception.Message, exception);
        }
    }
}
