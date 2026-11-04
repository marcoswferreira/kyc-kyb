using System.Net;
using System.Net.Http.Json;
using Application.Dtos;
using FluentAssertions;
using Infrastructure.Database.Seeders;
using IntegrationTests.Common;
using Xunit;

namespace IntegrationTests.Controllers;

public class SecurityAndLgpdIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SecurityAndLgpdIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-API-Key", DatabaseSeeder.DefaultApiKey);
        _client.DefaultRequestHeaders.Add("X-User-Role", "ComplianceOfficer");
    }

    [Fact]
    public async Task AuditLogs_ShouldReturnChainedLogWithValidIntegrity()
    {
        var response = await _client.GetAsync("/api/v1/audit/logs");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<AuditLogsApiResponse>();
        content.Should().NotBeNull();
        content!.all_valid.Should().BeTrue();
        content.logs.Should().NotBeEmpty();
    }

    private record AuditLogsApiResponse(int total, bool all_valid, List<AuditLogDto> logs);

    [Fact]
    public async Task MeteringUsage_ShouldReturnUsageStatistics()
    {
        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");
        var response = await _client.GetAsync($"/api/v1/metering/usage?yearMonth={currentMonth}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var usage = await response.Content.ReadFromJsonAsync<UsageMetricResponse>();
        usage.Should().NotBeNull();
        usage!.YearMonth.Should().Be(currentMonth);
    }

    [Fact]
    public async Task PurgeExpired_ShouldExecuteSuccessfullyAndReturnCount()
    {
        var response = await _client.PostAsync("/api/v1/checks/purge-expired", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Rotina de expurgo LGPD concluída");
    }

    [Fact]
    public async Task Tenants_ShouldCreateTenantAndGenerateApiKey()
    {
        var newTenantRequest = new CreateTenantRequest(
            Name: "Nova Fintech Parceira",
            Slug: $"fintech-{Guid.NewGuid():N}",
            RateLimitPerMinute: 60);

        var response = await _client.PostAsJsonAsync("/api/v1/tenants", newTenantRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<CreateTenantApiResponse>();
        body.Should().NotBeNull();
        body!.api_key.RawApiKey.Should().StartWith("sk_live_");

        // Rotação de chave
        var rotateResponse = await _client.PostAsync($"/api/v1/tenants/{body.tenant.Id}/api-keys?environment=test", null);
        rotateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var rotateResult = await rotateResponse.Content.ReadFromJsonAsync<ApiKeyResponse>();
        rotateResult!.RawApiKey.Should().StartWith("sk_test_");
    }

    private record CreateTenantApiResponse(TenantResponse tenant, ApiKeyResponse api_key);
}

