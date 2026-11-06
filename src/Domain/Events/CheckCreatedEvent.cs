using Domain.Enums;

namespace Domain.Events;

public sealed record CheckCreatedEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid CheckId,
    Guid TenantId,
    string DocumentMasked,
    SubjectType SubjectType,
    CheckPurpose Purpose) : IDomainEvent;

