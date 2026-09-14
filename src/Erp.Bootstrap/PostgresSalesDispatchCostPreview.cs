using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Domain;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using KaguERP.Modules.Sales.Application.Orders;
using Npgsql;

namespace KaguERP.Bootstrap;

public sealed record SalesDispatchCostLineContext(Guid OrderLineId, Guid MovementId, long SequenceKey,
    InventoryValuationWatermark Watermark, string Currency);

public sealed record SalesDispatchCostHistoryContext(Guid OrderLineId, Guid MovementId,
    InventoryValuationWatermark Watermark, string Currency);

public sealed record SalesDispatchCostPreview(SalesDispatchReservationPreview Reservation,
    IReadOnlyList<InventoryIssueCostAmount> Amounts);

/// <summary>Internal composition only; sequence, currency and rounding authorities are not client inputs.</summary>
public static class PostgresSalesDispatchCostPreview
{
    public static async ValueTask<SalesDispatchCostPreview> LoadAllocatedAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, Guid companyId, Guid dispatchId,
        IReadOnlyList<SalesDispatchCostHistoryContext> contexts, Guid roundingPolicySnapshotId,
        int amountScale, RequestAuditContext audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(audit);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Dispatch cost preparation requires the caller's ReadCommitted transaction.");
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, PostgresInventoryCostHistoryLoader.RequiredPermission) ||
            audit.TenantId != scope.TenantId || audit.ActorId != scope.ActorId || !scope.CompanyIds.SetEquals(audit.CompanyIds))
            throw new InventoryCostHistoryAccessException();
        if (roundingPolicySnapshotId == Guid.Empty || amountScale is < 0 or > 4)
            throw new InventoryInvariantException("INVENTORY_COST_ROUNDING_REQUIRED", "An explicit ledger rounding policy and scale are required.");
        var captured = contexts.Take(501).ToArray();
        if (captured.Length is < 1 or > 500 || captured.Any(line => line is null || line.Watermark is null) ||
            captured.Select(line => line.OrderLineId).Distinct().Count() != captured.Length)
            throw new ArgumentException("Cost preparation requires 1–500 unique source lines.");
        const string savepoint = "sales_dispatch_allocated_cost";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            // Reservation participant acquires all demand locks before all position locks.
            var reservation = await PostgresSalesDispatchReservationPreview.LoadAsync(connection, transaction,
                scope, companyId, dispatchId, audit, cancellationToken);
            var draft = reservation.Draft;
            if (!draft.Lines.Select(line => line.OrderLineId).ToHashSet().SetEquals(captured.Select(line => line.OrderLineId)))
                throw new ArgumentException("Cost contexts must match the complete persisted draft.");
            var byLine = captured.ToDictionary(line => line.OrderLineId);
            // Validate exact stock targets before requesting additional locks.
            foreach (var line in draft.Lines)
            {
                var mark = byLine[line.OrderLineId].Watermark;
                if (mark.TenantId != draft.TenantId || mark.CompanyId != draft.CompanyId ||
                    mark.ItemId != line.ItemId || mark.WarehouseId != line.WarehouseId)
                    throw new InventoryInvariantException("INVENTORY_POSITION_SCOPE_CONFLICT", "Cost watermark does not belong to the draft stock position.");
            }
            var positions = await PostgresInventoryIssuePositionAllocator.PrepareAsync(connection, transaction, scope,
                companyId, draft.EffectiveDate, draft.RecordedAt, draft.Lines.Select(line =>
                    new InventoryIssuePositionRequest(line.OrderLineId, byLine[line.OrderLineId].Watermark,
                        InventoryUomCode.Create(line.BaseUomCode))).ToArray(), cancellationToken);
            var sequenceByLine = positions.Lines.ToDictionary(line => line.LineId, line => line.SequenceKey);
            var lines = BuildLines(draft, captured.Select(line => new SalesDispatchCostLineContext(line.OrderLineId,
                line.MovementId, sequenceByLine[line.OrderLineId], line.Watermark, line.Currency)).ToArray(), positions.RecordedAt);
            var amounts = await PostgresInventoryIssueCostPreview.LoadAmountsAsync(connection, transaction, scope,
                lines, roundingPolicySnapshotId, amountScale, audit, cancellationToken);
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return new(reservation, amounts);
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }

    public static IReadOnlyList<InventoryIssueCostPreviewLine> BuildLines(SalesDispatchDraftSnapshot draft,
        IReadOnlyList<SalesDispatchCostLineContext> contexts, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(draft.Lines);
        var lines = draft.Lines.Take(501).ToArray();
        var supplied = contexts.Take(501).ToArray();
        if (lines.Length is < 1 or > 500 || supplied.Length != lines.Length ||
            lines.Any(line => line is null) || supplied.Any(line => line is null || line.Watermark is null) ||
            lines.Select(line => line.OrderLineId).Distinct().Count() != lines.Length ||
            supplied.Select(line => line.OrderLineId).Distinct().Count() != supplied.Length ||
            supplied.Select(line => line.MovementId).Distinct().Count() != supplied.Length ||
            supplied.Select(line => line.Currency).Distinct(StringComparer.Ordinal).Count() != 1 ||
            !lines.Select(line => line.OrderLineId).ToHashSet().SetEquals(supplied.Select(line => line.OrderLineId)))
            throw new ArgumentException("Dispatch cost context must match every draft line exactly once.");
        if (draft.OrderId == Guid.Empty || draft.OrderVersion <= 0 || draft.CreatedBy == Guid.Empty ||
            draft.RecordedAt == default || draft.RecordedAt.Offset != TimeSpan.Zero ||
            draft.RecordedAt.Ticks % TimeSpan.TicksPerMicrosecond != 0 || recordedAt < draft.RecordedAt)
            throw new ArgumentException("Dispatch cost preview requires valid source identity and recording chronology.");
        var lookup = supplied.ToDictionary(line => line.OrderLineId);
        var result = new List<InventoryIssueCostPreviewLine>(lines.Length);
        var positions = new HashSet<(Guid Item, Guid Warehouse, long Sequence)>();
        foreach (var line in lines)
        {
            var context = lookup[line.OrderLineId];
            if (line.Quantity <= 0m || !positions.Add((line.ItemId, line.WarehouseId, context.SequenceKey)))
                throw new ArgumentException("Dispatch quantity must be positive and valuation positions unique per stock position.");
            var issue = StockMovementDraft.Create(context.MovementId, draft.TenantId, draft.CompanyId, line.ItemId,
                line.WarehouseId, InventoryUomCode.Create(line.BaseUomCode), StockMovementKind.Issue,
                InventoryQuantity.Create(-line.Quantity), draft.EffectiveDate, recordedAt, context.SequenceKey,
                StockMovementSourceIdentity.Create(draft.TenantId, draft.CompanyId, "sales.dispatch", draft.DispatchId,
                    line.OrderLineId, 1, "issue"));
            InventoryIssueCostSelection.EnsureCompatible(issue, context.Watermark, issue.BaseUom);
            // Validate currency without interpreting missing publication as no history.
            _ = InventoryCostHistoryEvidence.NoHistory(context.Watermark, issue.BaseUom, context.Currency);
            result.Add(new(issue, context.Watermark, context.Currency));
        }
        return result.AsReadOnly();
    }

    public static async ValueTask<SalesDispatchCostPreview> LoadAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, Guid companyId, Guid dispatchId,
        IReadOnlyList<SalesDispatchCostLineContext> contexts, DateTimeOffset recordedAt,
        Guid roundingPolicySnapshotId, int amountScale, RequestAuditContext audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(audit);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Dispatch cost preview requires the caller's ReadCommitted transaction.");
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, PostgresInventoryCostHistoryLoader.RequiredPermission) ||
            audit.TenantId != scope.TenantId || audit.ActorId != scope.ActorId || !scope.CompanyIds.SetEquals(audit.CompanyIds))
            throw new InventoryCostHistoryAccessException();
        if (roundingPolicySnapshotId == Guid.Empty || amountScale is < 0 or > 4)
            throw new InventoryInvariantException("INVENTORY_COST_ROUNDING_REQUIRED", "An explicit ledger rounding policy and scale are required.");
        var captured = contexts.Take(501).ToArray();
        const string savepoint = "sales_dispatch_cost_preview";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            var reservation = await PostgresSalesDispatchReservationPreview.LoadAsync(connection, transaction,
                scope, companyId, dispatchId, audit, cancellationToken);
            var lines = BuildLines(reservation.Draft, captured, recordedAt);
            var amounts = await PostgresInventoryIssueCostPreview.LoadAmountsAsync(connection, transaction, scope,
                lines, roundingPolicySnapshotId, amountScale, audit, cancellationToken);
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return new(reservation, amounts);
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }
}
