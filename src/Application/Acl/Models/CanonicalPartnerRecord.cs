namespace Application.Acl.Models;

/// <summary>
/// Modelo canônico padronizado para Quadro de Sócios e Administradores (QSA/KYB).
/// </summary>
public sealed record CanonicalPartnerRecord(
    string PartnerName,
    string PartnerDocumentMasked,
    string Qualification,
    decimal SharePercentage,
    string LegalRepresentativeName,
    string LegalRepresentativeDocumentMasked,
    DateTime? EntryDate);

