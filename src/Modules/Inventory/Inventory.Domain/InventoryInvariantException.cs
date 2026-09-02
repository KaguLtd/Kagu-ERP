namespace KaguERP.Modules.Inventory.Domain;

public sealed class InventoryInvariantException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
