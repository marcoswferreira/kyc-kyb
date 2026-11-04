using Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Domain.UnitTests.Entities;

public class AuditLogEntryTests
{
    [Fact]
    public void Create_ShouldCalculateCryptographicHashAndVerifyIntegrity()
    {
        var tenantId = Guid.NewGuid();
        var entry = AuditLogEntry.Create(
            tenantId,
            "user_compliance_01",
            "CHECK_CREATED",
            "check_123",
            "Consulta de CPF disparada com finalidade de recrutamento",
            "192.168.1.10",
            "0000000000000000000000000000000000000000000000000000000000000000");

        entry.TenantId.Should().Be(tenantId);
        entry.Hash.Should().HaveLength(64);
        entry.VerifyIntegrity().Should().BeTrue();
    }

    [Fact]
    public void ChainedAuditLog_ShouldFormImmutableHashChain()
    {
        var tenantId = Guid.NewGuid();
        var genesis = AuditLogEntry.Create(
            tenantId,
            "system",
            "TENANT_INITIALIZED",
            tenantId.ToString(),
            "Tenant provisionado",
            "127.0.0.1",
            "0000000000000000000000000000000000000000000000000000000000000000");

        var second = AuditLogEntry.Create(
            tenantId,
            "operator_01",
            "CHECK_CREATED",
            "chk_01",
            "Checagem iniciada",
            "10.0.0.1",
            genesis.Hash);

        second.PreviousHash.Should().Be(genesis.Hash);
        second.VerifyIntegrity().Should().BeTrue();
        genesis.VerifyIntegrity().Should().BeTrue();
    }
}

