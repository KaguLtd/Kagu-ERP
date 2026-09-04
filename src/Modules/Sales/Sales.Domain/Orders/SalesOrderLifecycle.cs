namespace KaguERP.Modules.Sales.Domain.Orders;

public enum SalesOrderStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Confirmed = 4,
    PartiallyFulfilled = 5,
    Fulfilled = 6,
    Closed = 7,
    Rejected = 8,
    Cancelled = 9,
}

public enum SalesOrderTransition
{
    Submit = 1,
    Approve = 2,
    Reject = 3,
    Withdraw = 4,
    Revise = 5,
    Confirm = 6,
    RecordPartialFulfilment = 7,
    RecordFullFulfilment = 8,
    Close = 9,
    Cancel = 10,
}

public sealed record SalesOrderLifecycleState
{
    private SalesOrderLifecycleState(
        Guid tenantId,
        Guid companyId,
        Guid orderId,
        Guid makerId,
        long version,
        SalesOrderStatus status)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        OrderId = orderId;
        MakerId = makerId;
        Version = version;
        Status = status;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid OrderId { get; }
    public Guid MakerId { get; }
    public long Version { get; }
    public SalesOrderStatus Status { get; }

    public static SalesOrderLifecycleState CreateDraft(
        Guid tenantId,
        Guid companyId,
        Guid orderId,
        Guid makerId)
    {
        RequireId(tenantId, "SALES_ORDER_TENANT_REQUIRED");
        RequireId(companyId, "SALES_ORDER_COMPANY_REQUIRED");
        RequireId(orderId, "SALES_ORDER_ID_REQUIRED");
        RequireId(makerId, "SALES_ORDER_MAKER_REQUIRED");
        return new SalesOrderLifecycleState(
            tenantId,
            companyId,
            orderId,
            makerId,
            1,
            SalesOrderStatus.Draft);
    }

    public static SalesOrderLifecycleState Rehydrate(
        Guid tenantId,
        Guid companyId,
        Guid orderId,
        Guid makerId,
        long version,
        SalesOrderStatus status)
    {
        RequireId(tenantId, "SALES_ORDER_TENANT_REQUIRED");
        RequireId(companyId, "SALES_ORDER_COMPANY_REQUIRED");
        RequireId(orderId, "SALES_ORDER_ID_REQUIRED");
        RequireId(makerId, "SALES_ORDER_MAKER_REQUIRED");
        if (version <= 0 || !Enum.IsDefined(status))
        {
            throw new SalesOrderLifecycleException(
                "SALES_ORDER_PERSISTED_STATE_INVALID",
                "Persisted sales order status and version must be valid.");
        }

        return new SalesOrderLifecycleState(tenantId, companyId, orderId, makerId, version, status);
    }

    internal SalesOrderLifecycleState Advance(
        SalesOrderStatus status,
        Guid makerId) =>
        new(TenantId, CompanyId, OrderId, makerId, checked(Version + 1), status);

    private static void RequireId(Guid id, string code)
    {
        if (id == Guid.Empty)
        {
            throw new SalesOrderLifecycleException(code, "Sales order lifecycle identity is required.");
        }
    }
}

public sealed record SalesOrderTransitionEvent(
    Guid EventId,
    Guid TenantId,
    Guid CompanyId,
    Guid OrderId,
    long PreviousVersion,
    long NewVersion,
    SalesOrderStatus PreviousStatus,
    SalesOrderStatus NewStatus,
    SalesOrderTransition Transition,
    Guid ActorId,
    Guid CorrelationId,
    DateTimeOffset OccurredAt,
    string? Reason);

public sealed record SalesOrderTransitionResult(
    SalesOrderLifecycleState State,
    SalesOrderTransitionEvent Event);

