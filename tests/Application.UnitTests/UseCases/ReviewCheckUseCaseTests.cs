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

public class ReviewCheckUseCaseTests
{
    private readonly IBackgroundCheckRepository _checkRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IWebhookDispatcher _webhookDispatcher;
    private readonly ICurrentTenantContext _currentTenant;
    private readonly ReviewCheckUseCase _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public ReviewCheckUseCaseTests()
    {
        _checkRepository = Substitute.For<IBackgroundCheckRepository>();
        _auditLogRepository = Substitute.For<IAuditLogRepository>();
        _webhookDispatcher = Substitute.For<IWebhookDispatcher>();
        _currentTenant = Substitute.For<ICurrentTenantContext>();
        _currentTenant.TenantId.Returns(_tenantId);
        _currentTenant.Role.Returns(UserRole.ComplianceOfficer);
        _currentTenant.ActorId.Returns("officer_compliance_77");

        _sut = new ReviewCheckUseCase(_checkRepository, _auditLogRepository, _webhookDispatcher, _currentTenant);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidHumanDecision_ShouldPersistAuditAndDispatchWebhook()
    {
        var check = BackgroundCheck.Create(
            _tenantId,
            SubjectType.Person,
            "52998224725",
            "529.982.247-25",
            "***.982.247-**",
            "Candidato",
            CheckPurpose.RecrutamentoESelecao,
            LegalBasis.Consentimento,
            true);

        _checkRepository.GetByIdAsync(check.Id, Arg.Any<CancellationToken>())
            .Returns(check);

        var request = new ReviewCheckRequest(
            ReviewerId: "officer_compliance_77",
            Decision: HumanReviewDecision.ApprovedByHuman,
            Notes: "Apontamento processual cível avaliado. Causa extinta e sem condenação. Candidato aprovado pelo RH.");

        var result = await _sut.ExecuteAsync(check.Id, request);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Decision.Should().Be(HumanReviewDecision.ApprovedByHuman);

        await _checkRepository.Received(1).UpdateAsync(check, Arg.Any<CancellationToken>());
        await _auditLogRepository.Received(1).AddAsync(
            Arg.Is<AuditLogEntry>(a => a.Action == "HUMAN_REVIEW_RECORDED"),
            Arg.Any<CancellationToken>());
        await _webhookDispatcher.Received(1).DispatchAsync(check, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithoutJustification_ShouldFailUnderLgpdArt20()
    {
        var check = BackgroundCheck.Create(
            _tenantId,
            SubjectType.Person,
            "52998224725",
            "529.982.247-25",
            "***.982.247-**",
            "Candidato",
            CheckPurpose.RecrutamentoESelecao,
            LegalBasis.Consentimento,
            true);

        _checkRepository.GetByIdAsync(check.Id, Arg.Any<CancellationToken>())
            .Returns(check);

        var request = new ReviewCheckRequest(
            ReviewerId: "officer_01",
            Decision: HumanReviewDecision.ApprovedByHuman,
            Notes: ""); // Justificativa em branco

        var result = await _sut.ExecuteAsync(check.Id, request);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("LGPD_JUSTIFICATION_REQUIRED");
    }
}

