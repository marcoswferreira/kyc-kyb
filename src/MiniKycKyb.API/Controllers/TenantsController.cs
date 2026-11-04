using Application.Dtos;
using Application.UseCases.Tenants;
using Domain.Entities;
using Domain.Repositories;
using Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace MiniKycKyb.API.Controllers;

[ApiController]
[Route("api/v1/tenants")]
public class TenantsController : ControllerBase
{
    private readonly CreateTenantUseCase _createTenantUseCase;
    private readonly GenerateApiKeyUseCase _generateApiKeyUseCase;
    private readonly IWebhookSubscriptionRepository _webhookSubscriptionRepository;
    private readonly CurrentTenantContext _currentTenant;

    public TenantsController(
        CreateTenantUseCase createTenantUseCase,
        GenerateApiKeyUseCase generateApiKeyUseCase,
        IWebhookSubscriptionRepository webhookSubscriptionRepository,
        CurrentTenantContext currentTenant)
    {
        _createTenantUseCase = createTenantUseCase;
        _generateApiKeyUseCase = generateApiKeyUseCase;
        _webhookSubscriptionRepository = webhookSubscriptionRepository;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Cadastra um novo tenant no SaaS e gera a chave de API inicial.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        var result = await _createTenantUseCase.ExecuteAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error, code = result.ErrorCode });
        }

        return Created($"/api/v1/tenants/{result.Value.Response.Id}", new
        {
            tenant = result.Value.Response,
            api_key = result.Value.ApiKey
        });
    }

    /// <summary>
    /// Rotaciona ou gera uma nova chave de API para o tenant.
    /// </summary>
    [HttpPost("{id:guid}/api-keys")]
    public async Task<IActionResult> GenerateApiKey(Guid id, [FromQuery] string environment = "live", CancellationToken cancellationToken = default)
    {
        var result = await _generateApiKeyUseCase.ExecuteAsync(id, environment, cancellationToken);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Cadastra uma assinatura de webhook para notificações de eventos (ex: check.completed).
    /// </summary>
    [HttpPost("webhooks")]
    public async Task<IActionResult> RegisterWebhook(
        [FromBody] RegisterWebhookRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Unauthorized(new { error = "Tenant não autenticado." });
        }

        var secret = Guid.NewGuid().ToString("N");
        var subscription = WebhookSubscription.Create(
            tenantId,
            request.TargetUrl,
            secret,
            request.Events ?? "check.completed,check.failed");

        await _webhookSubscriptionRepository.AddAsync(subscription, cancellationToken);

        return Ok(new
        {
            id = subscription.Id,
            tenant_id = tenantId,
            target_url = subscription.TargetUrl,
            secret,
            events = subscription.Events,
            message = "Webhook cadastrado com sucesso. Guarde o segredo (secret) para validar assinaturas HMAC-SHA256."
        });
    }
}

