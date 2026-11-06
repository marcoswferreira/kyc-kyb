namespace Domain.Events;

public sealed record DataRetentionPurgedEvent(
    Guid EventId,
    Guid CheckId,
    Guid TenantId,
    DateTime PurgedAt,
    DateTime OccurredAt) : IDomainEvent;
