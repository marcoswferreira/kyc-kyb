using System.Collections.Concurrent;

namespace Infrastructure.Storage;

/// <summary>
/// Provedor nativo de armazenamento seguro de anexos em memória com simulação de S3 e ciclo de vida TTL.
/// Suporta expiração automática e geração de URLs assinadas temporárias sem dependências externas.
/// </summary>
public sealed class InMemoryAttachmentStorageService : IAttachmentStorageService
{
    private readonly ConcurrentDictionary<string, StoredAttachmentItem> _items = new();

    public async Task<string> UploadAttachmentAsync(
        string fileName,
        string contentType,
        Stream contentStream,
        TimeSpan retentionPeriod,
        CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await contentStream.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();

        var storageKey = $"attachments/{Guid.NewGuid():N}/{fileName}";
        var now = DateTime.UtcNow;

        var item = new StoredAttachmentItem
        {
            StorageKey = storageKey,
            FileName = fileName,
            ContentType = contentType,
            Content = bytes,
            UploadedAt = now,
            ExpiresAt = now.Add(retentionPeriod)
        };

        _items[storageKey] = item;
        return storageKey;
    }

    public Task<Stream?> DownloadAttachmentAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (_items.TryGetValue(storageKey, out var item))
        {
            if (DateTime.UtcNow > item.ExpiresAt)
            {
                // Item expirado não deve ser servido
                return Task.FromResult<Stream?>(null);
            }

            return Task.FromResult<Stream?>(new MemoryStream(item.Content));
        }

        return Task.FromResult<Stream?>(null);
    }

    public Task<AttachmentMetadata?> GetMetadataAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (_items.TryGetValue(storageKey, out var item))
        {
            var isExpired = DateTime.UtcNow > item.ExpiresAt;
            var meta = new AttachmentMetadata(
                item.StorageKey,
                item.FileName,
                item.ContentType,
                item.Content.LongLength,
                item.UploadedAt,
                item.ExpiresAt,
                isExpired);

            return Task.FromResult<AttachmentMetadata?>(meta);
        }

        return Task.FromResult<AttachmentMetadata?>(null);
    }

    public Task<bool> DeleteAttachmentAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_items.TryRemove(storageKey, out _));
    }

    public Task<int> PurgeExpiredAttachmentsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _items.Values
            .Where(i => i.ExpiresAt <= now)
            .Select(i => i.StorageKey)
            .ToList();

        int purgedCount = 0;
        foreach (var key in expiredKeys)
        {
            if (_items.TryRemove(key, out _))
            {
                purgedCount++;
            }
        }

        return Task.FromResult(purgedCount);
    }

    public string GeneratePresignedUrl(string storageKey, TimeSpan validity)
    {
        var expiresUnix = DateTimeOffset.UtcNow.Add(validity).ToUnixTimeSeconds();
        return $"https://s3.sa-east-1.amazonaws.com/kyc-kyb-vault/{storageKey}?X-Amz-Expires={validity.TotalSeconds}&Expires={expiresUnix}&X-Signature=simulated_sha256_presigned_token";
    }
}
