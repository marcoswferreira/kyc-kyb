using Domain.Enums;

namespace Application.Dtos;

public record CheckFindingDto(
    Guid Id,
    string ProviderName,
    string Category,
    RiskIndicator RiskLevel,
    string Title,
    string Description,
    string ExplanationWhy,
    string EvidenceKey,
    string RawExcerpt,
    DateTime CreatedAt);

