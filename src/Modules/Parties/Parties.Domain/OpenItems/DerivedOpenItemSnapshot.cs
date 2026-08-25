using System.Collections.ObjectModel;
using KaguERP.Modules.Parties.Domain.DueSchedules;

namespace KaguERP.Modules.Parties.Domain.OpenItems;

public sealed class DerivedOpenItemSnapshot
{
    private DerivedOpenItemSnapshot(
        DueScheduleLine dueScheduleLine,
        DateOnly asOfEffectiveDate,
        DateTimeOffset recordedCutoff,
        ReadOnlyCollection<OpenItemImpactEvent> consideredEvents,
        decimal allocatedAmount,
        decimal writtenOffAmount)
    {
        DueScheduleLine = dueScheduleLine;
        AsOfEffectiveDate = asOfEffectiveDate;
        RecordedCutoff = recordedCutoff;
        ConsideredEvents = consideredEvents;
        AllocatedAmount = allocatedAmount;
        WrittenOffAmount = writtenOffAmount;
    }

    public DueScheduleLine DueScheduleLine { get; }

    public DateOnly AsOfEffectiveDate { get; }

    public DateTimeOffset RecordedCutoff { get; }

    public IReadOnlyList<OpenItemImpactEvent> ConsideredEvents { get; }

    public decimal AllocatedAmount { get; }

    public decimal WrittenOffAmount { get; }

    public decimal RemainingAmount => DueScheduleLine.OriginalAmount - AllocatedAmount - WrittenOffAmount;

