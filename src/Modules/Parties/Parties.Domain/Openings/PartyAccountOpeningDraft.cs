namespace KaguERP.Modules.Parties.Domain.Openings;

public sealed record PartyAccountOpeningDraft
{
    public const string SourceType = "party.account-opening";
    public const string PostingPurpose = "party.account-opening.post";
    public const long InitialSourceVersion = 1;
    private const decimal MaximumPersistedAmount = 9999999999999999.9999m;

    private PartyAccountOpeningDraft(
        Guid tenantId,
        Guid companyId,
        Guid openingEventId,
        Guid partyAccountId,
        PartyAccountOpeningEntrySide entrySide,
        decimal originalAmount,
        DateOnly effectiveDate,
        DateTimeOffset recordedAt,
        Guid dueScheduleId,
        IReadOnlyList<PartyAccountOpeningDueLineDraft> dueLines)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        OpeningEventId = openingEventId;
        PartyAccountId = partyAccountId;
        EntrySide = entrySide;
        OriginalAmount = originalAmount;
        EffectiveDate = effectiveDate;
        RecordedAt = recordedAt;
        DueScheduleId = dueScheduleId;
        DueLines = dueLines;
        SourceVersion = InitialSourceVersion;
    }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public Guid OpeningEventId { get; }

    public Guid PartyAccountId { get; }

    public long SourceVersion { get; }

    public PartyAccountOpeningEntrySide EntrySide { get; }

    public decimal OriginalAmount { get; }

    public DateOnly EffectiveDate { get; }

    public DateTimeOffset RecordedAt { get; }

    public Guid DueScheduleId { get; }

    public IReadOnlyList<PartyAccountOpeningDueLineDraft> DueLines { get; }

    public static PartyAccountOpeningDraft Create(
        Guid tenantId,
        Guid companyId,
        Guid openingEventId,
        Guid partyAccountId,
        PartyAccountOpeningEntrySide entrySide,
        decimal originalAmount,
        DateOnly effectiveDate,
        DateTimeOffset recordedAt,
        Guid dueScheduleId,
        IEnumerable<PartyAccountOpeningDueLineDraft?>? dueLines)
    {
        RequireId(tenantId, "PARTY_OPENING_TENANT_REQUIRED", "Opening-balance tenant ID is required.");
        RequireId(companyId, "PARTY_OPENING_COMPANY_REQUIRED", "Opening-balance company ID is required.");
        RequireId(openingEventId, "PARTY_OPENING_EVENT_REQUIRED", "Opening-balance event ID is required.");
        RequireId(partyAccountId, "PARTY_OPENING_ACCOUNT_REQUIRED", "Opening-balance party-account ID is required.");
        RequireId(dueScheduleId, "PARTY_OPENING_DUE_SCHEDULE_REQUIRED", "Opening-balance due-schedule ID is required.");

        if (!Enum.IsDefined(entrySide))
        {
            throw new PartyAccountOpeningInvariantException(
                "PARTY_OPENING_ENTRY_SIDE_INVALID",
                "Opening-balance entry side must be debit or credit.");
        }

        if (originalAmount <= decimal.Zero || originalAmount > MaximumPersistedAmount)
        {
            throw new PartyAccountOpeningInvariantException(
                "PARTY_OPENING_AMOUNT_INVALID",
                "Opening-balance original amount must be positive and fit numeric(20,4).");
        }

        if (GetScale(originalAmount) > 4)
        {
            throw new PartyAccountOpeningInvariantException(
                "PARTY_OPENING_AMOUNT_SCALE_INVALID",
                "Opening-balance original amount cannot exceed four decimal places.");
        }

        if (effectiveDate == default)
        {
            throw new PartyAccountOpeningInvariantException(
                "PARTY_OPENING_EFFECTIVE_DATE_REQUIRED",
                "Opening-balance effective date is required.");
        }

        if (recordedAt.Offset != TimeSpan.Zero)
        {
            throw new PartyAccountOpeningInvariantException(
                "PARTY_OPENING_RECORDED_AT_NOT_UTC",
                "Opening-balance recorded timestamp must use the UTC offset.");
        }

        if (dueLines is null)
        {
            throw new PartyAccountOpeningInvariantException(
                "PARTY_OPENING_DUE_LINES_REQUIRED",
                "Opening-balance due lines are required.");
        }
        PartyAccountOpeningDueLineDraft?[] copiedLines = dueLines.ToArray();
        if (copiedLines.Length == 0)
        {
            throw new PartyAccountOpeningInvariantException(
                "PARTY_OPENING_DUE_LINES_REQUIRED",
                "Opening-balance due lines are required.");
        }
        if (copiedLines.Any(line => line is null))
        {
            throw new PartyAccountOpeningInvariantException(
                "PARTY_OPENING_DUE_LINE_REQUIRED",
                "Opening-balance due lines cannot contain null values.");
        }

        PartyAccountOpeningDueLineDraft[] validatedLines = copiedLines
            .Cast<PartyAccountOpeningDueLineDraft>()
            .ToArray();
        if (validatedLines.Select(line => line.DueScheduleLineId).Distinct().Count() != validatedLines.Length)
        {
            throw new PartyAccountOpeningInvariantException(
                "PARTY_OPENING_DUE_LINE_DUPLICATE",
                "An opening-balance due line can occur only once.");
        }

        decimal dueTotal;
        try
        {
            dueTotal = validatedLines.Sum(line => line.OriginalAmount);
        }
        catch (OverflowException exception)
        {
            throw new PartyAccountOpeningInvariantException(
                "PARTY_OPENING_DUE_TOTAL_OVERFLOW",
                "Opening-balance due-line total exceeded decimal range.",
                exception);
        }
        if (dueTotal != originalAmount)
        {
            throw new PartyAccountOpeningInvariantException(
                "PARTY_OPENING_DUE_TOTAL_MISMATCH",
                "Opening-balance due-line total must equal the source amount exactly.");
        }
        Array.Sort(
            validatedLines,
            (left, right) =>
            {
                int dateComparison = left.DueDate.CompareTo(right.DueDate);
                return dateComparison != 0
                    ? dateComparison
                    : left.DueScheduleLineId.CompareTo(right.DueScheduleLineId);
            });

        return new PartyAccountOpeningDraft(
            tenantId,
            companyId,
            openingEventId,
            partyAccountId,
            entrySide,
            originalAmount,
            effectiveDate,
            recordedAt,
            dueScheduleId,
            Array.AsReadOnly(validatedLines));
    }

    private static int GetScale(decimal value) =>
        (decimal.GetBits(value)[3] >> 16) & 0xFF;

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new PartyAccountOpeningInvariantException(code, message);
        }
    }
}

public sealed class PartyAccountOpeningInvariantException : InvalidOperationException
{
    public PartyAccountOpeningInvariantException(string code, string message)
        : base(message) => Code = code;

    public PartyAccountOpeningInvariantException(string code, string message, Exception innerException)
        : base(message, innerException) => Code = code;

    public string Code { get; }
}
