using System.Net;
using System.Text.Json;
using Application.AI;
using Application.Interfaces;
using Application.Rules;
using Application.UseCases.MeteringAndAudit;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Application.UnitTests.UseCases;

public class MoreApplicationTests
{
    [Fact]
    public async Task GetTenantUsage_WhenMetricExists_ShouldReturnCorrectData()
    {
        var repo = Substitute.For<IUsageMetricRepository>();
        var tenantId = Guid.NewGuid();
        var metric = UsageMetric.Create(tenantId, "2026-11");
        metric.IncrementUsage(checksCount: 5, providerCallsCount: 25, unitCost: 0.50m);

        repo.GetByTenantAndMonthAsync(tenantId, "2026-11", Arg.Any<CancellationToken>())
            .Returns(metric);

        var sut = new GetTenantUsageUseCase(repo);

        var result = await sut.ExecuteAsync(tenantId, "2026-11");

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalChecks.Should().Be(5);
        result.Value.TotalProviderCalls.Should().Be(25);
        result.Value.BillableUnits.Should().Be(2.50m);
    }

    [Fact]
    public async Task GetTenantUsage_WhenMetricNull_ShouldReturnZeroes()
    {
        var repo = Substitute.For<IUsageMetricRepository>();
        var tenantId = Guid.NewGuid();

        repo.GetByTenantAndMonthAsync(tenantId, "2026-11", Arg.Any<CancellationToken>())
            .Returns((UsageMetric?)null);

        var sut = new GetTenantUsageUseCase(repo);

        var result = await sut.ExecuteAsync(tenantId, "2026-11");

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalChecks.Should().Be(0);
        result.Value.BillableUnits.Should().Be(0m);
    }

    [Fact]
    public async Task GetAuditLogs_ShouldReturnMappedEntries()
    {
        var repo = Substitute.For<IAuditLogRepository>();
        var tenantId = Guid.NewGuid();
        var entry = AuditLogEntry.Create(
            tenantId,
            "admin_user",
            "CONFIG_CHANGED",
            "tenant_01",
            "Rate limit alterado",
            "10.0.0.1",
            "0000000000000000000000000000000000000000000000000000000000000000");

        repo.ListByTenantAsync(tenantId, 0, 50, Arg.Any<CancellationToken>())
            .Returns(new List<AuditLogEntry> { entry });

        var sut = new GetAuditLogsUseCase(repo);

        var list = await sut.ExecuteAsync(tenantId);

        list.Should().HaveCount(1);
        list[0].Action.Should().Be("CONFIG_CHANGED");
        list[0].IntegrityValid.Should().BeTrue();
    }

    [Fact]
    public void RiskPolicyEngine_MoreScenarios_ShouldEvaluateAccurately()
    {
        var sut = new RiskPolicyEngine();
        var checkId = Guid.NewGuid();

        // 1. Receita Suspensa/Pendente -> Yellow
        var p1 = ProviderResult.Success("ReceitaFederalMockProvider", "{\"situacao_cadastral\":\"SUSPENSA\"}", 10);
        var r1 = sut.Evaluate(checkId, new[] { p1 });
        r1.OverallRisk.Should().Be(RiskIndicator.Yellow);

        // 2. Receita Inapta/Cancelada -> Red
        var p2 = ProviderResult.Success("ReceitaFederalMockProvider", "{\"situacao_cadastral\":\"INAPTA\"}", 10);
        var r2 = sut.Evaluate(checkId, new[] { p2 });
        r2.OverallRisk.Should().Be(RiskIndicator.Red);

        // 3. Trabalho Escravo match -> Red
        var p3 = ProviderResult.Success("TrabalhoEscravoMockProvider", "{\"consta_lista_suja\":true,\"estabelecimento\":\"Fazenda X\"}", 10);
        var r3 = sut.Evaluate(checkId, new[] { p3 });
        r3.OverallRisk.Should().Be(RiskIndicator.Red);

        // 4. Listas Restritivas CEIS/CNEP CGU -> Red
        var p4 = ProviderResult.Success("ListasRestritivasMockProvider", "{\"ceis_cnep\":true,\"ofac_sanctioned\":false,\"pep\":false}", 10);
        var r4 = sut.Evaluate(checkId, new[] { p4 });
        r4.OverallRisk.Should().Be(RiskIndicator.Red);

        // 5. QSA Sócios com sócio PEP -> Yellow
        var p5 = ProviderResult.Success("QsaSociosMockProvider", "{\"socios\":[{\"nome\":\"Sócio Político\",\"is_pep\":true}]}", 10);
        var r5 = sut.Evaluate(checkId, new[] { p5 });
        r5.OverallRisk.Should().Be(RiskIndicator.Yellow);

        // 6. QSA Sócios sem sócio PEP -> Green
        var p6 = ProviderResult.Success("QsaSociosMockProvider", "{\"socios\":[{\"nome\":\"Sócio Comum\",\"is_pep\":false}]}", 10);
        var r6 = sut.Evaluate(checkId, new[] { p6 });
        r6.OverallRisk.Should().Be(RiskIndicator.Green);
    }

    [Fact]
    public async Task GeminiAiAssistant_WhenGeminiReturnsValidCandidate_ShouldReturnGeneratedText()
    {
        var responseJson = """
            {
                "candidates": [
                    {
                        "content": {
                            "parts": [
                                {
                                    "text": "Parecer elaborado pela inteligência artificial com fundamentação legal."
                                }
                            ]
                        }
                    }
                ]
            }
            """;

        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson)
        });

        var httpClient = new HttpClient(handler);
        var fallback = Substitute.For<IAiAssistantService>();
        var logger = Substitute.For<ILogger<GeminiAiAssistantService>>();

        var sut = new GeminiAiAssistantService(httpClient, fallback, "test_api_key", logger);

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

        var result = await sut.GenerateReportSummaryAsync(check, new List<CheckFinding>());

        result.Should().Be("Parecer elaborado pela inteligência artificial com fundamentação legal.");
        await fallback.DidNotReceive().GenerateReportSummaryAsync(Arg.Any<BackgroundCheck>(), Arg.Any<IReadOnlyList<CheckFinding>>(), Arg.Any<CancellationToken>());
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public MockHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }
}

