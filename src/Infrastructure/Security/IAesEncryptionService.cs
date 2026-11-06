namespace Infrastructure.Security;

/// <summary>
/// Serviço de criptografia autenticada em repouso utilizando AES-256-GCM.
/// Garante confidencialidade e integridade criptográfica para campos sensíveis (PII) sob LGPD/GDPR.
/// </summary>
public interface IAesEncryptionService
{
    string Encrypt(string plainText);
    string EncryptDeterministic(string plainText);
    string Decrypt(string cipherText);
}
