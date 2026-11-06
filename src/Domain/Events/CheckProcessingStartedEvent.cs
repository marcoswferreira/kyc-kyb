namespace Domain.Events;

public sealed record CheckProcessingStartedEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid CheckId,
    Guid TenantId) : IDomainEvent;

