using Domain.Entities;
using Domain.Enums;

namespace Application.Interfaces;

public record PolicyEvaluationResult(
    RiskIndicator OverallRisk,
    IReadOnlyList<CheckFinding> Findings);

