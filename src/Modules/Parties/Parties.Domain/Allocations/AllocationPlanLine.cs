namespace KaguERP.Modules.Parties.Domain.Allocations;

public sealed record AllocationPlanLine
{
    private AllocationPlanLine(OpenItemAllocationCapacity openItem, decimal amount)
    {
        OpenItem = openItem;
        Amount = amount;
    }

    public OpenItemAllocationCapacity OpenItem { get; }
    public decimal Amount { get; }
    public decimal OpenItemRemainingAfter => OpenItem.RemainingAmount - Amount;

    public static AllocationPlanLine Create(OpenItemAllocationCapacity? openItem, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(openItem);

        if (amount <= 0m)
        {
            throw new AllocationInvariantException(
                "ALLOCATION_AMOUNT_INVALID",
                "Allocation amount must be greater than zero.");
        }

        if (amount > openItem.RemainingAmount)
        {
            throw new AllocationInvariantException(
                "ALLOCATION_OPEN_ITEM_EXCEEDED",
                "Allocation amount cannot exceed the open-item remaining amount.");
        }

        return new AllocationPlanLine(openItem, amount);
    }
}
