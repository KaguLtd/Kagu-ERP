namespace KaguERP.BuildingBlocks.Application.Idempotency;

public enum IdempotencyRecordStatus : short
{
    InProgress = 1,
    Completed = 2,
}

public sealed record IdempotencyRecord(
    Guid RecordId,
    Guid TenantId,
    Guid CompanyId,
    Guid ActorId,
    string CommandName,
    string Key,
    string RequestHash,
    IdempotencyRecordStatus Status,
    int? ResponseStatus,
    string? ResponseBodyJson,
    Guid? AggregateId,
    bool Created);
