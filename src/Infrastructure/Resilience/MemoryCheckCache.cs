using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Resilience;

public class MemoryCheckCache : ICheckCache
{
    private record CacheEntry(string Content, DateTime ExpiresAt);
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    public Task<string?> GetAsync(string providerName, string documentNumber)
    {
        var key = ComputeKey(providerName, documentNumber);
        if (_cache.TryGetValue(key, out var entry))
        {
            if (DateTime.UtcNow <= entry.ExpiresAt)
            {
                return Task.FromResult<string?>(entry.Content);
            }

            _cache.TryRemove(key, out _);
        }

        return Task.FromResult<string?>(null);
    }

    public Task SetAsync(string providerName, string documentNumber, string jsonContent, TimeSpan ttl)
    {
        var key = ComputeKey(providerName, documentNumber);
        var entry = new CacheEntry(jsonContent, DateTime.UtcNow.Add(ttl));
        _cache[key] = entry;
        return Task.CompletedTask;
    }

    private static string ComputeKey(string providerName, string documentNumber)
    {
        var raw = $"{providerName}:{documentNumber}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }
}

