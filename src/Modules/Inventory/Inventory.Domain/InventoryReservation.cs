namespace KaguERP.Modules.Inventory.Domain;

public enum InventoryReservationStatus
{
    Active = 1,
    PartiallyConsumed = 2,
    Consumed = 3,
    Released = 4,
    Expired = 5,
}

public enum InventoryReservationTransition
{
    Consume = 1,
    Release = 2,
    Expire = 3,
}

public sealed record InventoryDemandSourceIdentity
{
    private InventoryDemandSourceIdentity(
        string sourceType,
        Guid sourceId,
        Guid sourceLineId,
        long sourceVersion)
    {
        SourceType = sourceType;
        SourceId = sourceId;
        SourceLineId = sourceLineId;
        SourceVersion = sourceVersion;
    }

    public string SourceType { get; }
    public Guid SourceId { get; }
    public Guid SourceLineId { get; }
    public long SourceVersion { get; }

    public static InventoryDemandSourceIdentity Create(
        string sourceType,
        Guid sourceId,
        Guid sourceLineId,
        long sourceVersion)
    {
        string normalizedType = sourceType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedType.Length is < 3 or > 64 || sourceId == Guid.Empty ||
            sourceLineId == Guid.Empty || sourceVersion <= 0 ||
            !normalizedType.All(character => char.IsAsciiLetterLower(character) ||
                char.IsAsciiDigit(character) || character is '.' or '-'))
        {
            throw new InventoryInvariantException(
                "INVENTORY_RESERVATION_SOURCE_INVALID",
                "Inventory reservation requires a canonical versioned demand source line.");
        }

        return new InventoryDemandSourceIdentity(normalizedType, sourceId, sourceLineId, sourceVersion);
    }
}

public sealed record InventoryReservationState
{
    private InventoryReservationState(
        Guid reservationId,
        Guid tenantId,
        Guid companyId,
        Guid itemId,
        Guid warehouseId,
        InventoryUomCode baseUom,
        InventoryDemandSourceIdentity source,
        InventoryQuantity reservedQuantity,
        InventoryQuantity consumedQuantity,
        InventoryReservationStatus status,
        long version,
        DateTimeOffset? expiresAt)
    {
        ReservationId = reservationId;
        TenantId = tenantId;
        CompanyId = companyId;
        ItemId = itemId;
        WarehouseId = warehouseId;
        BaseUom = baseUom;
        Source = source;
        ReservedQuantity = reservedQuantity;
        ConsumedQuantity = consumedQuantity;
        Status = status;
        Version = version;
        ExpiresAt = expiresAt;
    }

    public Guid ReservationId { get; }
    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid ItemId { get; }
    public Guid WarehouseId { get; }
    public InventoryUomCode BaseUom { get; }
    public InventoryDemandSourceIdentity Source { get; }
    public InventoryQuantity ReservedQuantity { get; }
    public InventoryQuantity ConsumedQuantity { get; }
    public InventoryQuantity RemainingQuantity =>
        Status is InventoryReservationStatus.Active or InventoryReservationStatus.PartiallyConsumed
            ? InventoryQuantity.Create(ReservedQuantity.Value - ConsumedQuantity.Value)
            : InventoryQuantity.Create(decimal.Zero);
    public InventoryReservationStatus Status { get; }
    public long Version { get; }
    public DateTimeOffset? ExpiresAt { get; }

    public static InventoryReservationState CreateActive(
        Guid reservationId,
        Guid tenantId,
        Guid companyId,
        Guid itemId,
        Guid warehouseId,
        InventoryUomCode baseUom,
        InventoryDemandSourceIdentity source,
        InventoryQuantity reservedQuantity,
        DateTimeOffset? expiresAt = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (new[] { reservationId, tenantId, companyId, itemId, warehouseId }.Any(id => id == Guid.Empty) ||
            string.IsNullOrEmpty(baseUom.Value) || !reservedQuantity.IsPositive ||
            (expiresAt is not null && (expiresAt.Value.Offset != TimeSpan.Zero ||
                expiresAt.Value.Ticks % TimeSpan.TicksPerMicrosecond != 0)))
        {
            throw new InventoryInvariantException(
                "INVENTORY_RESERVATION_INVALID",
                "Inventory reservation requires scope, target, positive quantity and an optional UTC expiry.");
        }

        return new InventoryReservationState(
            reservationId,
            tenantId,
            companyId,
            itemId,
            warehouseId,
            baseUom,
            source,
            reservedQuantity,
            InventoryQuantity.Create(decimal.Zero),
            InventoryReservationStatus.Active,
            1,
            expiresAt);
    }

    internal InventoryReservationState Advance(
        InventoryQuantity consumedQuantity,
        InventoryReservationStatus status) =>
        new(
            ReservationId,
            TenantId,
            CompanyId,
            ItemId,
            WarehouseId,
            BaseUom,
            Source,
            ReservedQuantity,
            consumedQuantity,
            status,
            checked(Version + 1),
            ExpiresAt);
}

