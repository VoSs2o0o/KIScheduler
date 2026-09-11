using KIScheduler.Core.Scheduling;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KIScheduler.WinForms;

internal sealed class SchedulerWorker(ISchedulerEngine scheduler, SchedulerOptions options,
    ILogger<SchedulerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("KIScheduler worker started.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var dispatched = await scheduler.RunCycleAsync(stoppingToken);
                    if (dispatched > 0)
                        logger.LogInformation("Scheduler dispatched {Count} work item(s).", dispatched);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Scheduler cycle failed; the next cycle will retry.");
                }
                await Task.Delay(options.PollInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        finally
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await scheduler.StopAsync(timeout.Token);
        }

        logger.LogInformation("KIScheduler worker stopped.");
    }
}
