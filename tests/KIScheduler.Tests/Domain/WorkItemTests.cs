using KIScheduler.Core.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Domain;

[TestClass]
public sealed class WorkItemTests
{
    private static readonly PlatformProfileId ProfileId = new(new Guid("11111111-1111-1111-1111-111111111111"));
    [TestMethod]
    public void TitleCanBeChangedWithDomainValidation()
    {
        var item = CreateWorkItem();

        item.ChangeTitle("  Neuer Titel  ");

        Assert.AreEqual("Neuer Titel", item.Title);
        Assert.ThrowsException<ArgumentException>(() => item.ChangeTitle(" "));
    }
    [TestMethod]
    public void CommitMessageCanBeDerivedFromApFileAndOverridden()
    {
        var item = new WorkItem(WorkItemId.New(), "Git-Prüfung und Auto-Commit", new(50), new("codex"),
            ProfileId, new("gpt"), new("high"), new(Path.Combine("docs", "009_AP9.md")), true,
            DateTimeOffset.UtcNow);

        Assert.AreEqual("AP9: Git-Prüfung und Auto-Commit", item.ResolveCommitMessage());
        item.ChangeCommitMessage("Eigene Commitnachricht");
        Assert.AreEqual("Eigene Commitnachricht", item.ResolveCommitMessage());
        item.ChangeCommitMessage("  ");
        Assert.AreEqual("AP9: Git-Prüfung und Auto-Commit", item.ResolveCommitMessage());
    }

    [TestMethod]
    public void ProjectTargetBranchDefaultsToMaster()
    {
        var project = new ProjectDefinition(ProjectId.New(), "Test", Path.GetTempPath());

        Assert.AreEqual("master", project.TargetBranch);
    }
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void PlatformAndModelCanBeChangedBeforeFirstAttempt()
    {
        var item = CreateWorkItem();

        var newProfileId = PlatformProfileId.New();
        item.ChangeExecutionConfiguration(new PlatformId("claude"), newProfileId,
            new ModelId("opus"), new EffortLevel("high"));

        Assert.AreEqual("claude", item.PlatformId.Value);
        Assert.AreEqual(newProfileId, item.PlatformProfileId);
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
            item.ChangeExecutionConfiguration(new PlatformId("claude"), PlatformProfileId.New(),
                new ModelId("opus"), new EffortLevel("high")));
    }

    [TestMethod]
    public void AllEditableFieldsAreFrozenWhileRunningAndAfterExecutionStarted()
    {
        var item = CreateWorkItem();
        item.TransitionTo(WorkItemStatus.InWarteschlange);
        item.TransitionTo(WorkItemStatus.Reserviert);

        Assert.IsFalse(item.CanEdit);
        Assert.ThrowsException<InvalidOperationException>(() => item.ChangeTitle("Neuer Titel"));
        Assert.ThrowsException<InvalidOperationException>(() => item.ChangePlanning(
            new WorkItemPriority(60), new PromptPath("docs/changed.md"), false, ProjectId.New()));
        Assert.ThrowsException<InvalidOperationException>(() => item.ChangeCommitMessage("Neue Nachricht"));

        item.TransitionTo(WorkItemStatus.InBearbeitung);
        item.MarkAttemptStarted(Now.AddMinutes(1));
        item.CompleteCurrentAttempt(ExecutionAttemptResult.TechnischErfolgreich);

        Assert.IsFalse(item.CanEdit);
        Assert.ThrowsException<InvalidOperationException>(() => item.ChangeTitle("Nach Abschluss"));

        var cancelledBeforeStart = CreateWorkItem();
        cancelledBeforeStart.TransitionTo(WorkItemStatus.Abgebrochen);
        Assert.IsFalse(cancelledBeforeStart.CanEdit);
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
        ProfileId, new ModelId("gpt-5.6-sol"), new EffortLevel("medium"), new PromptPath("docs/001_AP1.md"),
        true, Now, ProjectId.New());
}
