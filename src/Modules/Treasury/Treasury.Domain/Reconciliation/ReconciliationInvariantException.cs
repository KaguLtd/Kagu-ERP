namespace KaguERP.Modules.Treasury.Domain.Reconciliation;

public sealed class ReconciliationInvariantException : InvalidOperationException
{
    public ReconciliationInvariantException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
