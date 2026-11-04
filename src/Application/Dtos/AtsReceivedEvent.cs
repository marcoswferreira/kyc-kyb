using Domain.Enums;

namespace Application.Dtos;

public record AtsReceivedEvent(
    string Event,
    Guid CheckId,
    string DocumentMasked,
    RiskIndicator Risk,
    HumanReviewDecision HumanReviewStatus,
    string Status,
    string? AiSummary,
    DateTime ReceivedAt);

