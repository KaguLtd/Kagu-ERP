using KaguERP.Modules.Inventory.Domain;

namespace KaguERP.Modules.Inventory.Application.Queries;

public sealed record InventoryMovementLine(
    Guid MovementId,
    Guid ItemId,
    Guid WarehouseId,
    InventoryUomCode BaseUom,
    StockMovementKind Kind,
    InventoryQuantity BaseQuantity,
    DateOnly EffectiveDate,
    DateTimeOffset RecordedAt,
    long SequenceKey,
    StockMovementSourceIdentity Source,
    Guid? TransferId,
    Guid? CounterpartWarehouseId,
    Guid? ReversalOfMovementId);

public sealed class InventoryMovementPage
{
    public InventoryMovementPage(
        Guid tenantId,
        Guid companyId,
        Guid itemId,
        DateOnly effectiveFrom,
        DateOnly effectiveThrough,
        DateTimeOffset recordedCutoff,
        IEnumerable<InventoryMovementLine> lines,
        InventoryMovementCursor? nextCursor)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (tenantId == Guid.Empty || companyId == Guid.Empty || itemId == Guid.Empty ||
            effectiveThrough < effectiveFrom || recordedCutoff.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Inventory movement page requires valid scope and bitemporal cutoffs.");
        }

        InventoryMovementLine[] snapshot = lines.ToArray();
        if (snapshot.Any(line => line.ItemId != itemId) ||
            snapshot.Select(line => line.MovementId).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException("Inventory movement page lines must belong to the item and be unique.", nameof(lines));
        }

        TenantId = tenantId;
        CompanyId = companyId;
        ItemId = itemId;
        EffectiveFrom = effectiveFrom;
        EffectiveThrough = effectiveThrough;
        RecordedCutoff = recordedCutoff;
        Lines = Array.AsReadOnly(snapshot);
        NextCursor = nextCursor;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid ItemId { get; }
    public DateOnly EffectiveFrom { get; }
    public DateOnly EffectiveThrough { get; }
    public DateTimeOffset RecordedCutoff { get; }
    public IReadOnlyList<InventoryMovementLine> Lines { get; }
    public InventoryMovementCursor? NextCursor { get; }
}
