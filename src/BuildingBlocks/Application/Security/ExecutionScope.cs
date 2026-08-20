using System.Collections.Frozen;

namespace KaguERP.BuildingBlocks.Application.Security;

public sealed class ExecutionScope
{
    private readonly FrozenDictionary<Guid, FrozenSet<string>> companyPermissions;
    private readonly FrozenSet<Guid> companyIds;

    public ExecutionScope(Guid tenantId, Guid actorId, IEnumerable<Guid> companyIds)
        : this(tenantId, actorId, companyIds.Select(companyId => new CompanyAccess(companyId, [])))
    {
    }

    public ExecutionScope(Guid tenantId, Guid actorId, IEnumerable<CompanyAccess> companyAccess)
    {
        ArgumentNullException.ThrowIfNull(companyAccess);

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("Actor ID cannot be empty.", nameof(actorId));
        }

        CompanyAccess[] access = companyAccess.ToArray();
        companyIds = access.Select(item => item.CompanyId).ToFrozenSet();
        if (this.companyIds.Count == 0 || this.companyIds.Contains(Guid.Empty))
        {
            throw new ArgumentException("At least one non-empty company ID is required.", nameof(companyAccess));
        }

        if (access.Length != companyIds.Count)
        {
            throw new ArgumentException("Company access entries must be unique.", nameof(companyAccess));
        }

        companyPermissions = access.ToFrozenDictionary(item => item.CompanyId, item => item.Permissions);

        TenantId = tenantId;
        ActorId = actorId;
    }

    public Guid TenantId { get; }

    public Guid ActorId { get; }

    public IReadOnlySet<Guid> CompanyIds => companyIds;

    public bool Allows(Guid tenantId, Guid companyId) =>
        tenantId == TenantId && companyIds.Contains(companyId);

    public bool HasPermission(Guid companyId, string permissionCode) =>
        companyPermissions.TryGetValue(companyId, out FrozenSet<string>? permissions) &&
        permissions.Contains(permissionCode);

    public void EnsureAllowed(Guid tenantId, Guid companyId)
    {
        if (!Allows(tenantId, companyId))
        {
            throw new ExecutionScopeDeniedException();
        }
    }
}

public sealed class CompanyAccess
{
    public CompanyAccess(Guid companyId, IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Company ID cannot be empty.", nameof(companyId));
        }

        FrozenSet<string> normalizedPermissions = permissions
            .Select(permission => permission?.Trim())
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Select(permission => permission!)
            .ToFrozenSet(StringComparer.Ordinal);

        CompanyId = companyId;
        Permissions = normalizedPermissions;
    }

    public Guid CompanyId { get; }

    public FrozenSet<string> Permissions { get; }
}

public sealed class ExecutionScopeDeniedException : Exception
{
    public ExecutionScopeDeniedException()
        : base("The requested resource is outside the active execution scope.")
    {
    }
}
