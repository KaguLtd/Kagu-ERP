using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Sales.Application.Orders;

internal static class SalesDispatchQueryChecks
{
    public static void QueryRequiresScopeAndBothPermissions()
    {
        Guid tenant = Guid.NewGuid(), actor = Guid.NewGuid(), company = Guid.NewGuid(), draft = Guid.NewGuid();
        ExecutionScope Scope(params string[] permissions) => new(tenant, actor, [new CompanyAccess(company, permissions)]);
        var allowed = Scope("dispatch.create", "sales.order.view");
        var query = new AuthorizedSalesDispatchDraftQuery(allowed, company, draft);
        if (query.Scope != allowed || query.CompanyId != company || query.DispatchId != draft)
            throw new InvalidOperationException("Dispatch query changed trusted identities.");
        foreach (var scope in new[] { Scope(), Scope("dispatch.create"), Scope("sales.order.view") })
        {
            try { _ = new AuthorizedSalesDispatchDraftQuery(scope, company, draft); }
            catch (SalesOrderAuthorizationException) { continue; }
            throw new InvalidOperationException("Dispatch query accepted incomplete permissions.");
        }
        bool scopeRejected = false;
        try { _ = new AuthorizedSalesDispatchDraftQuery(allowed, Guid.NewGuid(), draft); }
        catch (ExecutionScopeDeniedException) { scopeRejected = true; }
        if (!scopeRejected) throw new InvalidOperationException("Dispatch query accepted another company.");
        try { _ = new AuthorizedSalesDispatchDraftQuery(allowed, company, Guid.Empty); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Dispatch query accepted an empty identity.");
    }
}
