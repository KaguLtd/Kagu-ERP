using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Application.Reservations;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;
using KaguERP.Modules.Sales.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.Bootstrap;

public sealed record SalesDispatchReservationPreview(SalesDispatchDraftSnapshot Draft,
    IReadOnlyList<InventoryDispatchReservationLinePreview> Lines);

/// <summary>Internal read preparation, never a posting receipt. All locks live until the caller ends the transaction.</summary>
public static class PostgresSalesDispatchReservationPreview
{
    public static async ValueTask<SalesDispatchReservationPreview> LoadAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, Guid companyId, Guid dispatchId,
        RequestAuditContext audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Dispatch reservation preview requires the same ReadCommitted transaction.");
        const string savepoint = "sales_dispatch_reservation_preview";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            var draft = await PostgresSalesDispatchDraftOrchestrator.LoadAsync(connection, transaction, scope,
                companyId, dispatchId, audit, cancellationToken);
            // Immutable draft replay is historical; posting preparation must ALSO validate the current source.
            _ = await PostgresSalesDispatchPreparationLoader.LoadAsync(connection, transaction, scope, companyId,
                draft.OrderId, draft.OrderVersion, draft.Lines.Select(line => new SalesDispatchLineRequest(
                    line.OrderLineId, SalesOrderQuantity.Create(line.Quantity))), cancellationToken);
            var queries = draft.Lines.Select(line => new InventoryDispatchReservationLineQuery(
                InventoryDemandSourceIdentity.Create("sales.order", draft.OrderId, line.OrderLineId, draft.OrderVersion),
                line.ItemId, line.WarehouseId, InventoryUomCode.Create(line.BaseUomCode),
                InventoryQuantity.Create(line.Quantity), draft.EffectiveDate)).ToArray();
            var lines = await PostgresInventoryDispatchReservationPreview.LoadAsync(connection, transaction,
                scope, companyId, queries, cancellationToken);
            await PostgresAuthorizationAuditWriter.AppendAsync(connection, transaction,
                audit with { CompanyIds = new HashSet<Guid> { companyId } }, Guid.CreateVersion7(),
                new AuthorizationAuditEvent("dispatch.reservation.preview", "sales-dispatch-draft", dispatchId.ToString("D"),
                    "allowed", "SALES_DISPATCH_RESERVATION_PREVIEWED"), cancellationToken);
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return new(draft, lines);
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }
}
