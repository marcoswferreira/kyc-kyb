using Application.Dtos;
using Application.Interfaces;
using Application.UseCases.Checks;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Application.UnitTests.UseCases;

public class CreateCheckUseCaseTests
{
    private readonly IBackgroundCheckRepository _checkRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICurrentTenantContext _currentTenant;
    private readonly CreateCheckUseCase _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public CreateCheckUseCaseTests()
    {
        _checkRepository = Substitute.For<IBackgroundCheckRepository>();
        _auditLogRepository = Substitute.For<IAuditLogRepository>();
        _currentTenant = Substitute.For<ICurrentTenantContext>();
        _currentTenant.TenantId.Returns(_tenantId);
        _currentTenant.ActorId.Returns("test_operator");
        _currentTenant.IpAddress.Returns("192.168.1.1");

        _sut = new CreateCheckUseCase(_checkRepository, _auditLogRepository, _currentTenant);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutConsent_ShouldFailUnderLgpd()
    {
        var request = new CreateCheckRequest(
            Document: "52998224725",
            SubjectType: SubjectType.Person,
            Name: "Candidato",
            Purpose: CheckPurpose.RecrutamentoESelecao,
            LegalBasis: LegalBasis.Consentimento,
            ConsentObtained: false); // Não obteve consentimento

        var result = await _sut.ExecuteAsync(request);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("LGPD_CONSENT_REQUIRED");
        await _checkRepository.DidNotReceive().AddAsync(Arg.Any<BackgroundCheck>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCpf_ShouldFailValidation()
    {
        var request = new CreateCheckRequest(
            Document: "123456", // CPF inválido
            SubjectType: SubjectType.Person,
            Name: "Candidato",
            Purpose: CheckPurpose.RecrutamentoESelecao,
            LegalBasis: LegalBasis.Consentimento,
            ConsentObtained: true);

        var result = await _sut.ExecuteAsync(request);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().StartWith("INVALID_CPF");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCpf_ShouldPersistCheckAndAuditEntry()
    {
        var request = new CreateCheckRequest(
            Document: "52998224725",
            SubjectType: SubjectType.Person,
            Name: "Candidato Aprovado Teste",
            Purpose: CheckPurpose.RecrutamentoESelecao,
            LegalBasis: LegalBasis.Consentimento,
            ConsentObtained: true);

        var result = await _sut.ExecuteAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.TenantId.Should().Be(_tenantId);
        result.Value.DocumentMasked.Should().Be("***.982.247-**");

        await _checkRepository.Received(1).AddAsync(Arg.Any<BackgroundCheck>(), Arg.Any<CancellationToken>());
        await _auditLogRepository.Received(1).AddAsync(Arg.Is<AuditLogEntry>(a => a.Action == "CHECK_CREATED"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithValidAlphanumericCnpj_ShouldSucceed()
    {
        var request = new CreateCheckRequest(
            Document: "12ABC345000188", // CNPJ Alfanumérico IN RFB nº 2.229/2024
            SubjectType: SubjectType.Company,
            Name: "Empresa Inovadora Alfanumérica Ltda",
            Purpose: CheckPurpose.ComplianceKyb,
            LegalBasis: LegalBasis.ObrigacaoLegal,
            ConsentObtained: true);

        var result = await _sut.ExecuteAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DocumentMasked.Should().Be("**.ABC.***0001-**");

        await _checkRepository.Received(1).AddAsync(Arg.Any<BackgroundCheck>(), Arg.Any<CancellationToken>());
    }
}