    public static DerivedOpenItemSnapshot Create(
        DueScheduleLine? dueScheduleLine,
        DateOnly asOfEffectiveDate,
        DateTimeOffset recordedCutoff,
        IEnumerable<OpenItemImpactEvent?>? events)
    {
        ArgumentNullException.ThrowIfNull(dueScheduleLine);

        if (recordedCutoff.Offset != TimeSpan.Zero)
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_CUTOFF_NOT_UTC",
                "Open-item recorded cutoff must use the UTC offset.");
        }

        if (events is null)
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_EVENTS_REQUIRED",
                "Open-item event collection is required; use an empty collection when there are no impacts.");
        }

        var copiedEvents = events.ToArray();
        if (copiedEvents.Any(item => item is null))
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_EVENT_REQUIRED",
                "Open-item event collection cannot contain null values.");
        }

        var validatedEvents = copiedEvents.Cast<OpenItemImpactEvent>().ToArray();
        var eventsById = new Dictionary<Guid, OpenItemImpactEvent>();
        foreach (var impactEvent in validatedEvents)
        {
            RequireSameContext(dueScheduleLine, impactEvent);
            if (!eventsById.TryAdd(impactEvent.EventId, impactEvent))
            {
                throw new PartyOpenItemInvariantException(
                    "OPEN_ITEM_EVENT_DUPLICATE",
                    "An open-item event can occur only once.");
            }
        }

        ValidateCounterEvents(validatedEvents, eventsById);

        var consideredEvents = validatedEvents
            .Where(item => item.EffectiveDate <= asOfEffectiveDate && item.RecordedAt <= recordedCutoff)
            .OrderBy(item => item.EffectiveDate)
            .ThenBy(item => item.RecordedAt)
            .ThenBy(item => item.EventId)
            .ToArray();
        var allocatedAmount = decimal.Zero;
        var writtenOffAmount = decimal.Zero;

        foreach (var impactEvent in consideredEvents)
        {
            try
            {
                checked
                {
                    switch (impactEvent.Kind)
                    {
                        case OpenItemImpactKind.Allocation:
                            allocatedAmount += impactEvent.Amount;
                            break;
                        case OpenItemImpactKind.Unallocation:
                            allocatedAmount -= impactEvent.Amount;
                            break;
                        case OpenItemImpactKind.WriteOff:
                            writtenOffAmount += impactEvent.Amount;
                            break;
                        case OpenItemImpactKind.WriteOffReversal:
                            writtenOffAmount -= impactEvent.Amount;
                            break;
                        default:
                            throw new PartyOpenItemInvariantException(
                                "OPEN_ITEM_EVENT_KIND_INVALID",
                                "Open-item event kind is invalid.");
                    }

                    if (allocatedAmount + writtenOffAmount > dueScheduleLine.OriginalAmount)
                    {
                        throw new PartyOpenItemInvariantException(
                            "OPEN_ITEM_CAPACITY_EXCEEDED",
                            "Allocation and write-off impacts cannot exceed the due-line original amount.");
                    }
                }
            }
            catch (OverflowException exception)
            {
                throw new PartyOpenItemInvariantException(
                    "OPEN_ITEM_AMOUNT_OVERFLOW",
                    "Open-item derived amount exceeded decimal range.",
                    exception);
            }

            if (allocatedAmount < decimal.Zero || writtenOffAmount < decimal.Zero)
            {
                throw new PartyOpenItemInvariantException(
                    "OPEN_ITEM_COUNTER_EVENT_PRECEDES_ORIGINAL",
                    "An open-item counter-event cannot reduce an impact before its original is effective.");
            }

        }

        return new DerivedOpenItemSnapshot(
            dueScheduleLine,
            asOfEffectiveDate,
            recordedCutoff,
            Array.AsReadOnly(consideredEvents),
            allocatedAmount,
            writtenOffAmount);
    }

    private static void ValidateCounterEvents(
        IEnumerable<OpenItemImpactEvent> events,
        Dictionary<Guid, OpenItemImpactEvent> eventsById)
    {
        var reversedEventIds = new HashSet<Guid>();
        foreach (var counterEvent in events.Where(item => item.ReversesEventId.HasValue))
        {
            var originalEventId = counterEvent.ReversesEventId!.Value;
            if (!eventsById.TryGetValue(originalEventId, out var originalEvent))
            {
                throw new PartyOpenItemInvariantException(
                    "OPEN_ITEM_REVERSED_EVENT_MISSING",
                    "An open-item counter-event must reference an event in the supplied immutable history.");
            }

            var expectedOriginalKind = counterEvent.Kind == OpenItemImpactKind.Unallocation
                ? OpenItemImpactKind.Allocation
                : OpenItemImpactKind.WriteOff;
            if (originalEvent.Kind != expectedOriginalKind)
            {
                throw new PartyOpenItemInvariantException(
                    "OPEN_ITEM_REVERSAL_KIND_MISMATCH",
                    "An open-item counter-event must reverse the matching original event kind.");
            }

            if (counterEvent.Amount != originalEvent.Amount)
            {
                throw new PartyOpenItemInvariantException(
                    "OPEN_ITEM_REVERSAL_AMOUNT_MISMATCH",
                    "An open-item counter-event must exactly reverse the original amount.");
            }

            if (counterEvent.PaymentId != originalEvent.PaymentId)
            {
                throw new PartyOpenItemInvariantException(
                    "OPEN_ITEM_REVERSAL_PAYMENT_MISMATCH",
                    "An unallocation must reference the same payment as the original allocation.");
            }

            if (counterEvent.EffectiveDate < originalEvent.EffectiveDate ||
                counterEvent.RecordedAt < originalEvent.RecordedAt)
            {
                throw new PartyOpenItemInvariantException(
                    "OPEN_ITEM_REVERSAL_PRECEDES_ORIGINAL",
                    "An open-item counter-event cannot precede its original event.");
            }

            if (!reversedEventIds.Add(originalEventId))
            {
                throw new PartyOpenItemInvariantException(
                    "OPEN_ITEM_REVERSAL_DUPLICATE",
                    "An open-item impact can have at most one counter-event.");
            }
        }
    }

    private static void RequireSameContext(DueScheduleLine dueScheduleLine, OpenItemImpactEvent impactEvent)
    {
        if (impactEvent.TenantId != dueScheduleLine.TenantId)
        {
            throw new PartyOpenItemInvariantException("OPEN_ITEM_TENANT_MISMATCH", "Open-item tenants must match.");
        }

        if (impactEvent.CompanyId != dueScheduleLine.CompanyId)
        {
            throw new PartyOpenItemInvariantException("OPEN_ITEM_COMPANY_MISMATCH", "Open-item companies must match.");
        }

        if (impactEvent.PartyAccountId != dueScheduleLine.PartyAccountId)
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_PARTY_ACCOUNT_MISMATCH",
                "Open-item party accounts must match.");
        }

        if (impactEvent.DueScheduleLineId != dueScheduleLine.DueScheduleLineId)
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_DUE_LINE_MISMATCH",
                "Open-item impact must reference the same due-schedule line.");
        }

        if (impactEvent.Currency != dueScheduleLine.Currency)
        {
            throw new PartyOpenItemInvariantException("OPEN_ITEM_CURRENCY_MISMATCH", "Open-item currencies must match.");
        }
    }
}
