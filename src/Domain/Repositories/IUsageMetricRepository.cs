using Domain.Entities;

namespace Domain.Repositories;

public interface IUsageMetricRepository
{
    Task<UsageMetric?> GetByTenantAndMonthAsync(Guid tenantId, string yearMonth, CancellationToken cancellationToken = default);
    Task AddOrUpdateUsageAsync(Guid tenantId, string yearMonth, int checks, int providerCalls, CancellationToken cancellationToken = default);
}

