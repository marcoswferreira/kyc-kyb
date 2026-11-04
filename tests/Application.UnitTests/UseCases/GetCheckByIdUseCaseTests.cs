using Application.Interfaces;
using Application.UseCases.Checks;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Application.UnitTests.UseCases;

public class GetCheckByIdUseCaseTests
{
    private readonly IBackgroundCheckRepository _checkRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICurrentTenantContext _currentTenant;
    private readonly GetCheckByIdUseCase _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public GetCheckByIdUseCaseTests()
    {
        _checkRepository = Substitute.For<IBackgroundCheckRepository>();
        _auditLogRepository = Substitute.For<IAuditLogRepository>();
        _currentTenant = Substitute.For<ICurrentTenantContext>();
        _currentTenant.TenantId.Returns(_tenantId);
        _currentTenant.ActorId.Returns("auditor_01");
        _currentTenant.IpAddress.Returns("127.0.0.1");

        _sut = new GetCheckByIdUseCase(_checkRepository, _auditLogRepository, _currentTenant);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCheckNotFound_ShouldReturnFailure()
    {
        _checkRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((BackgroundCheck?)null);

        var result = await _sut.ExecuteAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task ExecuteAsync_AsOperator_ShouldReturnMaskedDocumentUnderLgpd()
    {
        var check = BackgroundCheck.Create(
            _tenantId,
            SubjectType.Person,
            "52998224725",
            "529.982.247-25",
            "***.982.247-**",
            "Candidato Silva",
            CheckPurpose.RecrutamentoESelecao,
            LegalBasis.Consentimento,
            true);

        _checkRepository.GetByIdAsync(check.Id, Arg.Any<CancellationToken>())
            .Returns(check);

        _currentTenant.Role.Returns(UserRole.Operator);

        var result = await _sut.ExecuteAsync(check.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DocumentFormatted.Should().Be("***.982.247-**"); // Mascarado para Operator
        result.Value.DocumentNumber.Should().Be("***.982.247-**");

        await _auditLogRepository.Received(1).AddAsync(
            Arg.Is<AuditLogEntry>(a => a.Action == "CHECK_VIEWED_MASKED"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_AsComplianceOfficer_ShouldReturnUnmaskedDocument()
    {
        var check = BackgroundCheck.Create(
            _tenantId,
            SubjectType.Person,
            "52998224725",
            "529.982.247-25",
            "***.982.247-**",
            "Candidato Silva",
            CheckPurpose.RecrutamentoESelecao,
            LegalBasis.Consentimento,
            true);

        _checkRepository.GetByIdAsync(check.Id, Arg.Any<CancellationToken>())
            .Returns(check);

        _currentTenant.Role.Returns(UserRole.ComplianceOfficer);

        var result = await _sut.ExecuteAsync(check.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DocumentFormatted.Should().Be("529.982.247-25"); // Desmascarado para Compliance Officer
        result.Value.DocumentNumber.Should().Be("52998224725");

        await _auditLogRepository.Received(1).AddAsync(
            Arg.Is<AuditLogEntry>(a => a.Action == "CHECK_VIEWED_UNMASKED"),
            Arg.Any<CancellationToken>());
    }
}

