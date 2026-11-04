using Application.Interfaces;
using Domain.Enums;

namespace Infrastructure.Security;

public class CurrentTenantContext : ICurrentTenantContext
{
    public Guid? TenantId { get; set; }
    public string? ActorId { get; set; }
    public UserRole Role { get; set; } = UserRole.Operator;
    public string? IpAddress { get; set; }
}

