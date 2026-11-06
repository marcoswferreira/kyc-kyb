using System.Text.Json;
using Application.Acl;
using Application.Acl.Models;
using Application.Interfaces;
using Domain.Enums;

namespace Infrastructure.Acl.Adapters;

/// <summary>
/// Adapter da Camada Anticorrupção para Listas Restritivas (OFAC, CEIS/CNEP, PEP).
/// Converte o JSON proprietário em lista de CanonicalSanctionRecord.
/// </summary>
public sealed class ListasRestritivasAdapter : IProviderAdapter<IReadOnlyList<CanonicalSanctionRecord>>
{
    private readonly IBackgroundCheckProvider _provider;

    public string ProviderKey => "ListasRestritivas";

    public ListasRestritivasAdapter(IEnumerable<IBackgroundCheckProvider> providers)
    {
        _provider = providers.FirstOrDefault(p => p.Name == "ListasRestritivasMockProvider")
                    ?? throw new InvalidOperationException("Provedor ListasRestritivasMockProvider não encontrado.");
    }

    public async Task<ProviderAdapterResult<IReadOnlyList<CanonicalSanctionRecord>>> QueryCanonicalAsync(
        string documentNumber,
        SubjectType subjectType,
        CancellationToken cancellationToken = default)
    {
        var rawResult = await _provider.QueryAsync(documentNumber, subjectType, cancellationToken);
        if (!rawResult.IsSuccess || string.IsNullOrWhiteSpace(rawResult.RawDataJson))
        {
            return new ProviderAdapterResult<IReadOnlyList<CanonicalSanctionRecord>>(
                false,
                ProviderKey,
                null,
                rawResult.RawDataJson,
                rawResult.ErrorMessage ?? "Falha ao consultar Listas Restritivas.",
                rawResult.LatencyMs);
        }

        try
        {
            using var doc = JsonDocument.Parse(rawResult.RawDataJson);
            var root = doc.RootElement;
            var records = new List<CanonicalSanctionRecord>();

            bool isOfac = root.TryGetProperty("ofac_sanctioned", out var ofac) && ofac.GetBoolean();
            bool isCeis = root.TryGetProperty("ceis_cnep", out var ceis) && ceis.GetBoolean();
            bool isPep = root.TryGetProperty("pep", out var pep) && pep.GetBoolean();

            if (isOfac)
            {
                records.Add(new CanonicalSanctionRecord(
                    "OFAC-SDN",
                    "Sanções Internacionais",
                    "OFAC Specially Designated Nationals",
                    RiskIndicator.Red,
                    "Inclusão confirmada na lista de sanções e bloqueios internacionais do Tesouro dos EUA (OFAC).",
                    DateTime.UtcNow.AddYears(-1),
                    "U.S. Department of the Treasury"));
            }

            if (isCeis)
            {
                records.Add(new CanonicalSanctionRecord(
                    "CEIS-CNEP",
                    "Inidoneidade Pública",
                    "Cadastro de Empresas Inidôneas e Suspensas",
                    RiskIndicator.Red,
                    "Apontamento de sanção restritiva de contratar com a Administração Pública (CGU/CEIS/CNEP).",
                    DateTime.UtcNow.AddMonths(-8),
                    "Controladoria-Geral da União (CGU)"));
            }

            if (isPep)
            {
                string cargo = root.TryGetProperty("pep_cargo", out var c) ? c.GetString() ?? "Cargo Político" : "Cargo Político";
                records.Add(new CanonicalSanctionRecord(
                    "PEP-SISCOAF",
                    "Pessoa Exposta Politicamente",
                    "Cadastro Nacional PEP",
                    RiskIndicator.Yellow,
                    $"Identificado como PEP: {cargo}. Requer diligência ampliada (EDD) conforme Circular Bacen 3.978/2020.",
                    DateTime.UtcNow.AddYears(-2),
                    "COAF / Banco Central"));
            }

            return new ProviderAdapterResult<IReadOnlyList<CanonicalSanctionRecord>>(
                true,
                ProviderKey,
                records.AsReadOnly(),
                rawResult.RawDataJson,
                null,
                rawResult.LatencyMs);
        }
        catch (Exception ex)
        {
            return new ProviderAdapterResult<IReadOnlyList<CanonicalSanctionRecord>>(
                false,
                ProviderKey,
                null,
                rawResult.RawDataJson,
                $"Erro de parsing na ACL de Listas Restritivas: {ex.Message}",
                rawResult.LatencyMs);
        }
    }
}
