using System.Text.Json;
using Application.Interfaces;
using Domain.Enums;

namespace Infrastructure.Providers;

public class ReceitaFederalMockProvider : IBackgroundCheckProvider
{
    public string Name => "ReceitaFederalMockProvider";

    public async Task<ProviderResult> QueryAsync(string documentNumber, SubjectType subjectType, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // Injeção de Falha/Caos para Testes
        if (documentNumber.EndsWith("999"))
        {
            await Task.Delay(150, cancellationToken);
            return ProviderResult.Error(Name, "Receita Federal indisponível (Simulação de Erro 503).", 150);
        }

        if (documentNumber.EndsWith("8888"))
        {
            // Simula timeout
            await Task.Delay(2000, cancellationToken);
            return ProviderResult.Timeout(Name, 2000);
        }

        await Task.Delay(20, cancellationToken);

        string situacao = "REGULAR";
        if (documentNumber.Contains("444"))
        {
            situacao = "PENDENTE DE REGULARIZACAO";
        }
        else if (documentNumber.EndsWith("000") || documentNumber.Contains("666"))
        {
            situacao = subjectType == SubjectType.Person ? "CANCELADA" : "INAPTA";
        }

        var data = new
        {
            provedor = Name,
            documento = documentNumber,
            tipo = subjectType.ToString(),
            situacao_cadastral = situacao,
            data_situacao = "2026-08-15T00:00:00Z",
            razao_social_nome = subjectType == SubjectType.Person ? "Candidato Sintético RFB" : "Empresa Alfa Tecnologia Sintética Ltda",
            data_abertura_nascimento = "2010-03-21T00:00:00Z",
            natureza_juridica = subjectType == SubjectType.Company ? "206-2 - Sociedade Empresária Limitada" : null
        };

        var latency = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        return ProviderResult.Success(Name, JsonSerializer.Serialize(data), latency);
    }
}

