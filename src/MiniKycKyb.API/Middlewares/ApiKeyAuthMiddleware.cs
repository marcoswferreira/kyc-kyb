using System.Net;
using System.Text.Json;
using Domain.Enums;
using Domain.Repositories;
using Domain.ValueObjects;
using Infrastructure.Security;

namespace MiniKycKyb.API.Middlewares;

public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;

    public ApiKeyAuthMiddleware(RequestDelegate next, ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantRepository tenantRepository, CurrentTenantContext currentTenant)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Endpoints públicos ou de documentação
        if (path == "/" ||
            path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/v1/webhooks/providers", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/ats/callback", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/ats/received-callbacks", StringComparison.OrdinalIgnoreCase) ||
            (path.Equals("/api/v1/tenants", StringComparison.OrdinalIgnoreCase) && context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        string? apiKey = null;
        if (context.Request.Headers.TryGetValue("X-API-Key", out var headerKey))
        {
            apiKey = headerKey.ToString();
        }
        else if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var authStr = authHeader.ToString();
            if (authStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                apiKey = authStr["Bearer ".Length..].Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "Chave de API obrigatória ausente. Informe via header 'X-API-Key' ou 'Authorization: Bearer <chave>'."
            }));
            return;
        }

        var keyHash = ApiKey.ComputeHash(apiKey);
        var tenant = await tenantRepository.GetByApiKeyHashAsync(keyHash);

        if (tenant is null || !tenant.IsActive)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "Chave de API inválida ou tenant inativo."
            }));
            return;
        }

        // Configuração do contexto do tenant autenticado
        currentTenant.TenantId = tenant.Id;
        currentTenant.IpAddress = context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        // Determinação de perfil (RBAC) via header X-User-Role ou padrão ComplianceOfficer
        if (context.Request.Headers.TryGetValue("X-User-Role", out var roleHeader) &&
            Enum.TryParse<UserRole>(roleHeader.ToString(), true, out var parsedRole))
        {
            currentTenant.Role = parsedRole;
        }
        else
        {
            currentTenant.Role = UserRole.ComplianceOfficer;
        }

        // Ator identificado
        if (context.Request.Headers.TryGetValue("X-Actor-Id", out var actorHeader))
        {
            currentTenant.ActorId = actorHeader.ToString();
        }
        else
        {
            currentTenant.ActorId = $"api_client_{tenant.ApiKeyPrefix}";
        }

        await _next(context);
    }
}
