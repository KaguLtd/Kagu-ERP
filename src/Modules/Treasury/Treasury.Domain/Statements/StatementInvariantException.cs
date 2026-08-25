namespace KaguERP.Modules.Treasury.Domain.Statements;

public sealed class StatementInvariantException : InvalidOperationException
{
    public StatementInvariantException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
