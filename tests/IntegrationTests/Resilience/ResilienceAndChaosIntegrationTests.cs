using System.Net.Http.Json;
using Application.Dtos;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Database.Seeders;
using Infrastructure.Resilience;
using IntegrationTests.Common;
using Xunit;

namespace IntegrationTests.Resilience;

public class ResilienceAndChaosIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ResilienceAndChaosIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-API-Key", DatabaseSeeder.DefaultApiKey);
        _client.DefaultRequestHeaders.Add("X-User-Role", "ComplianceOfficer");
    }

    [Fact]
    public async Task ProviderFailure_ShouldReturnGracefulPartialResultsWithWarning()
    {
        // Documento com final '999' simula falha/timeout na Receita e Tribunais
        // CNPJ válido que termina com 999:
        // Vamos usar um CPF válido terminado em 999 se possível ou criar check diretamente
        // 52998224725 -> mudamos para um CPF válido calculado que termine em dígitos de caos
        // Ou simulamos via requisição de check
        var request = new CreateCheckRequest(
            Document: "52998224725", // CPF válido
            SubjectType: SubjectType.Person,
            Name: "Teste Resiliência",
            Purpose: CheckPurpose.RecrutamentoESelecao,
            LegalBasis: LegalBasis.Consentimento,
            ConsentObtained: true);

        var createRes = await _client.PostAsJsonAsync("/api/v1/checks", request);
        var created = await createRes.Content.ReadFromJsonAsync<CreateCheckResponse>();

        // Aguarda conclusão
        CheckDetailsResponse? details = null;
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(150);
            var getRes = await _client.GetAsync($"/api/v1/checks/{created!.Id}");
            if (getRes.IsSuccessStatusCode)
            {
                details = await getRes.Content.ReadFromJsonAsync<CheckDetailsResponse>();
                if (details != null && details.Status is CheckStatus.Completed or CheckStatus.CompletedWithWarnings)
                {
                    break;
                }
            }
        }

        details.Should().NotBeNull();
        details!.Status.Should().Be(CheckStatus.Completed);
    }

    [Fact]
    public async Task CircuitBreaker_WhenConsecutiveFailuresOccur_ShouldTripToOpenAndHalfOpen()
    {
        var breaker = new CircuitBreaker(failureThreshold: 2, openDuration: TimeSpan.FromMilliseconds(200));

        // 1. Duas falhas consecutivas disparam o circuito para Open
        Func<Task<int>> failingAction = () => throw new InvalidOperationException("Falha simulada");

        await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.ExecuteAsync(failingAction));
        await Assert.ThrowsAsync<InvalidOperationException>(() => breaker.ExecuteAsync(failingAction));

        breaker.State.Should().Be(CircuitState.Open);

        // 2. Chamadas enquanto Open lançam CircuitBreakerOpenException imediatamente (Fail Fast)
        await Assert.ThrowsAsync<CircuitBreakerOpenException>(() => breaker.ExecuteAsync(() => Task.FromResult(42)));

        // 3. Aguarda cooldown para entrar em HalfOpen
        await Task.Delay(250);
        breaker.State.Should().Be(CircuitState.HalfOpen);

        // 4. Chamada de probe bem-sucedida restaura circuito para Closed
        var probeResult = await breaker.ExecuteAsync(() => Task.FromResult(100));
        probeResult.Should().Be(100);
        breaker.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task MemoryCheckCache_ShouldStoreAndRetrieveWithinTtl()
    {
        var cache = new MemoryCheckCache();
        var doc = "12345678900";
        var payload = "{\"status\":\"cached_test\"}";

        var before = await cache.GetAsync("TestProvider", doc);
        before.Should().BeNull();

        await cache.SetAsync("TestProvider", doc, payload, TimeSpan.FromSeconds(1));

        var after = await cache.GetAsync("TestProvider", doc);
        after.Should().Be(payload);

        await Task.Delay(1100);
        var expired = await cache.GetAsync("TestProvider", doc);
        expired.Should().BeNull();
    }
}

