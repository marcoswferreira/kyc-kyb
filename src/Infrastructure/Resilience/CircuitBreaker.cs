namespace Infrastructure.Resilience;

public class CircuitBreaker : ICircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private readonly object _syncLock = new();

    private CircuitState _state = CircuitState.Closed;
    private int _consecutiveFailures;
    private DateTime _openedAt = DateTime.MinValue;

    public CircuitBreaker(int failureThreshold = 3, TimeSpan? openDuration = null)
    {
        _failureThreshold = failureThreshold;
        _openDuration = openDuration ?? TimeSpan.FromSeconds(5);
    }

    public CircuitState State
    {
        get
        {
            lock (_syncLock)
            {
                if (_state == CircuitState.Open && DateTime.UtcNow - _openedAt >= _openDuration)
                {
                    _state = CircuitState.HalfOpen;
                }
                return _state;
            }
        }
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        var currentState = State;

        if (currentState == CircuitState.Open)
        {
            throw new CircuitBreakerOpenException("Circuito aberto devido a falhas consecutivas anteriores.");
        }

        try
        {
            var result = await action();

            lock (_syncLock)
            {
                if (_state == CircuitState.HalfOpen)
                {
                    _state = CircuitState.Closed;
                    _consecutiveFailures = 0;
                }
                else if (_state == CircuitState.Closed)
                {
                    _consecutiveFailures = 0;
                }
            }

            return result;
        }
        catch (Exception)
        {
            lock (_syncLock)
            {
                _consecutiveFailures++;

                if (_state == CircuitState.HalfOpen || _consecutiveFailures >= _failureThreshold)
                {
                    _state = CircuitState.Open;
                    _openedAt = DateTime.UtcNow;
                }
            }

            throw;
        }
    }

    public void Reset()
    {
        lock (_syncLock)
        {
            _state = CircuitState.Closed;
            _consecutiveFailures = 0;
            _openedAt = DateTime.MinValue;
        }
    }
}

