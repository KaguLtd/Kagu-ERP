using System.Collections.ObjectModel;
using KaguERP.Modules.Parties.Domain.DueSchedules;
using KaguERP.Modules.Parties.Domain.OpenItems;

namespace KaguERP.Modules.Parties.Domain.Allocations;

public sealed class DerivedPaymentAllocationSnapshot
{
    private DerivedPaymentAllocationSnapshot(
        PaymentAllocationCapacity payment,
        DateOnly asOfEffectiveDate,
        DateTimeOffset recordedCutoff,
        ReadOnlyCollection<OpenItemImpactEvent> consideredEvents,
        decimal allocatedAmount)
    {
        Payment = payment;
        AsOfEffectiveDate = asOfEffectiveDate;
        RecordedCutoff = recordedCutoff;
        ConsideredEvents = consideredEvents;
        AllocatedAmount = allocatedAmount;
    }

    public PaymentAllocationCapacity Payment { get; }

    public DateOnly AsOfEffectiveDate { get; }

    public DateTimeOffset RecordedCutoff { get; }

    public IReadOnlyList<OpenItemImpactEvent> ConsideredEvents { get; }

    public decimal AllocatedAmount { get; }

    public decimal RemainingUsableAmount => Payment.UsableAmount - AllocatedAmount;

    public static DerivedPaymentAllocationSnapshot Create(
        PaymentAllocationCapacity? payment,
        DateOnly asOfEffectiveDate,
        DateTimeOffset recordedCutoff,
        IEnumerable<OpenItemImpactEvent?>? events)
    {
        ArgumentNullException.ThrowIfNull(payment);

        if (recordedCutoff.Offset != TimeSpan.Zero)
        {
            throw new PartyOpenItemInvariantException(
                "PAYMENT_ALLOCATION_CUTOFF_NOT_UTC",
                "Payment-allocation recorded cutoff must use the UTC offset.");
        }

        if (events is null)
        {
            throw new PartyOpenItemInvariantException(
                "PAYMENT_ALLOCATION_EVENTS_REQUIRED",
                "Payment-allocation event collection is required; use an empty collection when unused.");
        }

        var copiedEvents = events.ToArray();
        if (copiedEvents.Any(item => item is null))
        {
            throw new PartyOpenItemInvariantException(
                "PAYMENT_ALLOCATION_EVENT_REQUIRED",
                "Payment-allocation event collection cannot contain null values.");
        }

        var validatedEvents = copiedEvents.Cast<OpenItemImpactEvent>().ToArray();
        var eventsById = new Dictionary<Guid, OpenItemImpactEvent>();
        foreach (var impactEvent in validatedEvents)
        {
            RequirePaymentContext(payment, impactEvent);
            if (!eventsById.TryAdd(impactEvent.EventId, impactEvent))
            {
                throw new PartyOpenItemInvariantException(
                    "PAYMENT_ALLOCATION_EVENT_DUPLICATE",
                    "A payment-allocation event can occur only once.");
            }
        }

        ValidateUnallocations(validatedEvents, eventsById);

        var consideredEvents = validatedEvents
            .Where(item => item.EffectiveDate <= asOfEffectiveDate && item.RecordedAt <= recordedCutoff)
            .OrderBy(item => item.EffectiveDate)
            .ThenBy(item => item.RecordedAt)
            .ThenBy(item => item.EventId)
            .ToArray();
        var allocatedAmount = decimal.Zero;

        foreach (var impactEvent in consideredEvents)
        {
            try
            {
                checked
                {
                    allocatedAmount += impactEvent.Kind == OpenItemImpactKind.Allocation
                        ? impactEvent.Amount
                        : -impactEvent.Amount;
                }
            }
            catch (OverflowException exception)
            {
                throw new PartyOpenItemInvariantException(
                    "PAYMENT_ALLOCATION_AMOUNT_OVERFLOW",
                    "Payment allocated amount exceeded decimal range.",
                    exception);
            }

            if (allocatedAmount < decimal.Zero)
            {
                throw new PartyOpenItemInvariantException(
                    "PAYMENT_UNALLOCATION_PRECEDES_ALLOCATION",
                    "An unallocation cannot reduce payment usage before its allocation is effective.");
            }

            if (allocatedAmount > payment.UsableAmount)
            {
                throw new PartyOpenItemInvariantException(
                    "PAYMENT_ALLOCATION_CAPACITY_EXCEEDED",
                    "Payment allocations cannot exceed the usable amount.");
            }
        }

        return new DerivedPaymentAllocationSnapshot(
            payment,
            asOfEffectiveDate,
            recordedCutoff,
            Array.AsReadOnly(consideredEvents),
            allocatedAmount);
    }

