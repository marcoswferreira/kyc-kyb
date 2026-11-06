namespace Infrastructure.Storage;

/// <summary>
/// Metadados de um anexo armazenado em bucket com controle de expiração (LGPD / GDPR).
/// </summary>
public sealed record AttachmentMetadata(
    string StorageKey,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime UploadedAt,
    DateTime ExpiresAt,
    bool IsExpired);

