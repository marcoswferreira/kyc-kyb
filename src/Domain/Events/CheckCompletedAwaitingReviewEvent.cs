using Domain.Enums;

namespace Domain.Events;

public sealed record CheckCompletedAwaitingReviewEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid CheckId,
    Guid TenantId,
    RiskIndicator OverallRisk,
    int TotalFindings,
    string EvidenceSha256) : IDomainEvent;

