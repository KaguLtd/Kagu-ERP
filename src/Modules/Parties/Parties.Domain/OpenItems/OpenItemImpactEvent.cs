using KaguERP.Modules.Parties.Domain.Allocations;
using KaguERP.Modules.Parties.Domain.DueSchedules;

namespace KaguERP.Modules.Parties.Domain.OpenItems;

public sealed record OpenItemImpactEvent
{
    private OpenItemImpactEvent(
        Guid eventId,
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid dueScheduleLineId,
        Guid? paymentId,
        AllocationCurrencyCode currency,
        OpenItemImpactKind kind,
        decimal amount,
        DateOnly effectiveDate,
        DateTimeOffset recordedAt,
        Guid? reversesEventId)
    {
        EventId = eventId;
        TenantId = tenantId;
        CompanyId = companyId;
        PartyAccountId = partyAccountId;
        DueScheduleLineId = dueScheduleLineId;
        PaymentId = paymentId;
        Currency = currency;
        Kind = kind;
        Amount = amount;
        EffectiveDate = effectiveDate;
        RecordedAt = recordedAt;
        ReversesEventId = reversesEventId;
    }

    public Guid EventId { get; }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public Guid PartyAccountId { get; }

    public Guid DueScheduleLineId { get; }

    public Guid? PaymentId { get; }

    public AllocationCurrencyCode Currency { get; }

    public OpenItemImpactKind Kind { get; }

    public decimal Amount { get; }

    public DateOnly EffectiveDate { get; }

    public DateTimeOffset RecordedAt { get; }

    public Guid? ReversesEventId { get; }

    public static OpenItemImpactEvent Create(
        Guid eventId,
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid dueScheduleLineId,
        Guid? paymentId,
        AllocationCurrencyCode? currency,
        OpenItemImpactKind kind,
        decimal amount,
        DateOnly effectiveDate,
        DateTimeOffset recordedAt,
        Guid? reversesEventId = null)
    {
        RequireId(eventId, "OPEN_ITEM_EVENT_REQUIRED", "Open-item impact event ID is required.");
        RequireId(tenantId, "OPEN_ITEM_TENANT_REQUIRED", "Open-item tenant ID is required.");
        RequireId(companyId, "OPEN_ITEM_COMPANY_REQUIRED", "Open-item company ID is required.");
        RequireId(partyAccountId, "OPEN_ITEM_PARTY_ACCOUNT_REQUIRED", "Open-item party-account ID is required.");
        RequireId(dueScheduleLineId, "OPEN_ITEM_DUE_LINE_REQUIRED", "Open-item due-schedule line ID is required.");
        ArgumentNullException.ThrowIfNull(currency);

        if (!Enum.IsDefined(kind))
        {
            throw new PartyOpenItemInvariantException("OPEN_ITEM_EVENT_KIND_INVALID", "Open-item event kind is invalid.");
        }

        if (amount <= decimal.Zero)
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_EVENT_AMOUNT_INVALID",
                "Open-item impact amount must be positive.");
        }

        if (recordedAt.Offset != TimeSpan.Zero)
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_RECORDED_AT_NOT_UTC",
                "Open-item recorded timestamp must use the UTC offset.");
        }

        var hasPaymentImpact = kind is OpenItemImpactKind.Allocation or OpenItemImpactKind.Unallocation;
        if (hasPaymentImpact && (!paymentId.HasValue || paymentId == Guid.Empty))
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_PAYMENT_REQUIRED",
                "Allocation and unallocation impacts require a payment ID.");
        }

        if (!hasPaymentImpact && paymentId.HasValue)
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_PAYMENT_UNEXPECTED",
                "Write-off impacts cannot contain a payment ID.");
        }

        var isCounterEvent = kind is OpenItemImpactKind.Unallocation or OpenItemImpactKind.WriteOffReversal;
        if (isCounterEvent && (!reversesEventId.HasValue || reversesEventId == Guid.Empty))
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_REVERSAL_LINK_REQUIRED",
                "An open-item counter-event must link to the original event.");
        }

        if (!isCounterEvent && reversesEventId.HasValue)
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_REVERSAL_LINK_UNEXPECTED",
                "An original open-item event cannot contain a reversal link.");
        }

        return new OpenItemImpactEvent(
            eventId,
            tenantId,
            companyId,
            partyAccountId,
            dueScheduleLineId,
            paymentId,
            currency,
            kind,
            amount,
            effectiveDate,
            recordedAt,
            reversesEventId);
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new PartyOpenItemInvariantException(code, message);
        }
    }
}
