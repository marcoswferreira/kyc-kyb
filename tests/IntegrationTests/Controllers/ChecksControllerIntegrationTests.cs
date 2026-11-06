using System.Net;
using System.Net.Http.Json;
using Application.Dtos;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Database.Seeders;
using IntegrationTests.Common;
using Xunit;

namespace IntegrationTests.Controllers;

public class ChecksControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ChecksControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-API-Key", DatabaseSeeder.DefaultApiKey);
        _client.DefaultRequestHeaders.Add("X-User-Role", "ComplianceOfficer");
    }

    [Fact]
    public async Task FullCheckLifecycle_ShouldCreateProcessAndAllowHumanReview()
    {
        // 1. Criar solicitação de check
        var request = new CreateCheckRequest(
            Document: "52998224725",
            SubjectType: SubjectType.Person,
            Name: "Candidato E2E Teste",
            Purpose: CheckPurpose.RecrutamentoESelecao,
            LegalBasis: LegalBasis.Consentimento,
            ConsentObtained: true);

        var createResponse = await _client.PostAsJsonAsync("/api/v1/checks", request);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateCheckResponse>();
        created.Should().NotBeNull();
        var checkId = created!.Id;

        // 2. Aguarda processamento assíncrono pelo worker (até 5 segundos)
        CheckDetailsResponse? checkDetails = null;
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(200);
            var getResponse = await _client.GetAsync($"/api/v1/checks/{checkId}");
            if (getResponse.IsSuccessStatusCode)
            {
                checkDetails = await getResponse.Content.ReadFromJsonAsync<CheckDetailsResponse>();
                if (checkDetails != null && checkDetails.Status is CheckStatus.Completed or CheckStatus.CompletedWithWarnings)
                {
                    break;
                }
            }
        }

        checkDetails.Should().NotBeNull();
        checkDetails!.Status.Should().Be(CheckStatus.Completed);
        checkDetails.OverallRisk.Should().Be(RiskIndicator.Green);
        checkDetails.EvidenceSha256.Should().NotBeNullOrWhiteSpace();
        checkDetails.AiSummary.Should().Contain("LGPD Art. 20");
        checkDetails.HumanReviewStatus.Should().Be(HumanReviewDecision.AwaitingReview);

        // 3. Registrar revisão humana deliberada (Art. 20)
        var reviewRequest = new ReviewCheckRequest(
            ReviewerId: "officer_compliance_e2e",
            Decision: HumanReviewDecision.ApprovedByHuman,
            Notes: "Dossiê aprovado sem apontamentos impeditivos. Risco baixo validado.");

        var reviewResponse = await _client.PostAsJsonAsync($"/api/v1/checks/{checkId}/review", reviewRequest);
        reviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reviewResult = await reviewResponse.Content.ReadFromJsonAsync<ReviewCheckResponse>();
        reviewResult.Should().NotBeNull();
        reviewResult!.Decision.Should().Be(HumanReviewDecision.ApprovedByHuman);

        // 4. Consulta novamente para confirmar atualização
        var updatedResponse = await _client.GetAsync($"/api/v1/checks/{checkId}");
        var updatedDetails = await updatedResponse.Content.ReadFromJsonAsync<CheckDetailsResponse>();
        updatedDetails!.HumanReviewStatus.Should().Be(HumanReviewDecision.ApprovedByHuman);
        updatedDetails.HumanReviewerId.Should().Be("officer_compliance_e2e");
    }

    [Fact]
    public async Task CreateCheck_WithoutApiKey_ShouldReturnUnauthorized()
    {
        var unauthClient = _factory.CreateClient();

        var request = new CreateCheckRequest(
            Document: "52998224725",
            SubjectType: SubjectType.Person,
            Name: "Sem Chave",
            Purpose: CheckPurpose.RecrutamentoESelecao,
            LegalBasis: LegalBasis.Consentimento,
            ConsentObtained: true);

        var response = await unauthClient.PostAsJsonAsync("/api/v1/checks", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateCheck_WithAlphanumericCnpj_ShouldSucceedForCompany()
    {
        var request = new CreateCheckRequest(
            Document: "12ABC345000188", // CNPJ Alfanumérico IN RFB 2.229/2024
            SubjectType: SubjectType.Company,
            Name: "Empresa Alfanumérica E2E Ltda",
            Purpose: CheckPurpose.ComplianceKyb,
            LegalBasis: LegalBasis.ObrigacaoLegal,
            ConsentObtained: true);

        var response = await _client.PostAsJsonAsync("/api/v1/checks", request);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var created = await response.Content.ReadFromJsonAsync<CreateCheckResponse>();
        created.Should().NotBeNull();
        created!.DocumentMasked.Should().Be("**.ABC.***0001-**");
    }

    [Fact]
    public async Task CreateCheck_WithoutConsent_ShouldReturnBadRequestUnderLgpd()
    {
        var request = new CreateCheckRequest(
            Document: "52998224725",
            SubjectType: SubjectType.Person,
            Name: "Candidato Sem Consentimento",
            Purpose: CheckPurpose.RecrutamentoESelecao,
            LegalBasis: LegalBasis.Consentimento,
            ConsentObtained: false); // Consentimento negado

        var response = await _client.PostAsJsonAsync("/api/v1/checks", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("LGPD_CONSENT_REQUIRED");
    }

    [Fact]
    public async Task CheckWithMultipleRiskFindings_ShouldPersistFindingsAndCompleteWithoutConcurrencyError()
    {
        // Documento com 666 que dispara apontamentos em tribunais e situação cadastral
        var request = new CreateCheckRequest(
            Document: "66682247257",
            SubjectType: SubjectType.Person,
            Name: "Candidato Com Apontamentos",
            Purpose: CheckPurpose.RecrutamentoESelecao,
            LegalBasis: LegalBasis.Consentimento,
            ConsentObtained: true);

        var createResponse = await _client.PostAsJsonAsync("/api/v1/checks", request);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateCheckResponse>();
        created.Should().NotBeNull();
        var checkId = created!.Id;

        // Aguarda processamento assíncrono pelo worker
        CheckDetailsResponse? checkDetails = null;
        for (int i = 0; i < 25; i++)
        {
            await Task.Delay(200);
            var getResponse = await _client.GetAsync($"/api/v1/checks/{checkId}");
            if (getResponse.IsSuccessStatusCode)
            {
                checkDetails = await getResponse.Content.ReadFromJsonAsync<CheckDetailsResponse>();
                if (checkDetails != null && checkDetails.Status is CheckStatus.Completed or CheckStatus.CompletedWithWarnings)
                {
                    break;
                }
            }
        }

        checkDetails.Should().NotBeNull();
        checkDetails!.Status.Should().BeOneOf(CheckStatus.Completed, CheckStatus.CompletedWithWarnings);
        checkDetails.Findings.Should().NotBeEmpty();
        checkDetails.OverallRisk.Should().Be(RiskIndicator.Red);
    }
}
