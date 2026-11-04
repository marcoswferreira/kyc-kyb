using System.Text.RegularExpressions;
using Domain.Common;

namespace Domain.ValueObjects;

/// <summary>
/// Representa um CNPJ (Cadastro Nacional da Pessoa Jurídica), com suporte retrocompatível
/// ao formato numérico tradicional e ao novo CNPJ Alfanumérico regulado pela Instrução Normativa RFB nº 2.229/2024.
/// </summary>
public sealed record Cnpj
{
    private static readonly int[] WeightsDv1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] WeightsDv2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    public string Value { get; }
    public bool IsAlphanumeric { get; }

    private Cnpj(string value, bool isAlphanumeric)
    {
        Value = value;
        IsAlphanumeric = isAlphanumeric;
    }

    public static Result<Cnpj> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result<Cnpj>.Failure("O CNPJ não pode ser vazio.", "INVALID_CNPJ_EMPTY");
        }

        // Remove pontuações comuns: ., /, - e espaços em branco
        var clean = Regex.Replace(input.Trim().ToUpperInvariant(), @"[\.\/\-\s]", "");

        if (clean.Length != 14)
        {
            return Result<Cnpj>.Failure("O CNPJ deve conter exatamente 14 caracteres (12 posições cadastrais + 2 dígitos verificadores).", "INVALID_CNPJ_LENGTH");
        }

        // As primeiras 12 posições devem ser exclusivamente caracteres alfanuméricos (0-9, A-Z)
        for (int i = 0; i < 12; i++)
        {
            char c = clean[i];
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z')))
            {
                return Result<Cnpj>.Failure($"O caractere '{c}' na posição {i + 1} não é permitido para o CNPJ.", "INVALID_CNPJ_CHARACTER");
            }
        }

        // As 2 últimas posições (dígitos verificadores) devem ser estritamente numéricas
        if (!char.IsDigit(clean[12]) || !char.IsDigit(clean[13]))
        {
            return Result<Cnpj>.Failure("Os dois dígitos verificadores do CNPJ devem ser numéricos (0-9).", "INVALID_CNPJ_DV_NOT_NUMERIC");
        }

        // Se for puramente numérico, rejeita sequências repetidas conhecidas (00000000000000, etc.)
        bool isPurelyNumeric = clean.All(char.IsDigit);
        if (isPurelyNumeric && clean.Distinct().Count() == 1)
        {
            return Result<Cnpj>.Failure("O CNPJ não pode ser composto por dígitos idênticos repetidos.", "INVALID_CNPJ_REPEATED");
        }

        // 1º Dígito Verificador
        int sum1 = 0;
        for (int i = 0; i < 12; i++)
        {
            int charVal = GetCharacterValue(clean[i]);
            sum1 += charVal * WeightsDv1[i];
        }

        int remainder1 = sum1 % 11;
        int expectedDv1 = remainder1 < 2 ? 0 : 11 - remainder1;

        if ((clean[12] - '0') != expectedDv1)
        {
            return Result<Cnpj>.Failure("O primeiro dígito verificador do CNPJ é inválido.", "INVALID_CNPJ_DV1");
        }

        // 2º Dígito Verificador (inclui o 1º DV na 13ª posição)
        int sum2 = 0;
        for (int i = 0; i < 13; i++)
        {
            int charVal = GetCharacterValue(clean[i]);
            sum2 += charVal * WeightsDv2[i];
        }

        int remainder2 = sum2 % 11;
        int expectedDv2 = remainder2 < 2 ? 0 : 11 - remainder2;

        if ((clean[13] - '0') != expectedDv2)
        {
            return Result<Cnpj>.Failure("O segundo dígito verificador do CNPJ é inválido.", "INVALID_CNPJ_DV2");
        }

        bool isAlphanumeric = clean.Take(12).Any(c => c >= 'A' && c <= 'Z');

        return Result<Cnpj>.Success(new Cnpj(clean, isAlphanumeric));
    }

    /// <summary>
    /// Regra da IN RFB nº 2.229/2024:
    /// '0'-'9': Mantém o próprio valor numérico (0 a 9).
    /// 'A'-'Z': Valor correspondente ao código ASCII menos 48 (A=17, B=18, ... Z=42).
    /// </summary>
    public static int GetCharacterValue(char c)
    {
        return (c >= '0' && c <= '9') ? (c - '0') : (c - 48);
    }

    /// <summary>
    /// Retorna o CNPJ formatado no padrão clássico: 12.345.678/0001-90 ou 12.ABC.345/0001-90.
    /// </summary>
    public string Formatted =>
        $"{Value[..2]}.{Value.Substring(2, 3)}.{Value.Substring(5, 3)}/{Value.Substring(8, 4)}-{Value.Substring(12, 2)}";

    /// <summary>
    /// Retorna o CNPJ mascarado para proteção e minimização segundo a LGPD.
    /// </summary>
    public string Masked =>
        $"**.{Value.Substring(2, 3)}.***{Value.Substring(8, 4)}-**";

    public override string ToString() => Value;
}
