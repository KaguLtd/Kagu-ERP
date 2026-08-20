namespace KaguERP.BuildingBlocks.Application.Audit;

public interface IAuthorizationAuditWriter
{
    Task WriteAsync(
        RequestAuditContext context,
        AuthorizationAuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}
