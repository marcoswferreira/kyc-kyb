using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Interfaces;
using Domain.Enums;
using Infrastructure.Resilience;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Orchestration;

public class CheckOrchestrator : ICheckOrchestrator
{
    private readonly IEnumerable<IBackgroundCheckProvider> _providers;
    private readonly ICheckCache _cache;
    private readonly ICircuitBreaker _circuitBreaker;
    private readonly ILogger<CheckOrchestrator> _logger;
    private readonly TimeSpan _providerTimeout = TimeSpan.FromMilliseconds(800);

    public CheckOrchestrator(
        IEnumerable<IBackgroundCheckProvider> providers,
        ICheckCache cache,
        ICircuitBreaker circuitBreaker,
        ILogger<CheckOrchestrator> logger)
    {
        _providers = providers;
        _cache = cache;
        _circuitBreaker = circuitBreaker;
        _logger = logger;
    }

    public async Task<OrchestratorResult> ExecuteAllProvidersAsync(
        string documentNumber,
        SubjectType subjectType,
        CancellationToken cancellationToken = default)
    {
        var tasks = _providers.Select(provider => ExecuteSingleProviderSafeAsync(provider, documentNumber, subjectType, cancellationToken));
        var results = await Task.WhenAll(tasks);

        bool hasPartialWarnings = results.Any(r => !r.IsSuccess);

        // Agregação das evidências brutas
        var evidenceMap = results.ToDictionary(
            r => r.ProviderName,
            r => new
            {
                status = r.Status.ToString(),
                is_success = r.IsSuccess,
                latency_ms = r.LatencyMs,
                error = r.ErrorMessage,
                data = string.IsNullOrWhiteSpace(r.RawDataJson) ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(r.RawDataJson)
            });

        var rawEvidenceJson = JsonSerializer.Serialize(evidenceMap, new JsonSerializerOptions { WriteIndented = false });
        var evidenceSha256 = ComputeSha256(rawEvidenceJson);

        return new OrchestratorResult(results, hasPartialWarnings, rawEvidenceJson, evidenceSha256);
    }

    private async Task<ProviderResult> ExecuteSingleProviderSafeAsync(
        IBackgroundCheckProvider provider,
        string documentNumber,
        SubjectType subjectType,
        CancellationToken cancellationToken)
    {
        // 1. Tenta recuperar do cache
        try
        {
            var cachedJson = await _cache.GetAsync(provider.Name, documentNumber);
            if (!string.IsNullOrWhiteSpace(cachedJson))
            {
                return new ProviderResult(provider.Name, ProviderExecutionStatus.CacheHit, true, 1, cachedJson);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha na leitura do cache para {Provider}", provider.Name);
        }

        // 2. Execução com Circuit Breaker e Timeout Guard
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_providerTimeout);

        try
        {
            var result = await _circuitBreaker.ExecuteAsync(async () =>
            {
                return await provider.QueryAsync(documentNumber, subjectType, cts.Token);
            }, cancellationToken);

            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.RawDataJson))
            {
                await _cache.SetAsync(provider.Name, documentNumber, result.RawDataJson, TimeSpan.FromHours(1));
            }

            return result;
        }
        catch (CircuitBreakerOpenException)
        {
            _logger.LogWarning("Circuit Breaker aberto para o provedor {Provider}", provider.Name);
            return ProviderResult.CircuitBroken(provider.Name);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Timeout estourado ao consultar o provedor {Provider}", provider.Name);
            return ProviderResult.Timeout(provider.Name, (int)_providerTimeout.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao consultar o provedor {Provider}", provider.Name);
            return ProviderResult.Error(provider.Name, ex.Message, 0);
        }
    }

    private static string ComputeSha256(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