public static class SalesOrderLifecycle
{
    public static SalesOrderTransitionResult Apply(
        SalesOrderLifecycleState current,
        SalesOrderTransition transition,
        long expectedVersion,
        Guid actorId,
        Guid correlationId,
        DateTimeOffset occurredAt,
        string? reason = null,
        Guid? eventId = null,
        SalesOrderFulfilmentEvidence? fulfilmentEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (!Enum.IsDefined(transition))
        {
            throw new SalesOrderLifecycleException(
                "SALES_ORDER_TRANSITION_INVALID",
                "Sales order transition is invalid.");
        }
        if (expectedVersion != current.Version)
        {
            throw new SalesOrderLifecycleException(
                "SALES_ORDER_VERSION_CONFLICT",
                "Sales order changed after the caller's expected version.");
        }
        if (actorId == Guid.Empty || correlationId == Guid.Empty || occurredAt.Offset != TimeSpan.Zero)
        {
            throw new SalesOrderLifecycleException(
                "SALES_ORDER_TRANSITION_CONTEXT_INVALID",
                "Sales order transition requires actor, correlation and UTC occurrence time.");
        }

        string? normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (normalizedReason?.Length > 500)
        {
            throw new SalesOrderLifecycleException(
                "SALES_ORDER_REASON_INVALID",
                "Sales order transition reason cannot exceed 500 characters.");
        }
        if (transition is SalesOrderTransition.Reject or SalesOrderTransition.Revise or
            SalesOrderTransition.Cancel && normalizedReason is null)
        {
            throw new SalesOrderLifecycleException(
                "SALES_ORDER_REASON_REQUIRED",
                "Reject, revise and cancel transitions require a reason.");
        }
        if (transition is SalesOrderTransition.Approve or SalesOrderTransition.Reject &&
            actorId == current.MakerId)
        {
            throw new SalesOrderLifecycleException(
                "SALES_ORDER_MAKER_CHECKER_CONFLICT",
                "The sales order maker cannot approve or reject the same submitted version.");
        }
        if (transition is SalesOrderTransition.RecordPartialFulfilment or
            SalesOrderTransition.RecordFullFulfilment)
        {
            if (fulfilmentEvidence is null)
            {
                throw new SalesOrderLifecycleException(
                    "SALES_FULFILMENT_EVIDENCE_REQUIRED",
                    "Fulfilment transition requires quantity evidence.");
            }
            fulfilmentEvidence.EnsureMatches(current);
            if (transition == SalesOrderTransition.RecordPartialFulfilment &&
                !fulfilmentEvidence.IsPartiallyFulfilled ||
                transition == SalesOrderTransition.RecordFullFulfilment &&
                !fulfilmentEvidence.IsFullyFulfilled)
            {
                throw new SalesOrderLifecycleException(
                    "SALES_FULFILMENT_STATUS_MISMATCH",
                    "Fulfilment quantities do not support the requested lifecycle transition.");
            }
        }

        SalesOrderStatus nextStatus = ResolveNextStatus(current.Status, transition);
        Guid nextMakerId = transition == SalesOrderTransition.Submit ? actorId : current.MakerId;
        SalesOrderLifecycleState next = current.Advance(nextStatus, nextMakerId);
        Guid resolvedEventId = eventId ?? Guid.CreateVersion7();
        if (resolvedEventId == Guid.Empty)
        {
            throw new SalesOrderLifecycleException(
                "SALES_ORDER_EVENT_ID_REQUIRED",
                "Sales order transition event identity is required.");
        }

        var lifecycleEvent = new SalesOrderTransitionEvent(
            resolvedEventId,
            current.TenantId,
            current.CompanyId,
            current.OrderId,
            current.Version,
            next.Version,
            current.Status,
            next.Status,
            transition,
            actorId,
            correlationId,
            occurredAt,
            normalizedReason);
        return new SalesOrderTransitionResult(next, lifecycleEvent);
    }

    private static SalesOrderStatus ResolveNextStatus(
        SalesOrderStatus current,
        SalesOrderTransition transition) =>
        (current, transition) switch
        {
            (SalesOrderStatus.Draft, SalesOrderTransition.Submit) => SalesOrderStatus.Submitted,
            (SalesOrderStatus.Draft, SalesOrderTransition.Cancel) => SalesOrderStatus.Cancelled,
            (SalesOrderStatus.Submitted, SalesOrderTransition.Approve) => SalesOrderStatus.Approved,
            (SalesOrderStatus.Submitted, SalesOrderTransition.Reject) => SalesOrderStatus.Rejected,
            (SalesOrderStatus.Submitted, SalesOrderTransition.Withdraw) => SalesOrderStatus.Draft,
            (SalesOrderStatus.Rejected, SalesOrderTransition.Revise) => SalesOrderStatus.Draft,
            (SalesOrderStatus.Approved, SalesOrderTransition.Confirm) => SalesOrderStatus.Confirmed,
            (SalesOrderStatus.Approved, SalesOrderTransition.Cancel) => SalesOrderStatus.Cancelled,
            (SalesOrderStatus.Confirmed, SalesOrderTransition.RecordPartialFulfilment) =>
                SalesOrderStatus.PartiallyFulfilled,
            (SalesOrderStatus.Confirmed, SalesOrderTransition.RecordFullFulfilment) => SalesOrderStatus.Fulfilled,
            (SalesOrderStatus.Confirmed, SalesOrderTransition.Cancel) => SalesOrderStatus.Cancelled,
            (SalesOrderStatus.PartiallyFulfilled, SalesOrderTransition.RecordPartialFulfilment) =>
                SalesOrderStatus.PartiallyFulfilled,
            (SalesOrderStatus.PartiallyFulfilled, SalesOrderTransition.RecordFullFulfilment) =>
                SalesOrderStatus.Fulfilled,
            (SalesOrderStatus.Fulfilled, SalesOrderTransition.Close) => SalesOrderStatus.Closed,
            _ => throw new SalesOrderLifecycleException(
                "SALES_ORDER_TRANSITION_NOT_ALLOWED",
                $"Transition {transition} is not allowed from {current} sales order status."),
        };
}

public sealed class SalesOrderLifecycleException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