public sealed record InventoryReservationTransitionEvent(
    Guid EventId,
    Guid ReservationId,
    Guid TenantId,
    Guid CompanyId,
    long PreviousVersion,
    long NewVersion,
    InventoryReservationStatus PreviousStatus,
    InventoryReservationStatus NewStatus,
    InventoryReservationTransition Transition,
    InventoryQuantity Quantity,
    Guid ActorId,
    Guid CorrelationId,
    DateTimeOffset OccurredAt,
    string? Reason);

public sealed record InventoryReservationTransitionResult(
    InventoryReservationState State,
    InventoryReservationTransitionEvent Event);

public static class InventoryReservationLifecycle
{
    public static InventoryReservationTransitionResult Apply(
        InventoryReservationState state,
        InventoryReservationTransition transition,
        long expectedVersion,
        InventoryQuantity quantity,
        Guid actorId,
        Guid correlationId,
        DateTimeOffset occurredAt,
        string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (expectedVersion != state.Version)
        {
            throw new InventoryInvariantException(
                "INVENTORY_RESERVATION_VERSION_CONFLICT",
                "Inventory reservation expected version does not match current version.");
        }
        if (actorId == Guid.Empty || correlationId == Guid.Empty || occurredAt.Offset != TimeSpan.Zero ||
            occurredAt.Ticks % TimeSpan.TicksPerMicrosecond != 0)
        {
            throw new InventoryInvariantException(
                "INVENTORY_RESERVATION_TRANSITION_CONTEXT_INVALID",
                "Reservation transition requires actor, correlation and PostgreSQL-safe UTC time.");
        }
        if (state.Status is not (InventoryReservationStatus.Active or
            InventoryReservationStatus.PartiallyConsumed))
        {
            throw new InventoryInvariantException(
                "INVENTORY_RESERVATION_TRANSITION_NOT_ALLOWED",
                "A terminal reservation cannot transition again.");
        }

        string? normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        InventoryQuantity consumed = state.ConsumedQuantity;
        InventoryReservationStatus nextStatus;
        switch (transition)
        {
            case InventoryReservationTransition.Consume:
                if (!quantity.IsPositive || quantity.Value > state.RemainingQuantity.Value)
                {
                    throw new InventoryInvariantException(
                        "INVENTORY_RESERVATION_CONSUMPTION_INVALID",
                        "Consumption must be positive and cannot exceed the active remaining quantity.");
                }
                consumed = consumed + quantity;
                nextStatus = consumed.Value == state.ReservedQuantity.Value
                    ? InventoryReservationStatus.Consumed
                    : InventoryReservationStatus.PartiallyConsumed;
                break;
            case InventoryReservationTransition.Release:
                RequireZeroQuantity(quantity, transition);
                RequireReason(normalizedReason, transition);
                nextStatus = InventoryReservationStatus.Released;
                break;
            case InventoryReservationTransition.Expire:
                RequireZeroQuantity(quantity, transition);
                if (state.ExpiresAt is null || occurredAt < state.ExpiresAt.Value)
                {
                    throw new InventoryInvariantException(
                        "INVENTORY_RESERVATION_EXPIRY_NOT_REACHED",
                        "Reservation cannot expire before its explicit UTC expiry.");
                }
                nextStatus = InventoryReservationStatus.Expired;
                break;
            default:
                throw new InventoryInvariantException(
                    "INVENTORY_RESERVATION_TRANSITION_INVALID",
                    "Inventory reservation transition is invalid.");
        }

        InventoryReservationState next = state.Advance(consumed, nextStatus);
        var lifecycleEvent = new InventoryReservationTransitionEvent(
            Guid.CreateVersion7(),
            state.ReservationId,
            state.TenantId,
            state.CompanyId,
            state.Version,
            next.Version,
            state.Status,
            next.Status,
            transition,
            quantity,
            actorId,
            correlationId,
            occurredAt,
            normalizedReason);
        return new InventoryReservationTransitionResult(next, lifecycleEvent);
    }

    private static void RequireZeroQuantity(
        InventoryQuantity quantity,
        InventoryReservationTransition transition)
    {
        if (!quantity.IsZero)
        {
            throw new InventoryInvariantException(
                "INVENTORY_RESERVATION_TRANSITION_QUANTITY_INVALID",
                $"{transition} does not accept a quantity.");
        }
    }

    private static void RequireReason(string? reason, InventoryReservationTransition transition)
    {
        if (reason is null)
        {
            throw new InventoryInvariantException(
                "INVENTORY_RESERVATION_REASON_REQUIRED",
                $"{transition} requires a reason.");
        }
    }
}
