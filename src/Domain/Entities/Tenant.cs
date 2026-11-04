using Domain.Common;

namespace Domain.Entities;

public sealed class Tenant : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string ApiKeyHash { get; private set; } = string.Empty;
    public string ApiKeyPrefix { get; private set; } = string.Empty;
    public int RateLimitPerMinute { get; private set; } = 60;
    public bool IsActive { get; private set; } = true;

    private Tenant() { } // EF Core

    public Tenant(Guid id, string name, string slug, string apiKeyHash, string apiKeyPrefix, int rateLimitPerMinute = 60)
    {
        Id = id;
        Name = name;
        Slug = slug;
        ApiKeyHash = apiKeyHash;
        ApiKeyPrefix = apiKeyPrefix;
        RateLimitPerMinute = rateLimitPerMinute;
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
    }

    public static Tenant Create(string name, string slug, string apiKeyHash, string apiKeyPrefix, int rateLimitPerMinute = 60)
    {
        return new Tenant(Guid.NewGuid(), name, slug, apiKeyHash, apiKeyPrefix, rateLimitPerMinute);
    }

    public void UpdateApiKey(string newHash, string newPrefix)
    {
        ApiKeyHash = newHash;
        ApiKeyPrefix = newPrefix;
        SetUpdatedAt(DateTime.UtcNow);
    }

    public void UpdateRateLimit(int newLimit)
    {
        RateLimitPerMinute = newLimit;
        SetUpdatedAt(DateTime.UtcNow);
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdatedAt(DateTime.UtcNow);
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdatedAt(DateTime.UtcNow);
    }
}

