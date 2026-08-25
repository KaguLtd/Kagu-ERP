namespace KaguERP.Modules.Parties.Domain.DueSchedules;

public sealed class PartyOpenItemInvariantException : InvalidOperationException
{
    public PartyOpenItemInvariantException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public PartyOpenItemInvariantException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
