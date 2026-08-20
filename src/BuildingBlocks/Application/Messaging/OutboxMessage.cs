namespace KaguERP.BuildingBlocks.Application.Messaging;

public sealed record OutboxMessage(
    Guid EventId,
    Guid TenantId,
    Guid CompanyId,
    string AggregateType,
    Guid AggregateId,
    long AggregateSequence,
    string EventType,
    int SchemaVersion,
    DateTimeOffset OccurredAt,
    string PayloadJson);

public sealed class OutboxEventConflictException : Exception
{
    public OutboxEventConflictException()
        : base("The outbox event ID already exists with different content.")
    {
    }
}
