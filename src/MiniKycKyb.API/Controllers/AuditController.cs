using Application.UseCases.MeteringAndAudit;
using Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace MiniKycKyb.API.Controllers;

[ApiController]
[Route("api/v1/audit")]
public class AuditController : ControllerBase
{
    private readonly GetAuditLogsUseCase _auditUseCase;
    private readonly CurrentTenantContext _currentTenant;

    public AuditController(GetAuditLogsUseCase auditUseCase, CurrentTenantContext currentTenant)
    {
        _auditUseCase = auditUseCase;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Consulta os registros da cadeia imutável de auditoria (LGPD) com verificação de integridade criptográfica.
    /// </summary>
    [HttpGet]
    [HttpGet("logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.TenantId ?? Guid.Empty;
        var logs = await _auditUseCase.ExecuteAsync(tenantId, skip, take, cancellationToken);

        return Ok(new
        {
            total = logs.Count,
            all_valid = logs.All(l => l.IntegrityValid),
            logs
        });
    }
}

