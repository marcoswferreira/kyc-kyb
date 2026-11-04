using System.Text.RegularExpressions;
using Domain.Common;

namespace Domain.ValueObjects;

public sealed record Cpf
{
    public string Value { get; }

    private Cpf(string value)
    {
        Value = value;
    }

    public static Result<Cpf> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result<Cpf>.Failure("O CPF não pode ser vazio.", "INVALID_CPF_EMPTY");
        }

        var digitsOnly = Regex.Replace(input, @"[^\d]", "");

        if (digitsOnly.Length != 11)
        {
            return Result<Cpf>.Failure("O CPF deve conter exatamente 11 dígitos numéricos.", "INVALID_CPF_LENGTH");
        }

        // Rejeita sequências repetidas conhecidas (00000000000, 11111111111, etc.)
        if (digitsOnly.Distinct().Count() == 1)
        {
            return Result<Cpf>.Failure("O CPF não pode ser composto por dígitos repetidos.", "INVALID_CPF_REPEATED");
        }

        // Validação do 1º Dígito Verificador (pesos 10 a 2)
        int sum1 = 0;
        for (int i = 0; i < 9; i++)
        {
            sum1 += (digitsOnly[i] - '0') * (10 - i);
        }

        int remainder1 = sum1 % 11;
        int expectedDv1 = remainder1 < 2 ? 0 : 11 - remainder1;

        if ((digitsOnly[9] - '0') != expectedDv1)
        {
            return Result<Cpf>.Failure("O primeiro dígito verificador do CPF é inválido.", "INVALID_CPF_DV1");
        }

        // Validação do 2º Dígito Verificador (pesos 11 a 2)
        int sum2 = 0;
        for (int i = 0; i < 10; i++)
        {
            sum2 += (digitsOnly[i] - '0') * (11 - i);
        }

        int remainder2 = sum2 % 11;
        int expectedDv2 = remainder2 < 2 ? 0 : 11 - remainder2;

        if ((digitsOnly[10] - '0') != expectedDv2)
        {
            return Result<Cpf>.Failure("O segundo dígito verificador do CPF é inválido.", "INVALID_CPF_DV2");
        }

        return Result<Cpf>.Success(new Cpf(digitsOnly));
    }

    public string Formatted =>
        $"{Value[..3]}.{Value.Substring(3, 3)}.{Value.Substring(6, 3)}-{Value.Substring(9, 2)}";

    public string Masked =>
        $"***.{Value.Substring(3, 3)}.{Value.Substring(6, 3)}-**";

    public override string ToString() => Value;
}

