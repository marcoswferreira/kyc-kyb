using Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Domain.UnitTests.ValueObjects;

public class CnpjAlfanumericoTests
{
    [Theory]
    [InlineData("11222333000181")]
    [InlineData("11.222.333/0001-81")]
    [InlineData("00000000000191")] // Banco do Brasil tradicional
    public void Create_WithValidTraditionalCnpj_ShouldSucceedAndMarkAsNotAlphanumeric(string input)
    {
        var result = Cnpj.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.IsAlphanumeric.Should().BeFalse();
        result.Value.Value.Should().HaveLength(14);
    }

    [Theory]
    [InlineData("12ABC345000188")]
    [InlineData("12.ABC.345/0001-88")]
    public void Create_WithValidAlphanumericCnpj_ShouldSucceedAndMarkAsAlphanumeric(string input)
    {
        var result = Cnpj.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.IsAlphanumeric.Should().BeTrue();
        result.Value.Value.Should().Be("12ABC345000188");
        result.Value.Formatted.Should().Be("12.ABC.345/0001-88");
    }

    [Fact]
    public void Masked_ShouldObfuscateForLgpdCompliance()
    {
        var result = Cnpj.Create("12ABC345000188");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Masked.Should().Be("**.ABC.***0001-**");
    }

    [Fact]
    public void GetCharacterValue_ShouldFollowInRfb2229Specification()
    {
        // 0 a 9 mantêm seu valor
        Cnpj.GetCharacterValue('0').Should().Be(0);
        Cnpj.GetCharacterValue('5').Should().Be(5);
        Cnpj.GetCharacterValue('9').Should().Be(9);

        // Letras maiúsculas: ASCII - 48
        // 'A' (65) - 48 = 17
        Cnpj.GetCharacterValue('A').Should().Be(17);
        // 'B' (66) - 48 = 18
        Cnpj.GetCharacterValue('B').Should().Be(18);
        // 'Z' (90) - 48 = 42
        Cnpj.GetCharacterValue('Z').Should().Be(42);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyInput_ShouldFail(string? input)
    {
        var result = Cnpj.Create(input);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_CNPJ_EMPTY");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1122233300018")]
    [InlineData("112223330001819")]
    public void Create_WithInvalidLength_ShouldFail(string input)
    {
        var result = Cnpj.Create(input);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_CNPJ_LENGTH");
    }

    [Fact]
    public void Create_WithNonAlphanumericSpecialCharsInRoot_ShouldFail()
    {
        var result = Cnpj.Create("12@BC345000188");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_CNPJ_CHARACTER");
    }

    [Fact]
    public void Create_WithLetterInCheckDigits_ShouldFail()
    {
        var result = Cnpj.Create("12ABC34500018A");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_CNPJ_DV_NOT_NUMERIC");
    }

    [Theory]
    [InlineData("11222333000180")] // DV2 inválido tradicional
    [InlineData("11222333000101")] // DV1 inválido tradicional
    [InlineData("12ABC345000189")] // DV2 inválido alfanumérico
    [InlineData("12ABC345000178")] // DV1 inválido alfanumérico
    public void Create_WithInvalidCheckDigits_ShouldFail(string input)
    {
        var result = Cnpj.Create(input);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().StartWith("INVALID_CNPJ_DV");
    }

    [Fact]
    public void Create_WithRepeatedNumbers_ShouldFail()
    {
        var result = Cnpj.Create("11111111111111");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_CNPJ_REPEATED");
    }
}

