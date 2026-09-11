using System.Collections.Concurrent;
using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;

namespace KIScheduler.Platforms.Testing;

/// <summary>Deterministic, independently controllable usage source for scheduler tests.</summary>
public sealed class FakeUsageProvider : IUsageProvider
{
    private readonly ConcurrentQueue<UsageReadResult> readings = new();
    private readonly ConcurrentQueue<bool> forceRefreshRequests = new();
    private UsageReadResult current = UsageReadResult.Unknown("Noch kein Fake-Snapshot gesetzt.");

    public FakeUsageProvider(PlatformId platformId, UsageProviderCapabilities? capabilities = null)
    {
        PlatformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
        Capabilities = capabilities ?? new UsageProviderCapabilities(true, true, true);
    }

    public PlatformId PlatformId { get; }
    public UsageProviderCapabilities Capabilities { get; }
    public IReadOnlyList<bool> ForceRefreshRequests => forceRefreshRequests.ToArray();
    public event EventHandler<UsageChangedEventArgs>? UsageChanged;

    public void Enqueue(UsageReadResult result) =>
        readings.Enqueue(result ?? throw new ArgumentNullException(nameof(result)));

    public void SetCurrent(UsageReadResult result, bool publishChange = false)
    {
        ArgumentNullException.ThrowIfNull(result);
        ValidateSnapshotPlatform(result);
        Volatile.Write(ref current, result);
        if (publishChange)
        {
            UsageChanged?.Invoke(this, new UsageChangedEventArgs(result));
        }
    }

    public Task<UsageReadResult> ReadAsync(bool forceRefresh,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        forceRefreshRequests.Enqueue(forceRefresh);
        if (readings.TryDequeue(out var reading))
        {
            ValidateSnapshotPlatform(reading);
            Volatile.Write(ref current, reading);
        }

        return Task.FromResult(Volatile.Read(ref current));
    }

    private void ValidateSnapshotPlatform(UsageReadResult result)
    {
        if (result.Snapshot is { } snapshot &&
            !string.Equals(snapshot.PlatformId.Value, PlatformId.Value, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Der Snapshot gehört zu '{snapshot.PlatformId.Value}', nicht zu '{PlatformId.Value}'.",
                nameof(result));
        }
    }
}
