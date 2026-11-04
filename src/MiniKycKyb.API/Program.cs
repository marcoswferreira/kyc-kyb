using Infrastructure.Extensions;
using MiniKycKyb.API.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Configuração dos Serviços de Infraestrutura e Casos de Uso
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Migração e Seeding do Banco de Dados
await app.MigrateDatabaseAsync();

// Pipeline de Middlewares
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<ApiKeyAuthMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

// Necessário para CustomWebApplicationFactory em IntegrationTests
public partial class Program { }
