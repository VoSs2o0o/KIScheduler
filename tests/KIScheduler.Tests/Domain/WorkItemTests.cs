using KIScheduler.Core.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Domain;

[TestClass]
public sealed class WorkItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void PlatformAndModelCanBeChangedBeforeFirstAttempt()
    {
        var item = CreateWorkItem();

        item.ChangeExecutionConfiguration(new PlatformId("claude"), new ModelId("opus"), new EffortLevel("high"));

        Assert.AreEqual("claude", item.PlatformId.Value);
        Assert.AreEqual("opus", item.ModelId.Value);
    }

    [TestMethod]
    public void PlatformAndModelAreFrozenAfterFirstAttemptStarts()
    {
        var item = CreateWorkItem();
        item.TransitionTo(WorkItemStatus.InWarteschlange);
        item.TransitionTo(WorkItemStatus.Reserviert);
        item.TransitionTo(WorkItemStatus.InBearbeitung);
        item.MarkAttemptStarted(Now.AddMinutes(1));

        Assert.ThrowsException<InvalidOperationException>(() =>
            item.ChangeExecutionConfiguration(new PlatformId("claude"), new ModelId("opus"), new EffortLevel("high")));
    }

    [TestMethod]
    public void UsageExceededDoesNotConsumeNormalRetry()
    {
        var item = CreateWorkItem();
        item.TransitionTo(WorkItemStatus.InWarteschlange);
        item.TransitionTo(WorkItemStatus.Reserviert);
        item.TransitionTo(WorkItemStatus.InBearbeitung);
        item.CompleteCurrentAttempt(ExecutionAttemptResult.UsageExceeded);

        Assert.AreEqual(0, item.NormalRetryCount);
        Assert.AreEqual(WorkItemStatus.WartetAufUsage, item.Status);

        var failedItem = CreateWorkItem();
        failedItem.TransitionTo(WorkItemStatus.InWarteschlange);
        failedItem.TransitionTo(WorkItemStatus.Reserviert);
        failedItem.TransitionTo(WorkItemStatus.InBearbeitung);
        failedItem.CompleteCurrentAttempt(ExecutionAttemptResult.Fehlgeschlagen);
        Assert.AreEqual(1, failedItem.NormalRetryCount);
    }

    [TestMethod]
    public void ProjectHoldIsOnlyADerivedDisplayStatus()
    {
        var item = CreateWorkItem();
        item.TransitionTo(WorkItemStatus.InWarteschlange);

        Assert.AreEqual(WorkItemDisplayStatus.ProjektAngehalten, item.GetDisplayStatus(true));
        Assert.AreEqual(WorkItemStatus.InWarteschlange, item.Status);
        Assert.AreEqual(WorkItemDisplayStatus.InWarteschlange, item.GetDisplayStatus(false));
    }

    private static WorkItem CreateWorkItem() => new(
        WorkItemId.New(), "AP1", new WorkItemPriority(50), new PlatformId("codex"),
        new ModelId("gpt-5.6-sol"), new EffortLevel("medium"), new PromptPath("docs/001_AP1.md"),
        true, Now, ProjectId.New());
}
