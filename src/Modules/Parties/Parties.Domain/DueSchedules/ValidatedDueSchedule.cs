using System.Collections.ObjectModel;
using KaguERP.Modules.Parties.Domain.Allocations;

namespace KaguERP.Modules.Parties.Domain.DueSchedules;

public sealed class ValidatedDueSchedule
{
    private ValidatedDueSchedule(
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid sourceEventId,
        AllocationCurrencyCode currency,
        decimal sourceOriginalAmount,
        ReadOnlyCollection<DueScheduleLine> lines)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        PartyAccountId = partyAccountId;
        SourceEventId = sourceEventId;
        Currency = currency;
        SourceOriginalAmount = sourceOriginalAmount;
        Lines = lines;
    }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public Guid PartyAccountId { get; }

    public Guid SourceEventId { get; }

    public AllocationCurrencyCode Currency { get; }

    public decimal SourceOriginalAmount { get; }

    public IReadOnlyList<DueScheduleLine> Lines { get; }

    public static ValidatedDueSchedule Create(
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid sourceEventId,
        AllocationCurrencyCode? currency,
        decimal sourceOriginalAmount,
        IEnumerable<DueScheduleLine?>? lines)
    {
        RequireId(tenantId, "DUE_TENANT_REQUIRED", "Due-schedule tenant ID is required.");
        RequireId(companyId, "DUE_COMPANY_REQUIRED", "Due-schedule company ID is required.");
        RequireId(partyAccountId, "DUE_PARTY_ACCOUNT_REQUIRED", "Due-schedule party-account ID is required.");
        RequireId(sourceEventId, "DUE_SOURCE_REQUIRED", "Due-schedule source-event ID is required.");
        ArgumentNullException.ThrowIfNull(currency);

        if (sourceOriginalAmount <= decimal.Zero)
        {
            throw new PartyOpenItemInvariantException(
                "DUE_SOURCE_AMOUNT_INVALID",
                "Due-schedule source original amount must be positive.");
        }

        if (lines is null)
        {
            throw new PartyOpenItemInvariantException("DUE_LINES_REQUIRED", "Due-schedule lines are required.");
        }

        var copiedLines = lines.ToArray();
        if (copiedLines.Length == 0)
        {
            throw new PartyOpenItemInvariantException("DUE_LINES_REQUIRED", "Due-schedule lines are required.");
        }

        if (copiedLines.Any(line => line is null))
        {
            throw new PartyOpenItemInvariantException(
                "DUE_LINE_REQUIRED",
                "Due-schedule lines cannot contain null values.");
        }

        var validatedLines = copiedLines.Cast<DueScheduleLine>().ToArray();
        var lineIds = new HashSet<Guid>();
        var total = decimal.Zero;
        foreach (var line in validatedLines)
        {
            RequireSameContext(tenantId, companyId, partyAccountId, sourceEventId, currency, line);
            if (!lineIds.Add(line.DueScheduleLineId))
            {
                throw new PartyOpenItemInvariantException(
                    "DUE_LINE_DUPLICATE",
                    "A due-schedule line can occur only once.");
            }

            try
            {
                total = checked(total + line.OriginalAmount);
            }
            catch (OverflowException exception)
            {
                throw new PartyOpenItemInvariantException(
                    "DUE_TOTAL_OVERFLOW",
                    "Due-schedule total exceeded decimal range.",
                    exception);
            }
        }

        if (total != sourceOriginalAmount)
        {
            throw new PartyOpenItemInvariantException(
                "DUE_TOTAL_MISMATCH",
                "Due-schedule line total must equal the source original amount exactly.");
        }

        Array.Sort(validatedLines, CompareLines);
        return new ValidatedDueSchedule(
            tenantId,
            companyId,
            partyAccountId,
            sourceEventId,
            currency,
            sourceOriginalAmount,
            Array.AsReadOnly(validatedLines));
    }

    private static void RequireSameContext(
        Guid tenantId,
        Guid companyId,
        Guid partyAccountId,
        Guid sourceEventId,
        AllocationCurrencyCode currency,
        DueScheduleLine line)
    {
        if (line.TenantId != tenantId)
        {
            throw new PartyOpenItemInvariantException("DUE_TENANT_MISMATCH", "Due-schedule tenants must match.");
        }

        if (line.CompanyId != companyId)
        {
            throw new PartyOpenItemInvariantException("DUE_COMPANY_MISMATCH", "Due-schedule companies must match.");
        }

        if (line.PartyAccountId != partyAccountId)
        {
            throw new PartyOpenItemInvariantException(
                "DUE_PARTY_ACCOUNT_MISMATCH",
                "Due-schedule party accounts must match.");
        }

        if (line.SourceEventId != sourceEventId)
        {
            throw new PartyOpenItemInvariantException("DUE_SOURCE_MISMATCH", "Due-schedule sources must match.");
        }

        if (line.Currency != currency)
        {
            throw new PartyOpenItemInvariantException("DUE_CURRENCY_MISMATCH", "Due-schedule currencies must match.");
        }
    }

    private static int CompareLines(DueScheduleLine left, DueScheduleLine right)
    {
        var dateComparison = left.DueDate.CompareTo(right.DueDate);
        return dateComparison != 0
            ? dateComparison
            : left.DueScheduleLineId.CompareTo(right.DueScheduleLineId);
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new PartyOpenItemInvariantException(code, message);
        }
    }
}
