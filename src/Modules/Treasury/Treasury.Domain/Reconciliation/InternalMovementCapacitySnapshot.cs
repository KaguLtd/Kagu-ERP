using KaguERP.Modules.Treasury.Domain.Payments;

namespace KaguERP.Modules.Treasury.Domain.Reconciliation;

public sealed record InternalMovementCapacitySnapshot
{
    private InternalMovementCapacitySnapshot(
        Guid tenantId,
        Guid companyId,
        Guid treasuryAccountId,
        Guid movementId,
        long version,
        PaymentDirection direction,
        TreasuryCurrencyCode currency,
        decimal usableAmount)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        TreasuryAccountId = treasuryAccountId;
        MovementId = movementId;
        Version = version;
        Direction = direction;
        Currency = currency;
        UsableAmount = usableAmount;
    }

    public Guid TenantId { get; }

    public Guid CompanyId { get; }

    public Guid TreasuryAccountId { get; }

    public Guid MovementId { get; }

    public long Version { get; }

    public PaymentDirection Direction { get; }

    public TreasuryCurrencyCode Currency { get; }

    public decimal UsableAmount { get; }

    public static InternalMovementCapacitySnapshot Create(
        Guid tenantId,
        Guid companyId,
        Guid treasuryAccountId,
        Guid movementId,
        long version,
        PaymentDirection direction,
        TreasuryCurrencyCode? currency,
        decimal usableAmount)
    {
        RequireId(tenantId, "RECONCILIATION_TENANT_REQUIRED", "Movement tenant ID is required.");
        RequireId(companyId, "RECONCILIATION_COMPANY_REQUIRED", "Movement company ID is required.");
        RequireId(
            treasuryAccountId,
            "RECONCILIATION_TREASURY_ACCOUNT_REQUIRED",
            "Movement treasury-account ID is required.");
        RequireId(movementId, "RECONCILIATION_MOVEMENT_REQUIRED", "Internal movement ID is required.");
        ArgumentNullException.ThrowIfNull(currency);

        if (!Enum.IsDefined(direction))
        {
            throw new ReconciliationInvariantException(
                "RECONCILIATION_MOVEMENT_DIRECTION_INVALID",
                "Internal movement direction is invalid.");
        }

        if (version <= 0)
        {
            throw new ReconciliationInvariantException(
                "RECONCILIATION_MOVEMENT_VERSION_INVALID",
                "Internal movement snapshot version must be positive.");
        }

        if (usableAmount <= decimal.Zero)
        {
            throw new ReconciliationInvariantException(
                "RECONCILIATION_MOVEMENT_CAPACITY_INVALID",
                "Internal movement usable amount must be positive.");
        }

        return new InternalMovementCapacitySnapshot(
            tenantId,
            companyId,
            treasuryAccountId,
            movementId,
            version,
            direction,
            currency,
            usableAmount);
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new ReconciliationInvariantException(code, message);
        }
    }
}
