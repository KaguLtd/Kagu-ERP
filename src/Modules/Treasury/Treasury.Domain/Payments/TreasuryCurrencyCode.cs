namespace KaguERP.Modules.Treasury.Domain.Payments;

public sealed record TreasuryCurrencyCode
{
    private TreasuryCurrencyCode(string value) => Value = value;

    public string Value { get; }

    public static TreasuryCurrencyCode Create(string? value)
    {
        if (value is null || value.Length != 3 || value.Any(character => character is < 'A' or > 'Z'))
        {
            throw new PaymentInvariantException(
                "PAYMENT_CURRENCY_INVALID",
                "Payment currency must contain exactly three uppercase ASCII letters.");
        }

        return new TreasuryCurrencyCode(value);
    }

    public override string ToString() => Value;
}
