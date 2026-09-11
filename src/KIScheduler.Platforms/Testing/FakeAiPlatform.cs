using System.Collections.Concurrent;
using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;

namespace KIScheduler.Platforms.Testing;

/// <summary>Deterministic platform adapter for scheduler and concurrency tests.</summary>
public sealed class FakeAiPlatform : IAiPlatform
{
    private readonly ConcurrentQueue<Func<PlatformExecutionRequest, CancellationToken,
        Task<PlatformExecutionResult>>> executions = new();
    private readonly ConcurrentQueue<PlatformExecutionRequest> requests = new();
    private int activeExecutions;
    private int maximumConcurrentExecutions;

    public FakeAiPlatform(PlatformId platformId, PlatformCapabilities? capabilities = null)
    {
        PlatformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
        Capabilities = capabilities ?? new PlatformCapabilities(true, true, true);
    }

    public PlatformId PlatformId { get; }
    public PlatformCapabilities Capabilities { get; }
    public PlatformHealth Health { get; set; } = PlatformHealth.Available();
    public IReadOnlyList<PlatformExecutionRequest> Requests => requests.ToArray();
    public int ActiveExecutions => Volatile.Read(ref activeExecutions);
    public int MaximumConcurrentExecutions => Volatile.Read(ref maximumConcurrentExecutions);

    public Task<PlatformHealth> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Health);
    }

    public void EnqueueResult(PlatformExecutionResult result, TimeSpan? delay = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        EnqueueExecution(async (_, cancellationToken) =>
        {
            if (delay is { } duration && duration > TimeSpan.Zero)
            {
                await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
            }

            return result;
        });
    }

    public void EnqueueExecution(Func<PlatformExecutionRequest, CancellationToken,
        Task<PlatformExecutionResult>> execution)
    {
        executions.Enqueue(execution ?? throw new ArgumentNullException(nameof(execution)));
    }

    public async Task<PlatformExecutionResult> ExecuteAsync(PlatformExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.PlatformId.Value, PlatformId.Value, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Die Anfrage gehört zu einer anderen Plattform.", nameof(request));
        }

        requests.Enqueue(request);
        var current = Interlocked.Increment(ref activeExecutions);
        UpdateMaximum(current);
        try
        {
            if (!executions.TryDequeue(out var execution))
            {
                return new PlatformExecutionResult(PlatformExecutionOutcome.Succeeded, 0,
                    Capabilities.ProvidesSessionId ? $"fake-{Requests.Count}" : null);
            }

            return await execution(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref activeExecutions);
        }
    }

    private void UpdateMaximum(int candidate)
    {
        while (true)
        {
            var current = Volatile.Read(ref maximumConcurrentExecutions);
            if (candidate <= current || Interlocked.CompareExchange(
                    ref maximumConcurrentExecutions, candidate, current) == current)
            {
                return;
            }
        }
    }
}
