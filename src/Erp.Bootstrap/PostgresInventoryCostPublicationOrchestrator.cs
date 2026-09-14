using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.Bootstrap;

public static class PostgresInventoryCostPublicationOrchestrator
{
    public static async ValueTask<InventoryCostPublicationOutcome> PublishAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, Guid publicationId, InventoryCostHistoryEvidence evidence,
        RequestAuditContext audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(audit);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Publication audit requires the caller's ReadCommitted transaction.");
        if (scope.TenantId != audit.TenantId || scope.ActorId != audit.ActorId || !scope.CompanyIds.SetEquals(audit.CompanyIds))
            throw new InventoryCostHistoryAccessException();
        const string savepoint = "inventory_cost_publication_audit";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            var result = await PostgresInventoryCostPublicationWriter.WriteAsync(connection, transaction, scope,
                publicationId, evidence, cancellationToken);
            await PostgresAuthorizationAuditWriter.AppendAsync(connection, transaction,
                audit with { CompanyIds = new HashSet<Guid> { evidence.Watermark.CompanyId } }, Guid.CreateVersion7(),
                new AuthorizationAuditEvent("inventory.cost.publish", "inventory-cost-publication", publicationId.ToString("D"),
                    "allowed", result.Created ? "INVENTORY_COST_PUBLISHED" : "INVENTORY_COST_PUBLICATION_REPLAYED"), cancellationToken);
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }
}
