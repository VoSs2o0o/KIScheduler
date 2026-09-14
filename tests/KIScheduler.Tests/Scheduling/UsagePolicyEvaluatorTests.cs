using KIScheduler.Core.Domain;
using KIScheduler.Core.Scheduling;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Scheduling;

[TestClass]
public sealed class UsagePolicyEvaluatorTests
{
    private static readonly PlatformId Codex = new("codex");
    private static readonly ModelId Model = new("gpt");
    private readonly UsagePolicyEvaluator evaluator = new();

    [TestMethod]
    public void LocalIntervalIsHalfOpenAndUsesConfiguredTimeZone()
    {
        var policy = Policy(TimeOnly.FromTimeSpan(TimeSpan.FromHours(8)),
            TimeOnly.FromTimeSpan(TimeSpan.FromHours(20)), "Europe/Berlin", [DayOfWeek.Friday]);

        Assert.IsTrue(evaluator.IsActive(policy, Utc(2026, 9, 11, 6, 0)));
        Assert.IsTrue(evaluator.IsActive(policy, Utc(2026, 9, 11, 17, 59)));
        Assert.IsFalse(evaluator.IsActive(policy, Utc(2026, 9, 11, 18, 0)));
    }

    [TestMethod]
    public void OvernightIntervalUsesWeekdayOnWhichItStarts()
    {
        var policy = Policy(new TimeOnly(22, 0), new TimeOnly(2, 0), "UTC", [DayOfWeek.Friday]);

        Assert.IsTrue(evaluator.IsActive(policy, Utc(2026, 9, 11, 22, 0)));
        Assert.IsTrue(evaluator.IsActive(policy, Utc(2026, 9, 12, 1, 59)));
        Assert.IsFalse(evaluator.IsActive(policy, Utc(2026, 9, 12, 2, 0)));
    }

    [TestMethod]
    public void RegularLimitIsExclusive()
    {
        var now = Utc(2026, 9, 11, 10, 0);
        var decision = evaluator.Evaluate(Codex, Model, [Policy(max: 75)],
            Snapshot(now, Window("primary", 75, now, now.AddHours(1))), now);

        Assert.IsFalse(decision.IsAllowed);
        Assert.AreEqual(SchedulerReasonCodes.PercentLimitReached, decision.ReasonCode);
    }

    [TestMethod]
    public void EndSprintRaisesLimitButOneHundredPercentRemainsBlocked()
    {
        var now = Utc(2026, 9, 11, 10, 0);
        var policy = Policy(max: 75, endSprint: TimeSpan.FromMinutes(30), endSprintMax: 100);

        var belowHundred = evaluator.Evaluate(Codex, Model, [policy],
            Snapshot(now, Window("primary", 99.99m, now, now.AddMinutes(30))), now);
        var atHundred = evaluator.Evaluate(Codex, Model, [policy],
            Snapshot(now, Window("primary", 100, now, now.AddMinutes(1))), now);

        Assert.IsTrue(belowHundred.IsAllowed);
        Assert.IsTrue(belowHundred.Windows.Single().EndSprintActive);
        Assert.IsFalse(atHundred.IsAllowed);
    }

    [TestMethod]
    public void MissingResetDoesNotEnableEndSprint()
    {
        var now = Utc(2026, 9, 11, 10, 0);
        var decision = evaluator.Evaluate(Codex, Model,
            [Policy(max: 75, endSprint: TimeSpan.FromMinutes(30), endSprintMax: 100)],
            Snapshot(now, Window("primary", 80, now, null)), now);

        Assert.IsFalse(decision.IsAllowed);
        Assert.IsFalse(decision.Windows.Single().EndSprintActive);
        Assert.AreEqual(75m, decision.Windows.Single().EffectiveLimit);
    }

    [TestMethod]
    public void EveryUsageWindowMustBeAllowed()
    {
        var now = Utc(2026, 9, 11, 10, 0);
        var decision = evaluator.Evaluate(Codex, Model, [Policy(max: 75)],
            Snapshot(now,
                Window("primary", 20, now, now.AddHours(1)),
                Window("secondary", 75, now, now.AddDays(1))), now);

        Assert.IsFalse(decision.IsAllowed);
        Assert.AreEqual(2, decision.Windows.Count);
    }

