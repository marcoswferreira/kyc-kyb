using Domain.Entities;

namespace Application.Interfaces;

public interface IAiAssistantService
{
    Task<string> GenerateReportSummaryAsync(
        BackgroundCheck check,
        IReadOnlyList<CheckFinding> findings,
        CancellationToken cancellationToken = default);
}

