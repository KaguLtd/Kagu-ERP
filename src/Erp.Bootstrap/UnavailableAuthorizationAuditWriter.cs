using KaguERP.BuildingBlocks.Application.Audit;

namespace KaguERP.Bootstrap;

internal sealed class UnavailableAuthorizationAuditWriter : IAuthorizationAuditWriter
{
    public Task WriteAsync(
        RequestAuditContext context,
        AuthorizationAuditEvent auditEvent,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Authorization audit persistence is not configured.");
}
