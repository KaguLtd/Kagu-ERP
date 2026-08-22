namespace KaguERP.Modules.Accounting.Domain.Periods;

public sealed class PeriodInvariantException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
