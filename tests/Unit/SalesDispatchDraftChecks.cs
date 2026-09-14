using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Sales.Application.Orders;

internal static class SalesDispatchDraftChecks
{
    public static void DraftSelectionIsImmutableAndCanonical()
    {
        Guid tenant = Guid.NewGuid(), company = Guid.NewGuid(), actor = Guid.NewGuid(), dispatch = Guid.NewGuid(), order = Guid.NewGuid();
        var scope = new ExecutionScope(tenant, actor, [new CompanyAccess(company, ["dispatch.create", "sales.order.view"])]);
        var first = new SalesDispatchDraftSelection(Guid.NewGuid(), Guid.NewGuid(), 3m);
        var second = new SalesDispatchDraftSelection(Guid.NewGuid(), Guid.NewGuid(), 2m);
        var date = new DateOnly(2026, 9, 10);
        var input = new List<SalesDispatchDraftSelection> { first, second };
        var command = new AuthorizedSalesDispatchDraftCommand(scope, company, dispatch, order, 4, date, input);
        var replay = new AuthorizedSalesDispatchDraftCommand(scope, company, dispatch, order, 4, date,
            [second, first with { Quantity = 3.000000m }]);
        input.Clear();
        if (command.Lines.Count != 2 || command.Fingerprint != replay.Fingerprint || command.Fingerprint.Length != 64 ||
            !command.Lines.SequenceEqual(replay.Lines))
            throw new InvalidOperationException("Dispatch selection lost snapshot or canonical retry semantics.");
        var changed = new AuthorizedSalesDispatchDraftCommand(scope, company, dispatch, order, 4, date.AddDays(1), command.Lines);
        if (changed.Fingerprint == command.Fingerprint) throw new InvalidOperationException("Draft fingerprint ignored its effective date.");
        var otherActor = new ExecutionScope(tenant, Guid.NewGuid(), [new CompanyAccess(company, ["dispatch.create", "sales.order.view"])]);
        if (new AuthorizedSalesDispatchDraftCommand(otherActor, company, dispatch, order, 4, date, command.Lines).Fingerprint == command.Fingerprint)
            throw new InvalidOperationException("Draft fingerprint ignored its actor.");
        Reject(() => _ = new AuthorizedSalesDispatchDraftCommand(scope, company, dispatch, order, 4, date, []));
        Reject(() => _ = new AuthorizedSalesDispatchDraftCommand(scope, company, dispatch, order, 4, date, [first, first]));
        Reject(() => _ = new AuthorizedSalesDispatchDraftCommand(scope, company, dispatch, order, 4, date, [first with { WarehouseId = Guid.Empty }]));
        Reject(() => _ = new AuthorizedSalesDispatchDraftCommand(scope, company, dispatch, order, 0, date, [first]));
        var max = Enumerable.Range(0, 500).Select(_ => first with { OrderLineId = Guid.NewGuid() }).ToArray();
        _ = new AuthorizedSalesDispatchDraftCommand(scope, company, dispatch, order, 4, date, max);
        Reject(() => _ = new AuthorizedSalesDispatchDraftCommand(scope, company, dispatch, order, 4, date, [.. max, second]));
        try
        {
            _ = new AuthorizedSalesDispatchDraftCommand(new ExecutionScope(tenant, actor,
                [new CompanyAccess(company, ["dispatch.create"])]), company, dispatch, order, 4, date, [first]);
        }
        catch (SalesOrderAuthorizationException exception) when (exception.Code == "SALES_DISPATCH_DRAFT_PERMISSION_REQUIRED") { return; }
        throw new InvalidOperationException("Dispatch draft accepted missing source-order read permission.");
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Invalid dispatch draft request was accepted.");
    }
}
