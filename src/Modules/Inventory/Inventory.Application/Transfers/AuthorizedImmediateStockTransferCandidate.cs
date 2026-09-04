using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Domain;

namespace KaguERP.Modules.Inventory.Application.Transfers;

public sealed class AuthorizedImmediateStockTransferCandidate
{
    public const string RequiredPermission = "inventory.transfer.post";

    private AuthorizedImmediateStockTransferCandidate(
        ExecutionScope scope,
        InventoryWarehouseScopeEvidence warehouseScope,
        ValidatedImmediateStockTransferDraft transfer)
    {
        Scope = scope;
        WarehouseScope = warehouseScope;
        Transfer = transfer;
    }

    public ExecutionScope Scope { get; }

    public InventoryWarehouseScopeEvidence WarehouseScope { get; }

    public ValidatedImmediateStockTransferDraft Transfer { get; }

    public static AuthorizedImmediateStockTransferCandidate Create(
        ExecutionScope scope,
        InventoryWarehouseScopeEvidence warehouseScope,
        ValidatedImmediateStockTransferDraft transfer)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(warehouseScope);
        ArgumentNullException.ThrowIfNull(transfer);

        StockMovementDraft issue = transfer.SourceIssue;
        scope.EnsureAllowed(issue.TenantId, issue.CompanyId);
        if (!scope.HasPermission(issue.CompanyId, RequiredPermission))
        {
            throw new InventoryTransferAuthorizationException(
                "INVENTORY_TRANSFER_PERMISSION_REQUIRED",
                "The active actor cannot post inventory transfers for this company.");
        }

        warehouseScope.EnsureMatches(issue.TenantId, issue.CompanyId, scope.ActorId);
        if (!warehouseScope.WarehouseIds.Contains(issue.WarehouseId) ||
            !warehouseScope.WarehouseIds.Contains(transfer.DestinationReceipt.WarehouseId))
        {
            throw new InventoryTransferAuthorizationException(
                "INVENTORY_TRANSFER_WAREHOUSE_SCOPE_REQUIRED",
                "The active actor must be scoped to both transfer warehouses.");
        }

        return new AuthorizedImmediateStockTransferCandidate(scope, warehouseScope, transfer);
    }
}

public sealed class InventoryTransferAuthorizationException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