    private static void ValidateUnallocations(
        IEnumerable<OpenItemImpactEvent> events,
        Dictionary<Guid, OpenItemImpactEvent> eventsById)
    {
        var reversedAllocationIds = new HashSet<Guid>();
        foreach (var unallocation in events.Where(item => item.Kind == OpenItemImpactKind.Unallocation))
        {
            if (!eventsById.TryGetValue(unallocation.ReversesEventId!.Value, out var allocation))
            {
                throw new PartyOpenItemInvariantException(
                    "PAYMENT_ALLOCATION_REVERSED_EVENT_MISSING",
                    "An unallocation must reference an allocation in the supplied immutable history.");
            }

            if (allocation.Kind != OpenItemImpactKind.Allocation)
            {
                throw new PartyOpenItemInvariantException(
                    "PAYMENT_ALLOCATION_REVERSAL_KIND_MISMATCH",
                    "An unallocation can reverse only an allocation.");
            }

            if (unallocation.PaymentId != allocation.PaymentId ||
                unallocation.DueScheduleLineId != allocation.DueScheduleLineId ||
                unallocation.Currency != allocation.Currency)
            {
                throw new PartyOpenItemInvariantException(
                    "PAYMENT_ALLOCATION_REVERSAL_CONTEXT_MISMATCH",
                    "An unallocation must match the original payment, due line and currency.");
            }

            if (unallocation.Amount != allocation.Amount)
            {
                throw new PartyOpenItemInvariantException(
                    "PAYMENT_ALLOCATION_REVERSAL_AMOUNT_MISMATCH",
                    "An unallocation must exactly reverse the original allocation amount.");
            }

            if (unallocation.EffectiveDate < allocation.EffectiveDate || unallocation.RecordedAt < allocation.RecordedAt)
            {
                throw new PartyOpenItemInvariantException(
                    "PAYMENT_ALLOCATION_REVERSAL_PRECEDES_ORIGINAL",
                    "An unallocation cannot precede its original allocation.");
            }

            if (!reversedAllocationIds.Add(allocation.EventId))
            {
                throw new PartyOpenItemInvariantException(
                    "PAYMENT_ALLOCATION_REVERSAL_DUPLICATE",
                    "An allocation can have at most one unallocation counter-event.");
            }
        }
    }

    private static void RequirePaymentContext(PaymentAllocationCapacity payment, OpenItemImpactEvent impactEvent)
    {
        if (impactEvent.Kind is not (OpenItemImpactKind.Allocation or OpenItemImpactKind.Unallocation))
        {
            throw new PartyOpenItemInvariantException(
                "PAYMENT_ALLOCATION_EVENT_KIND_INVALID",
                "Payment-allocation history accepts only allocation and unallocation impacts.");
        }

        if (impactEvent.TenantId != payment.TenantId)
        {
            throw new PartyOpenItemInvariantException(
                "PAYMENT_ALLOCATION_TENANT_MISMATCH",
                "Payment-allocation tenants must match.");
        }

        if (impactEvent.CompanyId != payment.CompanyId)
        {
            throw new PartyOpenItemInvariantException(
                "PAYMENT_ALLOCATION_COMPANY_MISMATCH",
                "Payment-allocation companies must match.");
        }

        if (impactEvent.PartyAccountId != payment.PartyAccountId)
        {
            throw new PartyOpenItemInvariantException(
                "PAYMENT_ALLOCATION_PARTY_ACCOUNT_MISMATCH",
                "Payment-allocation party accounts must match.");
        }

        if (impactEvent.PaymentId != payment.PaymentId)
        {
            throw new PartyOpenItemInvariantException(
                "PAYMENT_ALLOCATION_PAYMENT_MISMATCH",
                "Payment-allocation event must reference the same payment.");
        }

        if (impactEvent.Currency != payment.Currency)
        {
            throw new PartyOpenItemInvariantException(
                "PAYMENT_ALLOCATION_CURRENCY_MISMATCH",
                "Payment-allocation currencies must match.");
        }
    }
}
