using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.Repositories;

public class UsageMetricRepository : IUsageMetricRepository
{
    private readonly KycKybDbContext _context;

    public UsageMetricRepository(KycKybDbContext context)
    {
        _context = context;
    }

    public async Task<UsageMetric?> GetByTenantAndMonthAsync(Guid tenantId, string yearMonth, CancellationToken cancellationToken = default)
    {
        return await _context.UsageMetrics
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.YearMonth == yearMonth, cancellationToken);
    }

    public async Task AddOrUpdateUsageAsync(Guid tenantId, string yearMonth, int checks, int providerCalls, CancellationToken cancellationToken = default)
    {
        var metric = await GetByTenantAndMonthAsync(tenantId, yearMonth, cancellationToken);
        if (metric is null)
        {
            metric = UsageMetric.Create(tenantId, yearMonth);
            metric.IncrementUsage(checks, providerCalls);
            await _context.UsageMetrics.AddAsync(metric, cancellationToken);
        }
        else
        {
            metric.IncrementUsage(checks, providerCalls);
            _context.UsageMetrics.Update(metric);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}

