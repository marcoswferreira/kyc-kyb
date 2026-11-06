using Infrastructure.Security;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Database.Converters;

/// <summary>
/// ValueConverter do EF Core para criptografar automaticamente strings sensíveis na gravação
/// e descriptografá-las na leitura, garantindo criptografia em repouso (LGPD/GDPR).
/// Suporta valores nulos com tipagem segura.
/// </summary>
public sealed class EncryptedStringConverter : ValueConverter<string?, string?>
{
    public EncryptedStringConverter(IAesEncryptionService encryptionService, bool deterministic = false)
        : base(
            plain => plain == null ? null : (deterministic ? encryptionService.EncryptDeterministic(plain) : encryptionService.Encrypt(plain)),
            cipher => cipher == null ? null : encryptionService.Decrypt(cipher))
    {
    }
}
