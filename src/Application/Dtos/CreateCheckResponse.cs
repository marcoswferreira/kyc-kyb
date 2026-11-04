using Domain.Enums;

namespace Application.Dtos;

public record CreateCheckResponse(
    Guid Id,
    Guid TenantId,
    string DocumentMasked,
    SubjectType SubjectType,
    CheckStatus Status,
    DateTime CreatedAt,
    string Message);

