using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Parties.Domain.Openings;

namespace KaguERP.Modules.Parties.Application.Openings;

public sealed class AuthorizedPartyAccountOpeningPreparation
{
    public const string RequiredPermission = "party.opening-balance.create";

    private AuthorizedPartyAccountOpeningPreparation(ExecutionScope scope, PartyAccountOpeningDraft draft)
    {
        Scope = scope;
        Draft = draft;
    }

    public ExecutionScope Scope { get; }

    public Guid ActorId => Scope.ActorId;

    public PartyAccountOpeningDraft Draft { get; }

    public static AuthorizedPartyAccountOpeningPreparation Create(
        ExecutionScope scope,
        PartyAccountOpeningDraft draft)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(draft);

        scope.EnsureAllowed(draft.TenantId, draft.CompanyId);
        if (!scope.HasPermission(draft.CompanyId, RequiredPermission))
        {
            throw new PartyAccountOpeningAuthorizationException();
        }

        return new AuthorizedPartyAccountOpeningPreparation(scope, draft);
    }
}

public sealed class PartyAccountOpeningAuthorizationException()
    : Exception("The active actor cannot prepare an opening balance for this party account.")
{
    public string Code { get; } = "PARTY_OPENING_PERMISSION_REQUIRED";
}
