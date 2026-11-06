using System.Text.Json;
using Domain.Entities;
using Domain.Events;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Audit;

/// <summary>
/// Despachador de eventos de domínio que persiste cada evento na trilha de auditoria encadeada com hash SHA-256 (Audit Trail).
/// Atende às normas de conformidade Bacen / CVM e aos artigos 18 e 20 da LGPD.
/// </summary>
public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IAuditLogRepository _auditRepository;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(
        IAuditLogRepository auditRepository,
        ILogger<DomainEventDispatcher> logger)
    {
        _auditRepository = auditRepository;
        _logger = logger;
    }

    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var (tenantId, checkId, actorId) = ExtractMetadata(domainEvent);
            var latest = await _auditRepository.GetLatestEntryAsync(tenantId, cancellationToken);
            var previousHash = latest?.Hash ?? "GENESIS_BLOCK_000000000000000000000000000000000000000000000000000000000000";

            var detailsJson = JsonSerializer.Serialize((object)domainEvent);
            var actionName = domainEvent.GetType().Name;

            var entry = AuditLogEntry.Create(
                tenantId,
                actorId,
                $"DOMAIN_EVENT_{actionName.ToUpperInvariant()}",
                checkId.ToString(),
                detailsJson,
                "127.0.0.1",
                previousHash);

            await _auditRepository.AddAsync(entry, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao persistir evento de domínio {EventType} na trilha de auditoria.", domainEvent.GetType().Name);
        }
    }

    public async Task DispatchAllAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var ev in domainEvents)
        {
            await DispatchAsync(ev, cancellationToken);
        }
    }

    private static (Guid tenantId, Guid checkId, string actorId) ExtractMetadata(IDomainEvent domainEvent)
    {
        return domainEvent switch
        {
            CheckCreatedEvent e => (e.TenantId, e.CheckId, "system:check-creator"),
            CheckProcessingStartedEvent e => (e.TenantId, e.CheckId, "system:orchestrator"),
            CheckCompletedAwaitingReviewEvent e => (e.TenantId, e.CheckId, "system:policy-engine"),
            HumanReviewDeliberatedEvent e => (e.TenantId, e.CheckId, $"analyst:{e.ReviewerId}"),
            DataRetentionPurgedEvent e => (e.TenantId, e.CheckId, "system:retention-worker"),
            _ => (Guid.Empty, Guid.Empty, "system:unknown")
        };
    }
}

