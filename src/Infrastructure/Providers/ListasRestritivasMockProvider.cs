using System.Text.Json;
using Application.Interfaces;
using Domain.Enums;

namespace Infrastructure.Providers;

public class ListasRestritivasMockProvider : IBackgroundCheckProvider
{
    public string Name => "ListasRestritivasMockProvider";

    public async Task<ProviderResult> QueryAsync(string documentNumber, SubjectType subjectType, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        if (documentNumber.EndsWith("999"))
        {
            await Task.Delay(80, cancellationToken);
            return ProviderResult.Error(Name, "Timeout simulado em listas restritivas.", 80);
        }

        await Task.Delay(25, cancellationToken);

        bool isOfac = documentNumber.Contains("888");
        bool isCeis = documentNumber.Contains("888");
        bool isPep = documentNumber.Contains("777");

        var data = new
        {
            provedor = Name,
            documento = documentNumber,
            ofac_sanctioned = isOfac,
            ceis_cnep = isCeis,
            pep = isPep,
            pep_cargo = isPep ? "Deputado Estadual (Mandato 2023-2026)" : null,
            orgao = isPep ? "Assembleia Legislativa" : null
        };

        var latency = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        return ProviderResult.Success(Name, JsonSerializer.Serialize(data), latency);
    }
}

