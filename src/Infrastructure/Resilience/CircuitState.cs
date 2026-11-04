namespace Infrastructure.Resilience;

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}

