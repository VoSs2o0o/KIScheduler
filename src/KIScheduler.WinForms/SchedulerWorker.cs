using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KIScheduler.WinForms;

internal sealed class SchedulerWorker(ILogger<SchedulerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("KIScheduler worker started.");

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }

        logger.LogInformation("KIScheduler worker stopped.");
    }
}
