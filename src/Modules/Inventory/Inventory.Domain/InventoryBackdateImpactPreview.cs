using System.Collections.ObjectModel;

namespace KaguERP.Modules.Inventory.Domain;

public enum InventoryLockScope
{
    Operational = 1,
    InventoryValuation = 2,
    GeneralLedger = 3,
    Tax = 4,
    HardLegal = 5,
}

public enum InventoryPeriodState
{
    Open = 1,
    SoftClosed = 2,
    Review = 3,
    HardClosed = 4,
}

public sealed record InventoryPeriodLockImpact
{
    private InventoryPeriodLockImpact(
        Guid tenantId,
        Guid companyId,
        Guid periodId,
        InventoryLockScope scope,
        InventoryPeriodState state,
        DateOnly startsOn,
        DateOnly endsOn)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        PeriodId = periodId;
        Scope = scope;
        State = state;
        StartsOn = startsOn;
        EndsOn = endsOn;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid PeriodId { get; }
    public InventoryLockScope Scope { get; }
    public InventoryPeriodState State { get; }
    public DateOnly StartsOn { get; }
    public DateOnly EndsOn { get; }

    public static InventoryPeriodLockImpact Create(
        Guid tenantId,
        Guid companyId,
        Guid periodId,
        InventoryLockScope scope,
        InventoryPeriodState state,
        DateOnly startsOn,
        DateOnly endsOn)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty || periodId == Guid.Empty)
        {
            throw new InventoryInvariantException("INVENTORY_IMPACT_LOCK_ID_REQUIRED", "Impact lock identities are required.");
        }
        if (!Enum.IsDefined(scope) || !Enum.IsDefined(state))
        {
            throw new InventoryInvariantException("INVENTORY_IMPACT_LOCK_INVALID", "Impact lock scope and state must be valid.");
        }
        if (startsOn == default || endsOn < startsOn)
        {
            throw new InventoryInvariantException("INVENTORY_IMPACT_LOCK_PERIOD_INVALID", "Impact lock period is invalid.");
        }

        return new InventoryPeriodLockImpact(tenantId, companyId, periodId, scope, state, startsOn, endsOn);
    }
}

public sealed record InventoryBackdateImpactPreview
{
    private InventoryBackdateImpactPreview(
        Guid previewId,
        BackdatedStockMovementImpactRequest request,
        DateTimeOffset generatedAt,
        IReadOnlyList<InventoryPeriodLockImpact> periodLocks,
        int affectedCostLayerCount,
        int affectedReportGenerationCount,
        bool affectsExternalDeclaration,
        string previewChecksumSha256)
    {
        PreviewId = previewId;
        Request = request;
        GeneratedAt = generatedAt;
        PeriodLocks = periodLocks;
        AffectedCostLayerCount = affectedCostLayerCount;
        AffectedReportGenerationCount = affectedReportGenerationCount;
        AffectsExternalDeclaration = affectsExternalDeclaration;
        PreviewChecksumSha256 = previewChecksumSha256;
    }

    public Guid PreviewId { get; }
    public BackdatedStockMovementImpactRequest Request { get; }
    public DateTimeOffset GeneratedAt { get; }
    public IReadOnlyList<InventoryPeriodLockImpact> PeriodLocks { get; }
    public int AffectedCostLayerCount { get; }
    public int AffectedReportGenerationCount { get; }
    public bool AffectsExternalDeclaration { get; }
    public string PreviewChecksumSha256 { get; }

    public static InventoryBackdateImpactPreview Create(
        Guid previewId,
        BackdatedStockMovementImpactRequest request,
        DateTimeOffset generatedAt,
        IEnumerable<InventoryPeriodLockImpact?> periodLocks,
        int affectedCostLayerCount,
        int affectedReportGenerationCount,
        bool affectsExternalDeclaration,
        string previewChecksumSha256)
    {
        if (previewId == Guid.Empty)
        {
            throw new InventoryInvariantException("INVENTORY_IMPACT_PREVIEW_ID_REQUIRED", "Impact preview ID is required.");
        }
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(periodLocks);
        if (generatedAt.Offset != TimeSpan.Zero || generatedAt < request.CurrentWatermark.RecordedCutoff)
        {
            throw new InventoryInvariantException(
                "INVENTORY_IMPACT_PREVIEW_TIME_INVALID",
                "Impact preview timestamp must be UTC and not precede its watermark cutoff.");
        }
        if (affectedCostLayerCount < 0 || affectedReportGenerationCount < 0)
        {
            throw new InventoryInvariantException(
                "INVENTORY_IMPACT_COUNT_INVALID",
                "Impact preview counts cannot be negative.");
        }

        InventoryPeriodLockImpact?[] copied = periodLocks.ToArray();
        if (copied.Any(item => item is null))
        {
            throw new InventoryInvariantException("INVENTORY_IMPACT_LOCK_REQUIRED", "Impact lock evidence cannot contain null.");
        }
        InventoryPeriodLockImpact[] locks = copied.Cast<InventoryPeriodLockImpact>()
            .OrderBy(item => item.Scope)
            .ThenBy(item => item.StartsOn)
            .ThenBy(item => item.PeriodId)
            .ToArray();
        if (locks.Any(item => item.TenantId != request.ProposedMovement.TenantId ||
                              item.CompanyId != request.ProposedMovement.CompanyId))
        {
            throw new InventoryInvariantException("INVENTORY_IMPACT_LOCK_SCOPE_MISMATCH", "Impact lock scope must match the movement.");
        }
        if (locks.GroupBy(item => (item.Scope, item.PeriodId)).Any(group => group.Count() != 1))
        {
            throw new InventoryInvariantException("INVENTORY_IMPACT_LOCK_DUPLICATE", "Impact lock evidence must be unique.");
        }

        InventoryLockScope[] requiredScopes = Enum.GetValues<InventoryLockScope>();
        if (requiredScopes.Any(scope => !locks.Any(item => item.Scope == scope &&
                                                           item.StartsOn <= request.AffectedThrough.EffectiveDate &&
                                                           item.EndsOn >= request.AffectedFrom.EffectiveDate)))
        {
            throw new InventoryInvariantException(
                "INVENTORY_IMPACT_LOCK_COVERAGE_INCOMPLETE",
                "Impact preview requires intersecting evidence for every period-lock scope.");
        }

        return new InventoryBackdateImpactPreview(
            previewId,
            request,
            generatedAt,
            new ReadOnlyCollection<InventoryPeriodLockImpact>(locks),
            affectedCostLayerCount,
            affectedReportGenerationCount,
            affectsExternalDeclaration,
            InventoryValuationWatermark.RequireSha256(previewChecksumSha256));
    }
}
