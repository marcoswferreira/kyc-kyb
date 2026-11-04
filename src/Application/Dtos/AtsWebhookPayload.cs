using Domain.Enums;

namespace Application.Dtos;

public record AtsWebhookPayload(
    string Event,
    Guid CheckId,
    string CandidateDocumentMasked,
    RiskIndicator OverallRisk,
    HumanReviewDecision HumanReviewStatus,
    string Status,
    string? AiSummary,
    string VerificationUrl,
    DateTime Timestamp);

