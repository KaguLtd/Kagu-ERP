using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Inventory.Domain;

namespace KaguERP.Modules.Inventory.Application.Reservations;

public sealed class InventoryReservationRequest
{
    public const string PolicyVersion = "DEC-MP01-025/v1";

    public InventoryReservationRequest(ExecutionScope scope, Guid companyId, Guid requestId,
        Guid warehouseId, InventoryDemandSourceIdentity source, InventoryQuantity requestedQuantity,
        DateOnly effectiveDate)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(source);
        scope.EnsureAllowed(scope.TenantId, companyId);
        if (!scope.HasPermission(companyId, AuthorizedInventoryReservationCandidate.RequiredPermission))
        {
            throw new InventoryReservationAuthorizationException("INVENTORY_RESERVATION_PERMISSION_REQUIRED",
                "The active actor cannot request reservations for this company.");
        }
        if (requestId == Guid.Empty || warehouseId == Guid.Empty || !requestedQuantity.IsPositive)
        {
            throw new ArgumentException("Reservation request requires identities and positive quantity.");
        }
        Scope = scope;
        CompanyId = companyId;
        RequestId = requestId;
        WarehouseId = warehouseId;
        Source = source;
        RequestedQuantity = requestedQuantity;
        EffectiveDate = effectiveDate;
        byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Format = "inventory-reservation-request/v1",
            scope.TenantId,
            CompanyId,
            RequestId,
            scope.ActorId,
            WarehouseId,
            source.SourceType,
            source.SourceId,
            source.SourceLineId,
            source.SourceVersion,
            Quantity = requestedQuantity.Value.ToString("G29", CultureInfo.InvariantCulture),
            Date = effectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Policy = PolicyVersion,
        });
        Fingerprint = Convert.ToHexStringLower(SHA256.HashData(canonical));
    }

    public ExecutionScope Scope { get; }
    public Guid CompanyId { get; }
    public Guid RequestId { get; }
    public Guid WarehouseId { get; }
    public InventoryDemandSourceIdentity Source { get; }
    public InventoryQuantity RequestedQuantity { get; }
    public DateOnly EffectiveDate { get; }
    public string Fingerprint { get; }
}

public sealed record InventoryReservationRequestResult(
    Guid RequestId, Guid? ReservationId, InventoryQuantity RequestedQuantity,
    InventoryQuantity ReservedQuantity, DateTimeOffset RecordedAt);

public sealed class InventoryReservationRequestConflictException()
    : InvalidOperationException("The reservation request identity was already used with different content.")
{
    public string Code { get; } = "INVENTORY_RESERVATION_REQUEST_CONFLICT";
}
