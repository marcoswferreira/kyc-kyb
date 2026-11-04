namespace Application.Interfaces;

public record OrchestratorResult(
    IReadOnlyList<ProviderResult> ProviderResults,
    bool HasPartialWarnings,
    string RawEvidenceJson,
    string EvidenceSha256);

