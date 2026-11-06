using Infrastructure.Extensions;
using Microsoft.OpenApi;
using MiniKycKyb.API.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Configuração dos Serviços de Infraestrutura e Casos de Uso
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Mini-Plataforma KYC/KYB Background Check API",
            Version = "v1",
            Description = "API corporativa para verificação cadastral (KYC/KYB), análise de risco regulatório e governança LGPD."
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            Name = "X-API-Key",
            In = ParameterLocation.Header,
            Description = "Chave de API do Tenant (Padrão: sk_live_master_kyc_default_key_12345)"
        };

        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "API Key",
            Description = "Chave de API via Bearer Token (Padrão: sk_live_master_kyc_default_key_12345)"
        };

        document.Components.SecuritySchemes["UserRole"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            Name = "X-User-Role",
            In = ParameterLocation.Header,
            Description = "Perfil RBAC para testes de mascaramento (ComplianceOfficer, Analyst, Auditor, Operations)"
        };

        document.Security ??= new List<OpenApiSecurityRequirement>();
        var requirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("ApiKey", document)] = new List<string>(),
            [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>(),
            [new OpenApiSecuritySchemeReference("UserRole", document)] = new List<string>()
        };
        document.Security.Add(requirement);

        return Task.CompletedTask;
    });
});

var app = builder.Build();

// Migração e Seeding do Banco de Dados
await app.MigrateDatabaseAsync();

// Pipeline de Middlewares
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<ApiKeyAuthMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "MiniKycKyb API v1");
    options.RoutePrefix = "swagger";
});

app.MapControllers();

app.Run();

// Necessário para CustomWebApplicationFactory em IntegrationTests
public partial class Program { }
