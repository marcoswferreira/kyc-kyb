using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Database.Seeders;

public static class DatabaseSeeder
{
    public const string DefaultApiKey = "sk_live_master_kyc_default_key_12345";
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task SeedAsync(KycKybDbContext context, ILogger logger)
    {
        if (await context.Tenants.AnyAsync())
        {
            return;
        }

        logger.LogInformation("Populando banco de dados com dados iniciais (Seeder)...");

        var apiKeyHash = ApiKey.ComputeHash(DefaultApiKey);
        var tenant = new Tenant(
            DefaultTenantId,
            "Master Compliance S.A.",
            "master-compliance",
            apiKeyHash,
            "sk_live_mast",
            rateLimitPerMinute: 120);

        await context.Tenants.AddAsync(tenant);

        var genesisAudit = AuditLogEntry.Create(
            tenant.Id,
            "system_seeder",
            "TENANT_INITIALIZED",
            tenant.Id.ToString(),
            "Tenant padrão provisionado com chave de API inicial.",
            "127.0.0.1",
            "0000000000000000000000000000000000000000000000000000000000000000");

        await context.AuditLogEntries.AddAsync(genesisAudit);

        await context.SaveChangesAsync();
        logger.LogInformation("Seeding finalizado com sucesso. API Key inicial: {ApiKey}", DefaultApiKey);
    }
}

