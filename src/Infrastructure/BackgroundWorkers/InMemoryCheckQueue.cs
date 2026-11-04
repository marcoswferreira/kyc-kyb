using System.Threading.Channels;

namespace Infrastructure.BackgroundWorkers;

public class InMemoryCheckQueue : ICheckQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(Guid checkId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(checkId, cancellationToken);
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }

    public bool TryPeek(out int count)
    {
        count = _channel.Reader.Count;
        return true;
    }
}

