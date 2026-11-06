namespace Domain.StateMachine;

/// <summary>
/// Estados determinísticos do ciclo de vida de uma verificação de Background Check (KYC/KYB).
/// Atende às exigências de conformidade regulatória (Bacen/CVM/LGPD).
/// </summary>
public enum CheckState
{
    Pending = 1,
    Processing = 2,
    AwaitingHumanReview = 3,
    Approved = 4,
    Rejected = 5,
    RequiresMoreInfo = 6,
    Failed = 7,
    Purged = 8
}

