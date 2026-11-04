using System.Net;
using System.Net.Http.Json;
using Application.Dtos;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Database.Seeders;
using IntegrationTests.Common;
using MiniKycKyb.API.Controllers;
using Xunit;

namespace IntegrationTests.Controllers;

public class AtsSimulationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AtsSimulationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-API-Key", DatabaseSeeder.DefaultApiKey);
    }

    [Fact]
    public async Task AtsWebhookEndpoint_ShouldReceiveAndRecordCallback()
    {
        var payload = new AtsWebhookPayload(
            Event: "check.completed",
            CheckId: Guid.NewGuid(),
            CandidateDocumentMasked: "***.982.247-**",
            OverallRisk: RiskIndicator.Green,
            HumanReviewStatus: HumanReviewDecision.AwaitingReview,
            Status: "Completed",
            AiSummary: "Parecer preliminar",
            VerificationUrl: "/api/v1/checks/123",
            Timestamp: DateTime.UtcNow);

        var response = await _client.PostAsJsonAsync("/api/v1/ats/callback", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await _client.GetAsync("/api/v1/ats/received-callbacks");
        var list = await listResponse.Content.ReadFromJsonAsync<List<AtsReceivedEvent>>();

        list.Should().NotBeNull();
        list!.Should().Contain(c => c.DocumentMasked == "***.982.247-**");
    }

    [Fact]
    public async Task AtsTriggerStage_ShouldRegisterCheckAndQueueExecution()
    {
        var triggerRequest = new AtsTriggerRequest(
            CandidateName: "Fernanda Lima Sintética",
            Document: "52998224725",
            JobTitle: "Engenheira de Software Sênior",
            StageName: "Checagem de Antecedentes",
            CallbackUrl: "/api/v1/ats/callback");

        var response = await _client.PostAsJsonAsync("/api/v1/ats/trigger-candidate-stage", triggerRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var triggerJson = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var checkId = triggerJson.GetProperty("check_id").GetGuid();

        // Aguarda a conclusão da checagem pelo background worker
        CheckDetailsResponse? checkDetails = null;
        for (int i = 0; i < 25; i++)
        {
            await Task.Delay(200);
            var checkRes = await _client.GetAsync($"/api/v1/checks/{checkId}");
            if (checkRes.IsSuccessStatusCode)
            {
                checkDetails = await checkRes.Content.ReadFromJsonAsync<CheckDetailsResponse>();
                if (checkDetails != null && checkDetails.Status is CheckStatus.Completed or CheckStatus.CompletedWithWarnings)
                {
                    break;
                }
            }
        }

        checkDetails.Should().NotBeNull();
        checkDetails!.Status.Should().Be(CheckStatus.Completed);
        checkDetails.OverallRisk.Should().Be(RiskIndicator.Green);
    }
}
