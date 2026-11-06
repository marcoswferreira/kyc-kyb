using System.Text.Json;
using Application.Acl;
using Application.Acl.Models;
using Application.Interfaces;
using Domain.Enums;

namespace Infrastructure.Acl.Adapters;

/// <summary>
/// Adapter da Camada Anticorrupção para Tribunais de Justiça e processos.
/// Converte o JSON proprietário em lista de CanonicalJudicialRecord.
/// </summary>
public sealed class TribunaisAdapter : IProviderAdapter<IReadOnlyList<CanonicalJudicialRecord>>
{
    private readonly IBackgroundCheckProvider _provider;

    public string ProviderKey => "Tribunais";

    public TribunaisAdapter(IEnumerable<IBackgroundCheckProvider> providers)
    {
        _provider = providers.FirstOrDefault(p => p.Name == "TribunaisProcessosMockProvider")
                    ?? throw new InvalidOperationException("Provedor TribunaisProcessosMockProvider não encontrado.");
    }

    public async Task<ProviderAdapterResult<IReadOnlyList<CanonicalJudicialRecord>>> QueryCanonicalAsync(
        string documentNumber,
        SubjectType subjectType,
        CancellationToken cancellationToken = default)
    {
        var rawResult = await _provider.QueryAsync(documentNumber, subjectType, cancellationToken);
        if (!rawResult.IsSuccess || string.IsNullOrWhiteSpace(rawResult.RawDataJson))
        {
            return new ProviderAdapterResult<IReadOnlyList<CanonicalJudicialRecord>>(
                false,
                ProviderKey,
                null,
                rawResult.RawDataJson,
                rawResult.ErrorMessage ?? "Falha ao consultar Tribunais.",
                rawResult.LatencyMs);
        }

        try
        {
            using var doc = JsonDocument.Parse(rawResult.RawDataJson);
            var root = doc.RootElement;
            var records = new List<CanonicalJudicialRecord>();

            if (root.TryGetProperty("processos", out var procList) && procList.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in procList.EnumerateArray())
                {
                    string tipo = item.TryGetProperty("tipo", out var t) ? t.GetString() ?? "Cível" : "Cível";
                    string numero = item.TryGetProperty("numero", out var n) ? n.GetString() ?? "Sem Número" : "Sem Número";
                    string tribunal = item.TryGetProperty("tribunal", out var trib) ? trib.GetString() ?? "TJ" : "TJ";
                    string status = item.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
                    string assunto = item.TryGetProperty("assunto", out var asst) ? asst.GetString() ?? "" : "";

                    bool isCriminal = string.Equals(tipo, "Criminal", StringComparison.OrdinalIgnoreCase);
                    var risk = isCriminal ? RiskIndicator.Red : RiskIndicator.Yellow;

                    records.Add(new CanonicalJudicialRecord(
                        numero,
                        tribunal,
                        tipo,
                        "Réu/Executado",
                        risk,
                        assunto,
                        $"Processo {tipo} ({status}) no tribunal {tribunal}: {assunto}",
                        DateTime.UtcNow.AddMonths(-6)));
                }
            }

            return new ProviderAdapterResult<IReadOnlyList<CanonicalJudicialRecord>>(
                true,
                ProviderKey,
                records.AsReadOnly(),
                rawResult.RawDataJson,
                null,
                rawResult.LatencyMs);
        }
        catch (Exception ex)
        {
            return new ProviderAdapterResult<IReadOnlyList<CanonicalJudicialRecord>>(
                false,
                ProviderKey,
                null,
                rawResult.RawDataJson,
                $"Erro de parsing na ACL de Tribunais: {ex.Message}",
                rawResult.LatencyMs);
        }
    }
}
