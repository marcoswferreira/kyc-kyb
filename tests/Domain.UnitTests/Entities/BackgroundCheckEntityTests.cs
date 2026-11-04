using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Domain.UnitTests.Entities;

public class BackgroundCheckEntityTests
{
    [Fact]
    public void Create_ShouldInitializeWithPendingAndAwaitingReviewStatus()
    {
        var tenantId = Guid.NewGuid();
        var check = BackgroundCheck.Create(
            tenantId,
            SubjectType.Person,
            "52998224725",
            "529.982.247-25",
            "***.982.247-**",
            "Candidato Sintético Teste",
            CheckPurpose.RecrutamentoESelecao,
            LegalBasis.Consentimento,
            consentObtained: true);

        check.TenantId.Should().Be(tenantId);
        check.Status.Should().Be(CheckStatus.Pending);
        check.OverallRisk.Should().Be(RiskIndicator.Green);
        check.HumanReviewStatus.Should().Be(HumanReviewDecision.AwaitingReview);
        check.ConsentObtained.Should().BeTrue();
        check.ConsentTimestamp.Should().NotBeNull();
        check.RetentionUntil.Should().BeAfter(DateTime.UtcNow);
        check.IsPurged.Should().BeFalse();
    }

    [Fact]
    public void StartProcessing_ShouldTransitionToProcessing()
    {
        var check = BackgroundCheck.Create(
            Guid.NewGuid(),
            SubjectType.Person,
            "52998224725",
            "529.982.247-25",
            "***.982.247-**",
            "Candidato",
            CheckPurpose.RecrutamentoESelecao,
            LegalBasis.Consentimento,
            true);

        check.StartProcessing();

        check.Status.Should().Be(CheckStatus.Processing);
    }

    [Fact]
    public void Complete_ShouldStoreFindingsAndEvidenceSha256()
    {
        var check = BackgroundCheck.Create(
            Guid.NewGuid(),
            SubjectType.Person,
            "52998224725",
            "529.982.247-25",
            "***.982.247-**",
            "Candidato",
            CheckPurpose.RecrutamentoESelecao,
            LegalBasis.Consentimento,
            true);

        check.StartProcessing();

        var finding = CheckFinding.Create(
            check.Id,
            "TribunaisMockProvider",
            "PROCESSOS_JUDICIAIS",
            RiskIndicator.Yellow,
            "Ação Trabalhista Arquivada",
            "Processo cível/trabalhista extinto sem condenação",
            "Processo nº 1002345-88 arquivado com quitação plena",
            "evidence_key_123",
            "{\"processo\": \"1002345-88\", \"status\": \"arquivado\"}");

        check.Complete(
            RiskIndicator.Yellow,
            [finding],
            "{\"raw\": true}",
            "hash_sha256_mock",
            hasPartialWarnings: false,
            aiSummary: "Resumo preliminar");

        check.Status.Should().Be(CheckStatus.Completed);
        check.OverallRisk.Should().Be(RiskIndicator.Yellow);
        check.Findings.Should().HaveCount(1);
        check.EvidenceSha256.Should().Be("hash_sha256_mock");
        check.AiSummary.Should().Be("Resumo preliminar");
        check.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordHumanReview_UnderLgpdArt20_ShouldEnforceReviewerAndNotes()
    {
        var check = BackgroundCheck.Create(
            Guid.NewGuid(),
            SubjectType.Person,
            "52998224725",
            "529.982.247-25",
            "***.982.247-**",
            "Candidato",
            CheckPurpose.RecrutamentoESelecao,
            LegalBasis.Consentimento,
            true);

        // Sem identificador do revisor -> Deve falhar
        var fail1 = check.RecordHumanReview("", HumanReviewDecision.ApprovedByHuman, "Validado pelo RH");
        fail1.IsFailure.Should().BeTrue();
        fail1.ErrorCode.Should().Be("LGPD_REVIEWER_REQUIRED");

        // Sem justificativa -> Deve falhar
        var fail2 = check.RecordHumanReview("officer-01", HumanReviewDecision.ApprovedByHuman, "");
        fail2.IsFailure.Should().BeTrue();
        fail2.ErrorCode.Should().Be("LGPD_JUSTIFICATION_REQUIRED");

        // Decisão inconclusiva (AwaitingReview) -> Deve falhar
        var fail3 = check.RecordHumanReview("officer-01", HumanReviewDecision.AwaitingReview, "Notas");
        fail3.IsFailure.Should().BeTrue();
        fail3.ErrorCode.Should().Be("LGPD_INVALID_DECISION");

        // Decisão válida deliberada por oficial humano
        var success = check.RecordHumanReview("officer-01", HumanReviewDecision.ApprovedByHuman, "Processo arquivado verificado sem prejuízo.");
        success.IsSuccess.Should().BeTrue();
        check.HumanReviewStatus.Should().Be(HumanReviewDecision.ApprovedByHuman);
        check.HumanReviewerId.Should().Be("officer-01");
        check.HumanReviewNotes.Should().Contain("Processo arquivado");
        check.HumanReviewedAt.Should().NotBeNull();
    }

    [Fact]
    public void PurgePersonalData_UnderLgpdRetention_ShouldAnonymizeSensitiveData()
    {
        var check = BackgroundCheck.Create(
            Guid.NewGuid(),
            SubjectType.Person,
            "52998224725",
            "529.982.247-25",
            "***.982.247-**",
            "Candidato Real Sensível",
            CheckPurpose.RecrutamentoESelecao,
            LegalBasis.Consentimento,
            true);

        check.PurgePersonalData();

        check.IsPurged.Should().BeTrue();
        check.PurgedAt.Should().NotBeNull();
        check.Name.Should().Be("[EXPURGADO_LGPD]");
        check.DocumentNumber.Should().Be("[EXPURGADO]");
        check.RawEvidenceJson.Should().BeNull();
        check.AiSummary.Should().Contain("EXPURGADO");
    }
}

