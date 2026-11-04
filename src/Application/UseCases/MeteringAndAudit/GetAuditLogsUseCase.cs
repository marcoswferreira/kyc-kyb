using Application.Dtos;
using Domain.Repositories;

namespace Application.UseCases.MeteringAndAudit;

public class GetAuditLogsUseCase
{
    private readonly IAuditLogRepository _auditLogRepository;

    public GetAuditLogsUseCase(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<IReadOnlyList<AuditLogDto>> ExecuteAsync(
        Guid tenantId,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var entries = await _auditLogRepository.ListByTenantAsync(tenantId, skip, take, cancellationToken);

        return entries.Select(e => new AuditLogDto(
            e.Id,
            e.TenantId,
            e.ActorId,
            e.Action,
            e.ResourceId,
            e.Details,
            e.IpAddress,
            e.Timestamp,
            e.PreviousHash,
            e.Hash,
            e.VerifyIntegrity())).ToList();
    }
}

