using KIScheduler.Core.Domain;

namespace KIScheduler.Core.Contracts;

public interface IUsageProvider
{
    PlatformId PlatformId { get; }
    UsageProviderCapabilities Capabilities { get; }
    Task<UsageReadResult> ReadAsync(bool forceRefresh,
        CancellationToken cancellationToken = default);
    event EventHandler<UsageChangedEventArgs>? UsageChanged;
}

public sealed record UsageProviderCapabilities(
    bool SupportsPushUpdates,
    bool SupportsMultipleWindows,
    bool ProvidesResetTime);

public enum UsageReadStatus
{
    Available,
    Unknown,
    ProviderUnavailable
}

public sealed record UsageReadResult
{
    public UsageReadResult(UsageReadStatus status, UsageSnapshot? snapshot = null, string? message = null)
    {
        if (status == UsageReadStatus.Available && snapshot is null)
        {
            throw new ArgumentException("Ein verfügbares Usage-Ergebnis benötigt einen Snapshot.", nameof(snapshot));
        }

        Status = status;
        Snapshot = snapshot;
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
    }

    public UsageReadStatus Status { get; }
    public UsageSnapshot? Snapshot { get; }
    public string? Message { get; }
    public bool IsAvailable => Status == UsageReadStatus.Available;

    public static UsageReadResult Available(UsageSnapshot snapshot) =>
        new(UsageReadStatus.Available, snapshot ?? throw new ArgumentNullException(nameof(snapshot)));

    public static UsageReadResult Unknown(string? message = null) =>
        new(UsageReadStatus.Unknown, message: message);
}

public sealed class UsageChangedEventArgs : EventArgs
{
    public UsageChangedEventArgs(UsageReadResult result) =>
        Result = result ?? throw new ArgumentNullException(nameof(result));

    public UsageReadResult Result { get; }
}
