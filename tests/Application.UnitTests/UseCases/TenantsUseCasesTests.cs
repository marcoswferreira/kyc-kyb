using Application.Dtos;
using Application.UseCases.Tenants;
using Domain.Entities;
using Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Application.UnitTests.UseCases;

public class TenantsUseCasesTests
{
    private readonly ITenantRepository _tenantRepository;
    private readonly CreateTenantUseCase _createSut;
    private readonly GenerateApiKeyUseCase _apiKeySut;

    public TenantsUseCasesTests()
    {
        _tenantRepository = Substitute.For<ITenantRepository>();
        _createSut = new CreateTenantUseCase(_tenantRepository);
        _apiKeySut = new GenerateApiKeyUseCase(_tenantRepository);
    }

    [Fact]
    public async Task CreateTenant_WithNewSlug_ShouldSucceedAndReturnLiveKey()
    {
        _tenantRepository.GetBySlugAsync("fintech-brasil", Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        var request = new CreateTenantRequest("Fintech Brasil", "fintech-brasil", 100);

        var result = await _createSut.ExecuteAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Response.Slug.Should().Be("fintech-brasil");
        result.Value.ApiKey.RawApiKey.Should().StartWith("sk_live_");

        await _tenantRepository.Received(1).AddAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTenant_WithExistingSlug_ShouldFail()
    {
        var existing = Tenant.Create("Fintech Existente", "fintech-brasil", "hash", "sk_live_123");
        _tenantRepository.GetBySlugAsync("fintech-brasil", Arg.Any<CancellationToken>())
            .Returns(existing);

        var request = new CreateTenantRequest("Fintech Brasil", "fintech-brasil", 100);

        var result = await _createSut.ExecuteAsync(request);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("SLUG_ALREADY_EXISTS");
    }

    [Fact]
    public async Task GenerateApiKey_WithValidTenant_ShouldRotateKey()
    {
        var tenant = Tenant.Create("Tenant Teste", "slug-teste", "hash_antigo", "sk_live_ant");
        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var result = await _apiKeySut.ExecuteAsync(tenant.Id, "test");

        result.IsSuccess.Should().BeTrue();
        result.Value!.RawApiKey.Should().StartWith("sk_test_");

        await _tenantRepository.Received(1).UpdateAsync(tenant, Arg.Any<CancellationToken>());
    }
}

