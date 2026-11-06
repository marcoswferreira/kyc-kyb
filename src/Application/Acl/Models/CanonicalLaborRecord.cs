namespace Application.Acl.Models;

/// <summary>
/// Modelo canônico padronizado para apontamentos de trabalho análogo à escravidão e autuações trabalhistas.
/// </summary>
public sealed record CanonicalLaborRecord(
    string EmployerName,
    string EstablishmentLocation,
    string Cnae,
    int RescuedWorkersCount,
    int InclusionYear,
    string InspectionDecision);
