using Domain.Enums;

namespace Application.Interfaces;

public interface ICurrentTenantContext
{
    Guid? TenantId { get; }
    string? ActorId { get; }
    UserRole Role { get; }
    string? IpAddress { get; }
}

