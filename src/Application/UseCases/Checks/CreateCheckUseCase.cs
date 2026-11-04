using Application.Dtos;
using Application.Interfaces;
using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Domain.ValueObjects;

namespace Application.UseCases.Checks;

public class CreateCheckUseCase
{
    private readonly IBackgroundCheckRepository _checkRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICurrentTenantContext _currentTenant;

    public CreateCheckUseCase(
        IBackgroundCheckRepository checkRepository,
        IAuditLogRepository auditLogRepository,
        ICurrentTenantContext currentTenant)
    {
        _checkRepository = checkRepository;
        _auditLogRepository = auditLogRepository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<CreateCheckResponse>> ExecuteAsync(
        CreateCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Result<CreateCheckResponse>.Failure("Tenant não identificado.", "TENANT_REQUIRED");
        }

        // Validação estrita de consentimento sob a LGPD
        if (!request.ConsentObtained)
        {
            return Result<CreateCheckResponse>.Failure(
                "O consentimento ou confirmação de base legal é obrigatório para prosseguir com a checagem (LGPD Art. 7º/8º).",
                "LGPD_CONSENT_REQUIRED");
        }

        string rawDocument;
        string formattedDocument;
        string maskedDocument;

        if (request.SubjectType == SubjectType.Person)
        {
            var cpfResult = Cpf.Create(request.Document);
            if (cpfResult.IsFailure)
            {
                return Result<CreateCheckResponse>.Failure(cpfResult.Error!, cpfResult.ErrorCode);
            }

            rawDocument = cpfResult.Value!.Value;
            formattedDocument = cpfResult.Value.Formatted;
            maskedDocument = cpfResult.Value.Masked;
        }
        else
        {
            var cnpjResult = Cnpj.Create(request.Document);
            if (cnpjResult.IsFailure)
            {
                return Result<CreateCheckResponse>.Failure(cnpjResult.Error!, cnpjResult.ErrorCode);
            }

            rawDocument = cnpjResult.Value!.Value;
            formattedDocument = cnpjResult.Value.Formatted;
            maskedDocument = cnpjResult.Value.Masked;
        }

        var check = BackgroundCheck.Create(
            tenantId,
            request.SubjectType,
            rawDocument,
            formattedDocument,
            maskedDocument,
            request.Name,
            request.Purpose,
            request.LegalBasis,
            request.ConsentObtained,
            request.ConsentTimestamp,
            request.RetentionDays);

        await _checkRepository.AddAsync(check, cancellationToken);

        // Registro de Auditoria Imutável (LGPD)
        var latestAudit = await _auditLogRepository.GetLatestEntryAsync(tenantId, cancellationToken);
        var previousHash = latestAudit?.Hash ?? "0000000000000000000000000000000000000000000000000000000000000000";

        var audit = AuditLogEntry.Create(
            tenantId,
            _currentTenant.ActorId ?? "system",
            "CHECK_CREATED",
            check.Id.ToString(),
            $"Checagem criada para {request.SubjectType} com finalidade {request.Purpose}.",
            _currentTenant.IpAddress ?? "127.0.0.1",
            previousHash);

        await _auditLogRepository.AddAsync(audit, cancellationToken);

        return Result<CreateCheckResponse>.Success(new CreateCheckResponse(
            check.Id,
            tenantId,
            maskedDocument,
            check.SubjectType,
            check.Status,
            check.CreatedAt,
            "Solicitação de background check registrada com sucesso na fila assíncrona."));
    }
}

