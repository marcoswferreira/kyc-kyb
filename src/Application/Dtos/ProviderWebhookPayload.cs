using System.Text.Json;

namespace Application.Dtos;

/// <summary>
/// Payload recebido nos webhooks de retorno assíncrono dos provedores de background check (ex: Idwall, Serasa, CAF).
/// </summary>
public sealed record ProviderWebhookPayload(
    string EventId,
    string ProviderName,
    string CheckReferenceId,
    string DocumentNumber,
    string Status,
    JsonElement Data,
    string? Signature,
    DateTime Timestamp);

