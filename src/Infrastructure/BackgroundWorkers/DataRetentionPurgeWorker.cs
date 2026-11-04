using Application.UseCases.Checks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundWorkers;

public class DataRetentionPurgeWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataRetentionPurgeWorker> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(10);

    public DataRetentionPurgeWorker(
        IServiceProvider serviceProvider,
        ILogger<DataRetentionPurgeWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker de expurgo de dados LGPD iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var purgeUseCase = scope.ServiceProvider.GetRequiredService<PurgeExpiredChecksUseCase>();

                await purgeUseCase.ExecuteAsync(stoppingToken);

                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no worker de retenção e expurgo LGPD.");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}

