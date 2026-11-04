namespace Infrastructure.BackgroundWorkers;

public interface ICheckQueue
{
    ValueTask EnqueueAsync(Guid checkId, CancellationToken cancellationToken = default);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default);
    bool TryPeek(out int count);
}

