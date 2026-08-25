namespace KaguERP.Modules.Accounting.Domain.Dimensions;

public sealed class DimensionInvariantException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
