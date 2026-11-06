namespace Domain.Events;

/// <summary>
/// Contrato base para eventos de domínio disparados no ciclo de vida de Background Checks.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
}

