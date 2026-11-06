using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.Repositories;

public class BackgroundCheckRepository : IBackgroundCheckRepository
{
    private readonly KycKybDbContext _context;

    public BackgroundCheckRepository(KycKybDbContext context)
    {
        _context = context;
    }

    public async Task<BackgroundCheck?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.BackgroundChecks
            .Include(b => b.Findings)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<BackgroundCheck?> GetByDocumentAndTenantAsync(Guid tenantId, string documentNumber, CancellationToken cancellationToken = default)
    {
        return await _context.BackgroundChecks
            .Include(b => b.Findings)
            .Where(b => b.TenantId == tenantId && b.DocumentNumber == documentNumber)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BackgroundCheck>> ListByTenantAsync(Guid tenantId, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        return await _context.BackgroundChecks
            .Include(b => b.Findings)
            .Where(b => b.TenantId == tenantId)
            .OrderByDescending(b => b.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BackgroundCheck>> GetPendingChecksAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        return await _context.BackgroundChecks
            .Include(b => b.Findings)
            .Where(b => b.Status == Domain.Enums.CheckStatus.Pending)
            .OrderBy(b => b.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BackgroundCheck>> GetExpiredChecksForPurgeAsync(DateTime now, int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _context.BackgroundChecks
            .Where(b => !b.IsPurged && b.RetentionUntil <= now)
            .OrderBy(b => b.RetentionUntil)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BackgroundCheck check, CancellationToken cancellationToken = default)
    {
        await _context.BackgroundChecks.AddAsync(check, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(BackgroundCheck check, CancellationToken cancellationToken = default)
    {
        var entry = _context.Entry(check);
        if (entry.State == EntityState.Detached)
        {
            _context.BackgroundChecks.Attach(check);
            entry.State = EntityState.Modified;
        }

        // Remove do ChangeTracker findings que foram removidos do agregado
        var currentFindingIds = check.Findings.Select(f => f.Id).ToHashSet();
        var trackedFindings = _context.CheckFindings.Local
            .Where(f => f.BackgroundCheckId == check.Id)
            .ToList();

        foreach (var tracked in trackedFindings)
        {
            if (!currentFindingIds.Contains(tracked.Id))
            {
                _context.CheckFindings.Remove(tracked);
            }
        }

        // Garante que findings novos com GUID gerado pelo cliente sejam marcados como Added
        foreach (var finding in check.Findings)
        {
            var findingEntry = _context.Entry(finding);
            if (findingEntry.State == EntityState.Detached || findingEntry.State == EntityState.Modified)
            {
                var existsInDb = await _context.CheckFindings.AnyAsync(f => f.Id == finding.Id, cancellationToken);
                findingEntry.State = existsInDb ? EntityState.Modified : EntityState.Added;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}

