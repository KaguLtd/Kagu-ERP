namespace KaguERP.Modules.Accounting.Domain.Reversals;

public sealed class ReversalInvariantException : InvalidOperationException
{
    public ReversalInvariantException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
