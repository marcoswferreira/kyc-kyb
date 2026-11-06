using System.Text.Json;
using Application.Acl;
using Application.Acl.Models;
using Application.Interfaces;
using Domain.Enums;

namespace Infrastructure.Acl.Adapters;

/// <summary>
/// Adapter da Camada Anticorrupção para a Lista Suja de Trabalho Escravo (MTE/SIT).
/// Converte o JSON proprietário em lista de CanonicalLaborRecord.
/// </summary>
public sealed class TrabalhoEscravoAdapter : IProviderAdapter<IReadOnlyList<CanonicalLaborRecord>>
{
    private readonly IBackgroundCheckProvider _provider;

    public string ProviderKey => "TrabalhoEscravo";

    public TrabalhoEscravoAdapter(IEnumerable<IBackgroundCheckProvider> providers)
    {
        _provider = providers.FirstOrDefault(p => p.Name == "TrabalhoEscravoMockProvider")
                    ?? throw new InvalidOperationException("Provedor TrabalhoEscravoMockProvider não encontrado.");
    }

    public async Task<ProviderAdapterResult<IReadOnlyList<CanonicalLaborRecord>>> QueryCanonicalAsync(
        string documentNumber,
        SubjectType subjectType,
        CancellationToken cancellationToken = default)
    {
        var rawResult = await _provider.QueryAsync(documentNumber, subjectType, cancellationToken);
        if (!rawResult.IsSuccess || string.IsNullOrWhiteSpace(rawResult.RawDataJson))
        {
            return new ProviderAdapterResult<IReadOnlyList<CanonicalLaborRecord>>(
                false,
                ProviderKey,
                null,
                rawResult.RawDataJson,
                rawResult.ErrorMessage ?? "Falha ao consultar Lista Suja de Trabalho Escravo.",
                rawResult.LatencyMs);
        }

        try
        {
            using var doc = JsonDocument.Parse(rawResult.RawDataJson);
            var root = doc.RootElement;
            var records = new List<CanonicalLaborRecord>();

            bool constaListaSuja = root.TryGetProperty("consta_lista_suja", out var prop) && prop.GetBoolean();
            if (constaListaSuja)
            {
                string estab = root.TryGetProperty("estabelecimento", out var e) ? e.GetString() ?? "Estabelecimento Rural" : "Estabelecimento Rural";
                int ano = root.TryGetProperty("ano_inclusao", out var a) && a.TryGetInt32(out var yr) ? yr : 2024;
                string orgao = root.TryGetProperty("orgao_fiscalizador", out var o) ? o.GetString() ?? "MTE" : "MTE";

                records.Add(new CanonicalLaborRecord(
                    estab,
                    "Brasil",
                    "01.11-3 - Cultivo de cereais",
                    5,
                    ano,
                    $"Autuação confirmada pelo {orgao} com inserção no Cadastro de Empregadores do MTE. Risco ESG Crítico."));
            }

            return new ProviderAdapterResult<IReadOnlyList<CanonicalLaborRecord>>(
                true,
                ProviderKey,
                records.AsReadOnly(),
                rawResult.RawDataJson,
                null,
                rawResult.LatencyMs);
        }
        catch (Exception ex)
        {
            return new ProviderAdapterResult<IReadOnlyList<CanonicalLaborRecord>>(
                false,
                ProviderKey,
                null,
                rawResult.RawDataJson,
                $"Erro de parsing na ACL de Trabalho Escravo: {ex.Message}",
                rawResult.LatencyMs);
        }
    }
}
