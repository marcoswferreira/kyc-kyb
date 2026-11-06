using Domain.Enums;

namespace Domain.Events;

public sealed record HumanReviewDeliberatedEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid CheckId,
    Guid TenantId,
    string ReviewerId,
    HumanReviewDecision Decision,
    string Notes) : IDomainEvent;

