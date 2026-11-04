using Domain.Entities;

namespace Domain.Repositories;

public interface IBackgroundCheckRepository
{
    Task<BackgroundCheck?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BackgroundCheck?> GetByDocumentAndTenantAsync(Guid tenantId, string documentNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackgroundCheck>> ListByTenantAsync(Guid tenantId, int skip = 0, int take = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackgroundCheck>> GetPendingChecksAsync(int limit = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackgroundCheck>> GetExpiredChecksForPurgeAsync(DateTime now, int limit = 100, CancellationToken cancellationToken = default);
    Task AddAsync(BackgroundCheck check, CancellationToken cancellationToken = default);
    Task UpdateAsync(BackgroundCheck check, CancellationToken cancellationToken = default);
}

