using Application.Dtos;
using Application.UseCases.Checks;
using Infrastructure.BackgroundWorkers;
using Microsoft.AspNetCore.Mvc;

namespace MiniKycKyb.API.Controllers;

[ApiController]
[Route("api/v1/checks")]
public class ChecksController : ControllerBase
{
    private readonly CreateCheckUseCase _createCheckUseCase;
    private readonly GetCheckByIdUseCase _getCheckByIdUseCase;
    private readonly ReviewCheckUseCase _reviewCheckUseCase;
    private readonly PurgeExpiredChecksUseCase _purgeExpiredChecksUseCase;
    private readonly ICheckQueue _checkQueue;

    public ChecksController(
        CreateCheckUseCase createCheckUseCase,
        GetCheckByIdUseCase getCheckByIdUseCase,
        ReviewCheckUseCase reviewCheckUseCase,
        PurgeExpiredChecksUseCase purgeExpiredChecksUseCase,
        ICheckQueue checkQueue)
    {
        _createCheckUseCase = createCheckUseCase;
        _getCheckByIdUseCase = getCheckByIdUseCase;
        _reviewCheckUseCase = reviewCheckUseCase;
        _purgeExpiredChecksUseCase = purgeExpiredChecksUseCase;
        _checkQueue = checkQueue;
    }

    /// <summary>
    /// Dispara uma nova solicitação assíncrona de background check (KYC/KYB).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateCheck([FromBody] CreateCheckRequest request, CancellationToken cancellationToken)
    {
        var result = await _createCheckUseCase.ExecuteAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error, code = result.ErrorCode });
        }

        // Enfileira na fila em memória para processamento assíncrono imediato
        await _checkQueue.EnqueueAsync(result.Value!.Id, cancellationToken);

        return AcceptedAtAction(nameof(GetCheckById), new { id = result.Value.Id }, result.Value);
    }

    /// <summary>
    /// Obtém o relatório e indicadores consolidados de uma verificação.
    /// Dados são mascarados automaticamente conforme perfil e diretrizes da LGPD.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCheckById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _getCheckByIdUseCase.ExecuteAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            if (result.ErrorCode == "NOT_FOUND")
            {
                return NotFound(new { error = result.Error });
            }
            if (result.ErrorCode == "FORBIDDEN")
            {
                return Forbid();
            }
            return BadRequest(new { error = result.Error, code = result.ErrorCode });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// LGPD Art. 20: Registra formalmente a revisão humana da decisão assistida.
    /// O sistema nunca reprova sozinho; a decisão terminal é de competência humana.
    /// </summary>
    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> ReviewCheck(Guid id, [FromBody] ReviewCheckRequest request, CancellationToken cancellationToken)
    {
        var result = await _reviewCheckUseCase.ExecuteAsync(id, request, cancellationToken);
        if (result.IsFailure)
        {
            if (result.ErrorCode == "NOT_FOUND")
            {
                return NotFound(new { error = result.Error });
            }
            return BadRequest(new { error = result.Error, code = result.ErrorCode });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// LGPD: Executa o expurgo manual ou programado de dados pessoais expirados segundo a política de retenção.
    /// </summary>
    [HttpPost("purge-expired")]
    public async Task<IActionResult> PurgeExpired(CancellationToken cancellationToken)
    {
        var count = await _purgeExpiredChecksUseCase.ExecuteAsync(cancellationToken);
        return Ok(new
        {
            message = $"Rotina de expurgo LGPD concluída com sucesso.",
            purged_records = count,
            timestamp = DateTime.UtcNow
        });
    }
}

