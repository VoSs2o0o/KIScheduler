using System.Collections.ObjectModel;

namespace KIScheduler.Core.Domain;

public sealed class UsagePolicy
{
    public UsagePolicy(PlatformId platformId, IEnumerable<DayOfWeek> days, TimeOnly localStart, TimeOnly localEnd,
        string timeZoneId, UsagePercent maxUsedPercent, UnknownUsageBehavior unknownUsageBehavior,
        TimeSpan refreshInterval, ModelId? modelId = null, TimeSpan? endSprintDuration = null,
        UsagePercent? endSprintMaxUsedPercent = null)
    {
        PlatformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
        ModelId = modelId;
        var configuredDays = days?.Distinct().ToList() ?? throw new ArgumentNullException(nameof(days));
        if (configuredDays.Count == 0)
        {
            throw new ArgumentException("Mindestens ein Wochentag muss angegeben werden.", nameof(days));
        }

        if (localStart == localEnd)
        {
            throw new ArgumentException("Start- und Endzeit dürfen nicht identisch sein.", nameof(localEnd));
        }

        TimeZoneId = DomainValidation.Required(timeZoneId, nameof(timeZoneId));
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new ArgumentException("Die Zeitzone ist unbekannt.", nameof(timeZoneId), exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new ArgumentException("Die Zeitzone ist ungültig.", nameof(timeZoneId), exception);
        }

        if (refreshInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(refreshInterval), "Das Aktualisierungsintervall muss größer als null sein.");
        }

        if (endSprintDuration.HasValue && endSprintDuration.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(endSprintDuration), "Die Endspurt-Dauer muss größer als null sein.");
        }

        if (endSprintDuration.HasValue != endSprintMaxUsedPercent.HasValue)
        {
            throw new ArgumentException("Endspurt-Dauer und Endspurt-Grenze müssen gemeinsam angegeben werden.");
        }

        Days = new ReadOnlyCollection<DayOfWeek>(configuredDays);
        LocalStart = localStart;
        LocalEnd = localEnd;
        MaxUsedPercent = maxUsedPercent;
        UnknownUsageBehavior = unknownUsageBehavior;
        RefreshInterval = refreshInterval;
        EndSprintDuration = endSprintDuration;
        EndSprintMaxUsedPercent = endSprintMaxUsedPercent;
    }

    public PlatformId PlatformId { get; }
    public ModelId? ModelId { get; }
    public IReadOnlyList<DayOfWeek> Days { get; }
    public TimeOnly LocalStart { get; }
    public TimeOnly LocalEnd { get; }
    public string TimeZoneId { get; }
    public UsagePercent MaxUsedPercent { get; }
    public UnknownUsageBehavior UnknownUsageBehavior { get; }
    public TimeSpan RefreshInterval { get; }
    public TimeSpan? EndSprintDuration { get; }
    public UsagePercent? EndSprintMaxUsedPercent { get; }
}

public sealed record UsageWindow
{
    public UsageWindow(string name, UsagePercent usedPercent, DateTimeOffset? resetAtUtc,
        string source, DateTimeOffset readAtUtc, UsageQuality quality, string? rateLimitReachedType = null,
        TimeSpan? windowDuration = null, string? limitId = null, string? limitName = null)
    {
        Name = DomainValidation.Required(name, nameof(name));
        UsedPercent = usedPercent;
        ResetAtUtc = resetAtUtc is null ? null : DomainValidation.Utc(resetAtUtc.Value, nameof(resetAtUtc));
        Source = DomainValidation.Required(source, nameof(source));
        ReadAtUtc = DomainValidation.Utc(readAtUtc, nameof(readAtUtc));
        if (windowDuration.HasValue && windowDuration.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(windowDuration), "Die Fensterdauer muss größer als null sein.");
        }

        Quality = quality;
        RateLimitReachedType = string.IsNullOrWhiteSpace(rateLimitReachedType) ? null : rateLimitReachedType.Trim();
        WindowDuration = windowDuration;
        LimitId = string.IsNullOrWhiteSpace(limitId) ? null : limitId.Trim();
        LimitName = string.IsNullOrWhiteSpace(limitName) ? null : limitName.Trim();
    }

    public string Name { get; }
    public UsagePercent UsedPercent { get; }
    public DateTimeOffset? ResetAtUtc { get; }
    public string Source { get; }
    public DateTimeOffset ReadAtUtc { get; }
    public UsageQuality Quality { get; }
    public string? RateLimitReachedType { get; }
    public TimeSpan? WindowDuration { get; }
    public string? LimitId { get; }
    public string? LimitName { get; }
    public bool IsServerLimitReached => RateLimitReachedType is not null;
}

public sealed class UsageSnapshot
{
    public UsageSnapshot(PlatformId platformId, DateTimeOffset readAtUtc, string source,
        UsageQuality quality, IEnumerable<UsageWindow> windows)
    {
        PlatformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
        ReadAtUtc = DomainValidation.Utc(readAtUtc, nameof(readAtUtc));
        Source = DomainValidation.Required(source, nameof(source));
        Quality = quality;
        var configuredWindows = windows?.ToList() ?? throw new ArgumentNullException(nameof(windows));
        if (configuredWindows.Select(window => window.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != configuredWindows.Count)
        {
            throw new ArgumentException("Usage-Fensternamen müssen eindeutig sein.", nameof(windows));
        }

        Windows = new ReadOnlyCollection<UsageWindow>(configuredWindows);
    }

    public PlatformId PlatformId { get; }
    public DateTimeOffset ReadAtUtc { get; }
    public string Source { get; }
    public UsageQuality Quality { get; }
    public IReadOnlyList<UsageWindow> Windows { get; }
}
