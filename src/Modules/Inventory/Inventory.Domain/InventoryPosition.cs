namespace KaguERP.Modules.Inventory.Domain;

public readonly record struct InventoryPosition : IComparable<InventoryPosition>
{
    private InventoryPosition(DateOnly effectiveDate, long sequenceKey)
    {
        EffectiveDate = effectiveDate;
        SequenceKey = sequenceKey;
    }

    public DateOnly EffectiveDate { get; }
    public long SequenceKey { get; }

    public static InventoryPosition Create(DateOnly effectiveDate, long sequenceKey)
    {
        if (effectiveDate == default)
        {
            throw new InventoryInvariantException("INVENTORY_POSITION_DATE_REQUIRED", "Inventory position date is required.");
        }
        if (sequenceKey <= 0)
        {
            throw new InventoryInvariantException("INVENTORY_POSITION_SEQUENCE_INVALID", "Inventory position sequence must be positive.");
        }

        return new InventoryPosition(effectiveDate, sequenceKey);
    }

    public int CompareTo(InventoryPosition other)
    {
        int dateComparison = EffectiveDate.CompareTo(other.EffectiveDate);
        return dateComparison != 0 ? dateComparison : SequenceKey.CompareTo(other.SequenceKey);
    }

    public static bool operator <(InventoryPosition left, InventoryPosition right) => left.CompareTo(right) < 0;

    public static bool operator <=(InventoryPosition left, InventoryPosition right) => left.CompareTo(right) <= 0;

    public static bool operator >(InventoryPosition left, InventoryPosition right) => left.CompareTo(right) > 0;

    public static bool operator >=(InventoryPosition left, InventoryPosition right) => left.CompareTo(right) >= 0;
}
