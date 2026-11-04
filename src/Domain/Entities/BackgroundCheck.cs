using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class BackgroundCheck : BaseEntity, IAggregateRoot
{
    public Guid TenantId { get; private set; }
    public SubjectType SubjectType { get; private set; }
    public string DocumentNumber { get; private set; } = string.Empty;
    public string DocumentFormatted { get; private set; } = string.Empty;
    public string DocumentMasked { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public CheckPurpose Purpose { get; private set; }
    public LegalBasis LegalBasis { get; private set; }
    public bool ConsentObtained { get; private set; }
    public DateTime? ConsentTimestamp { get; private set; }
    public CheckStatus Status { get; private set; } = CheckStatus.Pending;
    public RiskIndicator OverallRisk { get; private set; } = RiskIndicator.Green;
    
    // LGPD Art. 20: Decisão Assistida (Human-in-the-Loop)
    public HumanReviewDecision HumanReviewStatus { get; private set; } = HumanReviewDecision.AwaitingReview;
    public string? HumanReviewerId { get; private set; }
    public string? HumanReviewNotes { get; private set; }
    public DateTime? HumanReviewedAt { get; private set; }

    // LGPD Retenção e Expurgo
    public DateTime RetentionUntil { get; private set; }
    public bool IsPurged { get; private set; }
    public DateTime? PurgedAt { get; private set; }

    // Evidência Bruta com Integridade Criptográfica
    public string? RawEvidenceJson { get; private set; }
    public string? EvidenceSha256 { get; private set; }
    public string? AiSummary { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private readonly List<CheckFinding> _findings = [];
    public IReadOnlyCollection<CheckFinding> Findings => _findings.AsReadOnly();

    private BackgroundCheck() { } // EF Core

    public BackgroundCheck(
        Guid id,
        Guid tenantId,
        SubjectType subjectType,
        string documentNumber,
        string documentFormatted,
        string documentMasked,
        string name,
        CheckPurpose purpose,
        LegalBasis legalBasis,
        bool consentObtained,
        DateTime? consentTimestamp,
        int retentionDays = 30)
    {
        Id = id;
        TenantId = tenantId;
        SubjectType = subjectType;
        DocumentNumber = documentNumber;
        DocumentFormatted = documentFormatted;
        DocumentMasked = documentMasked;
        Name = name;
        Purpose = purpose;
        LegalBasis = legalBasis;
        ConsentObtained = consentObtained;
        ConsentTimestamp = consentTimestamp ?? (consentObtained ? DateTime.UtcNow : null);
        Status = CheckStatus.Pending;
        OverallRisk = RiskIndicator.Green;
        HumanReviewStatus = HumanReviewDecision.AwaitingReview;
        CreatedAt = DateTime.UtcNow;
        RetentionUntil = DateTime.UtcNow.AddDays(retentionDays);
        IsPurged = false;
    }

    public static BackgroundCheck Create(
        Guid tenantId,
        SubjectType subjectType,
        string documentNumber,
        string documentFormatted,
        string documentMasked,
        string name,
        CheckPurpose purpose,
        LegalBasis legalBasis,
        bool consentObtained,
        DateTime? consentTimestamp = null,
        int retentionDays = 30)
    {
        return new BackgroundCheck(
            Guid.NewGuid(),
            tenantId,
            subjectType,
            documentNumber,
            documentFormatted,
            documentMasked,
            name,
            purpose,
            legalBasis,
            consentObtained,
            consentTimestamp,
            retentionDays);
    }

    public void StartProcessing()
    {
        if (Status != CheckStatus.Pending)
        {
            return;
        }

        Status = CheckStatus.Processing;
        SetUpdatedAt(DateTime.UtcNow);
    }

    public void Complete(
        RiskIndicator overallRisk,
        IEnumerable<CheckFinding> findings,
        string rawEvidenceJson,
        string evidenceSha256,
        bool hasPartialWarnings = false,
        string? aiSummary = null)
    {
        OverallRisk = overallRisk;
        _findings.Clear();
        _findings.AddRange(findings);
        RawEvidenceJson = rawEvidenceJson;
        EvidenceSha256 = evidenceSha256;
        AiSummary = aiSummary;
        Status = hasPartialWarnings ? CheckStatus.CompletedWithWarnings : CheckStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        SetUpdatedAt(DateTime.UtcNow);
    }

    public void MarkAsFailed(string reason)
    {
        Status = CheckStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        SetUpdatedAt(DateTime.UtcNow);
    }

    /// <summary>
    /// LGPD Art. 20: Registro da decisão deliberada por analista humano.
    /// O sistema nunca reprova sozinho; a decisão final é de competência humana.
    /// </summary>
    public Result RecordHumanReview(string reviewerId, HumanReviewDecision decision, string notes)
    {
        if (string.IsNullOrWhiteSpace(reviewerId))
        {
            return Result.Failure("Identificação do revisor humano é obrigatória.", "LGPD_REVIEWER_REQUIRED");
        }

        if (decision == HumanReviewDecision.AwaitingReview)
        {
            return Result.Failure("A decisão de revisão deve ser conclusiva (Aprovado, Reprovado ou Informações Adicionais).", "LGPD_INVALID_DECISION");
        }

        if (string.IsNullOrWhiteSpace(notes))
        {
            return Result.Failure("A fundamentação/justificativa da revisão humana é obrigatória sob a LGPD.", "LGPD_JUSTIFICATION_REQUIRED");
        }

        HumanReviewStatus = decision;
        HumanReviewerId = reviewerId;
        HumanReviewNotes = notes;
        HumanReviewedAt = DateTime.UtcNow;
        SetUpdatedAt(DateTime.UtcNow);

        return Result.Success();
    }

    /// <summary>
    /// LGPD: Expurgo de dados pessoais e evidências após vencimento do prazo de retenção.
    /// </summary>
    public void PurgePersonalData()
    {
        if (IsPurged)
        {
            return;
        }

        Name = "[EXPURGADO_LGPD]";
        DocumentNumber = "[EXPURGADO]";
        DocumentFormatted = "[EXPURGADO]";
        RawEvidenceJson = null;
        AiSummary = "[CONTEÚDO EXPURGADO CONFORME POLÍTICA DE RETENÇÃO LGPD]";
        IsPurged = true;
        PurgedAt = DateTime.UtcNow;
        SetUpdatedAt(DateTime.UtcNow);
    }
}
