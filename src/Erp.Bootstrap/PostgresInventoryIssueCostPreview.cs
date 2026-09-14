using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.Bootstrap;

public sealed record InventoryIssueCostPreviewLine(StockMovementDraft Issue,
    InventoryValuationWatermark Watermark, string Currency);

/// <summary>Internal batch cost preview and audit participant. Never posts a movement or GL entry.</summary>
public static class PostgresInventoryIssueCostPreview
{
    public static async ValueTask<IReadOnlyList<InventoryIssueCostAmount>> LoadAmountsAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, IReadOnlyList<InventoryIssueCostPreviewLine> lines,
        Guid roundingPolicySnapshotId, int amountScale, RequestAuditContext audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Cost preview requires the caller's ReadCommitted transaction.");
        if (roundingPolicySnapshotId == Guid.Empty || amountScale is < 0 or > 4)
            throw new InventoryInvariantException("INVENTORY_COST_ROUNDING_REQUIRED", "An explicit ledger rounding policy and scale from zero to four are required.");
        const string savepoint = "inventory_issue_amount_preview";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            var selected = await LoadAsync(connection, transaction, scope, lines, audit, cancellationToken);
            var amounts = selected.Select(line => line.CalculateAmount(roundingPolicySnapshotId, amountScale)).ToArray();
            var reconciled = InventoryIssueCostBatch.Create(amounts);
            await PostgresAuthorizationAuditWriter.AppendAsync(connection, transaction,
                audit with { CompanyIds = new HashSet<Guid> { selected[0].Issue.CompanyId } }, Guid.CreateVersion7(),
                new AuthorizationAuditEvent("inventory.issue.amount.preview", "inventory-source-event",
                    selected[0].Issue.Source.SourceEventId.ToString("D"), "allowed", "INVENTORY_ISSUE_AMOUNT_PREVIEWED"), cancellationToken);
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return reconciled.Lines;
        }
        catch
        {
            // Amount overflow must also remove all successful inner cost-selection audits.
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }

    public static async ValueTask<IReadOnlyList<InventoryIssueCostSelection>> LoadAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, IReadOnlyList<InventoryIssueCostPreviewLine> lines,
        RequestAuditContext audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(audit);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Cost preview requires the caller's ReadCommitted transaction.");
        if (scope.TenantId != audit.TenantId || scope.ActorId != audit.ActorId || !scope.CompanyIds.SetEquals(audit.CompanyIds))
            throw new InventoryCostHistoryAccessException();
        var requests = lines.Take(501).ToArray();
        if (requests.Length is < 1 or > 500 || requests.Any(line => line is null || line.Issue is null || line.Watermark is null))
            throw new ArgumentException("Cost preview requires 1–500 complete issue lines.");
        var first = requests[0].Issue;
        if (requests.Any(line => line.Issue.TenantId != first.TenantId || line.Issue.CompanyId != first.CompanyId ||
                line.Issue.Source.SourceType != first.Source.SourceType || line.Issue.Source.SourceEventId != first.Source.SourceEventId ||
                line.Issue.Source.SourceVersion != first.Source.SourceVersion) ||
            requests.Select(line => line.Issue.MovementId).Distinct().Count() != requests.Length ||
            requests.Select(line => line.Issue.Source.SourceLineId).Distinct().Count() != requests.Length)
            throw new ArgumentException("Cost preview must contain unique lines from one source and company.");
        scope.EnsureAllowed(first.TenantId, first.CompanyId);
        if (!scope.HasPermission(first.CompanyId, PostgresInventoryCostHistoryLoader.RequiredPermission))
            throw new InventoryCostHistoryAccessException();
        foreach (var line in requests)
            InventoryIssueCostSelection.EnsureCompatible(line.Issue, line.Watermark, line.Issue.BaseUom);

        const string savepoint = "inventory_issue_cost_preview";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            var results = new List<InventoryIssueCostSelection>(requests.Length);
            foreach (var line in requests)
            {
                var history = await PostgresInventoryCostHistoryLoader.LoadAsync(connection, transaction, scope,
                    line.Watermark, line.Issue.BaseUom, line.Currency, cancellationToken);
                var selected = InventoryIssueCostSelection.Create(line.Issue, history);
                results.Add(selected);
                await PostgresAuthorizationAuditWriter.AppendAsync(connection, transaction,
                    audit with { CompanyIds = new HashSet<Guid> { first.CompanyId } }, Guid.CreateVersion7(),
                    new AuthorizationAuditEvent("inventory.issue.cost.preview", "inventory-issue-draft",
                        line.Issue.MovementId.ToString("D"), "allowed",
                        selected.Origin == InventoryIssueCostOrigin.NoHistoryZero
                            ? "INVENTORY_ISSUE_COST_NO_HISTORY_ZERO" : "INVENTORY_ISSUE_COST_LAST_KNOWN"), cancellationToken);
            }
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return results.AsReadOnly();
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }
}
