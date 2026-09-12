using KIScheduler.Core.Domain;

namespace KIScheduler.Core.Scheduling;

public static class HistoryEventFilter
{
    public static IReadOnlyList<ExecutionEvent> ThrottleUsageEvents(
        IEnumerable<ExecutionEvent> events, TimeSpan minimumInterval)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (minimumInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(minimumInterval));

        var result = new List<ExecutionEvent>();
        DateTimeOffset? lastUsageEventAt = null;
        foreach (var executionEvent in events.OrderBy(x => x.OccurredAtUtc))
        {
            if (IsUsageEvent(executionEvent))
            {
                if (lastUsageEventAt.HasValue
                    && executionEvent.OccurredAtUtc - lastUsageEventAt.Value < minimumInterval)
                    continue;
                lastUsageEventAt = executionEvent.OccurredAtUtc;
            }
            result.Add(executionEvent);
        }
        return result;
    }

    private static bool IsUsageEvent(ExecutionEvent executionEvent)
    {
        if (executionEvent.EventType.Equals("usage_exceeded", StringComparison.OrdinalIgnoreCase)) return true;
        if (!executionEvent.EventType.Equals("scheduler.decision", StringComparison.OrdinalIgnoreCase)) return false;
        var reasonCode = executionEvent.Data.GetValueOrDefault("reasonCode") ?? executionEvent.Message;
        return reasonCode.StartsWith("usage.", StringComparison.OrdinalIgnoreCase)
            || reasonCode.Equals(SchedulerReasonCodes.PlatformBlocked, StringComparison.OrdinalIgnoreCase);
    }
}
