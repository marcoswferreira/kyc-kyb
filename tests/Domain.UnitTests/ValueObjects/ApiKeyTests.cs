using Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Domain.UnitTests.ValueObjects;

public class ApiKeyTests
{
    [Fact]
    public void Generate_ShouldReturnValidApiKeyWithLivePrefix()
    {
        var apiKey = ApiKey.Generate("live");

        apiKey.Key.Should().StartWith("sk_live_");
        apiKey.Prefix.Should().StartWith("sk_live_");
        apiKey.Hash.Should().HaveLength(64); // SHA-256 hex string
    }

    [Fact]
    public void Verify_WithMatchingKey_ShouldReturnTrue()
    {
        var apiKey = ApiKey.Generate("test");

        var isValid = ApiKey.Verify(apiKey.Key, apiKey.Hash);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void Verify_WithTamperedKey_ShouldReturnFalse()
    {
        var apiKey = ApiKey.Generate("live");

        var isValid = ApiKey.Verify(apiKey.Key + "tampered", apiKey.Hash);

        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "abc")]
    [InlineData("sk_live_123", "")]
    [InlineData(null, null)]
    public void Verify_WithInvalidInput_ShouldReturnFalse(string? key, string? hash)
    {
        var isValid = ApiKey.Verify(key!, hash!);

        isValid.Should().BeFalse();
    }
}

