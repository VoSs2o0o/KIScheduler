namespace KIScheduler.Core.Scheduling;

public sealed class SchedulerOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromHours(2);
    public TimeSpan HistoryUsageEventInterval { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan AgingInterval { get; set; } = TimeSpan.FromMinutes(30);
    public int AgingBonusPerInterval { get; set; } = 1;

    public void Validate()
    {
        if (PollInterval <= TimeSpan.Zero) throw new InvalidOperationException("PollInterval muss größer als null sein.");
        if (LeaseDuration <= TimeSpan.Zero) throw new InvalidOperationException("LeaseDuration muss größer als null sein.");
        if (ExecutionTimeout <= TimeSpan.Zero) throw new InvalidOperationException("ExecutionTimeout muss größer als null sein.");
        if (HistoryUsageEventInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("HistoryUsageEventInterval muss größer als null sein.");
        if (LeaseDuration < ExecutionTimeout)
            throw new InvalidOperationException("LeaseDuration darf nicht kürzer als ExecutionTimeout sein.");
        if (AgingInterval <= TimeSpan.Zero) throw new InvalidOperationException("AgingInterval muss größer als null sein.");
        if (AgingBonusPerInterval < 0) throw new InvalidOperationException("AgingBonusPerInterval darf nicht negativ sein.");
    }
}

public sealed class SchedulerPriorityCalculator
{
    public long Calculate(int basePriority, DateTimeOffset queuedAtUtc, DateTimeOffset nowUtc,
        TimeSpan agingInterval, int bonusPerInterval)
    {
        if (agingInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(agingInterval));
        if (bonusPerInterval < 0) throw new ArgumentOutOfRangeException(nameof(bonusPerInterval));
        if (queuedAtUtc.Offset != TimeSpan.Zero || nowUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Zeitangaben müssen in UTC vorliegen.");

        var waiting = nowUtc > queuedAtUtc ? nowUtc - queuedAtUtc : TimeSpan.Zero;
        var intervals = (long)Math.Floor(waiting.Ticks / (double)agingInterval.Ticks);
        return basePriority + intervals * bonusPerInterval;
    }
}
