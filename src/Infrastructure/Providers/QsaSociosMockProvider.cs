using System.Text.Json;
using Application.Interfaces;
using Domain.Enums;

namespace Infrastructure.Providers;

public class QsaSociosMockProvider : IBackgroundCheckProvider
{
    public string Name => "QsaSociosMockProvider";

    public async Task<ProviderResult> QueryAsync(string documentNumber, SubjectType subjectType, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        await Task.Delay(20, cancellationToken);

        if (subjectType != SubjectType.Company)
        {
            // Não se aplica a Pessoa Física
            return ProviderResult.Success(Name, "{\"provedor\":\"QsaSociosMockProvider\",\"status\":\"NOT_APPLICABLE_FOR_PERSON\"}", 5);
        }

        bool hasPepSocio = documentNumber.Contains("777");

        var socios = new List<object>
        {
            new
            {
                nome = "Carlos Eduardo Ferreira Sintético",
                cpf_mascarado = "***.432.109-**",
                qualificacao = "Sócio-Administrador",
                participacao_percentual = 60.0,
                is_pep = hasPepSocio
            },
            new
            {
                nome = "Mariana Alves Sintética",
                cpf_mascarado = "***.876.543-**",
                qualificacao = "Sócia Cotista",
                participacao_percentual = 40.0,
                is_pep = false
            }
        };

        var data = new
        {
            provedor = Name,
            documento = documentNumber,
            socios
        };

        var latency = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        return ProviderResult.Success(Name, JsonSerializer.Serialize(data), latency);
    }
}

