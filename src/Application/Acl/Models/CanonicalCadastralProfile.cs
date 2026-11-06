using Domain.Enums;

namespace Application.Acl.Models;

/// <summary>
/// Modelo canônico padronizado para dados cadastrais (Pessoa Física ou Jurídica).
/// Isola o core do sistema das particularidades de provedores como Receita Federal, Serasa ou Idwall.
/// </summary>
public sealed record CanonicalCadastralProfile(
    string Document,
    SubjectType SubjectType,
    string LegalName,
    string CadastralStatus,
    DateTime? RegistrationDate,
    string? LegalNature,
    bool IsActive);

