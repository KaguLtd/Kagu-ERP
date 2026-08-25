using KaguERP.Modules.Reporting.Domain.ControlAccounts;

namespace KaguERP.Modules.Reporting.Domain.PartyReports;

public sealed record OpenItemAgingSnapshot
{
    private OpenItemAgingSnapshot(
        Guid openItemId,
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid controlAccountId,
        Guid sourceEventId,
        Guid dueScheduleLineId,
        ReportCurrencyCode currency,
        decimal originalAmount,
        decimal remainingAmount,
        DateOnly dueDate,
        DateOnly effectiveAsOf,
        DateTimeOffset dataCutoffAt,
        bool isDisputed,
        bool isBlocked)
    {
        OpenItemId = openItemId;
        TenantId = tenantId;
        CompanyId = companyId;
        PartyAccountId = partyAccountId;
        ControlAccountId = controlAccountId;
        SourceEventId = sourceEventId;
        DueScheduleLineId = dueScheduleLineId;
        Currency = currency;
        OriginalAmount = originalAmount;
        RemainingAmount = remainingAmount;
        DueDate = dueDate;
        EffectiveAsOf = effectiveAsOf;
        DataCutoffAt = dataCutoffAt;
        IsDisputed = isDisputed;
        IsBlocked = isBlocked;
    }

    public Guid OpenItemId { get; }
    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid PartyAccountId { get; }
    public Guid ControlAccountId { get; }
    public Guid SourceEventId { get; }
    public Guid DueScheduleLineId { get; }
    public ReportCurrencyCode Currency { get; }
    public decimal OriginalAmount { get; }
    public decimal RemainingAmount { get; }
    public DateOnly DueDate { get; }
    public DateOnly EffectiveAsOf { get; }
    public DateTimeOffset DataCutoffAt { get; }
    public bool IsDisputed { get; }
    public bool IsBlocked { get; }

    public int DaysOverdue => EffectiveAsOf.DayNumber - DueDate.DayNumber;

    public static OpenItemAgingSnapshot Create(
        Guid openItemId,
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid controlAccountId,
        Guid sourceEventId,
        Guid dueScheduleLineId,
        ReportCurrencyCode? currency,
        decimal originalAmount,
        decimal remainingAmount,
        DateOnly dueDate,
        DateOnly effectiveAsOf,
        DateTimeOffset dataCutoffAt,
        bool isDisputed,
        bool isBlocked)
    {
        RequireId(openItemId, "AGING_OPEN_ITEM_REQUIRED", "Aging open-item ID is required.");
        RequireId(tenantId, "PARTY_REPORT_TENANT_REQUIRED", "Aging tenant ID is required.");
        RequireId(companyId, "PARTY_REPORT_COMPANY_REQUIRED", "Aging company ID is required.");
        RequireId(partyAccountId, "PARTY_REPORT_ACCOUNT_REQUIRED", "Aging party-account ID is required.");
        RequireId(controlAccountId, "PARTY_REPORT_CONTROL_ACCOUNT_REQUIRED", "Aging control-account ID is required.");
        RequireId(sourceEventId, "PARTY_REPORT_SOURCE_REQUIRED", "Aging source-event ID is required.");
        RequireId(dueScheduleLineId, "PARTY_REPORT_DUE_LINE_REQUIRED", "Aging due-line ID is required.");
        ArgumentNullException.ThrowIfNull(currency);

        if (originalAmount <= decimal.Zero || remainingAmount <= decimal.Zero || remainingAmount > originalAmount)
        {
            throw new ReportingInvariantException(
                "AGING_AMOUNT_INVALID",
                "Aging original and remaining amounts must be positive and remaining cannot exceed original.");
        }

        if (dueDate == default || effectiveAsOf == default)
        {
            throw new ReportingInvariantException("AGING_DATE_REQUIRED", "Aging due and effective as-of dates are required.");
        }

        if (dataCutoffAt.Offset != TimeSpan.Zero)
        {
            throw new ReportingInvariantException(
                "AGING_DATA_CUTOFF_NOT_UTC",
                "Aging data-cutoff timestamp must use the UTC offset.");
        }

        return new OpenItemAgingSnapshot(
            openItemId,
            tenantId,
            companyId,
            partyAccountId,
            controlAccountId,
            sourceEventId,
            dueScheduleLineId,
            currency,
            originalAmount,
            remainingAmount,
            dueDate,
            effectiveAsOf,
            dataCutoffAt,
            isDisputed,
            isBlocked);
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new ReportingInvariantException(code, message);
        }
    }
}
