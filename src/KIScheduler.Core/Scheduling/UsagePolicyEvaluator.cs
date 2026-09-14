using KIScheduler.Core.Domain;

namespace KIScheduler.Core.Scheduling;

public static class SchedulerReasonCodes
{
    public const string Allowed = "usage.allowed";
    public const string OutsideSchedule = "schedule.outside_window";
    public const string UnknownUsageBlocked = "usage.unknown_blocked";
    public const string UnknownUsageAllowed = "usage.unknown_allowed";
    public const string StaleUsageBlocked = "usage.stale_blocked";
    public const string StaleUsageAllowed = "usage.stale_allowed";
    public const string ServerLimitReached = "usage.server_limit_reached";
    public const string PercentLimitReached = "usage.percent_limit_reached";
    public const string PlatformBlocked = "platform.usage_blocked";
    public const string ProjectHeld = "project.execution_held";
    public const string PlatformBusy = "platform.busy";
    public const string PlatformDisabled = "platform.disabled";
    public const string ProfileMissing = "profile.missing";
    public const string ProfileDisabled = "profile.disabled";
    public const string ProfilePlatformMismatch = "profile.platform_mismatch";
    public const string ProfileChanged = "profile.changed_after_selection";
    public const string ProjectBusy = "project.busy";
    public const string ReservationConflict = "scheduler.reservation_conflict";
    public const string Reserved = "scheduler.reserved";
    public const string ResumeUnavailable = "execution.resume_unavailable";
}

public sealed record UsageWindowDecision(
    string WindowName,
    decimal UsedPercent,
    decimal EffectiveLimit,
    bool EndSprintActive,
    bool IsAllowed,
    string ReasonCode);

public sealed record UsageDecision(
    bool IsAllowed,
    string ReasonCode,
    bool HasApplicablePolicy,
    bool IsFreshSnapshot,
    IReadOnlyList<UsageWindowDecision> Windows)
{
    public static UsageDecision Blocked(string reasonCode, bool hasApplicablePolicy = true) =>
        new(false, reasonCode, hasApplicablePolicy, false, []);
}

/// <summary>
/// Evaluates local schedule windows and all usage windows without reading system time.
/// This keeps the boundary behavior deterministic and directly testable with a fake clock.
/// </summary>
public sealed class UsagePolicyEvaluator
{
    public UsageDecision Evaluate(PlatformId platformId, ModelId modelId,
        IReadOnlyCollection<UsagePolicy> policies, UsageSnapshot? snapshot, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(platformId);
        ArgumentNullException.ThrowIfNull(modelId);
        ArgumentNullException.ThrowIfNull(policies);
        EnsureUtc(nowUtc);

        var matching = policies
            .Where(policy => PlatformEquals(policy.PlatformId, platformId)
                && (policy.ModelId is null || ModelEquals(policy.ModelId, modelId))
                && IsActive(policy, nowUtc))
            .ToList();

        if (matching.Count == 0)
        {
            return UsageDecision.Blocked(SchedulerReasonCodes.OutsideSchedule, hasApplicablePolicy: false);
        }

        // A model-specific rule takes precedence over generic rules for the same instant.
        var specific = matching.Where(policy => policy.ModelId is not null).ToList();
        var effectivePolicies = specific.Count > 0 ? specific : matching;
        var allowUnknown = effectivePolicies.All(policy => policy.UnknownUsageBehavior == UnknownUsageBehavior.Erlauben);

        // A known server-side limit is a hard block, even when the remainder of the
        // snapshot is old or incomplete and the policy would otherwise allow unknown usage.
        if (snapshot is not null && PlatformEquals(snapshot.PlatformId, platformId)
            && snapshot.Windows.Any(window => window.IsServerLimitReached))
        {
            return UsageDecision.Blocked(SchedulerReasonCodes.ServerLimitReached);
        }

        if (snapshot is null || !PlatformEquals(snapshot.PlatformId, platformId)
            || snapshot.Quality is UsageQuality.Unbekannt or UsageQuality.Geschaetzt
            || snapshot.Windows.Count == 0)
        {
            return new UsageDecision(allowUnknown,
                allowUnknown ? SchedulerReasonCodes.UnknownUsageAllowed : SchedulerReasonCodes.UnknownUsageBlocked,
                true, false, []);
        }

        var isStale = snapshot.Quality == UsageQuality.Veraltet
            || effectivePolicies.Any(policy => nowUtc - snapshot.ReadAtUtc > policy.RefreshInterval)
            || snapshot.Windows.Any(window => window.Quality != UsageQuality.Aktuell
                || effectivePolicies.Any(policy => nowUtc - window.ReadAtUtc > policy.RefreshInterval));
        if (isStale)
        {
            return new UsageDecision(allowUnknown,
                allowUnknown ? SchedulerReasonCodes.StaleUsageAllowed : SchedulerReasonCodes.StaleUsageBlocked,
                true, false, []);
        }

        var decisions = new List<UsageWindowDecision>(snapshot.Windows.Count);
        foreach (var window in snapshot.Windows)
        {
            foreach (var policy in effectivePolicies)
            {
                var endSprint = policy.EndSprintDuration.HasValue
                    && window.ResetAtUtc.HasValue
                    && window.ResetAtUtc.Value >= nowUtc
                    && window.ResetAtUtc.Value - nowUtc <= policy.EndSprintDuration.Value;
                var limit = endSprint
                    ? policy.EndSprintMaxUsedPercent!.Value.Value
                    : policy.MaxUsedPercent.Value;
                var reason = window.IsServerLimitReached
                    ? SchedulerReasonCodes.ServerLimitReached
                    : window.UsedPercent.Value < limit
                        ? SchedulerReasonCodes.Allowed
                        : SchedulerReasonCodes.PercentLimitReached;
                decisions.Add(new UsageWindowDecision(window.Name, window.UsedPercent.Value, limit,
                    endSprint, reason == SchedulerReasonCodes.Allowed, reason));
            }
        }

        var firstBlock = decisions.FirstOrDefault(decision => !decision.IsAllowed);
        return new UsageDecision(firstBlock is null,
            firstBlock?.ReasonCode ?? SchedulerReasonCodes.Allowed, true, true, decisions);
    }

    public bool IsActive(UsagePolicy policy, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(policy);
        EnsureUtc(nowUtc);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId);
        var local = TimeZoneInfo.ConvertTime(nowUtc, zone);
        var time = TimeOnly.FromDateTime(local.DateTime);

        if (policy.LocalStart < policy.LocalEnd)
        {
            return policy.Days.Contains(local.DayOfWeek)
                && time >= policy.LocalStart && time < policy.LocalEnd;
        }

        // For a window crossing midnight, the configured weekday denotes the day on which it starts.
        return time >= policy.LocalStart
            ? policy.Days.Contains(local.DayOfWeek)
            : time < policy.LocalEnd && policy.Days.Contains(local.AddDays(-1).DayOfWeek);
    }

    private static bool PlatformEquals(PlatformId left, PlatformId right) =>
        string.Equals(left.Value, right.Value, StringComparison.OrdinalIgnoreCase);

    private static bool ModelEquals(ModelId left, ModelId right) =>
        string.Equals(left.Value, right.Value, StringComparison.OrdinalIgnoreCase);

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Zeitangaben müssen in UTC vorliegen.", nameof(value));
        }
    }
}
