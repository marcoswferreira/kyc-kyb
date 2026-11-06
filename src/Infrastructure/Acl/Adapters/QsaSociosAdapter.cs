using System.Text.Json;
using Application.Acl;
using Application.Acl.Models;
using Application.Interfaces;
using Domain.Enums;

namespace Infrastructure.Acl.Adapters;

/// <summary>
/// Adapter da Camada Anticorrupção para Quadro de Sócios e Administradores (QSA/KYB).
/// Converte o JSON proprietário em lista de CanonicalPartnerRecord.
/// </summary>
public sealed class QsaSociosAdapter : IProviderAdapter<IReadOnlyList<CanonicalPartnerRecord>>
{
    private readonly IBackgroundCheckProvider _provider;

    public string ProviderKey => "QsaSocios";

    public QsaSociosAdapter(IEnumerable<IBackgroundCheckProvider> providers)
    {
        _provider = providers.FirstOrDefault(p => p.Name == "QsaSociosMockProvider")
                    ?? throw new InvalidOperationException("Provedor QsaSociosMockProvider não encontrado.");
    }

    public async Task<ProviderAdapterResult<IReadOnlyList<CanonicalPartnerRecord>>> QueryCanonicalAsync(
        string documentNumber,
        SubjectType subjectType,
        CancellationToken cancellationToken = default)
    {
        var rawResult = await _provider.QueryAsync(documentNumber, subjectType, cancellationToken);
        if (!rawResult.IsSuccess || string.IsNullOrWhiteSpace(rawResult.RawDataJson))
        {
            return new ProviderAdapterResult<IReadOnlyList<CanonicalPartnerRecord>>(
                false,
                ProviderKey,
                null,
                rawResult.RawDataJson,
                rawResult.ErrorMessage ?? "Falha ao consultar QSA de Sócios.",
                rawResult.LatencyMs);
        }

        try
        {
            using var doc = JsonDocument.Parse(rawResult.RawDataJson);
            var root = doc.RootElement;
            var records = new List<CanonicalPartnerRecord>();

            if (root.TryGetProperty("socios", out var socios) && socios.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in socios.EnumerateArray())
                {
                    string nome = item.TryGetProperty("nome", out var n) ? n.GetString() ?? "" : "";
                    string cpfMasc = item.TryGetProperty("cpf_mascarado", out var c) ? c.GetString() ?? "" : "";
                    string qualif = item.TryGetProperty("qualificacao", out var q) ? q.GetString() ?? "" : "";
                    decimal perc = item.TryGetProperty("participacao_percentual", out var p) && p.TryGetDecimal(out var val) ? val : 0m;

                    records.Add(new CanonicalPartnerRecord(
                        nome,
                        cpfMasc,
                        qualif,
                        perc,
                        nome,
                        cpfMasc,
                        DateTime.UtcNow.AddYears(-3)));
                }
            }

            return new ProviderAdapterResult<IReadOnlyList<CanonicalPartnerRecord>>(
                true,
                ProviderKey,
                records.AsReadOnly(),
                rawResult.RawDataJson,
                null,
                rawResult.LatencyMs);
        }
        catch (Exception ex)
        {
            return new ProviderAdapterResult<IReadOnlyList<CanonicalPartnerRecord>>(
                false,
                ProviderKey,
                null,
                rawResult.RawDataJson,
                $"Erro de parsing na ACL de QSA Sócios: {ex.Message}",
                rawResult.LatencyMs);
        }
    }
}
