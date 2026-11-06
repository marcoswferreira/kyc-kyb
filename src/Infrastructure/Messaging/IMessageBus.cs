namespace Infrastructure.Messaging;

/// <summary>
/// Barramento de mensageria assíncrona orientada a eventos para desacoplamento de esteiras KYC/KYB.
/// Suporta filas no padrão RabbitMQ/Kafka/SQS com fallback nativo em canais reativos.
/// </summary>
public interface IMessageBus
{
    ValueTask PublishAsync<T>(string topic, T message, CancellationToken cancellationToken = default);
    IAsyncEnumerable<T> SubscribeAsync<T>(string topic, CancellationToken cancellationToken = default);
}
