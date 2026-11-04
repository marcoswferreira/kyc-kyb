using Application.AI;
using Application.Interfaces;
using Application.Rules;
using Application.UseCases.Checks;
using Application.UseCases.MeteringAndAudit;
using Application.UseCases.Tenants;
using Domain.Repositories;
using Infrastructure.BackgroundWorkers;
using Infrastructure.Database;
using Infrastructure.Database.Repositories;
using Infrastructure.Database.Seeders;
using Infrastructure.Orchestration;
using Infrastructure.Providers;
using Infrastructure.Resilience;
using Infrastructure.Security;
using Infrastructure.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var dbProvider = configuration["Database:Provider"] ?? "InMemory";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? configuration["ConnectionStrings:DefaultConnection"];

        if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<KycKybDbContext>(options =>
                options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(KycKybDbContext).Assembly.FullName)));
        }
        else
        {
            services.AddDbContext<KycKybDbContext>(options =>
                options.UseInMemoryDatabase("MiniKycKybDb"));
        }

        // Repositories
        services.AddScoped<IBackgroundCheckRepository, BackgroundCheckRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IWebhookSubscriptionRepository, WebhookSubscriptionRepository>();
        services.AddScoped<IUsageMetricRepository, UsageMetricRepository>();

        // Resilience & Cache
        services.AddSingleton<ICircuitBreaker>(_ => new CircuitBreaker(failureThreshold: 3, openDuration: TimeSpan.FromSeconds(5)));
        services.AddSingleton<ICheckCache, MemoryCheckCache>();

        // Provedores Sintéticos (Mock Providers)
        services.AddSingleton<IBackgroundCheckProvider, ReceitaFederalMockProvider>();
        services.AddSingleton<IBackgroundCheckProvider, TribunaisProcessosMockProvider>();
        services.AddSingleton<IBackgroundCheckProvider, ListasRestritivasMockProvider>();
        services.AddSingleton<IBackgroundCheckProvider, TrabalhoEscravoMockProvider>();
        services.AddSingleton<IBackgroundCheckProvider, QsaSociosMockProvider>();

        // Engine de Regras e Orquestrador
        services.AddSingleton<IRiskPolicyEngine, RiskPolicyEngine>();
        services.AddScoped<ICheckOrchestrator, CheckOrchestrator>();

        // Security, Context & Masking
        services.AddSingleton<IDataMasker, DataMasker>();
        services.AddScoped<CurrentTenantContext>();
        services.AddScoped<ICurrentTenantContext>(sp => sp.GetRequiredService<CurrentTenantContext>());

        // Webhooks & Http
        services.AddHttpClient<IWebhookDispatcher, WebhookDispatcher>();
        services.AddHttpClient<GeminiAiAssistantService>();

        // Assistente de IA (Gemini com fallback Heurístico)
        services.AddSingleton<HeuristicFallbackAiService>();
        services.AddScoped<IAiAssistantService>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            var fallback = sp.GetRequiredService<HeuristicFallbackAiService>();
            var apiKey = configuration["GEMINI_API_KEY"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            var logger = sp.GetRequiredService<ILogger<GeminiAiAssistantService>>();

            return new GeminiAiAssistantService(httpClient, fallback, apiKey, logger);
        });

        // Fila e Background Workers
        services.AddSingleton<ICheckQueue, InMemoryCheckQueue>();

        var enableWorkers = configuration.GetValue<bool>("Workers:Enabled", true);
        if (enableWorkers)
        {
            services.AddHostedService<CheckOrchestrationWorker>();
            services.AddHostedService<DataRetentionPurgeWorker>();
        }

        // Use Cases
        services.AddScoped<CreateCheckUseCase>();
        services.AddScoped<GetCheckByIdUseCase>();
        services.AddScoped<ReviewCheckUseCase>();
        services.AddScoped<PurgeExpiredChecksUseCase>();
        services.AddScoped<CreateTenantUseCase>();
        services.AddScoped<GenerateApiKeyUseCase>();
        services.AddScoped<GetTenantUsageUseCase>();
        services.AddScoped<GetAuditLogsUseCase>();

        return services;
    }

    public static async Task MigrateDatabaseAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<KycKybDbContext>>();
        var context = services.GetRequiredService<KycKybDbContext>();

        try
        {
            if (context.Database.IsRelational())
            {
                logger.LogInformation("Aplicando migrações relacionais (PostgreSQL)...");
                await context.Database.MigrateAsync();
            }
            else
            {
                logger.LogInformation("Criando banco em memória...");
                await context.Database.EnsureCreatedAsync();
            }

            await DatabaseSeeder.SeedAsync(context, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro durante a inicialização/migração do banco de dados.");
            throw;
        }
    }
}

