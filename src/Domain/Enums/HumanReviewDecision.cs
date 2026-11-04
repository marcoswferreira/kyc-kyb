namespace Domain.Enums;

public enum HumanReviewDecision
{
    AwaitingReview = 1,                     // Decisão automatizada assistida pendente (Art. 20 LGPD)
    ApprovedByHuman = 2,                    // Analista de compliance validou e aprovou
    RejectedByHuman = 3,                    // Analista de compliance deliberou por reprovar
    AdditionalInformationRequested = 4     // Analista solicitou mais dados ao candidato/empresa
}

