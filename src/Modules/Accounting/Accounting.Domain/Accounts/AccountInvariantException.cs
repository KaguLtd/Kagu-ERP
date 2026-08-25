namespace KaguERP.Modules.Accounting.Domain.Accounts;

public sealed class AccountInvariantException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
