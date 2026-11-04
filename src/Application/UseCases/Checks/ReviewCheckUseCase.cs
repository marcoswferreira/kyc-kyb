using Application.Dtos;
using Application.Interfaces;
using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;

namespace Application.UseCases.Checks;

public class ReviewCheckUseCase
{
    private readonly IBackgroundCheckRepository _checkRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IWebhookDispatcher _webhookDispatcher;
    private readonly ICurrentTenantContext _currentTenant;

    public ReviewCheckUseCase(
        IBackgroundCheckRepository checkRepository,
        IAuditLogRepository auditLogRepository,
        IWebhookDispatcher webhookDispatcher,
        ICurrentTenantContext currentTenant)
    {
        _checkRepository = checkRepository;
        _auditLogRepository = auditLogRepository;
        _webhookDispatcher = webhookDispatcher;
        _currentTenant = currentTenant;
    }

    public async Task<Result<ReviewCheckResponse>> ExecuteAsync(
        Guid checkId,
        ReviewCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        var check = await _checkRepository.GetByIdAsync(checkId, cancellationToken);
        if (check is null)
        {
            return Result<ReviewCheckResponse>.Failure("Verificação não encontrada.", "NOT_FOUND");
        }

        if (_currentTenant.TenantId.HasValue && check.TenantId != _currentTenant.TenantId.Value)
        {
            return Result<ReviewCheckResponse>.Failure("Acesso negado a este recurso.", "FORBIDDEN");
        }

        // LGPD Art. 20: Registro deliberado da decisão assistida por oficial humano
        var reviewResult = check.RecordHumanReview(request.ReviewerId, request.Decision, request.Notes);
        if (reviewResult.IsFailure)
        {
            return Result<ReviewCheckResponse>.Failure(reviewResult.Error!, reviewResult.ErrorCode);
        }

        await _checkRepository.UpdateAsync(check, cancellationToken);

        // Registro de Auditoria Imutável
        var latestAudit = await _auditLogRepository.GetLatestEntryAsync(check.TenantId, cancellationToken);
        var previousHash = latestAudit?.Hash ?? "0000000000000000000000000000000000000000000000000000000000000000";

        var audit = AuditLogEntry.Create(
            check.TenantId,
            request.ReviewerId,
            "HUMAN_REVIEW_RECORDED",
            check.Id.ToString(),
            $"Revisão humana registrada: {request.Decision}. Justificativa: {request.Notes}",
            _currentTenant.IpAddress ?? "127.0.0.1",
            previousHash);

        await _auditLogRepository.AddAsync(audit, cancellationToken);

        // Dispara notificação via webhook (ex: para atualizar o ATS integrado)
        await _webhookDispatcher.DispatchAsync(check, cancellationToken);

        return Result<ReviewCheckResponse>.Success(new ReviewCheckResponse(
            check.Id,
            check.HumanReviewStatus,
            check.HumanReviewerId!,
            check.HumanReviewedAt!.Value,
            "Revisão humana registrada com sucesso e em conformidade com o Art. 20 da LGPD."));
    }
}
