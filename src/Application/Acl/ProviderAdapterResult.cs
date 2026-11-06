namespace Application.Acl;

/// <summary>
/// Resultado padronizado retornado pela Camada Anticorrupção (ACL).
/// Isola status HTTP, deserialização e metadados de observabilidade do provedor externo.
/// </summary>
public sealed record ProviderAdapterResult<T>(
    bool IsSuccess,
    string ProviderKey,
    T? Data,
    string? RawJson,
    string? ErrorMessage,
    int LatencyMs);

