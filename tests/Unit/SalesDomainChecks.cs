using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Sales.Application.Orders;
using KaguERP.Modules.Sales.Domain.Orders;

internal static class SalesDomainChecks
{
    public static void OrderLifecycleIsAppendOnlyAndVersioned()
    {
        Guid makerId = Guid.NewGuid();
        Guid approverId = Guid.NewGuid();
        SalesOrderLifecycleState state = SalesOrderLifecycleState.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            makerId);
        SalesOrderTransitionResult submitted = Apply(state, SalesOrderTransition.Submit, makerId);
        SalesOrderTransitionResult approved = Apply(submitted.State, SalesOrderTransition.Approve, approverId);
        SalesOrderTransitionResult confirmed = Apply(approved.State, SalesOrderTransition.Confirm, approverId);
        SalesOrderLineCommitment line = SalesOrderLineCommitment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            " ea ",
            SalesOrderQuantity.Create(10m));
        SalesOrderFulfilmentAllocation firstDispatch = CreateAllocation(state, line, 4m);
        SalesOrderFulfilmentEvidence partialEvidence = SalesOrderFulfilmentEvidence.Create(
            state.TenantId,
            state.CompanyId,
            state.OrderId,
            [line],
            [firstDispatch]);
        SalesOrderTransitionResult partial = Apply(
            confirmed.State,
            SalesOrderTransition.RecordPartialFulfilment,
            approverId,
            fulfilmentEvidence: partialEvidence);
        SalesOrderFulfilmentEvidence fullEvidence = SalesOrderFulfilmentEvidence.Create(
            state.TenantId,
            state.CompanyId,
            state.OrderId,
            [line],
            [firstDispatch, CreateAllocation(state, line, 6m)]);
        SalesOrderTransitionResult fulfilled = Apply(
            partial.State,
            SalesOrderTransition.RecordFullFulfilment,
            approverId,
            fulfilmentEvidence: fullEvidence);
        SalesOrderTransitionResult closed = Apply(fulfilled.State, SalesOrderTransition.Close, approverId);

        Equal(SalesOrderStatus.Closed, closed.State.Status, "Sales order did not reach closed status.");
        Equal(7L, closed.State.Version, "Sales order lifecycle version did not advance exactly once per event.");
        Equal(state.OrderId, closed.Event.OrderId, "Sales order event lost aggregate identity.");
        Equal(SalesOrderStatus.Fulfilled, closed.Event.PreviousStatus, "Close event lost previous status.");

        Expect(
            "SALES_ORDER_VERSION_CONFLICT",
            () => SalesOrderLifecycle.Apply(
                state,
                SalesOrderTransition.Submit,
                2,
                makerId,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow));
        Expect(
            "SALES_ORDER_MAKER_CHECKER_CONFLICT",
            () => Apply(submitted.State, SalesOrderTransition.Approve, makerId));
        Expect(
            "SALES_ORDER_TRANSITION_NOT_ALLOWED",
            () => Apply(state, SalesOrderTransition.Confirm, approverId));
        Expect(
            "SALES_ORDER_REASON_REQUIRED",
            () => Apply(state, SalesOrderTransition.Cancel, makerId));
        Expect(
            "SALES_ORDER_TRANSITION_CONTEXT_INVALID",
            () => SalesOrderLifecycle.Apply(
                state,
                SalesOrderTransition.Submit,
                state.Version,
                makerId,
                Guid.NewGuid(),
                new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.FromHours(3))));
        Expect(
            "SALES_FULFILMENT_EVIDENCE_REQUIRED",
            () => Apply(
                confirmed.State,
                SalesOrderTransition.RecordPartialFulfilment,
                approverId));
        Expect(
            "SALES_FULFILMENT_STATUS_MISMATCH",
            () => Apply(
                confirmed.State,
                SalesOrderTransition.RecordFullFulfilment,
                approverId,
                fulfilmentEvidence: partialEvidence));
        Expect(
            "SALES_FULFILMENT_EXCEEDS_ORDERED",
            () => SalesOrderFulfilmentEvidence.Create(
                state.TenantId,
                state.CompanyId,
                state.OrderId,
                [line],
                [CreateAllocation(state, line, 11m)]));
    }

    public static void OrderCommandsEnforcePermissionAndPersistenceBoundary()
    {
        Guid tenantId = Guid.NewGuid();
        Guid companyId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();
        var denied = new ExecutionScope(tenantId, actorId, [companyId]);
        ExpectAuthorization(
            "SALES_ORDER_CREATE_PERMISSION_REQUIRED",
            () => AuthorizedSalesOrderCreateCommand.Create(denied, companyId, orderId));

        var allowed = new ExecutionScope(
            tenantId,
            actorId,
            [new CompanyAccess(
                companyId,
                ["sales.order.create", "sales.order.submit", "sales.order.view", "sales.fulfilment.record"])]);
        _ = AuthorizedSalesOrderCreateCommand.Create(allowed, companyId, orderId);
        _ = AuthorizedSalesOrderLifecycleQuery.Create(allowed, companyId, orderId);
        _ = AuthorizedSalesOrderTransitionCommand.Create(
            allowed,
            companyId,
            orderId,
            1,
            SalesOrderTransition.Submit,
            Guid.NewGuid(),
            new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero));
        ExpectAuthorization(
            "SALES_ORDER_FULFILMENT_PERSISTENCE_NOT_READY",
            () => AuthorizedSalesOrderTransitionCommand.Create(
                allowed,
                companyId,
                orderId,
                4,
                SalesOrderTransition.RecordPartialFulfilment,
                Guid.NewGuid(),
                new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero)));
        ExpectAuthorization(
            "SALES_ORDER_TRANSITION_COMMAND_INVALID",
            () => AuthorizedSalesOrderTransitionCommand.Create(
                allowed,
                companyId,
                orderId,
                1,
                SalesOrderTransition.Submit,
                Guid.NewGuid(),
                new DateTimeOffset(638925840000000001, TimeSpan.Zero)));

        SalesOrderLifecycleState draft = SalesOrderLifecycleState.CreateDraft(
            tenantId,
            companyId,
            orderId,
            actorId);
        SalesOrderTransitionResult submitted = SalesOrderLifecycle.Apply(
            draft,
            SalesOrderTransition.Submit,
            draft.Version,
            actorId,
            Guid.NewGuid(),
            new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero));
        var view = new SalesOrderLifecycleView(submitted.State, [submitted.Event]);
        Equal(1, view.Transitions.Count, "Sales order lifecycle view lost its transition history.");
        ExpectView(
            "SALES_ORDER_TIMELINE_STATE_MISMATCH",
            () => _ = new SalesOrderLifecycleView(submitted.State, []));
    }

    private static SalesOrderTransitionResult Apply(
        SalesOrderLifecycleState state,
        SalesOrderTransition transition,
        Guid actorId,
        string? reason = null,
        SalesOrderFulfilmentEvidence? fulfilmentEvidence = null) =>
        SalesOrderLifecycle.Apply(
            state,
            transition,
            state.Version,
            actorId,
            Guid.NewGuid(),
            new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero),
            reason,
            fulfilmentEvidence: fulfilmentEvidence);

    private static SalesOrderFulfilmentAllocation CreateAllocation(
        SalesOrderLifecycleState state,
        SalesOrderLineCommitment line,
        decimal quantity) =>
        SalesOrderFulfilmentAllocation.Create(
            Guid.NewGuid(),
            state.TenantId,
            state.CompanyId,
            state.OrderId,
            line.OrderLineId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            SalesOrderQuantity.Create(quantity));

    private static void Expect(string expectedCode, Action action)
    {
        try
        {
            action();
        }
        catch (SalesOrderLifecycleException exception) when (exception.Code == expectedCode)
        {
            return;
        }

        throw new InvalidOperationException($"Expected sales order lifecycle error {expectedCode}.");
    }

    private static void ExpectAuthorization(string expectedCode, Action action)
    {
        try
        {
            action();
        }
        catch (SalesOrderAuthorizationException exception) when (exception.Code == expectedCode)
        {
            return;
        }

        throw new InvalidOperationException($"Expected sales order authorization error {expectedCode}.");
    }

    private static void ExpectView(string expectedCode, Action action)
    {
        try
        {
            action();
        }
        catch (SalesOrderLifecycleViewException exception) when (exception.Code == expectedCode)
        {
            return;
        }

        throw new InvalidOperationException($"Expected sales order lifecycle view error {expectedCode}.");
    }

    private static void Equal<T>(T expected, T actual, string message) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected {expected}; actual {actual}.");
        }
    }
}
