using Domain.Common;

namespace Domain.Entities;

public sealed class WebhookSubscription : BaseEntity, IAggregateRoot
{
    public Guid TenantId { get; private set; }
    public string TargetUrl { get; private set; } = string.Empty;
    public string Secret { get; private set; } = string.Empty;
    public string Events { get; private set; } = string.Empty; // Ex: "check.completed,check.failed"
    public bool IsActive { get; private set; } = true;

    private WebhookSubscription() { } // EF Core

    public WebhookSubscription(Guid id, Guid tenantId, string targetUrl, string secret, string events)
    {
        Id = id;
        TenantId = tenantId;
        TargetUrl = targetUrl;
        Secret = secret;
        Events = events;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public static WebhookSubscription Create(Guid tenantId, string targetUrl, string secret, string events)
    {
        return new WebhookSubscription(Guid.NewGuid(), tenantId, targetUrl, secret, events);
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

