using System.Net;
using Application.AI;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Application.UnitTests.AI;

public class AiAssistantTests
{
    private readonly HeuristicFallbackAiService _heuristicService = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public async Task HeuristicFallback_ShouldIncludeLgpdArt20NoticeAndFindings()
    {
        var check = BackgroundCheck.Create(
            _tenantId,
            SubjectType.Person,
            "52998224725",
            "529.982.247-25",
            "***.982.247-**",
            "Candidato Silva",
            CheckPurpose.RecrutamentoESelecao,
            LegalBasis.Consentimento,
            true);

        var findings = new List<CheckFinding>
        {
            CheckFinding.Create(
                check.Id,
                "TribunaisProcessosMockProvider",
                "PROCESSOS_JUDICIAIS",
                RiskIndicator.Yellow,
                "Ação Trabalhista Arquivada",
                "Ação nº 1000456-12 arquivada em 2024",
                "Processo arquivado sem constrição patrimonial.",
                "ev_key_1",
                "excerpt")
        };

        var summary = await _heuristicService.GenerateReportSummaryAsync(check, findings);

        summary.Should().Contain("Parecer Preliminar de Compliance (Decisão Assistida - LGPD Art. 20)");
        summary.Should().Contain("***.982.247-**");
        summary.Should().Contain("Ação Trabalhista Arquivada");
        summary.Should().Contain("Artigo 20 da Lei Geral de Proteção de Dados");
        summary.Should().Contain("não toma decisões automatizadas terminativas de reprovação");
    }

    [Fact]
    public async Task GeminiAiAssistant_WithoutApiKey_ShouldGracefullyFallbackToHeuristic()
    {
        var httpClient = new HttpClient();
        var fallback = Substitute.For<IAiAssistantService>();
        fallback.GenerateReportSummaryAsync(Arg.Any<BackgroundCheck>(), Arg.Any<IReadOnlyList<CheckFinding>>(), Arg.Any<CancellationToken>())
            .Returns("Resumo Heurístico de Fallback");

        var logger = Substitute.For<ILogger<GeminiAiAssistantService>>();

        var sut = new GeminiAiAssistantService(httpClient, fallback, apiKey: null, logger);

        var check = BackgroundCheck.Create(
            _tenantId,
            SubjectType.Person,
            "52998224725",
            "529.982.247-25",
            "***.982.247-**",
            "Candidato",
            CheckPurpose.RecrutamentoESelecao,
            LegalBasis.Consentimento,
            true);

        var result = await sut.GenerateReportSummaryAsync(check, new List<CheckFinding>());

        result.Should().Be("Resumo Heurístico de Fallback");
        await fallback.Received(1).GenerateReportSummaryAsync(check, Arg.Any<IReadOnlyList<CheckFinding>>(), Arg.Any<CancellationToken>());
    }
}

