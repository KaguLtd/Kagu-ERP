using KaguERP.Modules.Parties.Domain.Allocations;

namespace KaguERP.Modules.Parties.Domain.DueSchedules;

public sealed record DueScheduleLine
{
    private DueScheduleLine(
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid sourceEventId,
        Guid dueScheduleLineId,
        AllocationCurrencyCode currency,
        decimal originalAmount,
        DateOnly dueDate,
        Guid paymentTermSnapshotId,
        long paymentTermVersion,
        Guid controlAccountId)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        PartyAccountId = partyAccountId;
        SourceEventId = sourceEventId;
        DueScheduleLineId = dueScheduleLineId;
        Currency = currency;
        OriginalAmount = originalAmount;
        DueDate = dueDate;
        PaymentTermSnapshotId = paymentTermSnapshotId;
        PaymentTermVersion = paymentTermVersion;
        ControlAccountId = controlAccountId;
    }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public Guid PartyAccountId { get; }

    public Guid SourceEventId { get; }

    public Guid DueScheduleLineId { get; }

    public AllocationCurrencyCode Currency { get; }

    public decimal OriginalAmount { get; }

    public DateOnly DueDate { get; }

    public Guid PaymentTermSnapshotId { get; }

    public long PaymentTermVersion { get; }

    public Guid ControlAccountId { get; }

    public static DueScheduleLine Create(
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid sourceEventId,
        Guid dueScheduleLineId,
        AllocationCurrencyCode? currency,
        decimal originalAmount,
        DateOnly dueDate,
        Guid paymentTermSnapshotId,
        long paymentTermVersion,
        Guid controlAccountId)
    {
        RequireId(tenantId, "DUE_TENANT_REQUIRED", "Due-schedule tenant ID is required.");
        RequireId(companyId, "DUE_COMPANY_REQUIRED", "Due-schedule company ID is required.");
        RequireId(partyAccountId, "DUE_PARTY_ACCOUNT_REQUIRED", "Due-schedule party-account ID is required.");
        RequireId(sourceEventId, "DUE_SOURCE_REQUIRED", "Due-schedule source-event ID is required.");
        RequireId(dueScheduleLineId, "DUE_LINE_REQUIRED", "Due-schedule line ID is required.");
        ArgumentNullException.ThrowIfNull(currency);
        RequireId(
            paymentTermSnapshotId,
            "DUE_PAYMENT_TERM_REQUIRED",
            "Payment-term snapshot ID is required.");
        RequireId(controlAccountId, "DUE_CONTROL_ACCOUNT_REQUIRED", "Control-account ID is required.");

        if (originalAmount <= decimal.Zero)
        {
            throw new PartyOpenItemInvariantException(
                "DUE_ORIGINAL_AMOUNT_INVALID",
                "Due-schedule original amount must be positive.");
        }

        if (dueDate == default)
        {
            throw new PartyOpenItemInvariantException("DUE_DATE_REQUIRED", "Due date is required.");
        }

        if (paymentTermVersion <= 0)
        {
            throw new PartyOpenItemInvariantException(
                "DUE_PAYMENT_TERM_VERSION_INVALID",
                "Payment-term snapshot version must be positive.");
        }

        return new DueScheduleLine(
            tenantId,
            companyId,
            partyAccountId,
            sourceEventId,
            dueScheduleLineId,
            currency,
            originalAmount,
            dueDate,
            paymentTermSnapshotId,
            paymentTermVersion,
            controlAccountId);
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new PartyOpenItemInvariantException(code, message);
        }
    }
}
