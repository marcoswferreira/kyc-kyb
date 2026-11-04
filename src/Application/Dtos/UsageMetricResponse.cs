namespace Application.Dtos;

public record UsageMetricResponse(
    Guid TenantId,
    string YearMonth,
    int TotalChecks,
    int TotalProviderCalls,
    decimal BillableUnits,
    DateTime? UpdatedAt);

