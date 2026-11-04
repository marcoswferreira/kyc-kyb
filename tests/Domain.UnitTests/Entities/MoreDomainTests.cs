using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Domain.UnitTests.Entities;

public class MoreDomainTests
{
    [Fact]
    public void Tenant_Mutations_ShouldUpdatePropertiesAndTimestamps()
    {
        var tenant = Tenant.Create("Fintech Alfa", "fintech-alfa", "hash1", "sk_live_1", 60);

        tenant.Name.Should().Be("Fintech Alfa");
        tenant.Slug.Should().Be("fintech-alfa");
        tenant.IsActive.Should().BeTrue();
        tenant.RateLimitPerMinute.Should().Be(60);

        tenant.UpdateRateLimit(120);
        tenant.RateLimitPerMinute.Should().Be(120);
        tenant.UpdatedAt.Should().NotBeNull();

        tenant.Deactivate();
        tenant.IsActive.Should().BeFalse();

        tenant.Activate();
        tenant.IsActive.Should().BeTrue();

        tenant.UpdateApiKey("hash2", "sk_live_2");
        tenant.ApiKeyHash.Should().Be("hash2");
        tenant.ApiKeyPrefix.Should().Be("sk_live_2");
    }

    [Fact]
    public void UsageMetric_ShouldTrackUsageCorrectly()
    {
        var tenantId = Guid.NewGuid();
        var metric = UsageMetric.Create(tenantId, "2026-11");

        metric.TenantId.Should().Be(tenantId);
        metric.YearMonth.Should().Be("2026-11");
        metric.TotalChecks.Should().Be(0);
        metric.TotalProviderCalls.Should().Be(0);
        metric.BillableUnits.Should().Be(0m);

        metric.IncrementUsage(checksCount: 2, providerCallsCount: 8, unitCost: 0.50m);

        metric.TotalChecks.Should().Be(2);
        metric.TotalProviderCalls.Should().Be(8);
        metric.BillableUnits.Should().Be(1.00m);
        metric.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void WebhookSubscription_Mutations_ShouldWork()
    {
        var tenantId = Guid.NewGuid();
        var sub = WebhookSubscription.Create(tenantId, "https://api.exemplo.com/webhook", "secret123", "check.completed");

        sub.TenantId.Should().Be(tenantId);
        sub.TargetUrl.Should().Be("https://api.exemplo.com/webhook");
        sub.Secret.Should().Be("secret123");
        sub.Events.Should().Be("check.completed");
        sub.IsActive.Should().BeTrue();

        sub.Deactivate();
        sub.IsActive.Should().BeFalse();

        sub.Activate();
        sub.IsActive.Should().BeTrue();
    }

    [Fact]
    public void BackgroundCheck_EdgeCases_ShouldBeHandled()
    {
        var check = BackgroundCheck.Create(
            Guid.NewGuid(),
            SubjectType.Person,
            "52998224725",
            "529.982.247-25",
            "***.982.247-**",
            "Candidato",
            CheckPurpose.RecrutamentoESelecao,
            LegalBasis.Consentimento,
            true);

        check.MarkAsFailed("Erro irrecuperável de infraestrutura");

        check.Status.Should().Be(CheckStatus.Failed);
        check.CompletedAt.Should().NotBeNull();

        // StartProcessing em check não pendente não deve alterar status
        check.StartProcessing();
        check.Status.Should().Be(CheckStatus.Failed);

        // Purge repetido deve ser idempotente
        check.PurgePersonalData();
        check.PurgePersonalData();
        check.IsPurged.Should().BeTrue();
    }

    [Fact]
    public void ValueObjects_ToString_ShouldReturnValue()
    {
        var cpf = Cpf.Create("52998224725").Value!;
        cpf.ToString().Should().Be("52998224725");

        var cnpj = Cnpj.Create("11222333000181").Value!;
        cnpj.ToString().Should().Be("11222333000181");
    }
}

