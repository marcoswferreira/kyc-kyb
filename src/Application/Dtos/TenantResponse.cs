namespace Application.Dtos;

public record TenantResponse(
    Guid Id,
    string Name,
    string Slug,
    string ApiKeyPrefix,
    int RateLimitPerMinute,
    bool IsActive,
    DateTime CreatedAt);

