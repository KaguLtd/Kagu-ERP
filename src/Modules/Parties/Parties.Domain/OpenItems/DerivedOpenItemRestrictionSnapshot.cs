using KaguERP.Modules.Parties.Domain.DueSchedules;

namespace KaguERP.Modules.Parties.Domain.OpenItems;

public sealed class DerivedOpenItemRestrictionSnapshot
{
    private DerivedOpenItemRestrictionSnapshot(
        Guid dueScheduleLineId,
        DateOnly effectiveAsOf,
        DateTimeOffset recordedCutoff,
        bool isDisputed,
        bool isCollectionBlocked,
        IReadOnlyList<OpenItemRestrictionEvent> consideredEvents)
    {
        DueScheduleLineId = dueScheduleLineId;
        EffectiveAsOf = effectiveAsOf;
        RecordedCutoff = recordedCutoff;
        IsDisputed = isDisputed;
        IsCollectionBlocked = isCollectionBlocked;
        ConsideredEvents = consideredEvents;
    }

    public Guid DueScheduleLineId { get; }

    public DateOnly EffectiveAsOf { get; }

    public DateTimeOffset RecordedCutoff { get; }

    public bool IsDisputed { get; }

    public bool IsCollectionBlocked { get; }

    public IReadOnlyList<OpenItemRestrictionEvent> ConsideredEvents { get; }

    public static DerivedOpenItemRestrictionSnapshot Create(
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid dueScheduleLineId,
        DateOnly effectiveAsOf,
        DateTimeOffset recordedCutoff,
        IReadOnlyCollection<OpenItemRestrictionEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (tenantId == Guid.Empty || companyId == Guid.Empty || partyAccountId == Guid.Empty ||
            dueScheduleLineId == Guid.Empty || effectiveAsOf == default)
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_RESTRICTION_SNAPSHOT_CONTEXT_INVALID",
                "Restriction snapshot scope and as-of context are required.");
        }
        if (recordedCutoff == default || recordedCutoff.Offset != TimeSpan.Zero)
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_RESTRICTION_SNAPSHOT_CUTOFF_INVALID",
                "Restriction snapshot cutoff is required and must use the UTC offset.");
        }
        if (events.Any(item => item.TenantId != tenantId || item.CompanyId != companyId ||
                item.PartyAccountId != partyAccountId || item.DueScheduleLineId != dueScheduleLineId))
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_RESTRICTION_SCOPE_CONFLICT",
                "Restriction events must belong to the same open-item scope.");
        }
        if (events.Select(item => item.EventId).Distinct().Count() != events.Count)
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_RESTRICTION_EVENT_DUPLICATE",
                "Restriction event IDs must be unique.");
        }

        OpenItemRestrictionEvent[] considered = events
            .Where(item => item.EffectiveDate <= effectiveAsOf && item.RecordedAt <= recordedCutoff)
            .OrderBy(item => item.EffectiveDate)
            .ThenBy(item => item.RecordedAt)
            .ThenBy(item => item.EventId)
            .ToArray();
        Dictionary<Guid, OpenItemRestrictionEvent> byId = considered.ToDictionary(item => item.EventId);
        foreach (OpenItemRestrictionEvent release in considered.Where(
                     item => item.Action == OpenItemRestrictionAction.Released))
        {
            if (!byId.TryGetValue(release.ReleasesEventId!.Value, out OpenItemRestrictionEvent? applied) ||
                applied.Action != OpenItemRestrictionAction.Applied || applied.Kind != release.Kind ||
                applied.EffectiveDate > release.EffectiveDate || applied.RecordedAt > release.RecordedAt)
            {
                throw new PartyOpenItemInvariantException(
                    "OPEN_ITEM_RESTRICTION_RELEASE_CONFLICT",
                    "Restriction release does not exactly follow its applied event.");
            }
        }

        HashSet<Guid> released = considered
            .Where(item => item.Action == OpenItemRestrictionAction.Released)
            .Select(item => item.ReleasesEventId!.Value)
            .ToHashSet();
        OpenItemRestrictionEvent[] active = considered
            .Where(item => item.Action == OpenItemRestrictionAction.Applied && !released.Contains(item.EventId))
            .ToArray();
        if (active.GroupBy(item => item.Kind).Any(group => group.Count() > 1))
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_RESTRICTION_MULTIPLE_ACTIVE",
                "Only one active restriction of each kind is allowed per open item.");
        }

        return new DerivedOpenItemRestrictionSnapshot(
            dueScheduleLineId,
            effectiveAsOf,
            recordedCutoff,
            active.Any(item => item.Kind == OpenItemRestrictionKind.Dispute),
            active.Any(item => item.Kind == OpenItemRestrictionKind.CollectionBlock),
            Array.AsReadOnly(considered));
    }
}
