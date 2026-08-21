namespace KaguERP.Modules.Accounting.Domain.Journals;

public sealed class JournalInvariantException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
