namespace KaguERP.Modules.Treasury.Domain.Payments;

public sealed class PaymentInvariantException : InvalidOperationException
{
    public PaymentInvariantException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
