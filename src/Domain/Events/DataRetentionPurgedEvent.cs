namespace Domain.Events;

public sealed record DataRetentionPurgedEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid CheckId,
    Guid TenantId,
    DateTime PurgedAt) : IDomainEvent;
