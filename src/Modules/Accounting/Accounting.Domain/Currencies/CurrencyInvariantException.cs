namespace KaguERP.Modules.Accounting.Domain.Currencies;

public sealed class CurrencyInvariantException : InvalidOperationException
{
    public CurrencyInvariantException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
