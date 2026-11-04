using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class CheckFinding : BaseEntity
{
    public Guid BackgroundCheckId { get; private set; }
    public string ProviderName { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public RiskIndicator RiskLevel { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string ExplanationWhy { get; private set; } = string.Empty;
    public string EvidenceKey { get; private set; } = string.Empty;
    public string RawExcerpt { get; private set; } = string.Empty;

    private CheckFinding() { } // EF Core

    public CheckFinding(
        Guid id,
        Guid backgroundCheckId,
        string providerName,
        string category,
        RiskIndicator riskLevel,
        string title,
        string description,
        string explanationWhy,
        string evidenceKey,
        string rawExcerpt)
    {
        Id = id;
        BackgroundCheckId = backgroundCheckId;
        ProviderName = providerName;
        Category = category;
        RiskLevel = riskLevel;
        Title = title;
        Description = description;
        ExplanationWhy = explanationWhy;
        EvidenceKey = evidenceKey;
        RawExcerpt = rawExcerpt;
        CreatedAt = DateTime.UtcNow;
    }

    public static CheckFinding Create(
        Guid backgroundCheckId,
        string providerName,
        string category,
        RiskIndicator riskLevel,
        string title,
        string description,
        string explanationWhy,
        string evidenceKey,
        string rawExcerpt)
    {
        return new CheckFinding(
            Guid.NewGuid(),
            backgroundCheckId,
            providerName,
            category,
            riskLevel,
            title,
            description,
            explanationWhy,
            evidenceKey,
            rawExcerpt);
    }
}
