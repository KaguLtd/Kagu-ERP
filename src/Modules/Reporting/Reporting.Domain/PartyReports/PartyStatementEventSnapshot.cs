using KaguERP.Modules.Reporting.Domain.ControlAccounts;

namespace KaguERP.Modules.Reporting.Domain.PartyReports;

public sealed record PartyStatementEventSnapshot
{
    private PartyStatementEventSnapshot(
        Guid eventId,
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid controlAccountId,
        ReportCurrencyCode currency,
        PartyStatementEventKind kind,
        string sourceType,
        Guid sourceEventId,
        Guid dueScheduleLineId,
        Guid? paymentId,
        decimal exposureEffect,
        DateOnly effectiveDate,
        long sequenceKey,
        DateTimeOffset recordedAt)
    {
        EventId = eventId;
        TenantId = tenantId;
        CompanyId = companyId;
        PartyAccountId = partyAccountId;
        ControlAccountId = controlAccountId;
        Currency = currency;
        Kind = kind;
        SourceType = sourceType;
        SourceEventId = sourceEventId;
        DueScheduleLineId = dueScheduleLineId;
        PaymentId = paymentId;
        ExposureEffect = exposureEffect;
        EffectiveDate = effectiveDate;
        SequenceKey = sequenceKey;
        RecordedAt = recordedAt;
    }

    public Guid EventId { get; }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public Guid PartyAccountId { get; }

    public Guid ControlAccountId { get; }

    public ReportCurrencyCode Currency { get; }

    public PartyStatementEventKind Kind { get; }

    public string SourceType { get; }

    public Guid SourceEventId { get; }

    public Guid DueScheduleLineId { get; }

    public Guid? PaymentId { get; }

    public decimal ExposureEffect { get; }

    public DateOnly EffectiveDate { get; }

    public long SequenceKey { get; }

    public DateTimeOffset RecordedAt { get; }

    public static PartyStatementEventSnapshot Create(
        Guid eventId,
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid controlAccountId,
        ReportCurrencyCode? currency,
        PartyStatementEventKind kind,
        string sourceType,
        Guid sourceEventId,
        Guid dueScheduleLineId,
        Guid? paymentId,
        decimal exposureEffect,
        DateOnly effectiveDate,
        long sequenceKey,
        DateTimeOffset recordedAt)
    {
        RequireId(eventId, "PARTY_REPORT_EVENT_REQUIRED", "Party statement event ID is required.");
        RequireId(tenantId, "PARTY_REPORT_TENANT_REQUIRED", "Party statement tenant ID is required.");
        RequireId(companyId, "PARTY_REPORT_COMPANY_REQUIRED", "Party statement company ID is required.");
        RequireId(partyAccountId, "PARTY_REPORT_ACCOUNT_REQUIRED", "Party-account ID is required.");
        RequireId(controlAccountId, "PARTY_REPORT_CONTROL_ACCOUNT_REQUIRED", "Party control-account ID is required.");
        RequireId(sourceEventId, "PARTY_REPORT_SOURCE_REQUIRED", "Party statement source-event ID is required.");
        RequireId(dueScheduleLineId, "PARTY_REPORT_DUE_LINE_REQUIRED", "Party statement due-line ID is required.");
        ArgumentNullException.ThrowIfNull(currency);

        if (!Enum.IsDefined(kind))
        {
            throw new ReportingInvariantException("PARTY_REPORT_EVENT_KIND_INVALID", "Party statement event kind is invalid.");
        }

        if (string.IsNullOrWhiteSpace(sourceType))
        {
            throw new ReportingInvariantException("PARTY_REPORT_SOURCE_TYPE_REQUIRED", "Party statement source type is required.");
        }

        var isPaymentImpact = kind is PartyStatementEventKind.Allocation or PartyStatementEventKind.Unallocation;
        if (isPaymentImpact && (paymentId is null || paymentId == Guid.Empty))
        {
            throw new ReportingInvariantException(
                "PARTY_REPORT_PAYMENT_REQUIRED",
                "Allocation and unallocation statement events require a payment ID.");
        }

        if (!isPaymentImpact && paymentId is not null)
        {
            throw new ReportingInvariantException(
                "PARTY_REPORT_PAYMENT_NOT_ALLOWED",
                "Only allocation and unallocation statement events can reference a payment.");
        }

        var expectedPositive = kind is PartyStatementEventKind.OpenItem or PartyStatementEventKind.Unallocation or PartyStatementEventKind.WriteOffReversal;
        if (exposureEffect == decimal.Zero || expectedPositive != exposureEffect > decimal.Zero)
        {
            throw new ReportingInvariantException(
                "PARTY_REPORT_EFFECT_INVALID",
                "Party statement exposure effect sign must agree with its normalized event kind.");
        }

        if (effectiveDate == default)
        {
            throw new ReportingInvariantException("PARTY_REPORT_EFFECTIVE_DATE_REQUIRED", "Party statement effective date is required.");
        }

        if (sequenceKey <= 0)
        {
            throw new ReportingInvariantException(
                "PARTY_REPORT_SEQUENCE_INVALID",
                "Party statement sequence key must be positive.");
        }

        if (recordedAt.Offset != TimeSpan.Zero)
        {
            throw new ReportingInvariantException(
                "PARTY_REPORT_RECORDED_AT_NOT_UTC",
                "Party statement recorded timestamp must use the UTC offset.");
        }

        return new PartyStatementEventSnapshot(
            eventId,
            tenantId,
            companyId,
            partyAccountId,
            controlAccountId,
            currency,
            kind,
            sourceType.Trim(),
            sourceEventId,
            dueScheduleLineId,
            paymentId,
            exposureEffect,
            effectiveDate,
            sequenceKey,
            recordedAt);
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new ReportingInvariantException(code, message);
        }
    }
}
