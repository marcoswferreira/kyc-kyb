using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Infrastructure.Security;

namespace MiniKycKyb.API.Middlewares;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private static readonly ConcurrentDictionary<Guid, RateTracker> Trackers = new();

    private class RateTracker
    {
        private readonly object _lock = new();
        private DateTime _windowStart = DateTime.UtcNow;
        private int _requestCount;

        public bool AllowRequest(int limitPerMinute)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                if (now - _windowStart >= TimeSpan.FromMinutes(1))
                {
                    _windowStart = now;
                    _requestCount = 0;
                }

                if (_requestCount >= limitPerMinute)
                {
                    return false;
                }

                _requestCount++;
                return true;
            }
        }
    }

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, CurrentTenantContext currentTenant)
    {
        if (currentTenant.TenantId.HasValue)
        {
            var tracker = Trackers.GetOrAdd(currentTenant.TenantId.Value, _ => new RateTracker());
            // Limite padrão de 120 req/min
            if (!tracker.AllowRequest(120))
            {
                _logger.LogWarning("Rate limit excedido para o Tenant {TenantId}", currentTenant.TenantId.Value);

                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.ContentType = "application/json";
                context.Response.Headers.Append("Retry-After", "60");

                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    error = "Limite de requisições excedido (Rate Limit: 120 req/min). Tente novamente em instantes."
                }));
                return;
            }
        }

        await _next(context);
    }
}

