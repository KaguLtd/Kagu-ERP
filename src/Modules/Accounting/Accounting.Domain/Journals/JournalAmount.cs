namespace KaguERP.Modules.Accounting.Domain.Journals;

public readonly record struct JournalAmount
{
    private JournalAmount(decimal debit, decimal credit)
    {
        Debit = debit;
        Credit = credit;
    }

    public decimal Debit { get; }

    public decimal Credit { get; }

    public static JournalAmount Create(decimal debit, decimal credit)
    {
        if (debit < decimal.Zero || credit < decimal.Zero)
        {
            throw new JournalInvariantException(
                "JOURNAL_AMOUNT_NEGATIVE",
                "Journal debit and credit amounts cannot be negative.");
        }

        if ((debit > decimal.Zero) == (credit > decimal.Zero))
        {
            throw new JournalInvariantException(
                "JOURNAL_AMOUNT_SIDE_INVALID",
                "A journal line must contain exactly one positive debit or credit amount.");
        }

        return new JournalAmount(debit, credit);
    }

    internal bool IsValid => Debit >= decimal.Zero &&
        Credit >= decimal.Zero &&
        (Debit > decimal.Zero) != (Credit > decimal.Zero);
}
