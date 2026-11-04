namespace Application.Dtos;

public record AuditLogDto(
    Guid Id,
    Guid TenantId,
    string ActorId,
    string Action,
    string ResourceId,
    string Details,
    string IpAddress,
    DateTime Timestamp,
    string PreviousHash,
    string Hash,
    bool IntegrityValid);

