using KaguERP.Modules.Inventory.Domain;

namespace KaguERP.Modules.Inventory.Application.Queries;

public sealed record InventoryOnHandLine(
    Guid ItemId,
    Guid WarehouseId,
    InventoryUomCode BaseUom,
    InventoryQuantity OnHand);

public sealed class InventoryOnHandSnapshot
{
    public InventoryOnHandSnapshot(
        Guid tenantId,
        Guid companyId,
        DateOnly effectiveAsOf,
        DateTimeOffset recordedCutoff,
        IEnumerable<InventoryOnHandLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (tenantId == Guid.Empty || companyId == Guid.Empty || recordedCutoff.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Inventory on-hand snapshot requires valid scope and a UTC cutoff.");
        }

        InventoryOnHandLine[] snapshot = lines.ToArray();
        if (snapshot.Any(line => line.ItemId == Guid.Empty || line.WarehouseId == Guid.Empty) ||
            snapshot.Select(line => (line.ItemId, line.WarehouseId)).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException("Inventory on-hand lines require unique item and warehouse identities.", nameof(lines));
        }

        TenantId = tenantId;
        CompanyId = companyId;
        EffectiveAsOf = effectiveAsOf;
        RecordedCutoff = recordedCutoff;
        Lines = Array.AsReadOnly(snapshot);
    }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public DateOnly EffectiveAsOf { get; }

    public DateTimeOffset RecordedCutoff { get; }

    public IReadOnlyList<InventoryOnHandLine> Lines { get; }
}
