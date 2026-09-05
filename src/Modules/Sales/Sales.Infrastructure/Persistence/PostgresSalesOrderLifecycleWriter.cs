using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using Npgsql;

namespace KaguERP.Modules.Sales.Infrastructure.Persistence;

public sealed record SalesOrderLifecyclePersistenceResult(
    SalesOrderLifecycleState State,
    SalesOrderCommitment Commitment,
    SalesOrderTransitionEvent? Event,
    bool Created);

public static class PostgresSalesOrderLifecycleWriter
{
    public static async ValueTask<SalesOrderLifecyclePersistenceResult> CreateDraftAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuthorizedSalesOrderCreateCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateTransaction(connection, transaction);
        ArgumentNullException.ThrowIfNull(command);
        await SetScopeAsync(connection, transaction, command.Scope, command.CompanyId, cancellationToken);

        SalesOrderLifecycleState draft = SalesOrderLifecycleState.CreateDraft(
            command.Scope.TenantId,
            command.CompanyId,
            command.OrderId,
            command.Scope.ActorId);
        const string sql = """
            INSERT INTO sales.sales_order
                (tenant_id,company_id,order_id,maker_id,version,status,created_by,updated_by)
            VALUES ($1,$2,$3,$4,$5,$6,$4,$4)
            ON CONFLICT ON CONSTRAINT pk_sales_order DO NOTHING
            RETURNING order_id
            """;
        await using var insert = new NpgsqlCommand(sql, connection, transaction);
        insert.Parameters.AddWithValue(draft.TenantId);
        insert.Parameters.AddWithValue(draft.CompanyId);
        insert.Parameters.AddWithValue(draft.OrderId);
        insert.Parameters.AddWithValue(draft.MakerId);
        insert.Parameters.AddWithValue(draft.Version);
        insert.Parameters.AddWithValue((short)draft.Status);
        if (await insert.ExecuteScalarAsync(cancellationToken) is Guid)
        {
            await InsertLinesAsync(connection, transaction, command.Commitment, command.Scope.ActorId, cancellationToken);
            return new SalesOrderLifecyclePersistenceResult(draft, command.Commitment, null, true);
        }

        SalesOrderLifecycleState? existing = await LoadStateAsync(
            connection, transaction, draft.TenantId, draft.CompanyId, draft.OrderId, false, cancellationToken);
        if (existing is null || existing != draft)
        {
            throw new SalesOrderPersistenceConflictException(
                "SALES_ORDER_CREATE_CONFLICT",
                "The sales order identity already has different lifecycle state.");
        }

        SalesOrderCommitment existingCommitment = await LoadCommitmentAsync(
            connection, transaction, existing, cancellationToken);
        if (!command.Commitment.HasSameLines(existingCommitment.Lines))
        {
            throw new SalesOrderPersistenceConflictException(
                "SALES_ORDER_CREATE_CONFLICT",
                "The sales order identity already has different immutable lines.");
        }

