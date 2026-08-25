namespace KaguERP.Modules.Reporting.Domain.ControlAccounts;

public sealed class ReportingInvariantException : InvalidOperationException
{
    public ReportingInvariantException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
