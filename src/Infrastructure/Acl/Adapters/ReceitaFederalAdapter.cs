using System.Text.Json;
using Application.Acl;
using Application.Acl.Models;
using Application.Interfaces;
using Domain.Enums;

namespace Infrastructure.Acl.Adapters;

/// <summary>
/// Adapter da Camada Anticorrupção para consultas cadastrais da Receita Federal (RFB).
/// Converte o JSON proprietário do provedor em CanonicalCadastralProfile.
/// </summary>
public sealed class ReceitaFederalAdapter : IProviderAdapter<CanonicalCadastralProfile>
{
    private readonly IBackgroundCheckProvider _provider;

    public string ProviderKey => "ReceitaFederal";

    public ReceitaFederalAdapter(IEnumerable<IBackgroundCheckProvider> providers)
    {
        _provider = providers.FirstOrDefault(p => p.Name == "ReceitaFederalMockProvider")
                    ?? throw new InvalidOperationException("Provedor ReceitaFederalMockProvider não encontrado.");
    }

    public async Task<ProviderAdapterResult<CanonicalCadastralProfile>> QueryCanonicalAsync(
        string documentNumber,
        SubjectType subjectType,
        CancellationToken cancellationToken = default)
    {
        var rawResult = await _provider.QueryAsync(documentNumber, subjectType, cancellationToken);
        if (!rawResult.IsSuccess || string.IsNullOrWhiteSpace(rawResult.RawDataJson))
        {
            return new ProviderAdapterResult<CanonicalCadastralProfile>(
                false,
                ProviderKey,
                null,
                rawResult.RawDataJson,
                rawResult.ErrorMessage ?? "Falha ao consultar Receita Federal.",
                rawResult.LatencyMs);
        }

        try
        {
            using var doc = JsonDocument.Parse(rawResult.RawDataJson);
            var root = doc.RootElement;

            string situacao = root.TryGetProperty("situacao_cadastral", out var sitProp) ? sitProp.GetString() ?? "DESCONHECIDA" : "DESCONHECIDA";
            string nome = root.TryGetProperty("razao_social_nome", out var nomeProp) ? nomeProp.GetString() ?? string.Empty : string.Empty;
            string? natJuridica = root.TryGetProperty("natureza_juridica", out var natProp) ? natProp.GetString() : null;
            DateTime? dataAbertura = null;
            if (root.TryGetProperty("data_abertura_nascimento", out var dataProp) && DateTime.TryParse(dataProp.GetString(), out var dt))
            {
                dataAbertura = dt;
            }

            bool isActive = string.Equals(situacao, "REGULAR", StringComparison.OrdinalIgnoreCase);

            var profile = new CanonicalCadastralProfile(
                documentNumber,
                subjectType,
                nome,
                situacao,
                dataAbertura,
                natJuridica,
                isActive);

            return new ProviderAdapterResult<CanonicalCadastralProfile>(
                true,
                ProviderKey,
                profile,
                rawResult.RawDataJson,
                null,
                rawResult.LatencyMs);
        }
        catch (Exception ex)
        {
            return new ProviderAdapterResult<CanonicalCadastralProfile>(
                false,
                ProviderKey,
                null,
                rawResult.RawDataJson,
                $"Erro de parsing na ACL da Receita Federal: {ex.Message}",
                rawResult.LatencyMs);
        }
    }
}

