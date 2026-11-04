using Domain.Common;

namespace Domain.Entities;

public sealed class UsageMetric : BaseEntity, IAggregateRoot
{
    public Guid TenantId { get; private set; }
    public string YearMonth { get; private set; } = string.Empty; // Ex: "2026-11"
    public int TotalChecks { get; private set; }
    public int TotalProviderCalls { get; private set; }
    public decimal BillableUnits { get; private set; }

    private UsageMetric() { } // EF Core

    public UsageMetric(Guid id, Guid tenantId, string yearMonth, int totalChecks = 0, int totalProviderCalls = 0, decimal billableUnits = 0)
    {
        Id = id;
        TenantId = tenantId;
        YearMonth = yearMonth;
        TotalChecks = totalChecks;
        TotalProviderCalls = totalProviderCalls;
        BillableUnits = billableUnits;
        CreatedAt = DateTime.UtcNow;
    }

    public static UsageMetric Create(Guid tenantId, string yearMonth)
    {
        return new UsageMetric(Guid.NewGuid(), tenantId, yearMonth);
    }

    public void IncrementUsage(int checksCount = 1, int providerCallsCount = 1, decimal unitCost = 0.50m)
    {
        TotalChecks += checksCount;
        TotalProviderCalls += providerCallsCount;
        BillableUnits += checksCount * unitCost;
        SetUpdatedAt(DateTime.UtcNow);
    }
}

