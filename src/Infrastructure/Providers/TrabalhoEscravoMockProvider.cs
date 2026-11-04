using System.Text.Json;
using Application.Interfaces;
using Domain.Enums;

namespace Infrastructure.Providers;

public class TrabalhoEscravoMockProvider : IBackgroundCheckProvider
{
    public string Name => "TrabalhoEscravoMockProvider";

    public async Task<ProviderResult> QueryAsync(string documentNumber, SubjectType subjectType, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        await Task.Delay(15, cancellationToken);

        bool constaListaSuja = documentNumber.Contains("555");

        var data = new
        {
            provedor = Name,
            documento = documentNumber,
            consta_lista_suja = constaListaSuja,
            estabelecimento = constaListaSuja ? "Fazenda Santa Fé - Agropecuária Sintética" : null,
            ano_inclusao = constaListaSuja ? 2024 : (int?)null,
            orgao_fiscalizador = "MTE / SIT"
        };

        var latency = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        return ProviderResult.Success(Name, JsonSerializer.Serialize(data), latency);
    }
}

