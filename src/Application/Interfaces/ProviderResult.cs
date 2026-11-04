using Domain.Enums;

namespace Application.Interfaces;

public record ProviderResult(
    string ProviderName,
    ProviderExecutionStatus Status,
    bool IsSuccess,
    int LatencyMs,
    string? RawDataJson,
    string? ErrorMessage = null)
{
    public static ProviderResult Success(string providerName, string rawDataJson, int latencyMs) =>
        new(providerName, ProviderExecutionStatus.Success, true, latencyMs, rawDataJson);

    public static ProviderResult Timeout(string providerName, int latencyMs) =>
        new(providerName, ProviderExecutionStatus.Timeout, false, latencyMs, null, "Tempo limite de resposta excedido.");

    public static ProviderResult CircuitBroken(string providerName) =>
        new(providerName, ProviderExecutionStatus.CircuitBroken, false, 0, null, "Provedor temporariamente indisponível (Circuit Breaker aberto).");

    public static ProviderResult Error(string providerName, string error, int latencyMs) =>
        new(providerName, ProviderExecutionStatus.Error, false, latencyMs, null, error);
}

