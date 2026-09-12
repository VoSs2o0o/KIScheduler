using KIScheduler.Core.Domain;
using KIScheduler.Core.Scheduling;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Scheduling;

[TestClass]
public sealed class HistoryEventFilterTests
{
    [TestMethod]
    public void RepeatedUsageEventsAreShownOnlyAfterConfiguredInterval()
    {
        var workItemId = WorkItemId.New();
        var start = new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);
        var events = new[]
        {
            Event(workItemId, start, "scheduler.decision", SchedulerReasonCodes.PercentLimitReached),
            Event(workItemId, start.AddMinutes(5), "scheduler.decision", SchedulerReasonCodes.PlatformBlocked),
            Event(workItemId, start.AddMinutes(6), "stdout", "Normale Ausgabe"),
            Event(workItemId, start.AddMinutes(10), "scheduler.decision", SchedulerReasonCodes.PlatformBlocked)
        };

        var visible = HistoryEventFilter.ThrottleUsageEvents(events, TimeSpan.FromMinutes(10));

        CollectionAssert.AreEqual(new[] { start, start.AddMinutes(6), start.AddMinutes(10) },
            visible.Select(x => x.OccurredAtUtc).ToArray());
    }

    private static ExecutionEvent Event(WorkItemId workItemId, DateTimeOffset occurredAt,
        string eventType, string message) => new(Guid.NewGuid(), workItemId, occurredAt,
        ExecutionEventSeverity.Information, eventType, message, data:
        new Dictionary<string, string> { ["reasonCode"] = message });
}
