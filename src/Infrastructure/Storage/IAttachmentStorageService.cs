namespace Infrastructure.Storage;

/// <summary>
/// Contrato de armazenamento de documentos e anexos (S3 / Storage nativo) com política de retenção e expiração.
/// Garante que cópias de CNH, RG, Contratos Sociais e Comprovantes sejam expurgados conforme TTL.
/// </summary>
public interface IAttachmentStorageService
{
    Task<string> UploadAttachmentAsync(
        string fileName,
        string contentType,
        Stream contentStream,
        TimeSpan retentionPeriod,
        CancellationToken cancellationToken = default);

    Task<Stream?> DownloadAttachmentAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<AttachmentMetadata?> GetMetadataAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<bool> DeleteAttachmentAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<int> PurgeExpiredAttachmentsAsync(CancellationToken cancellationToken = default);

    string GeneratePresignedUrl(string storageKey, TimeSpan validity);
}
