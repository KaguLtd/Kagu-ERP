namespace KaguERP.BuildingBlocks.Application.Audit;

public sealed record AuthorizationAuditEvent(
    string Action,
    string TargetType,
    string? TargetId,
    string Outcome,
    string ReasonCode);
