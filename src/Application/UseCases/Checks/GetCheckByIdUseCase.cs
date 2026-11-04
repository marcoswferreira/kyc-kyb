using Application.Dtos;
using Application.Interfaces;
using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;

namespace Application.UseCases.Checks;

public class GetCheckByIdUseCase
{
    private readonly IBackgroundCheckRepository _checkRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICurrentTenantContext _currentTenant;

    public GetCheckByIdUseCase(
        IBackgroundCheckRepository checkRepository,
        IAuditLogRepository auditLogRepository,
        ICurrentTenantContext currentTenant)
    {
        _checkRepository = checkRepository;
        _auditLogRepository = auditLogRepository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<CheckDetailsResponse>> ExecuteAsync(
        Guid checkId,
        CancellationToken cancellationToken = default)
    {
        var check = await _checkRepository.GetByIdAsync(checkId, cancellationToken);
        if (check is null)
        {
            return Result<CheckDetailsResponse>.Failure("Verificação de background check não encontrada.", "NOT_FOUND");
        }

        // Multi-tenant check
        if (_currentTenant.TenantId.HasValue && check.TenantId != _currentTenant.TenantId.Value)
        {
            return Result<CheckDetailsResponse>.Failure("Acesso negado a este recurso.", "FORBIDDEN");
        }

        // LGPD: Minimização e mascaramento por perfil (RBAC)
        bool canViewUnmasked = _currentTenant.Role is UserRole.Admin or UserRole.ComplianceOfficer;

        var documentToReturn = canViewUnmasked ? check.DocumentFormatted : check.DocumentMasked;
        var nameToReturn = check.IsPurged ? "[EXPURGADO_LGPD]" : check.Name;

        var findingsDtos = check.Findings.Select(f => new CheckFindingDto(
            f.Id,
            f.ProviderName,
            f.Category,
            f.RiskLevel,
            f.Title,
            f.Description,
            f.ExplanationWhy,
            f.EvidenceKey,
            f.RawExcerpt,
            f.CreatedAt)).ToList();

        // Registra acesso na auditoria se visualizado por humano
        if (!string.IsNullOrWhiteSpace(_currentTenant.ActorId))
        {
            var latestAudit = await _auditLogRepository.GetLatestEntryAsync(check.TenantId, cancellationToken);
            var previousHash = latestAudit?.Hash ?? "0000000000000000000000000000000000000000000000000000000000000000";

            var audit = AuditLogEntry.Create(
                check.TenantId,
                _currentTenant.ActorId,
                canViewUnmasked ? "CHECK_VIEWED_UNMASKED" : "CHECK_VIEWED_MASKED",
                check.Id.ToString(),
                $"Relatório consultado com papel {_currentTenant.Role}.",
                _currentTenant.IpAddress ?? "127.0.0.1",
                previousHash);

            await _auditLogRepository.AddAsync(audit, cancellationToken);
        }

        var response = new CheckDetailsResponse(
            check.Id,
            check.TenantId,
            check.SubjectType,
            canViewUnmasked ? check.DocumentNumber : check.DocumentMasked,
            documentToReturn,
            check.DocumentMasked,
            nameToReturn,
            check.Purpose,
            check.LegalBasis,
            check.ConsentObtained,
            check.ConsentTimestamp,
            check.Status,
            check.OverallRisk,
            check.HumanReviewStatus,
            check.HumanReviewerId,
            check.HumanReviewNotes,
            check.HumanReviewedAt,
            check.RetentionUntil,
            check.IsPurged,
            check.EvidenceSha256,
            check.AiSummary,
            check.CreatedAt,
            check.CompletedAt,
            findingsDtos);

        return Result<CheckDetailsResponse>.Success(response);
    }
}
