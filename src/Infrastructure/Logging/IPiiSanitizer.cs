namespace Infrastructure.Logging;

/// <summary>
/// Contrato para sanitização de dados sensíveis (PII / LGPD) antes de envio para logs ou telemetria.
/// Impede que CPFs, CNPJs ou credenciais vazem em logs da aplicação.
/// </summary>
public interface IPiiSanitizer
{
    string Sanitize(string? rawMessage);
}

