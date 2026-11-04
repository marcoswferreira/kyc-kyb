using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database;

public class KycKybDbContext : DbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<BackgroundCheck> BackgroundChecks => Set<BackgroundCheck>();
    public DbSet<CheckFinding> CheckFindings => Set<CheckFinding>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<UsageMetric> UsageMetrics => Set<UsageMetric>();

    public KycKybDbContext(DbContextOptions<KycKybDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(builder =>
        {
            builder.ToTable("tenants");
            builder.HasKey(t => t.Id);
            builder.HasIndex(t => t.Slug).IsUnique();
            builder.HasIndex(t => t.ApiKeyHash);
            builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
            builder.Property(t => t.Slug).HasMaxLength(100).IsRequired();
            builder.Property(t => t.ApiKeyHash).HasMaxLength(64).IsRequired();
            builder.Property(t => t.ApiKeyPrefix).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<BackgroundCheck>(builder =>
        {
            builder.ToTable("background_checks");
            builder.HasKey(b => b.Id);
            builder.HasIndex(b => new { b.TenantId, b.DocumentNumber });
            builder.HasIndex(b => b.Status);
            builder.HasIndex(b => b.RetentionUntil);
            builder.Property(b => b.DocumentNumber).HasMaxLength(50).IsRequired();
            builder.Property(b => b.DocumentFormatted).HasMaxLength(50).IsRequired();
            builder.Property(b => b.DocumentMasked).HasMaxLength(50).IsRequired();
            builder.Property(b => b.Name).HasMaxLength(200).IsRequired();
            builder.Property(b => b.EvidenceSha256).HasMaxLength(64);
            builder.HasMany(b => b.Findings)
                   .WithOne()
                   .HasForeignKey(f => f.BackgroundCheckId)
                   .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CheckFinding>(builder =>
        {
            builder.ToTable("check_findings");
            builder.HasKey(f => f.Id);
            builder.HasIndex(f => f.BackgroundCheckId);
            builder.Property(f => f.ProviderName).HasMaxLength(100).IsRequired();
            builder.Property(f => f.Category).HasMaxLength(100).IsRequired();
            builder.Property(f => f.Title).HasMaxLength(250).IsRequired();
            builder.Property(f => f.EvidenceKey).HasMaxLength(150);
        });

        modelBuilder.Entity<AuditLogEntry>(builder =>
        {
            builder.ToTable("audit_log_entries");
            builder.HasKey(a => a.Id);
            builder.HasIndex(a => new { a.TenantId, a.Timestamp });
            builder.HasIndex(a => a.ResourceId);
            builder.Property(a => a.ActorId).HasMaxLength(100).IsRequired();
            builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
            builder.Property(a => a.ResourceId).HasMaxLength(100).IsRequired();
            builder.Property(a => a.Hash).HasMaxLength(64).IsRequired();
            builder.Property(a => a.PreviousHash).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<WebhookSubscription>(builder =>
        {
            builder.ToTable("webhook_subscriptions");
            builder.HasKey(w => w.Id);
            builder.HasIndex(w => w.TenantId);
            builder.Property(w => w.TargetUrl).HasMaxLength(500).IsRequired();
            builder.Property(w => w.Secret).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<UsageMetric>(builder =>
        {
            builder.ToTable("usage_metrics");
            builder.HasKey(u => u.Id);
            builder.HasIndex(u => new { u.TenantId, u.YearMonth }).IsUnique();
            builder.Property(u => u.YearMonth).HasMaxLength(7).IsRequired();
        });
    }
}

