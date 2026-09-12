using KIScheduler.Core.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KIScheduler.Infrastructure.Persistence;

/// <summary>Cross-process lock scoped to one SQLite database path.</summary>
public sealed class DatabaseWorkerLock(string databasePath) : IDisposable
{
    private readonly string lockPath = Path.GetFullPath(databasePath) + ".worker.lock";
    private FileStream? handle;

    public string LockPath => lockPath;
    public bool IsHeld => handle is not null;

    public bool TryAcquire()
    {
        if (handle is not null) return true;
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        try
        {
            handle = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                FileShare.None, 1, FileOptions.WriteThrough);
            handle.SetLength(0);
            using var writer = new StreamWriter(handle, leaveOpen: true);
            writer.Write($"process={Environment.ProcessId}; acquired={DateTimeOffset.UtcNow:O}");
            writer.Flush();
            handle.Flush(flushToDisk: true);
            return true;
        }
        catch (IOException)
        {
            handle?.Dispose();
            handle = null;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            handle?.Dispose();
            handle = null;
            return false;
        }
    }

    public void Dispose()
    {
        handle?.Dispose();
        handle = null;
    }
}

public sealed class DatabaseWorkerLockException(string lockPath)
    : InvalidOperationException($"Für diese Datenbank ist bereits ein KIScheduler-Worker aktiv ({lockPath}).")
{
    public string LockPath { get; } = lockPath;
}

internal sealed class DatabaseWorkerLockHostedService(
    DatabaseWorkerLock workerLock,
    ILogger<DatabaseWorkerLockHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!workerLock.TryAcquire())
            throw new DatabaseWorkerLockException(workerLock.LockPath);
        logger.LogInformation("Exclusive database worker lock acquired at {LockPath}.", workerLock.LockPath);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        workerLock.Dispose();
        return Task.CompletedTask;
    }
}

internal sealed class StartupRecoveryHostedService(
    IStartupRecoveryRepository recovery,
    IClock clock,
    ILogger<StartupRecoveryHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var result = await recovery.RecoverInterruptedAsync(clock.UtcNow,
            $"process-{Environment.ProcessId}", cancellationToken);
        if (result.InterruptedWorkItemCount > 0 || result.RemovedLeaseCount > 0)
            logger.LogWarning(
                "Startup recovery interrupted {WorkItems} orphaned work item(s), created {Holds} project hold(s), and removed {Leases} lease(s).",
                result.InterruptedWorkItemCount, result.CreatedProjectHoldCount, result.RemovedLeaseCount);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
