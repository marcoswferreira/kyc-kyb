namespace Infrastructure.Resilience;

public interface ICircuitBreaker
{
    CircuitState State { get; }
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);
    void Reset();
}

