using Application.UseCases.Checks;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Application.UnitTests.UseCases;

public class PurgeExpiredChecksUseCaseTests
{
    private readonly IBackgroundCheckRepository _checkRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<PurgeExpiredChecksUseCase> _logger;
    private readonly PurgeExpiredChecksUseCase _sut;

    public PurgeExpiredChecksUseCaseTests()
    {
        _checkRepository = Substitute.For<IBackgroundCheckRepository>();
        _auditLogRepository = Substitute.For<IAuditLogRepository>();
        _logger = Substitute.For<ILogger<PurgeExpiredChecksUseCase>>();

        _sut = new PurgeExpiredChecksUseCase(_checkRepository, _auditLogRepository, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_WithExpiredChecks_ShouldPurgeAndAudit()
    {
        var tenantId = Guid.NewGuid();
        var check1 = BackgroundCheck.Create(
            tenantId,
            SubjectType.Person,
            "52998224725",
            "529.982.247-25",
            "***.982.247-**",
            "Candidato Expirado",
            CheckPurpose.RecrutamentoESelecao,
            LegalBasis.Consentimento,
            true,
            retentionDays: -1); // Já expirado

        _checkRepository.GetExpiredChecksForPurgeAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<BackgroundCheck> { check1 });

        var purgedCount = await _sut.ExecuteAsync();

        purgedCount.Should().Be(1);
        check1.IsPurged.Should().BeTrue();
        check1.Name.Should().Be("[EXPURGADO_LGPD]");

        await _checkRepository.Received(1).UpdateAsync(check1, Arg.Any<CancellationToken>());
        await _auditLogRepository.Received(1).AddAsync(
            Arg.Is<AuditLogEntry>(a => a.Action == "DATA_PURGED_LGPD"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoExpiredChecks_ShouldReturnZero()
    {
        _checkRepository.GetExpiredChecksForPurgeAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<BackgroundCheck>());

        var purgedCount = await _sut.ExecuteAsync();

        purgedCount.Should().Be(0);
        await _checkRepository.DidNotReceive().UpdateAsync(Arg.Any<BackgroundCheck>(), Arg.Any<CancellationToken>());
    }
}

