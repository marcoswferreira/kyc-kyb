using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Infrastructure.Messaging;

/// <summary>
/// Implementação de barramento de mensagens assíncrono de alto desempenho baseado em System.Threading.Channels.
/// Oferece isolamento de esteiras, backpressure e desacoplamento sem necessidade obrigatória de brokers externos em desenvolvimento.
/// </summary>
public sealed class ChannelMessageBus : IMessageBus
{
    private readonly ConcurrentDictionary<string, Channel<object>> _channels = new();

    private Channel<object> GetOrCreateChannel(string topic)
    {
        return _channels.GetOrAdd(topic, _ =>
            Channel.CreateBounded<object>(new BoundedChannelOptions(10_000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = false
            }));
    }

    public async ValueTask PublishAsync<T>(string topic, T message, CancellationToken cancellationToken = default)
    {
        if (message == null)
        {
            return;
        }

        var channel = GetOrCreateChannel(topic);
        await channel.Writer.WriteAsync(message, cancellationToken);
    }

    public async IAsyncEnumerable<T> SubscribeAsync<T>(
        string topic,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = GetOrCreateChannel(topic);

        while (await channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (channel.Reader.TryRead(out var item))
            {
                if (item is T typedItem)
                {
                    yield return typedItem;
                }
            }
        }
    }
}
