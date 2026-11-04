using Domain.Entities;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Checks;

public class PurgeExpiredChecksUseCase
{
    private readonly IBackgroundCheckRepository _checkRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<PurgeExpiredChecksUseCase> _logger;

    public PurgeExpiredChecksUseCase(
        IBackgroundCheckRepository checkRepository,
        IAuditLogRepository auditLogRepository,
        ILogger<PurgeExpiredChecksUseCase> logger)
    {
        _checkRepository = checkRepository;
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiredChecks = await _checkRepository.GetExpiredChecksForPurgeAsync(now, limit: 100, cancellationToken);

        if (!expiredChecks.Any())
        {
            return 0;
        }

        int purgedCount = 0;

        foreach (var check in expiredChecks)
        {
            check.PurgePersonalData();
            await _checkRepository.UpdateAsync(check, cancellationToken);

            var latestAudit = await _auditLogRepository.GetLatestEntryAsync(check.TenantId, cancellationToken);
            var previousHash = latestAudit?.Hash ?? "0000000000000000000000000000000000000000000000000000000000000000";

            var audit = AuditLogEntry.Create(
                check.TenantId,
                "system_retention_worker",
                "DATA_PURGED_LGPD",
                check.Id.ToString(),
                $"Expurgo automático de dados pessoais e evidências executado. Retenção expirada em {check.RetentionUntil:O}.",
                "127.0.0.1",
                previousHash);

            await _auditLogRepository.AddAsync(audit, cancellationToken);
            purgedCount++;
        }

        _logger.LogInformation("Job de retenção LGPD expurgou {Count} checagens expiradas com sucesso.", purgedCount);
        return purgedCount;
    }
}

