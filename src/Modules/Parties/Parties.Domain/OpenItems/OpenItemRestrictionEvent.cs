using KaguERP.Modules.Parties.Domain.DueSchedules;

namespace KaguERP.Modules.Parties.Domain.OpenItems;

public enum OpenItemRestrictionKind
{
    Dispute = 1,
    CollectionBlock = 2,
}

public enum OpenItemRestrictionAction
{
    Applied = 1,
    Released = 2,
}

public sealed record OpenItemRestrictionEvent
{
    private OpenItemRestrictionEvent(
        Guid eventId,
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid dueScheduleLineId,
        OpenItemRestrictionKind kind,
        OpenItemRestrictionAction action,
        string reasonCode,
        DateOnly effectiveDate,
        DateTimeOffset recordedAt,
        Guid? releasesEventId)
    {
        EventId = eventId;
        TenantId = tenantId;
        CompanyId = companyId;
        PartyAccountId = partyAccountId;
        DueScheduleLineId = dueScheduleLineId;
        Kind = kind;
        Action = action;
        ReasonCode = reasonCode;
        EffectiveDate = effectiveDate;
        RecordedAt = recordedAt;
        ReleasesEventId = releasesEventId;
    }

    public Guid EventId { get; }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public Guid PartyAccountId { get; }

    public Guid DueScheduleLineId { get; }

    public OpenItemRestrictionKind Kind { get; }

    public OpenItemRestrictionAction Action { get; }

    public string ReasonCode { get; }

    public DateOnly EffectiveDate { get; }

    public DateTimeOffset RecordedAt { get; }

    public Guid? ReleasesEventId { get; }

    public static OpenItemRestrictionEvent Create(
        Guid eventId,
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid dueScheduleLineId,
        OpenItemRestrictionKind kind,
        OpenItemRestrictionAction action,
        string reasonCode,
        DateOnly effectiveDate,
        DateTimeOffset recordedAt,
        Guid? releasesEventId = null)
    {
        RequireId(eventId, "OPEN_ITEM_RESTRICTION_EVENT_REQUIRED", "Restriction event ID is required.");
        RequireId(tenantId, "OPEN_ITEM_RESTRICTION_TENANT_REQUIRED", "Restriction tenant ID is required.");
        RequireId(companyId, "OPEN_ITEM_RESTRICTION_COMPANY_REQUIRED", "Restriction company ID is required.");
        RequireId(
            partyAccountId,
            "OPEN_ITEM_RESTRICTION_PARTY_ACCOUNT_REQUIRED",
            "Restriction PartyAccount ID is required.");
        RequireId(
            dueScheduleLineId,
            "OPEN_ITEM_RESTRICTION_DUE_LINE_REQUIRED",
            "Restriction due-schedule line ID is required.");
        if (!Enum.IsDefined(kind))
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_RESTRICTION_KIND_INVALID",
                "Open-item restriction kind is invalid.");
        }
        if (!Enum.IsDefined(action))
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_RESTRICTION_ACTION_INVALID",
                "Open-item restriction action is invalid.");
        }
        string normalizedReasonCode = RequireText(reasonCode);
        if (effectiveDate == default)
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_RESTRICTION_EFFECTIVE_DATE_REQUIRED",
                "Restriction effective date is required.");
        }
        if (recordedAt == default || recordedAt.Offset != TimeSpan.Zero)
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_RESTRICTION_RECORDED_AT_INVALID",
                "Restriction recorded timestamp is required and must use the UTC offset.");
        }
        DateTimeOffset normalizedRecordedAt = new(
            recordedAt.Ticks - (recordedAt.Ticks % TimeSpan.TicksPerMicrosecond),
            TimeSpan.Zero);
        bool isRelease = action == OpenItemRestrictionAction.Released;
        if (isRelease && (!releasesEventId.HasValue || releasesEventId == Guid.Empty))
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_RESTRICTION_RELEASE_LINK_REQUIRED",
                "A restriction release must link to its applied event.");
        }
        if (!isRelease && releasesEventId.HasValue)
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_RESTRICTION_RELEASE_LINK_UNEXPECTED",
                "An applied restriction cannot contain a release link.");
        }

        return new OpenItemRestrictionEvent(
            eventId,
            tenantId,
            companyId,
            partyAccountId,
            dueScheduleLineId,
            kind,
            action,
            normalizedReasonCode,
            effectiveDate,
            normalizedRecordedAt,
            releasesEventId);
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new PartyOpenItemInvariantException(code, message);
        }
    }

    private static string RequireText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_RESTRICTION_REASON_INVALID",
                "Restriction reason code is required and cannot exceed 60 characters.");
        }
        string normalized = value.Trim();
        if (normalized.Length > 60)
        {
            throw new PartyOpenItemInvariantException(
                "OPEN_ITEM_RESTRICTION_REASON_INVALID",
                "Restriction reason code is required and cannot exceed 60 characters.");
        }
        return normalized;
    }
}
