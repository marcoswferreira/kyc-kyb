using Domain.Enums;

namespace Application.Dtos;

public record CheckDetailsResponse(
    Guid Id,
    Guid TenantId,
    SubjectType SubjectType,
    string DocumentNumber,
    string DocumentFormatted,
    string DocumentMasked,
    string Name,
    CheckPurpose Purpose,
    LegalBasis LegalBasis,
    bool ConsentObtained,
    DateTime? ConsentTimestamp,
    CheckStatus Status,
    RiskIndicator OverallRisk,
    HumanReviewDecision HumanReviewStatus,
    string? HumanReviewerId,
    string? HumanReviewNotes,
    DateTime? HumanReviewedAt,
    DateTime RetentionUntil,
    bool IsPurged,
    string? EvidenceSha256,
    string? AiSummary,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    IReadOnlyList<CheckFindingDto> Findings);

