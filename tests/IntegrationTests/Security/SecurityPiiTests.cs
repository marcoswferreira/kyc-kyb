using System.Text;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Database;
using Infrastructure.Logging;
using Infrastructure.Security;
using Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace IntegrationTests.Security;

public class SecurityPiiTests
{
    private readonly IAesEncryptionService _aesService;
    private readonly IPiiSanitizer _sanitizer;

    public SecurityPiiTests()
    {
        var config = new ConfigurationBuilder().Build();
        _aesService = new AesEncryptionService(config);
        _sanitizer = new PiiSanitizer();
    }

    [Fact]
    public void AesEncryptionService_Probabilistic_ShouldEncryptAndDecryptCorrectly()
    {
        var plain = "52998224725";
        var cipher1 = _aesService.Encrypt(plain);
        var cipher2 = _aesService.Encrypt(plain);

        cipher1.Should().StartWith("enc:rnd:");
        cipher2.Should().StartWith("enc:rnd:");
        cipher1.Should().NotBe(cipher2); // Nonce aleatório gera cifras distintas

        _aesService.Decrypt(cipher1).Should().Be(plain);
        _aesService.Decrypt(cipher2).Should().Be(plain);
    }

    [Fact]
    public void AesEncryptionService_Deterministic_ShouldProduceIdenticalCipherForIndexing()
    {
        var plain = "52998224725";
        var cipher1 = _aesService.EncryptDeterministic(plain);
        var cipher2 = _aesService.EncryptDeterministic(plain);

        cipher1.Should().StartWith("enc:det:");
        cipher1.Should().Be(cipher2); // Determinístico permite busca por igualdade no banco

        _aesService.Decrypt(cipher1).Should().Be(plain);
    }

    [Fact]
    public void AesEncryptionService_ShouldHandleLegacyPlainTextGracefully()
    {
        var legacy = "Texto_legado_sem_criptografia";
        var decrypted = _aesService.Decrypt(legacy);
        decrypted.Should().Be(legacy);
    }

    [Fact]
    public void PiiSanitizer_ShouldMaskCpfsAndApiKeysInLogs()
    {
        var log = "Processando CPF 529.982.247-25 com chave sk_live_master_kyc_default_key_12345 e Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";
        var sanitized = _sanitizer.Sanitize(log);

        sanitized.Should().NotContain("529.982.247-25");
        sanitized.Should().Contain("***.982.247-**");
        sanitized.Should().NotContain("sk_live_master_kyc_default_key_12345");
        sanitized.Should().Contain("sk_live_[REDACTED_API_KEY]");
        sanitized.Should().Contain("Bearer [REDACTED_TOKEN]");
    }

    [Fact]
    public void PiiSanitizer_ShouldMaskUnformattedCpfAndCnpj()
    {
        var log = "Requisição: cpf: \"52998224725\" para empresa cnpj: \"11222333000181\"";
        var sanitized = _sanitizer.Sanitize(log);

        sanitized.Should().NotContain("52998224725");
        sanitized.Should().NotContain("11222333000181");
        sanitized.Should().Contain("***.982.247-**");
        sanitized.Should().Contain("**.222.333/****-**");
    }

    [Fact]
    public async Task AttachmentStorage_ShouldEnforceExpirationAndPurging()
    {
        var storage = new InMemoryAttachmentStorageService();
        var content = Encoding.UTF8.GetBytes("Contrato Social Confidencial");

        // 1. Upload com retenção de 50 milissegundos
        using var stream1 = new MemoryStream(content);
        var key = await storage.UploadAttachmentAsync("contrato.pdf", "application/pdf", stream1, TimeSpan.FromMilliseconds(50));

        var meta = await storage.GetMetadataAsync(key);
        meta.Should().NotBeNull();
        meta!.IsExpired.Should().BeFalse();

        // Download imediato funciona
        var downloaded = await storage.DownloadAttachmentAsync(key);
        downloaded.Should().NotBeNull();

        // 2. Aguarda expiração
        await Task.Delay(80);

        // Download pós expiração deve retornar null
        var expiredDownload = await storage.DownloadAttachmentAsync(key);
        expiredDownload.Should().BeNull();

        // 3. Expurgo em lote
        var purgedCount = await storage.PurgeExpiredAttachmentsAsync();
        purgedCount.Should().Be(1);

        var metaAfterPurge = await storage.GetMetadataAsync(key);
        metaAfterPurge.Should().BeNull();
    }

    [Fact]
    public async Task DbContext_ShouldEncryptSensitiveFieldsAtRest()
    {
        var options = new DbContextOptionsBuilder<KycKybDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new KycKybDbContext(options, _aesService);

        var check = BackgroundCheck.Create(
            Guid.NewGuid(),
            SubjectType.Person,
            "52998224725",
            "529.982.247-25",
            "***.982.247-**",
            "Nome Altamente Confidencial",
            CheckPurpose.PrevencaoAFraude,
            LegalBasis.PrevencaoAFraude,
            true);

        db.BackgroundChecks.Add(check);
        await db.SaveChangesAsync();

        // Consultar do banco via LINQ por igualdade de DocumentNumber
        var reloaded = await db.BackgroundChecks
            .FirstOrDefaultAsync(b => b.DocumentNumber == "52998224725");

        reloaded.Should().NotBeNull();
        reloaded!.Name.Should().Be("Nome Altamente Confidencial");
        reloaded.DocumentNumber.Should().Be("52998224725");
    }
}

