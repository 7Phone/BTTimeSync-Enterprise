using BTTimeSync.Application;

namespace BTTimeSync.Service;

public class Worker(
    ILogger<Worker> logger,
    TimeSyncApplication application)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "BTTimeSync Service started.");

        await application.RunAsync(stoppingToken);
    }
}