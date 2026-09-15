using KIScheduler.Core.Domain;
using KIScheduler.Core.Scheduling;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Domain;

[TestClass]
public sealed class UsageAndHoldTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);

    [DataTestMethod]
    [DataRow(-0.01)]
    [DataRow(100.01)]
    public void InvalidUsagePercentIsRejected(double value)
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new UsagePercent((decimal)value));
    }

    [TestMethod]
    public void SnapshotSupportsMultipleUsageWindows()
    {
        var snapshot = new UsageSnapshot(new PlatformId("codex"), PlatformProfileId.New(), Now,
            "app-server", UsageQuality.Aktuell,
        [
            new UsageWindow("primary", new UsagePercent(20), Now.AddHours(1), "app-server", Now, UsageQuality.Aktuell),
            new UsageWindow("secondary", new UsagePercent(40), Now.AddDays(1), "app-server", Now, UsageQuality.Aktuell)
        ]);

        Assert.AreEqual(2, snapshot.Windows.Count);
    }

    [TestMethod]
    public void StatusBarUsageUsesCompactPercentagesAndNextResetCountdown()
    {
        var snapshot = new UsageSnapshot(new PlatformId("codex"), PlatformProfileId.New(), Now,
            "app-server", UsageQuality.Aktuell,
        [
            new UsageWindow("primary", new UsagePercent(1), Now.AddHours(6).AddMinutes(20),
                "app-server", Now, UsageQuality.Aktuell),
            new UsageWindow("secondary", new UsagePercent(58), Now.AddDays(2),
                "app-server", Now, UsageQuality.Aktuell)
        ]);

        Assert.AreEqual("1%/58%, 6:19", UsageStatusFormatter.Format(snapshot, Now.AddSeconds(1)));
        Assert.AreEqual("unbekannt", UsageStatusFormatter.Format(null, Now));
    }

    [TestMethod]
    public void ProfileStatusBarFormatsNoOneAndMultipleSelectedProfilesExactly()
    {
        var codexProfile = PlatformProfileId.New();
        var codexUsage = new UsageSnapshot(new("codex"), codexProfile, Now, "app-server",
            UsageQuality.Aktuell,
            [new UsageWindow("primary", new UsagePercent(12), null, "app-server", Now, UsageQuality.Aktuell)]);
        var claudeProfile = PlatformProfileId.New();
        var claudeUsage = new UsageSnapshot(new("claude"), claudeProfile, Now, "command",
            UsageQuality.Aktuell,
            [new UsageWindow("session", new UsagePercent(34), null, "command", Now, UsageQuality.Aktuell)]);

        Assert.AreEqual(string.Empty, ProfileUsageStatusFormatter.Format([], Now));
        Assert.AreEqual("CodexStd: 12%", ProfileUsageStatusFormatter.Format(
            [new(new("codex"), "CodexStd", codexUsage, true, true, true)], Now));
        Assert.AreEqual("CodexStd: 12%  |  claude2: Total cost: $0.0000",
            ProfileUsageStatusFormatter.Format(
            [
                new(new("codex"), "CodexStd", codexUsage, true, true, true),
                new(new("codex"), "ausgeblendet", codexUsage, true, true, false),
                new(new("claude"), "deaktiviert", claudeUsage, true, false, true),
                new(new("claude"), "Plattform aus", claudeUsage, false, true, true),
                new(new("claude"), "claude2", claudeUsage, true, true, true,
                    "Free account: usage assumed at 100%. | Total cost: $0.0000")
            ], Now));
        Assert.AreEqual("ClaudeStd: Total cost: unbekannt",
            ProfileUsageStatusFormatter.Format(
                [new(new("claude"), "ClaudeStd", claudeUsage, true, true, true)], Now));
    }

    [TestMethod]
    public void UsageExceededIsDistinctAndDoesNotConsumeRetry()
    {
        var attempt = new ExecutionAttempt(ExecutionAttemptId.New(), WorkItemId.New(), 1,
            new PlatformId("codex"), PlatformProfileId.New(), new ModelId("model"), new EffortLevel("medium"), Now,
            Now.AddMinutes(2), ExecutionAttemptResult.UsageExceeded, 1, "session-1");

        Assert.AreEqual(ExecutionAttemptResult.UsageExceeded, attempt.Result);
        Assert.IsFalse(attempt.ConsumesNormalRetry);
        Assert.AreEqual(1, attempt.ExitCode);
    }

    [TestMethod]
    public void ProjectHoldCarriesCauseAndEnforcesReleaseRule()
    {
        var trigger = WorkItemId.New();
        var hold = new ProjectExecutionHold(Guid.NewGuid(), ProjectId.New(), trigger, new PlatformId("codex"),
            "Usage-Limit während der Ausführung", Now);

        Assert.AreEqual(trigger, hold.TriggeringWorkItemId);
        Assert.AreEqual("codex", hold.PlatformId.Value);
        Assert.ThrowsException<InvalidOperationException>(() =>
            hold.Release(Now.AddMinutes(1), "zu früh", false, WorkItemStatus.WartetAufUsage));

        hold.Release(Now.AddMinutes(2), "Auftrag abgeschlossen", false, WorkItemStatus.TechnischErfolgreich);
        Assert.IsFalse(hold.IsActive);
    }

    [TestMethod]
    public void PlatformBlockNeedsFreshPermissibleSnapshot()
    {
        var block = new PlatformUsageBlock(Guid.NewGuid(), new PlatformId("codex"), PlatformProfileId.New(),
            WorkItemId.New(), null,
            "Limit erreicht", Now);

        Assert.ThrowsException<InvalidOperationException>(() =>
            block.Release(Now.AddMinutes(1), "nur Uhrzeit vergangen", false, false));

        block.Release(Now.AddMinutes(2), "frische Usage zulässig", false, true);
        Assert.IsFalse(block.IsActive);
    }
}
