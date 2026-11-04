using Application.Interfaces;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundWorkers;

public class CheckOrchestrationWorker : BackgroundService
{
    private readonly ICheckQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CheckOrchestrationWorker> _logger;

    public CheckOrchestrationWorker(
        ICheckQueue queue,
        IServiceProvider serviceProvider,
        ILogger<CheckOrchestrationWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker de orquestração assíncrona iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var checkId = await _queue.DequeueAsync(stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var checkRepo = scope.ServiceProvider.GetRequiredService<IBackgroundCheckRepository>();
                var orchestrator = scope.ServiceProvider.GetRequiredService<ICheckOrchestrator>();
                var policyEngine = scope.ServiceProvider.GetRequiredService<IRiskPolicyEngine>();
                var aiAssistant = scope.ServiceProvider.GetRequiredService<IAiAssistantService>();
                var webhookDispatcher = scope.ServiceProvider.GetRequiredService<IWebhookDispatcher>();
                var usageRepo = scope.ServiceProvider.GetRequiredService<IUsageMetricRepository>();
                var auditRepo = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

                var check = await checkRepo.GetByIdAsync(checkId, stoppingToken);
                if (check is null || check.Status != Domain.Enums.CheckStatus.Pending)
                {
                    continue;
                }

                check.StartProcessing();
                await checkRepo.UpdateAsync(check, stoppingToken);

                // Executa consultas concorrentes com timeout e circuit breaker
                var orchestratorResult = await orchestrator.ExecuteAllProvidersAsync(
                    check.DocumentNumber,
                    check.SubjectType,
                    stoppingToken);

                // Avalia regras de risco e semáforo
                var policyResult = policyEngine.Evaluate(check.Id, orchestratorResult.ProviderResults);

                // Gera parecer explicativo com IA (citando evidências, sem reprovação automática - Art. 20)
                var aiSummary = await aiAssistant.GenerateReportSummaryAsync(
                    check,
                    policyResult.Findings,
                    stoppingToken);

                // Completa o background check
                check.Complete(
                    policyResult.OverallRisk,
                    policyResult.Findings,
                    orchestratorResult.RawEvidenceJson,
                    orchestratorResult.EvidenceSha256,
                    orchestratorResult.HasPartialWarnings,
                    aiSummary);

                await checkRepo.UpdateAsync(check, stoppingToken);

                // Medição de consumo do SaaS
                var yearMonth = DateTime.UtcNow.ToString("yyyy-MM");
                await usageRepo.AddOrUpdateUsageAsync(
                    check.TenantId,
                    yearMonth,
                    checks: 1,
                    providerCalls: orchestratorResult.ProviderResults.Count,
                    stoppingToken);

                // Auditoria imutável
                var latestAudit = await auditRepo.GetLatestEntryAsync(check.TenantId, stoppingToken);
                var previousHash = latestAudit?.Hash ?? "0000000000000000000000000000000000000000000000000000000000000000";

                var audit = AuditLogEntry.Create(
                    check.TenantId,
                    "orchestration_worker",
                    "CHECK_COMPLETED",
                    check.Id.ToString(),
                    $"Verificação concluída com status {check.Status} e risco {check.OverallRisk}. Total apontamentos: {policyResult.Findings.Count}.",
                    "127.0.0.1",
                    previousHash);

                await auditRepo.AddAsync(audit, stoppingToken);

                // Notificação de Webhook
                await webhookDispatcher.DispatchAsync(check, stoppingToken);

                _logger.LogInformation(
                    "Background check {CheckId} concluído com sucesso. Risco: {Risk}. Parcial: {Partial}",
                    check.Id,
                    check.OverallRisk,
                    orchestratorResult.HasPartialWarnings);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no processamento de background check no worker.");
            }
        }
    }
}

