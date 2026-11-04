using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.Repositories;

public class WebhookSubscriptionRepository : IWebhookSubscriptionRepository
{
    private readonly KycKybDbContext _context;

    public WebhookSubscriptionRepository(KycKybDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<WebhookSubscription>> GetActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.WebhookSubscriptions
            .Where(w => w.TenantId == tenantId && w.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default)
    {
        await _context.WebhookSubscriptions.AddAsync(subscription, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default)
    {
        _context.WebhookSubscriptions.Update(subscription);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

