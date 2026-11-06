using System.Text.RegularExpressions;

namespace Infrastructure.Logging;

/// <summary>
/// Sanitizador de PII para mascaramento automático de documentos pessoais e credenciais em logs.
/// </summary>
public sealed class PiiSanitizer : IPiiSanitizer
{
    private static readonly Regex CpfFormattedRegex = new(@"\b\d{3}\.\d{3}\.\d{3}-\d{2}\b", RegexOptions.Compiled);
    private static readonly Regex CpfUnformattedRegex = new(@"(?i)(cpf|documento|doc|taxid|subject)\s*[:=]\s*[""']?(\d{11})[""']?", RegexOptions.Compiled);
    private static readonly Regex CnpjFormattedRegex = new(@"\b\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}\b", RegexOptions.Compiled);
    private static readonly Regex CnpjUnformattedRegex = new(@"(?i)(cnpj|company_doc)\s*[:=]\s*[""']?(\d{14})[""']?", RegexOptions.Compiled);
    private static readonly Regex ApiKeyRegex = new(@"sk_live_[a-zA-Z0-9_]{10,}", RegexOptions.Compiled);
    private static readonly Regex BearerRegex = new(@"(?i)bearer\s+[a-zA-Z0-9_\-\.]{15,}", RegexOptions.Compiled);

    public string Sanitize(string? rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return string.Empty;
        }

        var sanitized = rawMessage;

        // Mascarar CPF formatado
        sanitized = CpfFormattedRegex.Replace(sanitized, m =>
        {
            var val = m.Value;
            return $"***.{val.Substring(4, 7)}-**";
        });

        // Mascarar CPF puro quando precedido por identificador
        sanitized = CpfUnformattedRegex.Replace(sanitized, m =>
        {
            var prefix = m.Groups[1].Value;
            var num = m.Groups[2].Value;
            var masked = $"***.{num.Substring(3, 3)}.{num.Substring(6, 3)}-**";
            return $"{prefix}: \"{masked}\"";
        });

        // Mascarar CNPJ formatado
        sanitized = CnpjFormattedRegex.Replace(sanitized, m =>
        {
            var val = m.Value;
            return $"**.{val.Substring(3, 7)}/****-**";
        });

        // Mascarar CNPJ puro
        sanitized = CnpjUnformattedRegex.Replace(sanitized, m =>
        {
            var prefix = m.Groups[1].Value;
            var num = m.Groups[2].Value;
            var masked = $"**.{num.Substring(2, 3)}.{num.Substring(5, 3)}/****-**";
            return $"{prefix}: \"{masked}\"";
        });

        // Mascarar Chaves de API e Tokens Bearer
        sanitized = ApiKeyRegex.Replace(sanitized, "sk_live_[REDACTED_API_KEY]");
        sanitized = BearerRegex.Replace(sanitized, "Bearer [REDACTED_TOKEN]");

        return sanitized;
    }
}
