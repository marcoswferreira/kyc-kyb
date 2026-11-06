using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Security;

/// <summary>
/// Implementação de criptografia simétrica autenticada com AES-256-GCM (Galois/Counter Mode).
/// Suporta criptografia probabilística (aleatória) e determinística (para consultas indexadas no banco).
/// Formato do ciphertext em Base64: [12 bytes Nonce] + [16 bytes Tag] + [N bytes Ciphertext].
/// </summary>
public sealed class AesEncryptionService : IAesEncryptionService
{
    private readonly byte[] _key;

    public AesEncryptionService(IConfiguration configuration)
    {
        var keyHexOrBase64 = configuration["Security:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(keyHexOrBase64))
        {
            // Chave padrão determinística de desenvolvimento (32 bytes = 256 bits)
            _key = SHA256.HashData(Encoding.UTF8.GetBytes("KycKyb_Master_Enterprise_Encryption_Key_2026_Secure!"));
        }
        else
        {
            _key = Convert.FromHexString(keyHexOrBase64.Length == 64 ? keyHexOrBase64 : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyHexOrBase64))));
        }
    }

    public AesEncryptionService(byte[] key)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException("A chave para AES-256 deve possuir exatamente 32 bytes (256 bits).", nameof(key));
        }
        _key = (byte[])key.Clone();
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText;
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);

        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using (var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize))
        {
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
        }

        var combined = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, nonce.Length + tag.Length, cipherBytes.Length);

        return "enc:rnd:" + Convert.ToBase64String(combined);
    }

    public string EncryptDeterministic(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText;
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        // Nonce pseudo-aleatório determinístico baseado em HMAC-SHA256 do payload com a chave mestra
        var hmac = HMACSHA256.HashData(_key, plainBytes);
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        Buffer.BlockCopy(hmac, 0, nonce, 0, nonce.Length);

        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        using (var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize))
        {
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
        }

        var combined = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, nonce.Length + tag.Length, cipherBytes.Length);

        return "enc:det:" + Convert.ToBase64String(combined);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return cipherText;
        }

        string base64Payload;
        if (cipherText.StartsWith("enc:det:"))
        {
            base64Payload = cipherText["enc:det:".Length..];
        }
        else if (cipherText.StartsWith("enc:rnd:"))
        {
            base64Payload = cipherText["enc:rnd:".Length..];
        }
        else if (cipherText.StartsWith("enc:"))
        {
            base64Payload = cipherText["enc:".Length..];
        }
        else
        {
            // Texto puro (dados legados não criptografados)
            return cipherText;
        }

        try
        {
            var combined = Convert.FromBase64String(base64Payload);
            if (combined.Length < AesGcm.NonceByteSizes.MaxSize + AesGcm.TagByteSizes.MaxSize)
            {
                return cipherText;
            }

            var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
            var tag = new byte[AesGcm.TagByteSizes.MaxSize];
            var cipherLength = combined.Length - nonce.Length - tag.Length;
            var cipherBytes = new byte[cipherLength];

            Buffer.BlockCopy(combined, 0, nonce, 0, nonce.Length);
            Buffer.BlockCopy(combined, nonce.Length, tag, 0, tag.Length);
            Buffer.BlockCopy(combined, nonce.Length + tag.Length, cipherBytes, 0, cipherLength);

            var plainBytes = new byte[cipherLength];
            using (var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize))
            {
                aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
            }

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            return cipherText;
        }
    }
}
