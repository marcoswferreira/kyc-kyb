using System.Security.Cryptography;
using System.Text;
using Domain.Common;

namespace Domain.ValueObjects;

public sealed record ApiKey
{
    public string Key { get; }
    public string Hash { get; }
    public string Prefix { get; }

    private ApiKey(string key, string hash, string prefix)
    {
        Key = key;
        Hash = hash;
        Prefix = prefix;
    }

    public static ApiKey Generate(string environment = "live")
    {
        var randomBytes = new byte[24];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        var base64 = Convert.ToBase64String(randomBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");

        var key = $"sk_{environment}_{base64}";
        var hash = ComputeHash(key);
        var prefix = key.Length >= 12 ? key[..12] : key;

        return new ApiKey(key, hash, prefix);
    }

    public static string ComputeHash(string rawApiKey)
    {
        var bytes = Encoding.UTF8.GetBytes(rawApiKey);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static bool Verify(string rawApiKey, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(rawApiKey) || string.IsNullOrWhiteSpace(expectedHash))
        {
            return false;
        }

        var actualHash = ComputeHash(rawApiKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actualHash),
            Encoding.UTF8.GetBytes(expectedHash));
    }
}
