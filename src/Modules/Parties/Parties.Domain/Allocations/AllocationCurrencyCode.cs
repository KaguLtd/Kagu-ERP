namespace KaguERP.Modules.Parties.Domain.Allocations;

public sealed record AllocationCurrencyCode
{
    private AllocationCurrencyCode(string value) => Value = value;

    public string Value { get; }

    public static AllocationCurrencyCode Create(string? value)
    {
        if (value is null || value.Length != 3 || value.Any(character => character is < 'A' or > 'Z'))
        {
            throw new AllocationInvariantException(
                "ALLOCATION_CURRENCY_INVALID",
                "Currency must contain exactly three uppercase ASCII letters.");
        }

        return new AllocationCurrencyCode(value);
    }

    public override string ToString() => Value;
}
