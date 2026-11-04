namespace Domain.Enums;

public enum ProviderExecutionStatus
{
    Success = 1,
    Timeout = 2,
    CircuitBroken = 3,
    Error = 4,
    CacheHit = 5
}

