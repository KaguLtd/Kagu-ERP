using System.Collections.ObjectModel;

namespace KaguERP.Modules.Accounting.Domain.Periods;

public sealed class ValidatedPeriodLockSet
{
    private readonly IReadOnlyDictionary<PeriodLockScope, PeriodLockSnapshot> _locksByScope;

    private ValidatedPeriodLockSet(
        Guid tenantId,
        Guid companyId,
        Guid periodId,
        ReadOnlyCollection<PeriodLockSnapshot> locks,
        IReadOnlyDictionary<PeriodLockScope, PeriodLockSnapshot> locksByScope)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        PeriodId = periodId;
        Locks = locks;
        _locksByScope = locksByScope;
    }

    public Guid TenantId { get; }
    public Guid CompanyId { get; }
    public Guid PeriodId { get; }
    public IReadOnlyList<PeriodLockSnapshot> Locks { get; }

    public static ValidatedPeriodLockSet Create(
        Guid tenantId,
        Guid companyId,
        Guid periodId,
        IEnumerable<PeriodLockSnapshot?>? locks)
    {
        RequireId(tenantId, "PERIOD_TENANT_REQUIRED", "Tenant ID is required.");
        RequireId(companyId, "PERIOD_COMPANY_REQUIRED", "Company ID is required.");
        RequireId(periodId, "PERIOD_ID_REQUIRED", "Period ID is required.");

        if (locks is null)
        {
            throw new PeriodInvariantException("PERIOD_LOCKS_REQUIRED", "Period lock snapshots are required.");
        }

        var copiedLocks = locks.ToArray();
        if (copiedLocks.Length == 0)
        {
            throw new PeriodInvariantException("PERIOD_LOCKS_REQUIRED", "Period lock snapshots are required.");
        }

        if (copiedLocks.Any(periodLock => periodLock is null))
        {
            throw new PeriodInvariantException(
                "PERIOD_LOCK_REQUIRED",
                "Period lock snapshots cannot contain null values.");
        }

        var validatedLocks = copiedLocks.Cast<PeriodLockSnapshot>().ToArray();
        var locksByScope = new Dictionary<PeriodLockScope, PeriodLockSnapshot>();

        foreach (var periodLock in validatedLocks)
        {
            RequireSameContext(tenantId, companyId, periodId, periodLock);
            if (!locksByScope.TryAdd(periodLock.Scope, periodLock))
            {
                throw new PeriodInvariantException(
                    "PERIOD_LOCK_SCOPE_DUPLICATE",
                    "A period lock scope can occur only once in a validation snapshot.");
            }
        }

        return new ValidatedPeriodLockSet(
            tenantId,
            companyId,
            periodId,
            Array.AsReadOnly(validatedLocks),
            new ReadOnlyDictionary<PeriodLockScope, PeriodLockSnapshot>(locksByScope));
    }

    public PeriodLockSnapshot GetRequired(PeriodLockScope scope)
    {
        if (!_locksByScope.TryGetValue(scope, out var periodLock))
        {
            throw new PeriodInvariantException(
                "PERIOD_LOCK_SCOPE_MISSING",
                $"The {scope} period lock snapshot is required.");
        }

        return periodLock;
    }

    public void EnsureStandardPostingAllowed()
    {
        var hardLegalLock = GetRequired(PeriodLockScope.HardLegal);
        if (hardLegalLock.Stage != PeriodCloseStage.Open)
        {
            throw new PeriodInvariantException(
                "PERIOD_HARD_LOCK_BLOCKS_POSTING",
                "Standard posting is blocked unless the hard/legal period scope is open.");
        }

        var generalLedgerLock = GetRequired(PeriodLockScope.GeneralLedger);
        if (generalLedgerLock.Stage != PeriodCloseStage.Open)
        {
            throw new PeriodInvariantException(
                "PERIOD_GL_LOCK_BLOCKS_POSTING",
                "Standard posting is blocked unless the general-ledger period scope is open.");
        }
    }

    private static void RequireSameContext(
        Guid tenantId,
        Guid companyId,
        Guid periodId,
        PeriodLockSnapshot periodLock)
    {
        if (periodLock.TenantId != tenantId)
        {
            throw new PeriodInvariantException(
                "PERIOD_TENANT_MISMATCH",
                "All period lock snapshots must belong to the requested tenant.");
        }

        if (periodLock.CompanyId != companyId)
        {
            throw new PeriodInvariantException(
                "PERIOD_COMPANY_MISMATCH",
                "All period lock snapshots must belong to the requested company.");
        }

        if (periodLock.PeriodId != periodId)
        {
            throw new PeriodInvariantException(
                "PERIOD_ID_MISMATCH",
                "All period lock snapshots must belong to the requested period.");
        }
    }

    private static void RequireId(Guid value, string code, string message)
    {
        if (value == Guid.Empty)
        {
            throw new PeriodInvariantException(code, message);
        }
    }
}
