using System.Security.Cryptography;
using System.Text;
using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Registro de auditoria imutável com encadeamento de hash criptográfico (Audit Hash-Chain),
/// garantindo não-repúdio e detecção de qualquer adulteração nos registros de acesso e tratamento (LGPD).
/// </summary>
public sealed class AuditLogEntry : BaseEntity, IAggregateRoot
{
    public Guid TenantId { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string ResourceId { get; private set; } = string.Empty;
    public string Details { get; private set; } = string.Empty;
    public string IpAddress { get; private set; } = string.Empty;
    public DateTime Timestamp { get; private set; }
    public string PreviousHash { get; private set; } = string.Empty;
    public string Hash { get; private set; } = string.Empty;

    private AuditLogEntry() { } // EF Core

    public AuditLogEntry(
        Guid id,
        Guid tenantId,
        string actorId,
        string action,
        string resourceId,
        string details,
        string ipAddress,
        DateTime timestamp,
        string previousHash)
    {
        Id = id;
        TenantId = tenantId;
        ActorId = actorId;
        Action = action;
        ResourceId = resourceId;
        Details = details;
        IpAddress = ipAddress;
        Timestamp = timestamp;
        PreviousHash = previousHash;
        Hash = ComputeHash(previousHash, timestamp, tenantId, actorId, action, resourceId, details);
        CreatedAt = timestamp;
    }

    public static AuditLogEntry Create(
        Guid tenantId,
        string actorId,
        string action,
        string resourceId,
        string details,
        string ipAddress,
        string previousHash)
    {
        return new AuditLogEntry(
            Guid.NewGuid(),
            tenantId,
            actorId,
            action,
            resourceId,
            details,
            ipAddress,
            DateTime.UtcNow,
            previousHash);
    }

    public static string ComputeHash(
        string previousHash,
        DateTime timestamp,
        Guid tenantId,
        string actorId,
        string action,
        string resourceId,
        string details)
    {
        var raw = $"{previousHash}|{timestamp:O}|{tenantId}|{actorId}|{action}|{resourceId}|{details}";
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public bool VerifyIntegrity()
    {
        var expectedHash = ComputeHash(PreviousHash, Timestamp, TenantId, ActorId, Action, ResourceId, Details);
        return string.Equals(Hash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}

