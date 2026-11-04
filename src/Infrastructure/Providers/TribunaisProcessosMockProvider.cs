using System.Text.Json;
using Application.Interfaces;
using Domain.Enums;

namespace Infrastructure.Providers;

public class TribunaisProcessosMockProvider : IBackgroundCheckProvider
{
    public string Name => "TribunaisProcessosMockProvider";

    public async Task<ProviderResult> QueryAsync(string documentNumber, SubjectType subjectType, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        if (documentNumber.EndsWith("999"))
        {
            await Task.Delay(100, cancellationToken);
            return ProviderResult.Error(Name, "Falha de conectividade com Tribunal Regional.", 100);
        }

        await Task.Delay(30, cancellationToken);

        var processos = new List<object>();

        // Apontamento Crítico (Criminal ativo)
        if (documentNumber.Contains("666") || documentNumber.Contains("911"))
        {
            processos.Add(new
            {
                tipo = "Criminal",
                numero = "0012345-89.2025.8.26.0100",
                tribunal = "TJSP",
                comarca = "São Paulo / Foro Central Criminal",
                status = "Em andamento",
                assunto = "Crimes contra o Sistema Financeiro Nacional e Lavagem de Dinheiro",
                valor_causa = 1500000.00
            });
        }

        // Apontamento Amarelo (Trabalhista ou cível arquivado)
        if (documentNumber.Contains("444") || documentNumber.Contains("222"))
        {
            processos.Add(new
            {
                tipo = "Trabalhista",
                numero = "1000456-12.2023.5.02.0001",
                tribunal = "TRT2",
                comarca = "São Paulo",
                status = "Arquivado definitivamente",
                assunto = "Horas Extras e Adicional Noturno",
                valor_causa = 25000.00
            });
        }

        var data = new
        {
            provedor = Name,
            documento = documentNumber,
            total_processos = processos.Count,
            processos
        };

        var latency = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        return ProviderResult.Success(Name, JsonSerializer.Serialize(data), latency);
    }
}

