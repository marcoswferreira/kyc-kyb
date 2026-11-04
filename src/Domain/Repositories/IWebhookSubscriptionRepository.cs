using Domain.Entities;

namespace Domain.Repositories;

public interface IWebhookSubscriptionRepository
{
    Task<IReadOnlyList<WebhookSubscription>> GetActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default);
    Task UpdateAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default);
}

