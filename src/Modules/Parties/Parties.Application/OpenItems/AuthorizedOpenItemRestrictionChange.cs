using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Parties.Domain.OpenItems;

namespace KaguERP.Modules.Parties.Application.OpenItems;

public sealed class AuthorizedOpenItemRestrictionChange
{
    public const string RequiredPermission = "party.open-item-restriction.manage";

    private AuthorizedOpenItemRestrictionChange(Guid actorId, OpenItemRestrictionEvent restrictionEvent)
    {
        ActorId = actorId;
        RestrictionEvent = restrictionEvent;
    }

    public Guid ActorId { get; }

    public OpenItemRestrictionEvent RestrictionEvent { get; }

    public static AuthorizedOpenItemRestrictionChange Create(
        ExecutionScope scope,
        OpenItemRestrictionEvent restrictionEvent)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(restrictionEvent);
        scope.EnsureAllowed(restrictionEvent.TenantId, restrictionEvent.CompanyId);
        if (!scope.HasPermission(restrictionEvent.CompanyId, RequiredPermission))
        {
            throw new OpenItemRestrictionAuthorizationException();
        }
        return new AuthorizedOpenItemRestrictionChange(scope.ActorId, restrictionEvent);
    }
}

public sealed class OpenItemRestrictionAuthorizationException()
    : Exception("The active actor cannot manage restrictions for this open item.")
{
    public string Code { get; } = "OPEN_ITEM_RESTRICTION_PERMISSION_REQUIRED";
}
