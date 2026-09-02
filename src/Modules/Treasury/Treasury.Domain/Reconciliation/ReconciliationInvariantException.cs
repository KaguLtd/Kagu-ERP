namespace KaguERP.Modules.Treasury.Domain.Reconciliation;

public sealed class ReconciliationInvariantException : InvalidOperationException
{
    public ReconciliationInvariantException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public ReconciliationInvariantException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
