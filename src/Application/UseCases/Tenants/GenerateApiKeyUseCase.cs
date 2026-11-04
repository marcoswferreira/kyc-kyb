using Application.Dtos;
using Domain.Common;
using Domain.Repositories;
using Domain.ValueObjects;

namespace Application.UseCases.Tenants;

public class GenerateApiKeyUseCase
{
    private readonly ITenantRepository _tenantRepository;

    public GenerateApiKeyUseCase(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public async Task<Result<ApiKeyResponse>> ExecuteAsync(
        Guid tenantId,
        string environment = "live",
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<ApiKeyResponse>.Failure("Tenant não encontrado.", "NOT_FOUND");
        }

        var apiKey = ApiKey.Generate(environment);
        tenant.UpdateApiKey(apiKey.Hash, apiKey.Prefix);

        await _tenantRepository.UpdateAsync(tenant, cancellationToken);

        return Result<ApiKeyResponse>.Success(new ApiKeyResponse(apiKey.Key, apiKey.Prefix));
    }
}

