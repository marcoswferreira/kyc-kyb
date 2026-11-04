namespace Infrastructure.Resilience;

public interface ICheckCache
{
    Task<string?> GetAsync(string providerName, string documentNumber);
    Task SetAsync(string providerName, string documentNumber, string jsonContent, TimeSpan ttl);
}

