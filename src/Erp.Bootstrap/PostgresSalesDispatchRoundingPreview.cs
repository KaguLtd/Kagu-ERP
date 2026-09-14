using KaguERP.BuildingBlocks.Application.Audit;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Accounting.Domain.Currencies;
using KaguERP.Modules.Accounting.Infrastructure.Persistence;
using KaguERP.Modules.Inventory.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.Bootstrap;

public sealed record SalesDispatchRoundingPreview(RoundingPolicySnapshot Policy, SalesDispatchCostPreview Preview);

public static class PostgresSalesDispatchRoundingPreview
{
    public static async ValueTask<SalesDispatchRoundingPreview> LoadAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, ExecutionScope scope, Guid companyId, Guid dispatchId,
        IReadOnlyList<SalesDispatchCostHistoryContext> contexts, Guid policyId, long expectedPolicyVersion,
        RequestAuditContext audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(contexts);
        if (!ReferenceEquals(transaction.Connection, connection) || transaction.IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new ArgumentException("Dispatch rounding preview requires the caller's ReadCommitted transaction.");
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, "dispatch.create") || !scope.HasPermission(companyId, "sales.order.view") ||
            !scope.HasPermission(companyId, PostgresInventoryCostHistoryLoader.RequiredPermission) ||
            audit.TenantId != scope.TenantId || audit.ActorId != scope.ActorId || !scope.CompanyIds.SetEquals(audit.CompanyIds))
            throw new InventoryCostHistoryAccessException();
        const string savepoint = "dispatch_rounding_preview";
        await transaction.SaveAsync(savepoint, cancellationToken);
        try
        {
            var policy = await PostgresRoundingPolicySnapshotLoader.LoadAsync(connection, transaction, scope,
                companyId, policyId, expectedPolicyVersion, cancellationToken);
            if (policy.Mode != RoundingMode.AwayFromZero || policy.Scale != 2)
                throw new AuthoritativeCurrencyEvidenceException("DISPATCH_ROUNDING_POLICY_UNSUPPORTED",
                    "Dispatch amount rounding must match the approved two-decimal AwayFromZero policy.");
            var preview = await PostgresSalesDispatchCostPreview.LoadAllocatedAsync(connection, transaction,
                scope, companyId, dispatchId, contexts, policy.PolicyId, policy.Scale, audit, cancellationToken);
            await transaction.ReleaseAsync(savepoint, cancellationToken);
            return new(policy, preview);
        }
        catch
        {
            await transaction.RollbackAsync(savepoint, CancellationToken.None);
            await transaction.ReleaseAsync(savepoint, CancellationToken.None);
            throw;
        }
    }
}
