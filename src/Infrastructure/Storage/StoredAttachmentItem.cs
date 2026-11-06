namespace Infrastructure.Storage;

internal sealed class StoredAttachmentItem
{
    public required string StorageKey { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Content { get; init; }
    public required DateTime UploadedAt { get; init; }
    public required DateTime ExpiresAt { get; init; }
}

