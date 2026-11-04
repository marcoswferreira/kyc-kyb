using Application.UseCases.MeteringAndAudit;
using Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace MiniKycKyb.API.Controllers;

[ApiController]
[Route("api/v1/metering")]
public class MeteringController : ControllerBase
{
    private readonly GetTenantUsageUseCase _usageUseCase;
    private readonly CurrentTenantContext _currentTenant;

    public MeteringController(GetTenantUsageUseCase usageUseCase, CurrentTenantContext currentTenant)
    {
        _usageUseCase = usageUseCase;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Consulta o consumo e métricas de faturamento do tenant no mês (ano-mês).
    /// </summary>
    [HttpGet]
    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage([FromQuery] string? yearMonth, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId ?? Guid.Empty;
        var month = string.IsNullOrWhiteSpace(yearMonth) ? DateTime.UtcNow.ToString("yyyy-MM") : yearMonth;

        var result = await _usageUseCase.ExecuteAsync(tenantId, month, cancellationToken);
        return Ok(result.Value);
    }
}