    [TestMethod]
    public void ServerLimitAlwaysBlocksEvenAtLowPercentage()
    {
        var now = Utc(2026, 9, 11, 10, 0);
        var decision = evaluator.Evaluate(Codex, Model, [Policy(max: 100)],
            Snapshot(now, new UsageWindow("primary", new(1), now.AddMinutes(5), "fake", now,
                UsageQuality.Aktuell, "hard_limit")), now);

        Assert.IsFalse(decision.IsAllowed);
        Assert.AreEqual(SchedulerReasonCodes.ServerLimitReached, decision.ReasonCode);
    }

    [TestMethod]
    public void KnownServerLimitAlsoOverridesAllowUnknownPolicyOnStaleSnapshot()
    {
        var now = Utc(2026, 9, 11, 10, 0);
        var old = now.AddHours(-1);
        var decision = evaluator.Evaluate(Codex, Model,
            [Policy(max: 100, unknown: UnknownUsageBehavior.Erlauben)],
            Snapshot(old, new UsageWindow("primary", new(1), now.AddMinutes(5), "fake", old,
                UsageQuality.Veraltet, "hard_limit")), now);

        Assert.IsFalse(decision.IsAllowed);
        Assert.AreEqual(SchedulerReasonCodes.ServerLimitReached, decision.ReasonCode);
    }

    [TestMethod]
    public void UnknownAndStaleUsageFollowConfiguredBehavior()
    {
        var now = Utc(2026, 9, 11, 10, 0);
        var stale = Snapshot(now.AddMinutes(-6), Window("primary", 1, now.AddMinutes(-6), now.AddHours(1)));

        var blocked = evaluator.Evaluate(Codex, Model, [Policy(unknown: UnknownUsageBehavior.Blockieren)], stale, now);
        var allowed = evaluator.Evaluate(Codex, Model, [Policy(unknown: UnknownUsageBehavior.Erlauben)], stale, now);

        Assert.IsFalse(blocked.IsAllowed);
        Assert.AreEqual(SchedulerReasonCodes.StaleUsageBlocked, blocked.ReasonCode);
        Assert.IsTrue(allowed.IsAllowed);
        Assert.IsFalse(allowed.IsFreshSnapshot);
    }

    [TestMethod]
    public void AgingAddsConfiguredBonusPerCompletedInterval()
    {
        var calculator = new SchedulerPriorityCalculator();
        var now = Utc(2026, 9, 11, 10, 0);

        Assert.AreEqual(56L, calculator.Calculate(50, now.AddMinutes(-95), now,
            TimeSpan.FromMinutes(30), 2));
    }

    private static UsagePolicy Policy(TimeOnly? start = null, TimeOnly? end = null,
        string timeZone = "UTC", DayOfWeek[]? days = null, decimal max = 75,
        UnknownUsageBehavior unknown = UnknownUsageBehavior.Blockieren,
        TimeSpan? endSprint = null, decimal? endSprintMax = null) =>
        new(Codex, days ?? [DayOfWeek.Friday], start ?? new TimeOnly(0, 0),
            end ?? new TimeOnly(23, 59), timeZone, new(max), unknown, TimeSpan.FromMinutes(5),
            endSprintDuration: endSprint,
            endSprintMaxUsedPercent: endSprintMax.HasValue ? new UsagePercent(endSprintMax.Value) : null);

    private static UsageSnapshot Snapshot(DateTimeOffset readAt, params UsageWindow[] windows) =>
        new(Codex, PlatformProfileId.New(), readAt, "fake", UsageQuality.Aktuell, windows);

    private static UsageWindow Window(string name, decimal used, DateTimeOffset readAt, DateTimeOffset? resetAt) =>
        new(name, new(used), resetAt, "fake", readAt, UsageQuality.Aktuell);

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);
}
