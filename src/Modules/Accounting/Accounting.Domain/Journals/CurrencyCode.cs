namespace KaguERP.Modules.Accounting.Domain.Journals;

public sealed record CurrencyCode
{
    private CurrencyCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static CurrencyCode Create(string value)
    {
        if (value is null || value.Length != 3 || value.Any(character => character is < 'A' or > 'Z'))
        {
            throw new JournalInvariantException(
                "JOURNAL_CURRENCY_INVALID",
                "Currency must be a three-letter uppercase ASCII code.");
        }

        return new CurrencyCode(value);
    }

    public override string ToString() => Value;
}
