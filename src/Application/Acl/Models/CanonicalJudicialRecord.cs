using Domain.Enums;

namespace Application.Acl.Models;

/// <summary>
/// Modelo canônico padronizado para apontamentos judiciais e processos.
/// Isola particularidades de APIs como Escavador, Digesto, Jusbrasil ou Serasa Judicial.
/// </summary>
public sealed record CanonicalJudicialRecord(
    string ProcessNumber,
    string Court,
    string LegalSphere,
    string Role,
    RiskIndicator RiskLevel,
    string Nature,
    string Description,
    DateTime? DistributionDate);

