namespace KaguERP.BuildingBlocks.Application.Audit;

public sealed record RequestAuditContext(
    Guid CorrelationId,
    string TraceId,
    Guid TenantId,
    Guid ActorId,
    IReadOnlySet<Guid> CompanyIds,
    string? SessionId);
