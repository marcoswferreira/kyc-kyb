using Application.Dtos;
using Domain.Common;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueObjects;

namespace Application.UseCases.Tenants;

public class CreateTenantUseCase
{
    private readonly ITenantRepository _tenantRepository;

    public CreateTenantUseCase(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public async Task<Result<(TenantResponse Response, ApiKeyResponse ApiKey)>> ExecuteAsync(
        CreateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _tenantRepository.GetBySlugAsync(request.Slug, cancellationToken);
        if (existing is not null)
        {
            return Result<(TenantResponse, ApiKeyResponse)>.Failure("Já existe um tenant com este slug.", "SLUG_ALREADY_EXISTS");
        }

        var apiKey = ApiKey.Generate("live");

        var tenant = Tenant.Create(
            request.Name,
            request.Slug,
            apiKey.Hash,
            apiKey.Prefix,
            request.RateLimitPerMinute);

        await _tenantRepository.AddAsync(tenant, cancellationToken);

        var tenantResponse = new TenantResponse(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.ApiKeyPrefix,
            tenant.RateLimitPerMinute,
            tenant.IsActive,
            tenant.CreatedAt);

        var apiKeyResponse = new ApiKeyResponse(apiKey.Key, apiKey.Prefix);

        return Result<(TenantResponse, ApiKeyResponse)>.Success((tenantResponse, apiKeyResponse));
    }
}

