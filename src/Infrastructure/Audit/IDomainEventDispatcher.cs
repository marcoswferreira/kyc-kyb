using Domain.Events;

namespace Infrastructure.Audit;

/// <summary>
/// Despachador de eventos de domínio para gravação imediata na trilha de auditoria imutável (Audit Trail).
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
    Task DispatchAllAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
