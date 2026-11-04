namespace Domain.Enums;

public enum SubjectType
{
    Person = 1,
    Company = 2
}

public enum CheckPurpose
{
    RecrutamentoESelecao = 1,
    PrevencaoAFraude = 2,
    ComplianceKyb = 3,
    ConcessaoDeCredito = 4,
    OnboardingParceiro = 5
}

public enum LegalBasis
{
    Consentimento = 1,              // Art. 7º, I da LGPD
    ObrigacaoLegal = 2,             // Art. 7º, II da LGPD
    ExercicioRegularDireito = 3,    // Art. 7º, VI da LGPD
    LegitimoInteresse = 4,          // Art. 7º, IX da LGPD
    PrevencaoAFraude = 5            // Art. 11, II, 'g' da LGPD
}

public enum CheckStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    CompletedWithWarnings = 4,
    Failed = 5
}

public enum RiskIndicator
{
    Green = 1,    // Baixo Risco / Regular
    Yellow = 2,   // Atenção / Risco Moderado
    Red = 3       // Alto Risco / Apontamento Crítico
}

public enum ProviderExecutionStatus
{
    Success = 1,
    Timeout = 2,
    CircuitBroken = 3,
    Error = 4,
    CacheHit = 5
}

public enum HumanReviewDecision
{
    AwaitingReview = 1,                     // Decisão automatizada assistida pendente (Art. 20 LGPD)
    ApprovedByHuman = 2,                    // Analista de compliance validou e aprovou
    RejectedByHuman = 3,                    // Analista de compliance deliberou por reprovar
    AdditionalInformationRequested = 4     // Analista solicitou mais dados ao candidato/empresa
}

public enum UserRole
{
    Admin = 1,
    ComplianceOfficer = 2,
    Operator = 3,
    Auditor = 4
}
