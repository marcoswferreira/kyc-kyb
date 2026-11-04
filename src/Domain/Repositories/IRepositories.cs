using Domain.Entities;
using Domain.Enums;

namespace Domain.Repositories;

public interface IBackgroundCheckRepository
{
    Task<BackgroundCheck?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BackgroundCheck?> GetByDocumentAndTenantAsync(Guid tenantId, string documentNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackgroundCheck>> ListByTenantAsync(Guid tenantId, int skip = 0, int take = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackgroundCheck>> GetPendingChecksAsync(int limit = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackgroundCheck>> GetExpiredChecksForPurgeAsync(DateTime now, int limit = 100, CancellationToken cancellationToken = default);
    Task AddAsync(BackgroundCheck check, CancellationToken cancellationToken = default);
    Task UpdateAsync(BackgroundCheck check, CancellationToken cancellationToken = default);
}

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Tenant?> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);
}

public interface IAuditLogRepository
{
    Task<AuditLogEntry?> GetLatestEntryAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntry>> ListByTenantAsync(Guid tenantId, int skip = 0, int take = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntry>> ListByResourceAsync(string resourceId, CancellationToken cancellationToken = default);
    Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}

public interface IWebhookSubscriptionRepository
{
    Task<IReadOnlyList<WebhookSubscription>> GetActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default);
    Task UpdateAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default);
}

public interface IUsageMetricRepository
{
    Task<UsageMetric?> GetByTenantAndMonthAsync(Guid tenantId, string yearMonth, CancellationToken cancellationToken = default);
    Task AddOrUpdateUsageAsync(Guid tenantId, string yearMonth, int checks, int providerCalls, CancellationToken cancellationToken = default);
}
