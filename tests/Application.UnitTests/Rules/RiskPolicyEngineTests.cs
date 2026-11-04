using Application.Interfaces;
using Application.Rules;
using Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Application.UnitTests.Rules;

public class RiskPolicyEngineTests
{
    private readonly RiskPolicyEngine _sut = new();
    private readonly Guid _checkId = Guid.NewGuid();

    [Fact]
    public void Evaluate_WithCleanProviders_ShouldReturnGreen()
    {
        var providerResults = new List<ProviderResult>
        {
            ProviderResult.Success(
                "ReceitaFederalMockProvider",
                "{\"provedor\":\"ReceitaFederalMockProvider\",\"situacao_cadastral\":\"REGULAR\"}",
                20),
            ProviderResult.Success(
                "TribunaisProcessosMockProvider",
                "{\"provedor\":\"TribunaisProcessosMockProvider\",\"total_processos\":0,\"processos\":[]}",
                30),
            ProviderResult.Success(
                "ListasRestritivasMockProvider",
                "{\"provedor\":\"ListasRestritivasMockProvider\",\"ofac_sanctioned\":false,\"ceis_cnep\":false,\"pep\":false}",
                15),
            ProviderResult.Success(
                "TrabalhoEscravoMockProvider",
                "{\"provedor\":\"TrabalhoEscravoMockProvider\",\"consta_lista_suja\":false}",
                10)
        };

        var result = _sut.Evaluate(_checkId, providerResults);

        result.OverallRisk.Should().Be(RiskIndicator.Green);
        result.Findings.Should().NotBeEmpty();
        result.Findings.All(f => f.RiskLevel == RiskIndicator.Green).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithArchivedLawsuitAndPep_ShouldReturnYellowWithExplanationAndEvidence()
    {
        var providerResults = new List<ProviderResult>
        {
            ProviderResult.Success(
                "ReceitaFederalMockProvider",
                "{\"provedor\":\"ReceitaFederalMockProvider\",\"situacao_cadastral\":\"REGULAR\"}",
                20),
            ProviderResult.Success(
                "TribunaisProcessosMockProvider",
                """
                {
                    "provedor": "TribunaisProcessosMockProvider",
                    "total_processos": 1,
                    "processos": [
                        {
                            "tipo": "Trabalhista",
                            "numero": "1000456-12.2023.5.02.0001",
                            "tribunal": "TRT2",
                            "status": "Arquivado definitivamente",
                            "assunto": "Horas Extras"
                        }
                    ]
                }
                """,
                25),
            ProviderResult.Success(
                "ListasRestritivasMockProvider",
                """
                {
                    "provedor": "ListasRestritivasMockProvider",
                    "ofac_sanctioned": false,
                    "ceis_cnep": false,
                    "pep": true,
                    "pep_cargo": "Secretário Municipal"
                }
                """,
                15)
        };

        var result = _sut.Evaluate(_checkId, providerResults);

        result.OverallRisk.Should().Be(RiskIndicator.Yellow);
        var yellowFindings = result.Findings.Where(f => f.RiskLevel == RiskIndicator.Yellow).ToList();
        yellowFindings.Should().HaveCount(2);

        // Verifica explicação e evidência anexada
        var pepFinding = yellowFindings.First(f => f.Category == "LISTAS_RESTRITIVAS");
        pepFinding.ExplanationWhy.Should().Contain("diligência reforçada");
        pepFinding.EvidenceKey.Should().Be("pep_match_true");

        var lawsuitFinding = yellowFindings.First(f => f.Category == "PROCESSOS_JUDICIAIS");
        lawsuitFinding.ExplanationWhy.Should().Contain("Não configuram impedimento automático sob a LGPD");
    }

    [Fact]
    public void Evaluate_WithActiveCriminalLawsuit_ShouldReturnRedWithDetailedExplanation()
    {
        var providerResults = new List<ProviderResult>
        {
            ProviderResult.Success(
                "TribunaisProcessosMockProvider",
                """
                {
                    "provedor": "TribunaisProcessosMockProvider",
                    "total_processos": 1,
                    "processos": [
                        {
                            "tipo": "Criminal",
                            "numero": "0012345-89.2025.8.26.0100",
                            "tribunal": "TJSP",
                            "status": "Em andamento",
                            "assunto": "Crimes contra o Sistema Financeiro Nacional"
                        }
                    ]
                }
                """,
                25)
        };

        var result = _sut.Evaluate(_checkId, providerResults);

        result.OverallRisk.Should().Be(RiskIndicator.Red);
        var redFinding = result.Findings.First(f => f.RiskLevel == RiskIndicator.Red);
        redFinding.Category.Should().Be("PROCESSOS_JUDICIAIS");
        redFinding.Title.Should().Contain("Ação Penal");
        redFinding.ExplanationWhy.Should().Contain("exigindo análise humana especializada dos autos");
    }

    [Fact]
    public void Evaluate_WhenProviderFailsOrTimesOut_ShouldGracefullyDegradeToYellowFinding()
    {
        var providerResults = new List<ProviderResult>
        {
            ProviderResult.Success(
                "ReceitaFederalMockProvider",
                "{\"provedor\":\"ReceitaFederalMockProvider\",\"situacao_cadastral\":\"REGULAR\"}",
                20),
            ProviderResult.Timeout("TribunaisProcessosMockProvider", 800)
        };

        var result = _sut.Evaluate(_checkId, providerResults);

        result.OverallRisk.Should().Be(RiskIndicator.Yellow);
        var warningFinding = result.Findings.First(f => f.Category == "DISPONIBILIDADE_PROVEDOR");
        warningFinding.RiskLevel.Should().Be(RiskIndicator.Yellow);
        warningFinding.ExplanationWhy.Should().Contain("resultado parcial");
    }
}

