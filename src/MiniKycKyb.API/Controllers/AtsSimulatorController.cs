using System.Collections.Concurrent;
using Application.Dtos;
using Application.UseCases.Checks;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Infrastructure.BackgroundWorkers;
using Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace MiniKycKyb.API.Controllers;

[ApiController]
[Route("api/v1/ats")]
public class AtsSimulatorController : ControllerBase
{
    private readonly CreateCheckUseCase _createCheckUseCase;
    private readonly ICheckQueue _checkQueue;
    private readonly IWebhookSubscriptionRepository _webhookSubscriptionRepository;
    private readonly CurrentTenantContext _currentTenant;

    // Histórico de webhooks recebidos pelo ATS simulado
    private static readonly ConcurrentBag<AtsReceivedEvent> ReceivedCallbacks = new();

    public AtsSimulatorController(
        CreateCheckUseCase createCheckUseCase,
        ICheckQueue checkQueue,
        IWebhookSubscriptionRepository webhookSubscriptionRepository,
        CurrentTenantContext currentTenant)
    {
        _createCheckUseCase = createCheckUseCase;
        _checkQueue = checkQueue;
        _webhookSubscriptionRepository = webhookSubscriptionRepository;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Simulação de ATS: Candidato avança de etapa no processo seletivo (ex: Triagem -> Checagem de Antecedentes),
    /// disparando automaticamente a verificação de KYC na plataforma.
    /// </summary>
    [HttpPost("trigger-candidate-stage")]
    public async Task<IActionResult> TriggerCandidateStage([FromBody] AtsTriggerRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId ?? Guid.Empty;

        // Auto-registra o webhook de retorno para o ATS se não existir
        var activeSubscriptions = await _webhookSubscriptionRepository.GetActiveByTenantAsync(tenantId, cancellationToken);
        if (!activeSubscriptions.Any(s => s.TargetUrl == request.CallbackUrl))
        {
            var sub = WebhookSubscription.Create(
                tenantId,
                request.CallbackUrl,
                "ats_secret_hmac_12345",
                "check.completed");
            await _webhookSubscriptionRepository.AddAsync(sub, cancellationToken);
        }

        var checkRequest = new CreateCheckRequest(
            Document: request.Document,
            SubjectType: SubjectType.Person,
            Name: request.CandidateName,
            Purpose: CheckPurpose.RecrutamentoESelecao,
            LegalBasis: LegalBasis.Consentimento,
            ConsentObtained: true,
            ConsentTimestamp: DateTime.UtcNow,
            WebhookUrl: request.CallbackUrl,
            CustomReference: $"ATS-STAGE-{request.StageName}");

        var result = await _createCheckUseCase.ExecuteAsync(checkRequest, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error, code = result.ErrorCode });
        }

        await _checkQueue.EnqueueAsync(result.Value!.Id, cancellationToken);

        return Ok(new
        {
            ats_status = "STAGE_UPDATED_TO_BACKGROUND_CHECK",
            candidate_name = request.CandidateName,
            stage = request.StageName,
            job_title = request.JobTitle,
            check_id = result.Value.Id,
            document_masked = result.Value.DocumentMasked,
            message = "Candidato avançou de etapa no ATS. Verificação de background check disparada de forma assíncrona."
        });
    }

    /// <summary>
    /// Endpoint receptor do ATS simulado para receber os webhooks assinados enviados pela plataforma de Background Check.
    /// </summary>
    [HttpPost("callback")]
    public IActionResult ReceiveWebhookCallback(
        [FromBody] AtsWebhookPayload payload,
        [FromHeader(Name = "X-Signature-SHA256")] string? signature)
    {
        ReceivedCallbacks.Add(new AtsReceivedEvent(
            payload.Event,
            payload.CheckId,
            payload.CandidateDocumentMasked,
            payload.OverallRisk,
            payload.HumanReviewStatus,
            payload.Status,
            payload.AiSummary,
            DateTime.UtcNow));

        return Ok(new
        {
            ats_status = "RECEIVED_AND_PROCESSED",
            check_id = payload.CheckId,
            candidate_risk = payload.OverallRisk.ToString(),
            next_action = "Aguardando Revisão Humana de Compliance para liberação de proposta de contratação.",
            signature_present = !string.IsNullOrWhiteSpace(signature)
        });
    }

    /// <summary>
    /// Lista os webhooks recebidos pelo ATS simulado para fins de validação e testes.
    /// </summary>
    [HttpGet("received-callbacks")]
    public IActionResult GetReceivedCallbacks()
    {
        return Ok(ReceivedCallbacks.ToArray());
    }
}

