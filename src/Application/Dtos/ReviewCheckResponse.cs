using Domain.Enums;

namespace Application.Dtos;

public record ReviewCheckResponse(
    Guid CheckId,
    HumanReviewDecision Decision,
    string ReviewerId,
    DateTime ReviewedAt,
    string Message);

