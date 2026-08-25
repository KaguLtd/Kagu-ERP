using KaguERP.Modules.Accounting.Domain.Journals;

namespace KaguERP.Modules.Accounting.Domain.Reversals;

public sealed class JournalReversalDraft
{
    private JournalReversalDraft(
        Guid originalJournalId,
        ValidatedJournalDraft originalJournalDraft,
        ValidatedJournalDraft reversalJournalDraft)
    {
        OriginalJournalId = originalJournalId;
        OriginalJournalDraft = originalJournalDraft;
        ReversalJournalDraft = reversalJournalDraft;
    }

    public Guid OriginalJournalId { get; }

    public ValidatedJournalDraft OriginalJournalDraft { get; }

    public ValidatedJournalDraft ReversalJournalDraft { get; }

    public static JournalReversalDraft Create(
        Guid originalJournalId,
        ValidatedJournalDraft originalJournalDraft,
        Guid reversalPostingRuleVersionId,
        string reversalSourceType,
        string reversalPostingPurpose,
        DateOnly reversalEffectiveDate,
        DateTimeOffset recordedAt)
    {
        if (originalJournalId == Guid.Empty)
        {
            throw new ReversalInvariantException(
                "REVERSAL_ORIGINAL_JOURNAL_REQUIRED",
                "Original journal ID is required for a reversal.");
        }

        ArgumentNullException.ThrowIfNull(originalJournalDraft);

        var reversedLines = originalJournalDraft.Lines
            .Select(ReverseLine)
            .ToArray();
        var reversalJournalDraft = ValidatedJournalDraft.Create(
            originalJournalDraft.TenantId,
            originalJournalDraft.CompanyId,
            originalJournalId,
            reversalPostingRuleVersionId,
            reversalSourceType,
            reversalPostingPurpose,
            reversalEffectiveDate,
            recordedAt,
            originalJournalDraft.FunctionalCurrency,
            reversedLines);

        return new JournalReversalDraft(originalJournalId, originalJournalDraft, reversalJournalDraft);
    }

    private static JournalLineDraft ReverseLine(JournalLineDraft originalLine)
    {
        var reversedAmount = originalLine.Amount.Debit > decimal.Zero
            ? JournalAmount.Create(decimal.Zero, originalLine.Amount.Debit)
            : JournalAmount.Create(originalLine.Amount.Credit, decimal.Zero);

        if (originalLine.CurrencyAmount is null)
        {
            return JournalLineDraft.Create(
                originalLine.AccountId,
                originalLine.SourceLineId,
                reversedAmount,
                originalLine.Dimensions);
        }

        var reversedCurrencyAmount = originalLine.CurrencyAmount.Reverse();
        return JournalLineDraft.Create(
            originalLine.AccountId,
            originalLine.SourceLineId,
            reversedAmount,
            originalLine.Dimensions,
            reversedCurrencyAmount);
    }
}
