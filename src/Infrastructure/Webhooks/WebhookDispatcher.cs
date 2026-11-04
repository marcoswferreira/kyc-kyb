using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Dtos;
using Application.Interfaces;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Webhooks;

public class WebhookDispatcher : IWebhookDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly IWebhookSubscriptionRepository _subscriptionRepository;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(
        HttpClient httpClient,
        IWebhookSubscriptionRepository subscriptionRepository,
        ILogger<WebhookDispatcher> logger)
    {
        _httpClient = httpClient;
        _subscriptionRepository = subscriptionRepository;
        _logger = logger;
    }

    public async Task DispatchAsync(BackgroundCheck check, CancellationToken cancellationToken = default)
    {
        var subscriptions = await _subscriptionRepository.GetActiveByTenantAsync(check.TenantId, cancellationToken);
        if (!subscriptions.Any())
        {
            return;
        }

        var payload = new AtsWebhookPayload(
            Event: "check.completed",
            CheckId: check.Id,
            CandidateDocumentMasked: check.DocumentMasked,
            OverallRisk: check.OverallRisk,
            HumanReviewStatus: check.HumanReviewStatus,
            Status: check.Status.ToString(),
            AiSummary: check.AiSummary,
            VerificationUrl: $"/api/v1/checks/{check.Id}",
            Timestamp: DateTime.UtcNow);

        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        foreach (var sub in subscriptions)
        {
            try
            {
                var signature = ComputeHmacSha256(payloadBytes, sub.Secret);
                var targetUri = sub.TargetUrl.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(sub.TargetUrl).PathAndQuery
                    : sub.TargetUrl;

                using var request = new HttpRequestMessage(HttpMethod.Post, targetUri)
                {
                    Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
                };

                request.Headers.Add("X-Signature-SHA256", signature);
                request.Headers.Add("X-Event", "check.completed");
                request.Headers.Add("X-Tenant-Id", check.TenantId.ToString());

                var response = await _httpClient.SendAsync(request, cancellationToken);
                _logger.LogInformation(
                    "Webhook enviado para {TargetUrl} com resultado {StatusCode}",
                    sub.TargetUrl,
                    response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao despachar webhook para {TargetUrl}", sub.TargetUrl);
            }
        }
    }

    public static string ComputeHmacSha256(byte[] data, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