        return new SalesOrderLifecyclePersistenceResult(existing, existingCommitment, null, false);
    }

    public static async ValueTask<SalesOrderLifecyclePersistenceResult> TransitionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuthorizedSalesOrderTransitionCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateTransaction(connection, transaction);
        ArgumentNullException.ThrowIfNull(command);
        await SetScopeAsync(connection, transaction, command.Scope, command.CompanyId, cancellationToken);

        PersistedTransition? replay = await LoadTransitionByCorrelationAsync(
            connection, transaction, command, cancellationToken);
        if (replay is not null)
        {
            EnsureExactReplay(command, replay);
            SalesOrderCommitment commitment = await LoadCommitmentAsync(
                connection, transaction, replay.State, cancellationToken);
            return new SalesOrderLifecyclePersistenceResult(replay.State, commitment, replay.Event, false);
        }

        SalesOrderLifecycleState current = await LoadStateAsync(
            connection,
            transaction,
            command.Scope.TenantId,
            command.CompanyId,
            command.OrderId,
            true,
            cancellationToken) ?? throw new SalesOrderNotFoundException();
        replay = await LoadTransitionByCorrelationAsync(connection, transaction, command, cancellationToken);
        if (replay is not null)
        {
            EnsureExactReplay(command, replay);
            SalesOrderCommitment commitment = await LoadCommitmentAsync(
                connection, transaction, replay.State, cancellationToken);
            return new SalesOrderLifecyclePersistenceResult(replay.State, commitment, replay.Event, false);
        }

        SalesOrderTransitionResult result = SalesOrderLifecycle.Apply(
            current,
            command.Transition,
            command.ExpectedVersion,
            command.Scope.ActorId,
            command.CorrelationId,
            command.OccurredAt,
            command.Reason);

        const string updateSql = """
            UPDATE sales.sales_order
            SET maker_id=$1,version=$2,status=$3,updated_at=$4,updated_by=$5
            WHERE tenant_id=$6 AND company_id=$7 AND order_id=$8 AND version=$9
            """;
        await using var update = new NpgsqlCommand(updateSql, connection, transaction);
        update.Parameters.AddWithValue(result.State.MakerId);
        update.Parameters.AddWithValue(result.State.Version);
        update.Parameters.AddWithValue((short)result.State.Status);
        update.Parameters.AddWithValue(result.Event.OccurredAt);
        update.Parameters.AddWithValue(result.Event.ActorId);
        update.Parameters.AddWithValue(result.State.TenantId);
        update.Parameters.AddWithValue(result.State.CompanyId);
        update.Parameters.AddWithValue(result.State.OrderId);
        update.Parameters.AddWithValue(current.Version);
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new SalesOrderPersistenceConflictException(
                "SALES_ORDER_VERSION_CONFLICT",
                "The sales order changed before its transition was persisted.");
        }
        await InsertTransitionAsync(connection, transaction, current, result, cancellationToken);

        SalesOrderCommitment persistedCommitment = await LoadCommitmentAsync(
            connection, transaction, result.State, cancellationToken);
        return new SalesOrderLifecyclePersistenceResult(result.State, persistedCommitment, result.Event, true);
    }

    private static async ValueTask InsertLinesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        SalesOrderCommitment commitment,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO sales.sales_order_line
                (tenant_id,company_id,order_id,order_line_id,item_id,base_uom_code,
                 ordered_base_quantity,created_by)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8)
            """;
        foreach (SalesOrderLineCommitment line in commitment.Lines.OrderBy(item => item.OrderLineId))
        {
            await using var insert = new NpgsqlCommand(sql, connection, transaction);
            insert.Parameters.AddWithValue(commitment.TenantId);
            insert.Parameters.AddWithValue(commitment.CompanyId);
            insert.Parameters.AddWithValue(commitment.OrderId);
            insert.Parameters.AddWithValue(line.OrderLineId);
            insert.Parameters.AddWithValue(line.ItemId);
            insert.Parameters.AddWithValue(line.BaseUomCode);
            insert.Parameters.AddWithValue(line.OrderedQuantity.Value);
            insert.Parameters.AddWithValue(actorId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async ValueTask<SalesOrderCommitment> LoadCommitmentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        SalesOrderLifecycleState state,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT order_line_id,item_id,base_uom_code,ordered_base_quantity
            FROM sales.sales_order_line
            WHERE tenant_id=$1 AND company_id=$2 AND order_id=$3
            ORDER BY order_line_id
            """;
        await using var query = new NpgsqlCommand(sql, connection, transaction);
        query.Parameters.AddWithValue(state.TenantId);
        query.Parameters.AddWithValue(state.CompanyId);
        query.Parameters.AddWithValue(state.OrderId);
        var lines = new List<SalesOrderLineCommitment>();
        await using NpgsqlDataReader reader = await query.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            lines.Add(SalesOrderLineCommitment.Create(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                SalesOrderQuantity.Create(reader.GetDecimal(3))));
        }

        return SalesOrderCommitment.Create(state.TenantId, state.CompanyId, state.OrderId, lines);
    }

    private static async ValueTask InsertTransitionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        SalesOrderLifecycleState previous,
        SalesOrderTransitionResult result,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO sales.sales_order_transition_event
                (tenant_id,company_id,event_id,order_id,previous_version,new_version,
                 previous_status,new_status,transition,previous_maker_id,new_maker_id,
                 actor_id,correlation_id,occurred_at,reason)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)
            """;
        await using var insert = new NpgsqlCommand(sql, connection, transaction);
        object[] values =
        [
            result.State.TenantId,
            result.State.CompanyId,
            result.Event.EventId,
            result.State.OrderId,
            result.Event.PreviousVersion,
            result.Event.NewVersion,
            (short)result.Event.PreviousStatus,
            (short)result.Event.NewStatus,
            (short)result.Event.Transition,
            previous.MakerId,
            result.State.MakerId,
            result.Event.ActorId,
            result.Event.CorrelationId,
            result.Event.OccurredAt,
            result.Event.Reason is null ? DBNull.Value : result.Event.Reason,
        ];
        foreach (object value in values)
        {
            insert.Parameters.AddWithValue(value);
        }
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async ValueTask<SalesOrderLifecycleState?> LoadStateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid companyId,
        Guid orderId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        string sql = """
            SELECT maker_id,version,status
            FROM sales.sales_order
            WHERE tenant_id=$1 AND company_id=$2 AND order_id=$3
            """ + (forUpdate ? " FOR UPDATE" : string.Empty);
        await using var query = new NpgsqlCommand(sql, connection, transaction);
        query.Parameters.AddWithValue(tenantId);
        query.Parameters.AddWithValue(companyId);
        query.Parameters.AddWithValue(orderId);
        await using NpgsqlDataReader reader = await query.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return SalesOrderLifecycleState.Rehydrate(
            tenantId,
            companyId,
            orderId,
            reader.GetGuid(0),
            reader.GetInt64(1),
            (SalesOrderStatus)reader.GetInt16(2));
    }

    private static async ValueTask<PersistedTransition?> LoadTransitionByCorrelationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuthorizedSalesOrderTransitionCommand command,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT event_id,previous_version,new_version,previous_status,new_status,transition,
                   new_maker_id,actor_id,occurred_at,reason
            FROM sales.sales_order_transition_event
            WHERE tenant_id=$1 AND company_id=$2 AND order_id=$3 AND correlation_id=$4
            """;
        await using var query = new NpgsqlCommand(sql, connection, transaction);
        query.Parameters.AddWithValue(command.Scope.TenantId);
        query.Parameters.AddWithValue(command.CompanyId);
        query.Parameters.AddWithValue(command.OrderId);
        query.Parameters.AddWithValue(command.CorrelationId);
        await using NpgsqlDataReader reader = await query.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        long previousVersion = reader.GetInt64(1);
        long newVersion = reader.GetInt64(2);
        var previousStatus = (SalesOrderStatus)reader.GetInt16(3);
        var newStatus = (SalesOrderStatus)reader.GetInt16(4);
        var transition = (SalesOrderTransition)reader.GetInt16(5);
        Guid actorId = reader.GetGuid(7);
        DateTimeOffset occurredAt = reader.GetFieldValue<DateTimeOffset>(8);
        string? reason = reader.IsDBNull(9) ? null : reader.GetString(9);
        var state = SalesOrderLifecycleState.Rehydrate(
            command.Scope.TenantId,
            command.CompanyId,
            command.OrderId,
            reader.GetGuid(6),
            newVersion,
            newStatus);
        var lifecycleEvent = new SalesOrderTransitionEvent(
            reader.GetGuid(0),
            state.TenantId,
            state.CompanyId,
            state.OrderId,
            previousVersion,
            newVersion,
            previousStatus,
            newStatus,
            transition,
            actorId,
            command.CorrelationId,
            occurredAt,
            reason);
        return new PersistedTransition(state, lifecycleEvent);
    }

    private static void EnsureExactReplay(
        AuthorizedSalesOrderTransitionCommand command,
        PersistedTransition replay)
    {
        string? normalizedReason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim();
        if (replay.Event.PreviousVersion != command.ExpectedVersion ||
            replay.Event.Transition != command.Transition ||
            replay.Event.ActorId != command.Scope.ActorId ||
            !string.Equals(replay.Event.Reason, normalizedReason, StringComparison.Ordinal))
        {
            throw new SalesOrderPersistenceConflictException(
                "SALES_ORDER_IDEMPOTENCY_CONFLICT",
                "The sales order correlation identity was already used with different immutable content.");
        }
    }

    private static async ValueTask SetScopeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ExecutionScope scope,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        scope.EnsureAllowed(scope.TenantId, companyId);
        const string sql = """
            SELECT set_config('app.tenant_id',$1,true),
                   set_config('app.actor_id',$2,true),
                   set_config('app.company_ids',$3,true)
            """;
        await using var context = new NpgsqlCommand(sql, connection, transaction);
        context.Parameters.AddWithValue(scope.TenantId.ToString());
        context.Parameters.AddWithValue(scope.ActorId.ToString());
        context.Parameters.AddWithValue("{" + companyId + "}");
        await context.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ValidateTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new ArgumentException("The transaction does not belong to the supplied connection.", nameof(transaction));
        }
    }

    private sealed record PersistedTransition(
        SalesOrderLifecycleState State,
        SalesOrderTransitionEvent Event);
}

public sealed class SalesOrderNotFoundException()
    : InvalidOperationException("The sales order does not exist in the active scope.")
{
    public string Code { get; } = "SALES_ORDER_NOT_FOUND";
}

public sealed class SalesOrderPersistenceConflictException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
