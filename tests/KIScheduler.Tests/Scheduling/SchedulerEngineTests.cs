using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using KIScheduler.Core.Scheduling;
using KIScheduler.Infrastructure;
using KIScheduler.Infrastructure.Persistence;
using KIScheduler.Platforms;
using KIScheduler.Platforms.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Scheduling;

[TestClass]
public sealed class SchedulerEngineTests
{
    [TestMethod]
    public async Task SamePlatformNeverExecutesTwiceInParallel()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        var first = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("first"), 60);
        var second = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("second"), 50);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Platform("codex").EnqueueExecution(async (_, token) =>
        {
            await release.Task.WaitAsync(token);
            return new(PlatformExecutionOutcome.Succeeded, 0);
        });

        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await WaitUntilAsync(() => fixture.Platform("codex").ActiveExecutions == 1);
        Assert.AreEqual(0, await fixture.Engine.RunCycleAsync());
        release.SetResult(true);
        await fixture.Engine.WaitForIdleAsync();
        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();

        Assert.AreEqual(1, fixture.Platform("codex").MaximumConcurrentExecutions);
        Assert.AreEqual(WorkItemStatus.TechnischErfolgreich, (await fixture.WorkItems.GetAsync(first.Id))!.Status);
        Assert.AreEqual(WorkItemStatus.TechnischErfolgreich, (await fixture.WorkItems.GetAsync(second.Id))!.Status);
    }

    [TestMethod]
    public async Task ScheduledWorkIsIgnoredUntilItsStartTimeAndThenUsesNormalCriteria()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        var item = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("scheduled"), 50);
        item.ChangeScheduledStart(fixture.Clock.UtcNow.AddHours(1));
        await fixture.WorkItems.SaveAsync(item);
        var readsBefore = fixture.UsageReadCount("codex");

        Assert.AreEqual(0, await fixture.Engine.RunCycleAsync());
        Assert.AreEqual(readsBefore, fixture.UsageReadCount("codex"));
        Assert.AreEqual(0, fixture.Platform("codex").Requests.Count);
        Assert.AreEqual(WorkItemStatus.InWarteschlange, (await fixture.WorkItems.GetAsync(item.Id))!.Status);

        fixture.Clock.Advance(TimeSpan.FromHours(1));
        fixture.SetUsage("codex", 20);
        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();

        Assert.AreEqual(1, fixture.Platform("codex").Requests.Count);
        Assert.AreEqual(WorkItemStatus.TechnischErfolgreich,
            (await fixture.WorkItems.GetAsync(item.Id))!.Status);
    }

    [TestMethod]
    public async Task DifferentPlatformsCanRunInParallelInDifferentProjects()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex", "claude");
        await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("codex-project"), 50);
        await fixture.AddWorkItemAsync("claude", await fixture.AddProjectAsync("claude-project"), 50);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        foreach (var id in new[] { "codex", "claude" })
            fixture.Platform(id).EnqueueExecution(async (_, token) =>
            {
                await release.Task.WaitAsync(token);
                return new(PlatformExecutionOutcome.Succeeded, 0);
            });

        Assert.AreEqual(2, await fixture.Engine.RunCycleAsync());
        await WaitUntilAsync(() => fixture.Platform("codex").ActiveExecutions == 1
            && fixture.Platform("claude").ActiveExecutions == 1);
        release.SetResult(true);
        await fixture.Engine.WaitForIdleAsync();

        Assert.AreEqual(1, fixture.Platform("codex").MaximumConcurrentExecutions);
        Assert.AreEqual(1, fixture.Platform("claude").MaximumConcurrentExecutions);
    }

    [TestMethod]
    public async Task DifferentPlatformsDoNotRunInParallelInSameProject()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex", "claude");
        var project = await fixture.AddProjectAsync("shared");
        await fixture.AddWorkItemAsync("codex", project, 60);
        await fixture.AddWorkItemAsync("claude", project, 50);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        foreach (var id in new[] { "codex", "claude" })
            fixture.Platform(id).EnqueueExecution(async (_, token) =>
            {
                await release.Task.WaitAsync(token);
                return new(PlatformExecutionOutcome.Succeeded, 0);
            });

        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await WaitUntilAsync(() => fixture.Platform("codex").ActiveExecutions
            + fixture.Platform("claude").ActiveExecutions == 1);
        Assert.AreEqual(0, await fixture.Engine.RunCycleAsync());
        release.SetResult(true);
        await fixture.Engine.WaitForIdleAsync();
    }

    [TestMethod]
    public async Task BlockedCodexDoesNotPreventEligibleClaudeWork()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex", "claude");
        var codex = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("codex-project"), 90);
        await fixture.AddWorkItemAsync("claude", await fixture.AddProjectAsync("claude-project"), 10);
        fixture.SetUsage("codex", 75);
        fixture.SetUsage("claude", 20);

        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();

        Assert.AreEqual(0, fixture.Platform("codex").Requests.Count);
        Assert.AreEqual(1, fixture.Platform("claude").Requests.Count);
        Assert.AreEqual(WorkItemStatus.WartetAufUsage, (await fixture.WorkItems.GetAsync(codex.Id))!.Status);
    }

    [TestMethod]
    public async Task UsageExceededCreatesBothBlocksAndOnlyFreshUsageUnlocksResume()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex", "claude");
        var project = await fixture.AddProjectAsync("interrupted");
        var interrupted = await fixture.AddWorkItemAsync("codex", project, 80);
        var sameProject = await fixture.AddWorkItemAsync("claude", project, 70);
        await fixture.AddWorkItemAsync("claude", await fixture.AddProjectAsync("other"), 60);
        fixture.Platform("codex").EnqueueResult(new(PlatformExecutionOutcome.UsageExceeded, 7,
            "session-usage", "Limit erreicht", mayHavePartialChanges: true));

        Assert.AreEqual(2, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();

        var stored = (await fixture.WorkItems.GetAsync(interrupted.Id))!;
        Assert.AreEqual(WorkItemStatus.WartetAufUsage, stored.Status);
        Assert.AreEqual(0, stored.NormalRetryCount);
        Assert.AreEqual(1, (await fixture.Blocks.ListActivePlatformBlocksAsync()).Count);
        Assert.AreEqual(1, (await fixture.Blocks.ListActiveProjectHoldsAsync()).Count);
        var events = await fixture.History.ListEventsAsync(interrupted.Id);
        Assert.IsTrue(events.Any(item => item.EventType == "usage_exceeded"
            && item.Severity == ExecutionEventSeverity.Error && item.Data["autoCommit"] == "skipped"));

        fixture.SetUnknownUsage("codex");
        fixture.Clock.Advance(TimeSpan.FromHours(2));
        Assert.AreEqual(0, await fixture.Engine.RunCycleAsync());
        Assert.AreEqual(1, (await fixture.Blocks.ListActivePlatformBlocksAsync()).Count);
        Assert.AreEqual(0, fixture.Platform("claude").Requests.Count(request =>
            string.Equals(request.WorkingDirectory, fixture.ProjectRoot(project), StringComparison.OrdinalIgnoreCase)));

        fixture.SetUsage("codex", 1);
        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();

        Assert.AreEqual("session-usage", fixture.Platform("codex").Requests.Last().SessionId);
        Assert.AreEqual(0, (await fixture.Blocks.ListActivePlatformBlocksAsync()).Count);
        Assert.AreEqual(0, (await fixture.Blocks.ListActiveProjectHoldsAsync()).Count);
        Assert.AreEqual(WorkItemStatus.TechnischErfolgreich,
            (await fixture.WorkItems.GetAsync(interrupted.Id))!.Status);
        Assert.AreEqual(WorkItemStatus.InWarteschlange,
            (await fixture.WorkItems.GetAsync(sameProject.Id))!.Status);
    }

    [TestMethod]
    public async Task UsageBlockOnlyStopsItsProfileAndResumeKeepsOriginalProfileAndSession()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        PlatformProfileId defaultProfile = fixture.Profile("codex");
        PlatformProfileId secondProfile = await fixture.AddProfileAsync("codex", "codex2");
        var interrupted = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("default-profile"),
            90, autoCommit: false, profileId: defaultProfile);
        fixture.Platform("codex").EnqueueResult(new(PlatformExecutionOutcome.UsageExceeded, 7,
            "session-default", "Limit erreicht", mayHavePartialChanges: true));

        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();
        PlatformUsageBlock block = (await fixture.Blocks.ListActivePlatformBlocksAsync()).Single();
        Assert.AreEqual(defaultProfile, block.PlatformProfileId);

        var second = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("second-profile"),
            20, autoCommit: false, profileId: secondProfile);
        fixture.Clock.Advance(TimeSpan.FromHours(2));
        fixture.SetUnknownUsage("codex", defaultProfile);
        fixture.SetUsage("codex", secondProfile, 1);

        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();
        Assert.AreEqual(secondProfile, fixture.Platform("codex").Requests.Last().PlatformProfileId);
        Assert.AreEqual(WorkItemStatus.TechnischErfolgreich, (await fixture.WorkItems.GetAsync(second.Id))!.Status);
        Assert.AreEqual(1, (await fixture.Blocks.ListActivePlatformBlocksAsync()).Count,
            "Usage des zweiten Profils darf die Sperre des Standardprofils nicht aufheben.");

        fixture.SetUsage("codex", defaultProfile, 1);
        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();

        PlatformExecutionRequest resume = fixture.Platform("codex").Requests.Last();
        Assert.AreEqual(defaultProfile, resume.PlatformProfileId);
        Assert.AreEqual("session-default", resume.SessionId);
        Assert.AreEqual(WorkItemStatus.TechnischErfolgreich,
            (await fixture.WorkItems.GetAsync(interrupted.Id))!.Status);
    }

    [TestMethod]
    public async Task DifferentProfilesDoNotIncreasePlatformParallelism()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        PlatformProfileId secondProfile = await fixture.AddProfileAsync("codex", "codex2");
        await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("profile-one"), 60,
            profileId: fixture.Profile("codex"));
        await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("profile-two"), 50,
            profileId: secondProfile);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Platform("codex").EnqueueExecution(async (_, token) =>
        {
            await release.Task.WaitAsync(token);
            return new(PlatformExecutionOutcome.Succeeded, 0);
        });

        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await WaitUntilAsync(() => fixture.Platform("codex").ActiveExecutions == 1);
        Assert.AreEqual(0, await fixture.Engine.RunCycleAsync());
        release.SetResult(true);
        await fixture.Engine.WaitForIdleAsync();
        Assert.AreEqual(1, fixture.Platform("codex").MaximumConcurrentExecutions);
    }

    [TestMethod]
    public async Task DisabledProfileIsDiagnosedWithoutUsageReadOrDispatch()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        PlatformProfileId profileId = await fixture.AddProfileAsync("codex", "disabled");
        var item = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("disabled-profile"),
            50, profileId: profileId);
        await fixture.SetProfileEnabledAsync(profileId, false);
        int readsBefore = fixture.UsageReadCount("codex", profileId);

        Assert.AreEqual(0, await fixture.Engine.RunCycleAsync());

        Assert.AreEqual(readsBefore, fixture.UsageReadCount("codex", profileId));
        Assert.AreEqual(0, fixture.Platform("codex").Requests.Count);
        Assert.IsTrue((await fixture.History.ListEventsAsync(item.Id)).Any(x =>
            x.Data.GetValueOrDefault("reasonCode") == SchedulerReasonCodes.ProfileDisabled
            && x.Data.GetValueOrDefault("platformProfileId") == profileId.ToString()));
    }

    [TestMethod]
    public async Task MissingAndCrossPlatformProfilesAreSafelyDiagnosed()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex", "claude");
        var missing = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("missing-profile"), 60);
        var mismatch = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("wrong-profile"), 50);
        PlatformProfileId missingProfileId = PlatformProfileId.New();
        await fixture.CorruptStoredProfileAsync(missing.Id, missingProfileId);
        await fixture.CorruptStoredProfileAsync(mismatch.Id, fixture.Profile("claude"));

        Assert.AreEqual(0, await fixture.Engine.RunCycleAsync());

        Assert.AreEqual(0, fixture.Platform("codex").Requests.Count);
        Assert.IsTrue((await fixture.History.ListEventsAsync(missing.Id)).Any(x =>
            x.Data.GetValueOrDefault("reasonCode") == SchedulerReasonCodes.ProfileMissing));
        Assert.IsTrue((await fixture.History.ListEventsAsync(mismatch.Id)).Any(x =>
            x.Data.GetValueOrDefault("reasonCode") == SchedulerReasonCodes.ProfilePlatformMismatch));
    }

    [TestMethod]
    public async Task SchedulerRejectsProfileChangeAfterAnAttemptStarted()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        PlatformProfileId secondProfile = await fixture.AddProfileAsync("codex", "codex2");
        var item = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("changed-profile"),
            50, autoCommit: false);
        fixture.Platform("codex").EnqueueResult(new(PlatformExecutionOutcome.Failed, 2,
            message: "retry"));
        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();
        Assert.IsTrue((await fixture.WorkItems.GetAsync(item.Id))!.HasExecutionStarted);

        await fixture.CorruptStoredProfileAsync(item.Id, secondProfile);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));

        Assert.AreEqual(0, await fixture.Engine.RunCycleAsync());
        Assert.AreEqual(WorkItemStatus.InWarteschlange, (await fixture.WorkItems.GetAsync(item.Id))!.Status);
        Assert.AreEqual(1, fixture.Platform("codex").Requests.Count);
        Assert.IsTrue((await fixture.History.ListEventsAsync(item.Id)).Any(x =>
            x.Data.GetValueOrDefault("reasonCode") == SchedulerReasonCodes.ProfileChanged));
    }

    [TestMethod]
    public async Task PausePreventsDispatchAndResumeContinues()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("paused"), 50);

        fixture.Engine.Pause();
        Assert.IsTrue(fixture.Engine.IsPaused);
        Assert.AreEqual(0, await fixture.Engine.RunCycleAsync());
        fixture.Engine.Resume();
        Assert.IsFalse(fixture.Engine.IsPaused);
        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();
    }

    [TestMethod]
    public async Task DisabledPlatformDoesNotReadUsageOrDispatchQueuedWork()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        var item = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("disabled"), 50);
        await fixture.SetPlatformEnabledAsync("codex", false);
        var readsBefore = fixture.UsageReadCount("codex");

        Assert.AreEqual(0, await fixture.Engine.RunCycleAsync());

        Assert.AreEqual(readsBefore, fixture.UsageReadCount("codex"));
        Assert.AreEqual(0, fixture.Platform("codex").Requests.Count);
        Assert.AreEqual(WorkItemStatus.InWarteschlange, (await fixture.WorkItems.GetAsync(item.Id))!.Status);
        Assert.IsTrue((await fixture.History.ListEventsAsync(item.Id))
            .Any(x => x.Data.GetValueOrDefault("reasonCode") == SchedulerReasonCodes.PlatformDisabled));
    }

    [TestMethod]
    public async Task ControlledStopCancelsRunningWorkAsInterrupted()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        var item = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("shutdown"), 50);
        fixture.Platform("codex").EnqueueExecution(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new(PlatformExecutionOutcome.Succeeded, 0);
        });

        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await WaitUntilAsync(() => fixture.Platform("codex").ActiveExecutions == 1);
        await fixture.Engine.StopAsync();

        Assert.AreEqual(WorkItemStatus.Unterbrochen, (await fixture.WorkItems.GetAsync(item.Id))!.Status);
        var hold = (await fixture.Blocks.ListActiveProjectHoldsAsync()).Single();
        Assert.AreEqual(item.Id, hold.TriggeringWorkItemId);
    }

    [TestMethod]
    public async Task ManualCancelStopsRunningWorkAsCancelled()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        var item = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("cancel"), 50);
        fixture.Platform("codex").EnqueueExecution(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new(PlatformExecutionOutcome.Succeeded, 0);
        });

        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await WaitUntilAsync(() => fixture.Platform("codex").ActiveExecutions == 1);
        Assert.IsTrue(await fixture.Engine.CancelAsync(item.Id));
        await fixture.Engine.WaitForIdleAsync();

        Assert.AreEqual(WorkItemStatus.Abgebrochen, (await fixture.WorkItems.GetAsync(item.Id))!.Status);
    }

    [TestMethod]
    public async Task ManualCancelMovesQueuedWorkWithoutStartingPlatform()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        var item = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("cancel-queued"), 50);

        Assert.IsTrue(await fixture.Engine.CancelAsync(item.Id));

        Assert.AreEqual(WorkItemStatus.Abgebrochen, (await fixture.WorkItems.GetAsync(item.Id))!.Status);
        Assert.AreEqual(0, fixture.Platform("codex").Requests.Count);
        Assert.IsTrue((await fixture.History.ListEventsAsync(item.Id))
            .Any(x => x.EventType == "execution.cancelled"));
    }

    [TestMethod]
    public async Task AutoCommitPreflightFailureDoesNotStartPlatform()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        var item = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("wrong-branch"), 50);
        fixture.Git.NextInspection = new GitInspectionResult(GitInspectionStatus.BranchMismatch,
            "Falscher Branch.", new GitWorkingTreeSnapshot("repo", "develop", Array.Empty<string>()));

        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();

        Assert.AreEqual(0, fixture.Platform("codex").Requests.Count);
        Assert.AreEqual(WorkItemStatus.MenschlichePruefung, (await fixture.WorkItems.GetAsync(item.Id))!.Status);
        Assert.IsTrue((await fixture.History.ListEventsAsync(item.Id)).Any(e => e.EventType == "git.before"));
    }

    [TestMethod]
    public async Task SuccessfulPlatformWithoutGitChangesEndsWithWarning()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        var item = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("no-changes"), 50);
        fixture.Git.NextCommitStatus = GitCommitStatus.NoChanges;

        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();

        Assert.AreEqual(WorkItemStatus.ErfolgreichMitWarnung, (await fixture.WorkItems.GetAsync(item.Id))!.Status);
        Assert.AreEqual(1, fixture.Git.CommitRequests.Count);
    }

    [TestMethod]
    public async Task DisabledAutoCommitInspectsButNeverStagesOrCommits()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        var item = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("no-commit"), 50,
            autoCommit: false);
        fixture.Git.NextInspection = new GitInspectionResult(GitInspectionStatus.WorkingTreeDirty,
            "Vorhandene Änderungen.", new GitWorkingTreeSnapshot("repo", "master", [" M user.txt"]));

        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();

        Assert.AreEqual(WorkItemStatus.TechnischErfolgreich, (await fixture.WorkItems.GetAsync(item.Id))!.Status);
        Assert.AreEqual(0, fixture.Git.CommitRequests.Count);
        Assert.AreEqual(2, fixture.Git.InspectionRequests.Count);
    }

    [TestMethod]
    public async Task FailedRequiredCommitMovesWorkItemToHumanReview()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        var item = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("commit-fails"), 50);
        fixture.Git.NextCommitStatus = GitCommitStatus.Failed;

        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();

        Assert.AreEqual(WorkItemStatus.MenschlichePruefung, (await fixture.WorkItems.GetAsync(item.Id))!.Status);
        Assert.AreEqual(1, fixture.Git.CommitRequests.Count);
    }

    [TestMethod]
    public async Task FailedExecutionUsesBackoffAndStopsAtConfiguredMaximumAttempts()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        var item = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("retry"), 50,
            autoCommit: false);
        for (var index = 0; index < 3; index++)
            fixture.Platform("codex").EnqueueResult(new(PlatformExecutionOutcome.Failed, 2,
                message: $"Fehler {index + 1}"));

        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();
        var afterFirst = (await fixture.WorkItems.GetAsync(item.Id))!;
        Assert.AreEqual(WorkItemStatus.InWarteschlange, afterFirst.Status);
        Assert.AreEqual(1, afterFirst.NormalRetryCount);
        Assert.AreEqual(0, await fixture.Engine.RunCycleAsync(), "Backoff muss einen sofortigen Retry verhindern.");

        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();
        Assert.AreEqual(2, (await fixture.WorkItems.GetAsync(item.Id))!.NormalRetryCount);
        Assert.AreEqual(0, await fixture.Engine.RunCycleAsync(), "Der zweite Backoff muss exponentiell länger sein.");

        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();
        var final = (await fixture.WorkItems.GetAsync(item.Id))!;
        Assert.AreEqual(WorkItemStatus.Fehlgeschlagen, final.Status);
        Assert.AreEqual(3, final.NormalRetryCount);
        Assert.AreEqual(3, fixture.Platform("codex").Requests.Count);
        Assert.AreEqual(0, await fixture.Engine.RunCycleAsync());
    }

    [TestMethod]
    public async Task ExecutionTimeoutMovesWorkToHumanReviewAndHoldsProject()
    {
        await using var fixture = await SchedulerFixture.CreateAsync(new SchedulerOptions
        {
            ExecutionTimeout = TimeSpan.FromMilliseconds(100),
            LeaseDuration = TimeSpan.FromMinutes(1),
            AgingInterval = TimeSpan.FromMinutes(1)
        }, "codex");
        var item = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("timeout"), 50,
            autoCommit: false);
        fixture.Platform("codex").EnqueueExecution(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new(PlatformExecutionOutcome.Succeeded, 0);
        });

        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();

        Assert.AreEqual(WorkItemStatus.MenschlichePruefung, (await fixture.WorkItems.GetAsync(item.Id))!.Status);
        Assert.AreEqual(ExecutionAttemptResult.MenschlichePruefung,
            (await fixture.History.ListAttemptsAsync(item.Id)).Single().Result);
        Assert.AreEqual(item.Id, (await fixture.Blocks.ListActiveProjectHoldsAsync()).Single().TriggeringWorkItemId);
    }

    [TestMethod]
    public async Task EndSprintAllowsBelowHundredButExactHundredStillWaits()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        var allowed = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("end-sprint"), 60,
            autoCommit: false);
        fixture.SetUsage("codex", 99, TimeSpan.FromMinutes(15));

        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();
        Assert.AreEqual(WorkItemStatus.TechnischErfolgreich, (await fixture.WorkItems.GetAsync(allowed.Id))!.Status);

        var blocked = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("at-one-hundred"), 50,
            autoCommit: false);
        fixture.SetUsage("codex", 100, TimeSpan.FromMinutes(1));

        Assert.AreEqual(0, await fixture.Engine.RunCycleAsync());
        Assert.AreEqual(WorkItemStatus.WartetAufUsage, (await fixture.WorkItems.GetAsync(blocked.Id))!.Status);
    }

    [TestMethod]
    public async Task ServerLimitBelowHundredBlocksDispatch()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        var item = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("server-limit"), 50,
            autoCommit: false);
        fixture.SetUsage("codex", 1, TimeSpan.FromMinutes(15), "hard_limit");

        Assert.AreEqual(0, await fixture.Engine.RunCycleAsync());

        Assert.AreEqual(0, fixture.Platform("codex").Requests.Count);
        Assert.AreEqual(WorkItemStatus.WartetAufUsage, (await fixture.WorkItems.GetAsync(item.Id))!.Status);
        Assert.IsTrue((await fixture.History.ListEventsAsync(item.Id))
            .Any(x => x.Data.GetValueOrDefault("reasonCode") == SchedulerReasonCodes.ServerLimitReached));
    }

    [TestMethod]
    public async Task SensitivePlatformDiagnosticsAreRedactedBeforePersistence()
    {
        await using var fixture = await SchedulerFixture.CreateAsync("codex");
        var item = await fixture.AddWorkItemAsync("codex", await fixture.AddProjectAsync("redaction"), 50,
            autoCommit: false);
        fixture.Platform("codex").EnqueueResult(new(PlatformExecutionOutcome.Failed, 2,
            message: "Authorization: Bearer persisted-secret"));

        Assert.AreEqual(1, await fixture.Engine.RunCycleAsync());
        await fixture.Engine.WaitForIdleAsync();

        var attempt = (await fixture.History.ListAttemptsAsync(item.Id)).Single();
        Assert.IsNotNull(attempt.Diagnostic);
        Assert.IsFalse(attempt.Diagnostic.Contains("persisted-secret", StringComparison.Ordinal));
        Assert.IsTrue((await fixture.History.ListEventsAsync(item.Id))
            .Where(x => x.EventType == "execution.completed")
            .All(x => !x.Message.Contains("persisted-secret", StringComparison.Ordinal)));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }

    private sealed class SchedulerFixture : IAsyncDisposable
    {
        private readonly string databasePath;
        private readonly string directory;
        private readonly Dictionary<string, FakeAiPlatform> platformMap;
        private readonly Dictionary<string, FakeUsageProvider> usageMap;
        private readonly Dictionary<string, PlatformProfileId> profileIds;
        private readonly Dictionary<ProjectId, string> projectRoots = [];
        private readonly IDbContextFactory<KischedulerDbContext> contextFactory;

        private SchedulerFixture(string databasePath, string directory,
            Dictionary<string, FakeAiPlatform> platformMap,
            Dictionary<string, FakeUsageProvider> usageMap,
            Dictionary<string, PlatformProfileId> profileIds,
            IDbContextFactory<KischedulerDbContext> contextFactory,
            SchedulerEngine engine, FakeClock clock, FakeGitService git)
        {
            this.databasePath = databasePath;
            this.directory = directory;
            this.platformMap = platformMap;
            this.usageMap = usageMap;
            this.profileIds = profileIds;
            this.contextFactory = contextFactory;
            Engine = engine;
            Clock = clock;
            Git = git;
            WorkItems = new(contextFactory);
            Blocks = new(contextFactory);
            History = new(contextFactory);
        }

        public SchedulerEngine Engine { get; }
        public FakeClock Clock { get; }
        public FakeGitService Git { get; }
        public SqliteWorkItemRepository WorkItems { get; }
        public SqliteExecutionBlockRepository Blocks { get; }
        public SqliteExecutionHistoryRepository History { get; }

        public static async Task<SchedulerFixture> CreateAsync(params string[] platformIds)
            => await CreateAsync(null, platformIds);

        public static async Task<SchedulerFixture> CreateAsync(SchedulerOptions? schedulerOptions,
            params string[] platformIds)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"kischeduler-ap6-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var databasePath = Path.Combine(directory, "scheduler.db");
            var options = new DbContextOptionsBuilder<KischedulerDbContext>()
                .UseSqlite($"Data Source={databasePath};Foreign Keys=True;Default Timeout=5").Options;
            var factory = new TestContextFactory(options);
            await using (var db = factory.CreateDbContext()) await db.Database.MigrateAsync();

            var clock = new FakeClock(new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero));
            var platformMap = platformIds.ToDictionary(id => id, id => new FakeAiPlatform(new(id)),
                StringComparer.OrdinalIgnoreCase);
            var usageMap = platformIds.ToDictionary(id => id, id => new FakeUsageProvider(new(id)),
                StringComparer.OrdinalIgnoreCase);
            var platformRepository = new SqlitePlatformRepository(factory);
            var profileRepository = new SqlitePlatformProfileRepository(factory);
            var profileIds = new Dictionary<string, PlatformProfileId>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in platformIds)
            {
                await platformRepository.SaveAsync(new PlatformDefinition(new(id), id,
                    [new PlatformModel(new("gpt"), [new("medium")])]));
                var profileId = (await profileRepository.GetDefaultAsync(new(id)))!.Id;
                profileIds[id] = profileId;
                usageMap[id].SetCurrent(profileId,
                    UsageReadResult.Available(CreateSnapshot(id, profileId, clock.UtcNow, 1)));
            }
            await new SqliteUsagePolicyRepository(factory).ReplaceAsync(platformIds.Select(id =>
                new UsagePolicy(new(id), [DayOfWeek.Friday], new TimeOnly(0, 0), new TimeOnly(23, 59),
                    "UTC", new(75), UnknownUsageBehavior.Blockieren, TimeSpan.FromMinutes(5),
                    endSprintDuration: TimeSpan.FromMinutes(30), endSprintMaxUsedPercent: new(100))).ToArray());

            var workItems = new SqliteWorkItemRepository(factory);
            var projects = new SqliteProjectRepository(factory);
            var snapshots = new SqliteUsageSnapshotRepository(factory);
            var history = new SqliteExecutionHistoryRepository(factory);
            var blocks = new SqliteExecutionBlockRepository(factory);
            var atomic = new SqliteAtomicExecutionRepository(factory);
            var aiRegistry = new AiPlatformRegistry(platformMap.Values);
            var usageRegistry = new UsageProviderRegistry(usageMap.Values);
            var git = new FakeGitService();
            var engine = new SchedulerEngine(workItems, projects, platformRepository, profileRepository,
                new SqliteUsagePolicyRepository(factory), snapshots, history, blocks, atomic,
                aiRegistry, usageRegistry, new PlatformConfigurationValidator(aiRegistry, usageRegistry),
                new PhysicalFileSystem(), git, clock,
                schedulerOptions ?? new SchedulerOptions { AgingInterval = TimeSpan.FromMinutes(1) },
                ownerId: "test-worker");
            return new SchedulerFixture(databasePath, directory, platformMap, usageMap, profileIds,
                factory, engine, clock, git);
        }

        public FakeAiPlatform Platform(string id) => platformMap[id];

        public PlatformProfileId Profile(string id) => profileIds[id];

        public int UsageReadCount(string id) => usageMap[id].ForceRefreshRequests.Count;

        public int UsageReadCount(string id, PlatformProfileId profileId) =>
            usageMap[id].ProfileRequests.Count(x => x == profileId);

        public async Task<PlatformProfileId> AddProfileAsync(string platformId, string name)
        {
            var profile = new PlatformProfile(PlatformProfileId.New(), new(platformId), name, name,
                Path.Combine(directory, "profiles", name));
            await new SqlitePlatformProfileRepository(contextFactory).SaveAsync(profile);
            SetUsage(platformId, profile.Id, 1);
            return profile.Id;
        }

        public async Task SetProfileEnabledAsync(PlatformProfileId profileId, bool enabled)
        {
            var repository = new SqlitePlatformProfileRepository(contextFactory);
            PlatformProfile current = (await repository.GetAsync(profileId))!;
            await repository.SaveAsync(new PlatformProfile(current.Id, current.PlatformId, current.Name,
                current.DisplayName, current.ConfigurationDirectory, enabled, current.IsDefault,
                current.ShowUsageInStatusBar));
        }

        public async Task CorruptStoredProfileAsync(WorkItemId itemId, PlatformProfileId profileId)
        {
            await using var db = await contextFactory.CreateDbContextAsync();
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF;");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE WorkItems SET PlatformProfileId = {profileId.Value} WHERE Id = {itemId.Value}");
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
        }

        public async Task SetPlatformEnabledAsync(string id, bool enabled)
        {
            var repository = new SqlitePlatformRepository(contextFactory);
            var current = (await repository.GetAsync(new PlatformId(id)))!;
            await repository.SaveAsync(new PlatformDefinition(current.Id, current.Executable, current.Models,
                current.Capacity, enabled, current.ShowUsageInStatusBar));
        }

        public void SetUsage(string id, decimal used) => usageMap[id].SetCurrent(
            profileIds[id], UsageReadResult.Available(CreateSnapshot(id, profileIds[id], Clock.UtcNow, used)));

        public void SetUsage(string id, PlatformProfileId profileId, decimal used) =>
            usageMap[id].SetCurrent(profileId,
                UsageReadResult.Available(CreateSnapshot(id, profileId, Clock.UtcNow, used)));

        public void SetUsage(string id, decimal used, TimeSpan resetIn, string? rateLimitReachedType = null) =>
            usageMap[id].SetCurrent(profileIds[id], UsageReadResult.Available(new UsageSnapshot(new(id),
                profileIds[id], Clock.UtcNow, "fake",
                UsageQuality.Aktuell, [new UsageWindow("primary", new(used), Clock.UtcNow + resetIn, "fake",
                    Clock.UtcNow, UsageQuality.Aktuell, rateLimitReachedType)])));

        public void SetUnknownUsage(string id) => usageMap[id].SetCurrent(profileIds[id],
            UsageReadResult.Unknown("unbekannt").ForProfile(profileIds[id]));

        public void SetUnknownUsage(string id, PlatformProfileId profileId) => usageMap[id].SetCurrent(profileId,
            UsageReadResult.Unknown("unbekannt").ForProfile(profileId));

        public async Task<ProjectId> AddProjectAsync(string name)
        {
            var id = ProjectId.New();
            var root = Path.Combine(directory, name);
            Directory.CreateDirectory(Path.Combine(root, "docs"));
            projectRoots[id] = root;
            await new SqliteProjectRepository(contextFactory).SaveAsync(new ProjectDefinition(id, name, root, "master"));
            return id;
        }

        public string ProjectRoot(ProjectId id) => projectRoots[id];

        public async Task<WorkItem> AddWorkItemAsync(string platformId, ProjectId projectId, int priority,
            bool autoCommit = true, PlatformProfileId? profileId = null)
        {
            var prompt = Path.Combine(projectRoots[projectId], "docs", $"{Guid.NewGuid():N}.md");
            await File.WriteAllTextAsync(prompt, "Implementiere das Arbeitspaket.");
            var item = new WorkItem(WorkItemId.New(), prompt, new(priority), new(platformId),
                profileId ?? profileIds[platformId], new("gpt"),
                new("medium"), new(prompt), autoCommit, Clock.UtcNow, projectId);
            item.TransitionTo(WorkItemStatus.InWarteschlange);
            await WorkItems.SaveAsync(item);
            return item;
        }

        public async ValueTask DisposeAsync()
        {
            await Engine.DisposeAsync();
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }

        private static UsageSnapshot CreateSnapshot(string platformId, PlatformProfileId profileId,
            DateTimeOffset now, decimal used) =>
            new(new(platformId), profileId, now, "fake", UsageQuality.Aktuell,
                [new UsageWindow("primary", new(used), now.AddHours(1), "fake", now, UsageQuality.Aktuell)]);
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;
        public void Advance(TimeSpan duration) => UtcNow += duration;
    }

    private sealed class FakeGitService : IGitService
    {
        public GitInspectionResult? NextInspection { get; set; }
        public GitCommitStatus NextCommitStatus { get; set; } = GitCommitStatus.Committed;
        public List<GitInspectionRequest> InspectionRequests { get; } = [];
        public List<GitCommitRequest> CommitRequests { get; } = [];

        public Task<GitInspectionResult> InspectAsync(GitInspectionRequest request,
            CancellationToken cancellationToken = default)
        {
            InspectionRequests.Add(request);
            var snapshot = new GitWorkingTreeSnapshot(request.ProjectRoot, request.TargetBranch, Array.Empty<string>());
            return Task.FromResult(NextInspection
                ?? new GitInspectionResult(GitInspectionStatus.Ready, "bereit", snapshot));
        }

        public Task<GitCommitResult> CommitAllAsync(GitCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            CommitRequests.Add(request);
            var before = new GitWorkingTreeSnapshot(request.ProjectRoot, request.TargetBranch,
                NextCommitStatus == GitCommitStatus.NoChanges ? Array.Empty<string>() : [" M generated.txt"]);
            var after = new GitWorkingTreeSnapshot(request.ProjectRoot, request.TargetBranch, Array.Empty<string>());
            return Task.FromResult(new GitCommitResult(NextCommitStatus,
                NextCommitStatus == GitCommitStatus.NoChanges ? "keine Änderungen" : "committed", before, after,
                NextCommitStatus == GitCommitStatus.Committed ? "abc123" : null));
        }
    }

    private sealed class TestContextFactory(DbContextOptions<KischedulerDbContext> options)
        : IDbContextFactory<KischedulerDbContext>
    {
        public KischedulerDbContext CreateDbContext() => new(options);
    }
}
