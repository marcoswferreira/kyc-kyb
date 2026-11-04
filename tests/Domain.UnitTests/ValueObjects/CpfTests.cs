using Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Domain.UnitTests.ValueObjects;

public class CpfTests
{
    [Theory]
    [InlineData("52998224725")]
    [InlineData("529.982.247-25")]
    [InlineData("11144477735")]
    [InlineData("111.444.777-35")]
    public void Create_WithValidCpf_ShouldSucceed(string input)
    {
        var result = Cpf.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Value.Should().HaveLength(11);
    }

    [Fact]
    public void FormattedAndMasked_ShouldFormatCorrectly()
    {
        var result = Cpf.Create("52998224725");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Formatted.Should().Be("529.982.247-25");
        result.Value.Masked.Should().Be("***.982.247-**");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyInput_ShouldFail(string? input)
    {
        var result = Cpf.Create(input);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_CPF_EMPTY");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789012")]
    public void Create_WithInvalidLength_ShouldFail(string input)
    {
        var result = Cpf.Create(input);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_CPF_LENGTH");
    }

    [Theory]
    [InlineData("00000000000")]
    [InlineData("11111111111")]
    [InlineData("99999999999")]
    public void Create_WithRepeatedDigits_ShouldFail(string input)
    {
        var result = Cpf.Create(input);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_CPF_REPEATED");
    }

    [Theory]
    [InlineData("52998224720")] // DV2 inválido
    [InlineData("52998224715")] // DV1 inválido
    public void Create_WithInvalidCheckDigits_ShouldFail(string input)
    {
        var result = Cpf.Create(input);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().StartWith("INVALID_CPF_DV");
    }
}

