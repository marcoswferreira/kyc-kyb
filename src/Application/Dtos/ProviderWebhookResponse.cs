namespace Application.Dtos;

/// <summary>
/// Resposta de confirmação de recebimento (Ack) enviada ao provedor após enfileiramento seguro do webhook.
/// </summary>
public sealed record ProviderWebhookResponse(
    bool Acknowledged,
    string Message,
    string IngestionReceiptId,
    DateTime ProcessedAt);

