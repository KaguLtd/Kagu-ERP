using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using Npgsql;

namespace KaguERP.Modules.Sales.Infrastructure.Persistence;

public delegate ValueTask AuthorizeDispatchWarehouses(NpgsqlConnection connection, NpgsqlTransaction transaction,
    ExecutionScope scope, Guid companyId, IReadOnlyList<Guid> warehouseIds, CancellationToken cancellationToken);

/// <summary>Caller-owned transaction. The warehouse authority is supplied by the composition root, never the client.</summary>
public static class PostgresSalesDispatchDraftStore
{
    public static async ValueTask<SalesDispatchDraftOutcome> CreateAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, AuthorizedSalesDispatchDraftCommand command,
        AuthorizeDispatchWarehouses authorizeWarehouses, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(authorizeWarehouses);
        await SetScopeAsync(connection, transaction, command.Scope, command.CompanyId, cancellationToken);
        var warehouseIds = command.Lines.Select(line => line.WarehouseId).Distinct().ToArray();
        await authorizeWarehouses(connection, transaction, command.Scope, command.CompanyId, warehouseIds, cancellationToken);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"kagu.sales.dispatch-draft.v1/{command.Scope.TenantId:D}/{command.CompanyId:D}/{command.DispatchId:D}"));
        await using (var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock($1)", connection, transaction))
        {
            gate.Parameters.AddWithValue(BinaryPrimitives.ReadInt64BigEndian(hash));
            await gate.ExecuteNonQueryAsync(cancellationToken);
        }
        await authorizeWarehouses(connection, transaction, command.Scope, command.CompanyId, warehouseIds, cancellationToken);
        var existing = await ReadAsync(connection, transaction, command.Scope.TenantId, command.CompanyId,
            command.DispatchId, cancellationToken);
        if (existing is { } replay)
        {
            await authorizeWarehouses(connection, transaction, command.Scope, command.CompanyId,
                replay.Draft.Lines.Select(line => line.WarehouseId).Distinct().ToArray(), cancellationToken);
            if (replay.Fingerprint != command.Fingerprint) throw new SalesDispatchDraftConflictException();
            return new(replay.Draft, false);
        }

        const string savepoint = "sales_dispatch_draft_write";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            var prepared = await PostgresSalesDispatchPreparationLoader.LoadAsync(connection, transaction,
                command.Scope, command.CompanyId, command.OrderId, command.ExpectedOrderVersion,
                command.Lines.Select(line => new SalesDispatchLineRequest(line.OrderLineId, SalesOrderQuantity.Create(line.Quantity))),
                cancellationToken);
            await authorizeWarehouses(connection, transaction, command.Scope, command.CompanyId, warehouseIds, cancellationToken);
            await using (var header = new NpgsqlCommand("""
                INSERT INTO sales.dispatch_draft
                    (tenant_id,company_id,dispatch_id,order_id,order_version,effective_date,request_fingerprint,line_count,created_by)
                VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9)
                """, connection, transaction))
            {
                AddIdentity(header, command.Scope.TenantId, command.CompanyId, command.DispatchId);
                header.Parameters.AddWithValue(command.OrderId);
                header.Parameters.AddWithValue(command.ExpectedOrderVersion);
                header.Parameters.AddWithValue(command.EffectiveDate);
                header.Parameters.AddWithValue(command.Fingerprint);
                header.Parameters.AddWithValue(command.Lines.Count);
                header.Parameters.AddWithValue(command.Scope.ActorId);
                await header.ExecuteNonQueryAsync(cancellationToken);
            }
            var selections = command.Lines.ToDictionary(line => line.OrderLineId);
            foreach (var line in prepared.Preparation.Lines)
            {
                await using var insert = new NpgsqlCommand("""
                    INSERT INTO sales.dispatch_draft_line
                        (tenant_id,company_id,dispatch_id,order_id,order_line_id,warehouse_id,item_id,base_uom_code,base_quantity)
                    VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9)
                    """, connection, transaction);
                AddIdentity(insert, command.Scope.TenantId, command.CompanyId, command.DispatchId);
                insert.Parameters.AddWithValue(command.OrderId);
                insert.Parameters.AddWithValue(line.OrderLineId);
                insert.Parameters.AddWithValue(selections[line.OrderLineId].WarehouseId);
                insert.Parameters.AddWithValue(line.ItemId);
                insert.Parameters.AddWithValue(line.BaseUomCode);
                insert.Parameters.AddWithValue(line.Quantity.Value);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            var stored = await ReadAsync(connection, transaction, command.Scope.TenantId, command.CompanyId,
                command.DispatchId, cancellationToken) ?? throw new InvalidOperationException("New dispatch draft could not be loaded.");
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return new(stored.Draft, true);
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }

    public static async ValueTask<SalesDispatchDraftSnapshot> LoadAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, Guid companyId, Guid dispatchId,
        AuthorizeDispatchWarehouses authorizeWarehouses, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorizeWarehouses);
        if (dispatchId == Guid.Empty) throw new ArgumentException("Dispatch identity is required.", nameof(dispatchId));
        await SetScopeAsync(connection, transaction, scope, companyId, cancellationToken);
        var stored = await ReadAsync(connection, transaction, scope.TenantId, companyId, dispatchId, cancellationToken)
            ?? throw new SalesDispatchDraftNotFoundException();
        await authorizeWarehouses(connection, transaction, scope, companyId,
            stored.Draft.Lines.Select(line => line.WarehouseId).Distinct().ToArray(), cancellationToken);
        return stored.Draft;
    }

    private static async ValueTask<(SalesDispatchDraftSnapshot Draft, string Fingerprint)?> ReadAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid companyId, Guid dispatchId,
        CancellationToken cancellationToken)
    {
        Guid orderId, actorId;
        long version;
        int lineCount;
        string fingerprint;
        DateOnly date;
        DateTimeOffset recordedAt;
        await using (var header = new NpgsqlCommand("""
            SELECT order_id,order_version,effective_date,created_by,recorded_at,request_fingerprint,line_count
            FROM sales.dispatch_draft WHERE tenant_id=$1 AND company_id=$2 AND dispatch_id=$3
            """, connection, transaction))
        {
            AddIdentity(header, tenantId, companyId, dispatchId);
            await using var reader = await header.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            orderId = reader.GetGuid(0);
            version = reader.GetInt64(1);
            date = reader.GetFieldValue<DateOnly>(2);
            actorId = reader.GetGuid(3);
            recordedAt = reader.GetFieldValue<DateTimeOffset>(4);
            fingerprint = reader.GetString(5);
            lineCount = reader.GetInt32(6);
        }
        var lines = new List<SalesDispatchDraftLine>();
        await using (var query = new NpgsqlCommand("""
            SELECT order_line_id,warehouse_id,item_id,base_uom_code,base_quantity
            FROM sales.dispatch_draft_line WHERE tenant_id=$1 AND company_id=$2 AND dispatch_id=$3
            ORDER BY order_line_id LIMIT 501
            """, connection, transaction))
        {
            AddIdentity(query, tenantId, companyId, dispatchId);
            await using var reader = await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                lines.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetDecimal(4)));
        }
        if (lineCount is < 1 or > 500 || lines.Count != lineCount)
            throw new InvalidOperationException("Dispatch draft line snapshot is incomplete.");
        return (new(tenantId, companyId, dispatchId, orderId, version, date, actorId, recordedAt,
            Array.AsReadOnly(lines.ToArray())), fingerprint);
    }

    private static async ValueTask SetScopeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ExecutionScope scope, Guid companyId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        AuthorizedSalesDispatchDraftCommand.EnsurePermission(scope, companyId);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Dispatch drafts require the caller's ReadCommitted transaction.");
        await using var sql = new NpgsqlCommand("""
            SELECT set_config('app.tenant_id',$1,true),set_config('app.company_ids',$2,true),set_config('app.actor_id',$3,true)
            """, connection, transaction);
        sql.Parameters.AddWithValue(scope.TenantId.ToString("D"));
        sql.Parameters.AddWithValue("{" + companyId.ToString("D") + "}");
        sql.Parameters.AddWithValue(scope.ActorId.ToString("D"));
        await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddIdentity(NpgsqlCommand sql, Guid tenantId, Guid companyId, Guid dispatchId)
    {
        sql.Parameters.AddWithValue(tenantId);
        sql.Parameters.AddWithValue(companyId);
        sql.Parameters.AddWithValue(dispatchId);
    }
}
