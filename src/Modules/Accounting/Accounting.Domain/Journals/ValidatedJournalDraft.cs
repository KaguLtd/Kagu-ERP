using System.Collections.ObjectModel;

namespace KaguERP.Modules.Accounting.Domain.Journals;

public sealed class ValidatedJournalDraft
{
    private ValidatedJournalDraft(
        Guid tenantId,
        Guid companyId,
        Guid sourceEventId,
        Guid postingRuleVersionId,
        string sourceType,
        string postingPurpose,
        DateOnly effectiveDate,
        DateTimeOffset recordedAt,
        CurrencyCode functionalCurrency,
        ReadOnlyCollection<JournalLineDraft> lines,
        decimal totalDebit,
        decimal totalCredit)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        SourceEventId = sourceEventId;
        PostingRuleVersionId = postingRuleVersionId;
        SourceType = sourceType;
        PostingPurpose = postingPurpose;
        EffectiveDate = effectiveDate;
        RecordedAt = recordedAt;
        FunctionalCurrency = functionalCurrency;
        Lines = lines;
        TotalDebit = totalDebit;
        TotalCredit = totalCredit;
    }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public Guid SourceEventId { get; }

    public Guid PostingRuleVersionId { get; }

    public string SourceType { get; }

    public string PostingPurpose { get; }

    public DateOnly EffectiveDate { get; }

    public DateTimeOffset RecordedAt { get; }

    public CurrencyCode FunctionalCurrency { get; }

    public IReadOnlyList<JournalLineDraft> Lines { get; }

    public decimal TotalDebit { get; }

    public decimal TotalCredit { get; }

    public static ValidatedJournalDraft Create(
        Guid tenantId,
        Guid companyId,
        Guid sourceEventId,
        Guid postingRuleVersionId,
        string sourceType,
        string postingPurpose,
        DateOnly effectiveDate,
        DateTimeOffset recordedAt,
        CurrencyCode functionalCurrency,
        IEnumerable<JournalLineDraft> lines)
    {
        RequireId(tenantId, "JOURNAL_TENANT_REQUIRED", "Tenant ID is required.");
        RequireId(companyId, "JOURNAL_COMPANY_REQUIRED", "Company ID is required.");
        RequireId(sourceEventId, "JOURNAL_SOURCE_REQUIRED", "Source event ID is required.");
        RequireId(
            postingRuleVersionId,
            "JOURNAL_RULE_VERSION_REQUIRED",
            "Posting rule version ID is required.");
        RequireText(sourceType, "JOURNAL_SOURCE_TYPE_REQUIRED", "Source type is required.");
        RequireText(postingPurpose, "JOURNAL_PURPOSE_REQUIRED", "Posting purpose is required.");

        if (recordedAt.Offset != TimeSpan.Zero)
        {
            throw new JournalInvariantException(
                "JOURNAL_RECORDED_AT_NOT_UTC",
                "Recorded timestamp must use the UTC offset.");
        }

        ArgumentNullException.ThrowIfNull(functionalCurrency);
        ArgumentNullException.ThrowIfNull(lines);

        var lineArray = lines.ToArray();
        if (lineArray.Length < 2)
        {
            throw new JournalInvariantException(
                "JOURNAL_LINES_INSUFFICIENT",
                "A journal draft requires at least two lines.");
        }

        if (lineArray.Any(line => line is null))
        {
            throw new JournalInvariantException("JOURNAL_LINE_REQUIRED", "Journal lines cannot contain null values.");
        }

        decimal totalDebit;
        decimal totalCredit;
        checked
        {
            totalDebit = lineArray.Sum(line => line.Amount.Debit);
            totalCredit = lineArray.Sum(line => line.Amount.Credit);
        }

        if (totalDebit != totalCredit)
        {
            throw new JournalInvariantException(
                "JOURNAL_NOT_BALANCED",
                "Journal total debit must equal total credit exactly.");
        }

        return new ValidatedJournalDraft(
            tenantId,
            companyId,
            sourceEventId,
            postingRuleVersionId,
            sourceType.Trim(),
            postingPurpose.Trim(),
            effectiveDate,
            recordedAt,
            functionalCurrency,
            Array.AsReadOnly(lineArray),
            totalDebit,
            totalCredit);
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new JournalInvariantException(code, message);
        }
    }

    private static void RequireText(string value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JournalInvariantException(code, message);
        }
    }
}
