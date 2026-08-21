namespace KaguERP.Modules.Accounting.Domain.Journals;

public sealed record JournalLineDraft
{
    private JournalLineDraft(Guid accountId, Guid? sourceLineId, JournalAmount amount)
    {
        AccountId = accountId;
        SourceLineId = sourceLineId;
        Amount = amount;
    }

    public Guid AccountId { get; }

    public Guid? SourceLineId { get; }

    public JournalAmount Amount { get; }

    public static JournalLineDraft Create(Guid accountId, Guid? sourceLineId, JournalAmount amount)
    {
        if (accountId == Guid.Empty)
        {
            throw new JournalInvariantException("JOURNAL_ACCOUNT_REQUIRED", "Journal account ID is required.");
        }

        if (sourceLineId == Guid.Empty)
        {
            throw new JournalInvariantException(
                "JOURNAL_SOURCE_LINE_INVALID",
                "Source line ID must be null or a non-empty UUID.");
        }

        if (!amount.IsValid)
        {
            throw new JournalInvariantException("JOURNAL_AMOUNT_INVALID", "Journal amount is invalid.");
        }

        return new JournalLineDraft(accountId, sourceLineId, amount);
    }
}
