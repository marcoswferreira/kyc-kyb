using System.Net;
using System.Net.Http.Json;
using Application.Dtos;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using FluentAssertions;
using Infrastructure.BackgroundWorkers;
using Infrastructure.Database;
using Infrastructure.Database.Seeders;
using Infrastructure.Providers;
using Infrastructure.Resilience;
using Infrastructure.Security;
using Infrastructure.Webhooks;
using IntegrationTests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiniKycKyb.API.Controllers;
using MiniKycKyb.API.Middlewares;
using NSubstitute;
using Xunit;

namespace IntegrationTests.Controllers;

public class MoreIntegrationAndInfrastructureTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MoreIntegrationAndInfrastructureTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-API-Key", DatabaseSeeder.DefaultApiKey);
        _client.DefaultRequestHeaders.Add("X-User-Role", "ComplianceOfficer");
    }

    [Fact]
    public void DataMasker_ShouldMaskAccordingToRole()
    {
        var masker = new DataMasker();

        // Operador comum: deve mascarar
        var maskedCpf = masker.MaskDocument("52998224725", UserRole.Operator);
        maskedCpf.Should().Be("***.982.247-**");

        var maskedCnpj = masker.MaskDocument("11222333000181", UserRole.Operator);
        maskedCnpj.Should().Be("**.222.***0001-**");

        var maskedName = masker.MaskName("João Carlos Ferreira da Silva", UserRole.Operator);
        maskedName.Should().Contain("***");

        // Compliance Officer: pode ver desmascarado
        var unmaskedCpf = masker.MaskDocument("52998224725", UserRole.ComplianceOfficer);
        unmaskedCpf.Should().Be("52998224725");

        var unmaskedName = masker.MaskName("João Carlos Ferreira da Silva", UserRole.ComplianceOfficer);
        unmaskedName.Should().Be("João Carlos Ferreira da Silva");
    }

    [Fact]
    public async Task MockProviders_AllScenarios_ShouldReturnExpectedPayloads()
    {
        var rfb = new ReceitaFederalMockProvider();
        var res1 = await rfb.QueryAsync("52998224725", SubjectType.Person);
        res1.IsSuccess.Should().BeTrue();
        res1.RawDataJson.Should().Contain("REGULAR");

        var resPendente = await rfb.QueryAsync("11144477735", SubjectType.Person);
        resPendente.RawDataJson.Should().Contain("PENDENTE DE REGULARIZACAO");

        var resInapta = await rfb.QueryAsync("12366678900", SubjectType.Company);
        resInapta.RawDataJson.Should().Contain("INAPTA");

        var resError = await rfb.QueryAsync("12345678999", SubjectType.Person);
        resError.IsSuccess.Should().BeFalse();

        var trib = new TribunaisProcessosMockProvider();
        var resTribClean = await trib.QueryAsync("52998224725", SubjectType.Person);
        resTribClean.RawDataJson.Should().Contain("\"total_processos\":0");

        var resTribCrim = await trib.QueryAsync("12366678900", SubjectType.Person);
        resTribCrim.RawDataJson.Should().Contain("Criminal");

        var resTribTrab = await trib.QueryAsync("12344478900", SubjectType.Person);
        resTribTrab.RawDataJson.Should().Contain("Trabalhista");

        var listas = new ListasRestritivasMockProvider();
        var resListasClean = await listas.QueryAsync("52998224725", SubjectType.Person);
        resListasClean.RawDataJson.Should().Contain("\"ofac_sanctioned\":false");

        var resListasOfac = await listas.QueryAsync("12388878900", SubjectType.Person);
        resListasOfac.RawDataJson.Should().Contain("\"ofac_sanctioned\":true");

        var resListasPep = await listas.QueryAsync("12377778900", SubjectType.Person);
        resListasPep.RawDataJson.Should().Contain("\"pep\":true");

        var trabEscravo = new TrabalhoEscravoMockProvider();
        var resTrabClean = await trabEscravo.QueryAsync("52998224725", SubjectType.Person);
        resTrabClean.RawDataJson.Should().Contain("\"consta_lista_suja\":false");

        var resTrabSujo = await trabEscravo.QueryAsync("12355578900", SubjectType.Person);
        resTrabSujo.RawDataJson.Should().Contain("\"consta_lista_suja\":true");

        var qsa = new QsaSociosMockProvider();
        var resQsaPessoa = await qsa.QueryAsync("52998224725", SubjectType.Person);
        resQsaPessoa.RawDataJson.Should().Contain("NOT_APPLICABLE_FOR_PERSON");

        var resQsaEmpresa = await qsa.QueryAsync("11222333000181", SubjectType.Company);
        resQsaEmpresa.RawDataJson.Should().Contain("socios");
    }

    [Fact]
    public async Task Repositories_DirectQueries_ShouldSucceed()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KycKybDbContext>();
        var checkRepo = scope.ServiceProvider.GetRequiredService<IBackgroundCheckRepository>();
        var auditRepo = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
        var usageRepo = scope.ServiceProvider.GetRequiredService<IUsageMetricRepository>();
        var subRepo = scope.ServiceProvider.GetRequiredService<IWebhookSubscriptionRepository>();

        var tenantId = DatabaseSeeder.DefaultTenantId;

        // WebhookSubscriptionRepository
        var newSub = WebhookSubscription.Create(tenantId, "https://webhook.site/1", "sec1", "check.completed");
        await subRepo.AddAsync(newSub);
        newSub.Deactivate();
        await subRepo.UpdateAsync(newSub);
        newSub.IsActive.Should().BeFalse();

        // BackgroundCheckRepository
        var byDoc = await checkRepo.GetByDocumentAndTenantAsync(tenantId, "52998224725");
        var pending = await checkRepo.GetPendingChecksAsync(10);
        var list = await checkRepo.ListByTenantAsync(tenantId, 0, 10);
        list.Should().NotBeNull();

        // AuditLogRepository
        var auditByResource = await auditRepo.ListByResourceAsync(tenantId.ToString());
        auditByResource.Should().NotBeNull();

        // UsageMetricRepository
        await usageRepo.AddOrUpdateUsageAsync(tenantId, "2026-11", 1, 4);
        var metric = await usageRepo.GetByTenantAndMonthAsync(tenantId, "2026-11");
        metric.Should().NotBeNull();
        metric!.TotalChecks.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_ShouldCaptureUnhandledExceptionAndReturn500()
    {
        var logger = Substitute.For<ILogger<ExceptionHandlingMiddleware>>();
        var middleware = new ExceptionHandlingMiddleware(_ => throw new InvalidOperationException("Erro de teste"), logger);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        context.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task RateLimitingMiddleware_WhenLimitExceeded_ShouldReturn429()
    {
        var logger = Substitute.For<ILogger<RateLimitingMiddleware>>();
        var middleware = new RateLimitingMiddleware(_ => Task.CompletedTask, logger);

        var tenantContext = new CurrentTenantContext { TenantId = Guid.NewGuid() };

        // Simula 125 requisições rápidas para estourar o limite de 120
        bool hitLimit = false;
        for (int i = 0; i < 125; i++)
        {
            var ctx = new DefaultHttpContext();
            await middleware.InvokeAsync(ctx, tenantContext);
            if (ctx.Response.StatusCode == (int)HttpStatusCode.TooManyRequests)
            {
                hitLimit = true;
                break;
            }
        }

        hitLimit.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterWebhook_Endpoint_ShouldSucceed()
    {
        var req = new RegisterWebhookRequest("https://webhook.site/teste", "check.completed");
        var res = await _client.PostAsJsonAsync("/api/v1/tenants/webhooks", req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChecksController_ErrorBranches_ShouldReturnAppropriateStatus()
    {
        // 1. GetCheckById NotFound
        var notFound = await _client.GetAsync($"/api/v1/checks/{Guid.NewGuid()}");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 2. CreateCheck BadRequest (CPF inválido)
        var invalidReq = new CreateCheckRequest("123", SubjectType.Person, "Nome", CheckPurpose.RecrutamentoESelecao, LegalBasis.Consentimento, true);
        var badReq = await _client.PostAsJsonAsync("/api/v1/checks", invalidReq);
        badReq.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 3. ReviewCheck NotFound
        var reviewReq = new ReviewCheckRequest("rev_01", HumanReviewDecision.ApprovedByHuman, "Notas");
        var reviewNotFound = await _client.PostAsJsonAsync($"/api/v1/checks/{Guid.NewGuid()}/review", reviewReq);
        reviewNotFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CheckQueue_TryPeek_And_CircuitBreaker_Reset_ShouldWork()
    {
        var queue = new InMemoryCheckQueue();
        await queue.EnqueueAsync(Guid.NewGuid());
        queue.TryPeek(out var count).Should().BeTrue();
        count.Should().Be(1);
        var id = await queue.DequeueAsync();
        id.Should().NotBeEmpty();

        var breaker = new CircuitBreaker();
        breaker.Reset();
        breaker.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void HmacSha256_ShouldProduceConsistentSignature()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("payload_teste");
        var sig1 = WebhookDispatcher.ComputeHmacSha256(data, "segredo");
        var sig2 = WebhookDispatcher.ComputeHmacSha256(data, "segredo");

        sig1.Should().Be(sig2);
        sig1.Should().HaveLength(64);
    }

    [Fact]
    public async Task GetCheckById_WhenPurged_ShouldReturnAnonymizedData()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBackgroundCheckRepository>();

        var check = BackgroundCheck.Create(
            DatabaseSeeder.DefaultTenantId,
            SubjectType.Person,
            "52998224725",
            "529.982.247-25",
            "***.982.247-**",
            "Candidato Expirado",
            CheckPurpose.RecrutamentoESelecao,
            LegalBasis.Consentimento,
            true);

        check.PurgePersonalData();
        await repo.AddAsync(check);

        var res = await _client.GetAsync($"/api/v1/checks/{check.Id}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var details = await res.Content.ReadFromJsonAsync<CheckDetailsResponse>();
        details!.Name.Should().Be("[EXPURGADO_LGPD]");
        details.IsPurged.Should().BeTrue();
    }

    [Fact]
    public async Task Metering_WithoutYearMonth_ShouldDefaultToCurrentMonth()
    {
        var res = await _client.GetAsync("/api/v1/metering/usage");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
