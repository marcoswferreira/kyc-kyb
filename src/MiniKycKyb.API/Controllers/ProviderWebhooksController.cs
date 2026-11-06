using Application.Dtos;
using Infrastructure.Logging;
using Infrastructure.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace MiniKycKyb.API.Controllers;

/// <summary>
/// Endpoint para ingestão assíncrona de webhooks disparados por provedores terceiros de background check (Idwall, Serasa, CAF, RFB).
/// Garante que o retorno assíncrono seja enfileirado na mensageria sem onerar a latência do cliente.
/// </summary>
[ApiController]
[Route("api/v1/webhooks/providers")]
public class ProviderWebhooksController : ControllerBase
{
    private readonly IMessageBus _messageBus;
    private readonly IPiiSanitizer _sanitizer;
    private readonly ILogger<ProviderWebhooksController> _logger;

    public ProviderWebhooksController(
        IMessageBus messageBus,
        IPiiSanitizer sanitizer,
        ILogger<ProviderWebhooksController> logger)
    {
        _messageBus = messageBus;
        _sanitizer = sanitizer;
        _logger = logger;
    }

    /// <summary>
    /// Recebe webhook assíncrono de um provedor externo e publica no barramento de eventos.
    /// </summary>
    [HttpPost("{providerName}")]
    [ProducesResponseType(typeof(ProviderWebhookResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReceiveProviderWebhook(
        [FromRoute] string providerName,
        [FromBody] ProviderWebhookPayload payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.CheckReferenceId) && string.IsNullOrWhiteSpace(payload.DocumentNumber))
        {
            return BadRequest(new { error = "Identificador de referência ou documento obrigatório no payload do webhook." });
        }

        var receiptId = Guid.NewGuid().ToString("N");
        var logMessage = $"Webhook recebido do provedor {providerName}: Ref={payload.CheckReferenceId}, Status={payload.Status}, Documento={payload.DocumentNumber}";
        _logger.LogInformation("{SanitizedLog}", _sanitizer.Sanitize(logMessage));

        var topic = $"providers.webhooks.{providerName.ToLowerInvariant()}";
        await _messageBus.PublishAsync(topic, payload, cancellationToken);

        var response = new ProviderWebhookResponse(
            Acknowledged: true,
            Message: $"Webhook do provedor {providerName} aceito e enfileirado com sucesso para processamento assíncrono.",
            IngestionReceiptId: receiptId,
            ProcessedAt: DateTime.UtcNow);

        return Accepted(response);
    }
}

