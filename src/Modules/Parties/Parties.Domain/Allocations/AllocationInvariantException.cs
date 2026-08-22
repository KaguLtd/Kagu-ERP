namespace KaguERP.Modules.Parties.Domain.Allocations;

public sealed class AllocationInvariantException : Exception
{
    public AllocationInvariantException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public AllocationInvariantException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
