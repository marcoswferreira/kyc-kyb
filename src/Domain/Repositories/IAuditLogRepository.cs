using Domain.Entities;

namespace Domain.Repositories;

public interface IAuditLogRepository
{
    Task<AuditLogEntry?> GetLatestEntryAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntry>> ListByTenantAsync(Guid tenantId, int skip = 0, int take = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntry>> ListByResourceAsync(string resourceId, CancellationToken cancellationToken = default);
    Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}

