namespace KaguERP.Modules.Parties.Domain.Openings;

public sealed record PartyAccountOpeningDueLineDraft
{
    private const decimal MaximumPersistedAmount = 9999999999999999.9999m;

    private PartyAccountOpeningDueLineDraft(
        Guid dueScheduleLineId,
        decimal originalAmount,
        DateOnly dueDate,
        Guid paymentTermSnapshotId,
        long paymentTermVersion)
    {
        DueScheduleLineId = dueScheduleLineId;
        OriginalAmount = originalAmount;
        DueDate = dueDate;
        PaymentTermSnapshotId = paymentTermSnapshotId;
        PaymentTermVersion = paymentTermVersion;
    }

    public Guid DueScheduleLineId { get; }

    public decimal OriginalAmount { get; }

    public DateOnly DueDate { get; }

    public Guid PaymentTermSnapshotId { get; }

    public long PaymentTermVersion { get; }

    public static PartyAccountOpeningDueLineDraft Create(
        Guid dueScheduleLineId,
        decimal originalAmount,
        DateOnly dueDate,
        Guid paymentTermSnapshotId,
        long paymentTermVersion)
    {
        RequireId(
            dueScheduleLineId,
            "PARTY_OPENING_DUE_LINE_REQUIRED",
            "Opening-balance due-line ID is required.");
        RequireId(
            paymentTermSnapshotId,
            "PARTY_OPENING_PAYMENT_TERM_REQUIRED",
            "Opening-balance payment-term snapshot ID is required.");
        if (originalAmount <= decimal.Zero || originalAmount > MaximumPersistedAmount)
        {
            throw new PartyAccountOpeningInvariantException(
                "PARTY_OPENING_DUE_AMOUNT_INVALID",
                "Opening-balance due-line amount must be positive and fit numeric(20,4).");
        }
        if (GetScale(originalAmount) > 4)
        {
            throw new PartyAccountOpeningInvariantException(
                "PARTY_OPENING_DUE_AMOUNT_SCALE_INVALID",
                "Opening-balance due-line amount cannot exceed four decimal places.");
        }
        if (dueDate == default)
        {
            throw new PartyAccountOpeningInvariantException(
                "PARTY_OPENING_DUE_DATE_REQUIRED",
                "Opening-balance due date is required.");
        }
        if (paymentTermVersion <= 0)
        {
            throw new PartyAccountOpeningInvariantException(
                "PARTY_OPENING_PAYMENT_TERM_VERSION_INVALID",
                "Opening-balance payment-term snapshot version must be positive.");
        }

        return new PartyAccountOpeningDueLineDraft(
            dueScheduleLineId,
            originalAmount,
            dueDate,
            paymentTermSnapshotId,
            paymentTermVersion);
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
