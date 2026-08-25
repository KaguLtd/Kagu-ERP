namespace KaguERP.Modules.Accounting.Domain.Currencies;

public enum RoundingMode
{
    ToEven = 1,
    AwayFromZero = 2,
    ToZero = 3,
    ToNegativeInfinity = 4,
    ToPositiveInfinity = 5,
}
