using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Transfers;
using KaguERP.Modules.Inventory.Domain;
using Npgsql;

namespace KaguERP.Modules.Inventory.Infrastructure.Persistence;

public sealed record ImmediateStockTransferPersistenceResult(Guid TransferId, bool Created);

public static class PostgresImmediateStockTransferWriter
{
    private const string SavepointName = "inventory_immediate_transfer_write";

    public static async ValueTask<ImmediateStockTransferPersistenceResult> PersistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuthorizedImmediateStockTransferCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(candidate);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }

        ExecutionScope scope = candidate.Scope;
        ValidatedImmediateStockTransferDraft transfer = candidate.Transfer;
        StockMovementDraft issue = transfer.SourceIssue;
        StockMovementDraft receipt = transfer.DestinationReceipt;
        scope.EnsureAllowed(issue.TenantId, issue.CompanyId);
        InventoryWarehouseScopeEvidence currentWarehouseScope =
            await PostgresInventoryWarehouseScopeLoader.LoadAsync(
                connection,
                transaction,
                scope,
                issue.CompanyId,
                cancellationToken);
        _ = AuthorizedImmediateStockTransferCandidate.Create(scope, currentWarehouseScope, transfer);

        await ExecuteTransactionCommandAsync(connection, transaction, $"SAVEPOINT {SavepointName}", cancellationToken);
        bool issueCreated = await TryInsertAsync(connection, transaction, scope, issue, cancellationToken);
        bool receiptCreated = await TryInsertAsync(connection, transaction, scope, receipt, cancellationToken);
        if (issueCreated && receiptCreated)
        {
            await ExecuteTransactionCommandAsync(
                connection,
                transaction,
                $"RELEASE SAVEPOINT {SavepointName}",
                cancellationToken);
            return new ImmediateStockTransferPersistenceResult(transfer.TransferId, true);
        }

        await ExecuteTransactionCommandAsync(
            connection,
            transaction,
            $"ROLLBACK TO SAVEPOINT {SavepointName}",
            cancellationToken);
        await ExecuteTransactionCommandAsync(
            connection,
            transaction,
            $"RELEASE SAVEPOINT {SavepointName}",
            cancellationToken);

        StockMovementDraft? existingIssue = await LoadCanonicalMovementAsync(
            connection, transaction, issue, cancellationToken);
        StockMovementDraft? existingReceipt = await LoadCanonicalMovementAsync(
            connection, transaction, receipt, cancellationToken);
        if (existingIssue is null || existingReceipt is null ||
            !HasSameImmutableContent(issue, existingIssue) ||
            !HasSameImmutableContent(receipt, existingReceipt))
        {
            throw new ImmediateStockTransferPersistenceConflictException(
                existingIssue?.TransferId ?? existingReceipt?.TransferId);
        }

        return new ImmediateStockTransferPersistenceResult(existingIssue.TransferId!.Value, false);
    }

    private static async ValueTask<bool> TryInsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        StockMovementDraft movement,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO inventory.stock_movement
                (tenant_id,company_id,movement_id,item_id,warehouse_id,base_uom_code,movement_kind,
                 base_quantity,effective_date,recorded_at,recorded_by,sequence_key,source_type,
                 source_event_id,source_line_id,source_version,posting_purpose,transfer_id,
                 counterpart_warehouse_id,reversal_of_movement_id)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20)
            ON CONFLICT ON CONSTRAINT uq_stock_movement_source_result DO NOTHING
            RETURNING movement_id
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        AddParameters(command, scope, movement);
        return await command.ExecuteScalarAsync(cancellationToken) is Guid;
    }

    private static async ValueTask<StockMovementDraft?> LoadCanonicalMovementAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        StockMovementDraft expected,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT movement_id,item_id,warehouse_id,base_uom_code,movement_kind,base_quantity,
                   effective_date,recorded_at,sequence_key,transfer_id,counterpart_warehouse_id,
                   reversal_of_movement_id
            FROM inventory.stock_movement
            WHERE tenant_id=$1 AND company_id=$2 AND source_type=$3 AND source_event_id=$4
              AND source_line_id=$5 AND source_version=$6 AND posting_purpose=$7
              AND movement_kind=$8 AND warehouse_id=$9
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(expected.TenantId);
        command.Parameters.AddWithValue(expected.CompanyId);
        command.Parameters.AddWithValue(expected.Source.SourceType);
        command.Parameters.AddWithValue(expected.Source.SourceEventId);
        command.Parameters.AddWithValue(expected.Source.SourceLineId);
        command.Parameters.AddWithValue(expected.Source.SourceVersion);
        command.Parameters.AddWithValue(expected.Source.PostingPurpose);
        command.Parameters.AddWithValue((short)expected.Kind);
        command.Parameters.AddWithValue(expected.WarehouseId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return StockMovementDraft.Create(
            reader.GetGuid(0),
            expected.TenantId,
            expected.CompanyId,
            reader.GetGuid(1),
            reader.GetGuid(2),
            InventoryUomCode.Create(reader.GetString(3)),
            (StockMovementKind)reader.GetInt16(4),
            InventoryQuantity.Create(reader.GetDecimal(5)),
            reader.GetFieldValue<DateOnly>(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.GetInt64(8),
            expected.Source,
            reader.IsDBNull(9) ? null : reader.GetGuid(9),
            reader.IsDBNull(10) ? null : reader.GetGuid(10),
            reader.IsDBNull(11) ? null : reader.GetGuid(11));
    }

    private static bool HasSameImmutableContent(StockMovementDraft expected, StockMovementDraft actual) =>
        expected == actual;

    private static void AddParameters(
        NpgsqlCommand command,
        ExecutionScope scope,
        StockMovementDraft movement)
    {
        object[] values =
        [
            movement.TenantId,
            movement.CompanyId,
            movement.MovementId,
            movement.ItemId,
            movement.WarehouseId,
            movement.BaseUom.Value,
            (short)movement.Kind,
            movement.BaseQuantity.Value,
            movement.EffectiveDate,
            movement.RecordedAt,
            scope.ActorId,
            movement.SequenceKey,
            movement.Source.SourceType,
            movement.Source.SourceEventId,
            movement.Source.SourceLineId,
            movement.Source.SourceVersion,
            movement.Source.PostingPurpose,
            movement.TransferId!.Value,
            movement.CounterpartWarehouseId!.Value,
            movement.ReversalOfMovementId.HasValue ? movement.ReversalOfMovementId.Value : DBNull.Value,
        ];
        foreach (object value in values)
        {
            command.Parameters.AddWithValue(value);
        }
    }

    private static async ValueTask ExecuteTransactionCommandAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed class ImmediateStockTransferPersistenceConflictException(Guid? existingTransferId)
    : InvalidOperationException("The canonical stock-transfer source has different immutable content.")
{
    public string Code { get; } = "INVENTORY_TRANSFER_PERSISTENCE_CONFLICT";
    public Guid? ExistingTransferId { get; } = existingTransferId;
}
