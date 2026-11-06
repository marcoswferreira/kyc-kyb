using Domain.Enums;

namespace Application.Acl.Models;

/// <summary>
/// Modelo canônico padronizado para listas restritivas e sanções internacionais/nacionais.
/// Cobre OFAC, Interpol, PEP (Pessoa Exposta Politicamente), CEIS, CNEP e CEPIM.
/// </summary>
public sealed record CanonicalSanctionRecord(
    string ListCode,
    string Category,
    string EntityName,
    RiskIndicator RiskLevel,
    string Reason,
    DateTime? InscriptionDate,
    string? PublishingAgency);

