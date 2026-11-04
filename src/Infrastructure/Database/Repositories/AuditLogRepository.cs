using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly KycKybDbContext _context;

    public AuditLogRepository(KycKybDbContext context)
    {
        _context = context;
    }

    public async Task<AuditLogEntry?> GetLatestEntryAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogEntries
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> ListByTenantAsync(Guid tenantId, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogEntries
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> ListByResourceAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogEntries
            .Where(a => a.ResourceId == resourceId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        await _context.AuditLogEntries.AddAsync(entry, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

