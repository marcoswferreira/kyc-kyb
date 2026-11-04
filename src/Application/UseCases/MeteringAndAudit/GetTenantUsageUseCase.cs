using Application.Dtos;
using Domain.Common;
using Domain.Repositories;

namespace Application.UseCases.MeteringAndAudit;

public class GetTenantUsageUseCase
{
    private readonly IUsageMetricRepository _usageMetricRepository;

    public GetTenantUsageUseCase(IUsageMetricRepository usageMetricRepository)
    {
        _usageMetricRepository = usageMetricRepository;
    }

    public async Task<Result<UsageMetricResponse>> ExecuteAsync(
        Guid tenantId,
        string yearMonth,
        CancellationToken cancellationToken = default)
    {
        var metric = await _usageMetricRepository.GetByTenantAndMonthAsync(tenantId, yearMonth, cancellationToken);
        if (metric is null)
        {
            return Result<UsageMetricResponse>.Success(new UsageMetricResponse(
                tenantId,
                yearMonth,
                0,
                0,
                0m,
                null));
        }

        return Result<UsageMetricResponse>.Success(new UsageMetricResponse(
            metric.TenantId,
            metric.YearMonth,
            metric.TotalChecks,
            metric.TotalProviderCalls,
            metric.BillableUnits,
            metric.UpdatedAt));
    }
}

