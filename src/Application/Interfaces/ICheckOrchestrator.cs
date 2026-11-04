using Domain.Enums;

namespace Application.Interfaces;

public interface ICheckOrchestrator
{
    Task<OrchestratorResult> ExecuteAllProvidersAsync(
        string documentNumber,
        SubjectType subjectType,
        CancellationToken cancellationToken = default);
}

