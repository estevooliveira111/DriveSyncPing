using DriveSyncPing.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DriveSyncPing.Worker;

/// <summary>
/// Background service that fires an automatic synchronization whenever the
/// user-defined schedule says one is due (section 13 of the MVP plan).
/// </summary>
public class Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DriveSyncPing scheduler iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro no ciclo do scheduler.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TickAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var schedule = scope.ServiceProvider.GetRequiredService<IScheduleService>();

        if (!await schedule.IsRunDueAsync(DateTime.Now))
            return;

        logger.LogInformation("Agendamento disparado; iniciando sincronização automática.");

        var sync = scope.ServiceProvider.GetRequiredService<ISyncService>();
        var settings = await scope.ServiceProvider.GetRequiredService<ISettingsService>().GetAsync();

        await schedule.MarkRunAsync(DateTime.UtcNow);

        var options = new SyncRunOptions
        {
            DryRun = settings.DryRunByDefault,
            DeleteAfterUpload = settings.DeleteAfterUpload,
            DriveFolderName = settings.DriveFolderName,
            Trigger = DriveSyncPing.Domain.Enums.SyncTrigger.Scheduled
        };

        var job = await sync.RunSyncAsync(
            options,
            (status, progress) => logger.LogInformation("[{Progress}%] {Status}", progress, status),
            stoppingToken);

        logger.LogInformation(
            "Sincronização automática finalizada: {Status} (enviados {Uploaded}, duplicados {Ignored}, falhas {Failed}, excluídos {Deleted}).",
            job.Status, job.FilesUploaded, job.FilesIgnored, job.FilesFailed, job.FilesDeleted);
    }
}
