using KIScheduler.Core.Domain;
using KIScheduler.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Persistence;

[TestClass]
public sealed class SqlitePersistenceTests
{
    private string? databasePath;
    private IDbContextFactory<KischedulerDbContext>? factory;
    private PlatformProfileId codexProfileId;
    private PlatformProfileId claudeProfileId;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        databasePath = Path.Combine(Path.GetTempPath(), $"kischeduler-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<KischedulerDbContext>()
            .UseSqlite($"Data Source={databasePath};Foreign Keys=True;Default Timeout=5").Options;
        factory = new TestContextFactory(options);
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
        var platforms = new SqlitePlatformRepository(factory);
        await platforms.SaveAsync(new PlatformDefinition(new("codex"), "codex",
            [new PlatformModel(new("gpt"), [new("medium")])]));
        await platforms.SaveAsync(new PlatformDefinition(new("claude"), "claude",
            [new PlatformModel(new("gpt"), [new("medium")])]));
        var profiles = new SqlitePlatformProfileRepository(factory);
        codexProfileId = (await profiles.GetDefaultAsync(new("codex")))!.Id;
        claudeProfileId = (await profiles.GetDefaultAsync(new("claude")))!.Id;
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (databasePath is null) return;
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = databasePath + suffix;
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public async Task WorkItemWithAttemptsAndEventsSurvivesNewRepositoryInstances()
    {
        var workItems = new SqliteWorkItemRepository(factory!);
        var history = new SqliteExecutionHistoryRepository(factory!);
        var item = CreateQueuedWorkItem();
        item.ChangeCommitMessage("AP9: persistierte Nachricht");
        await workItems.SaveAsync(item);
        var now = DateTimeOffset.UtcNow;
        var attempt = new ExecutionAttempt(ExecutionAttemptId.New(), item.Id, 1, item.PlatformId,
            item.PlatformProfileId, item.ModelId,
            item.Effort, now, now.AddMinutes(1), ExecutionAttemptResult.TechnischErfolgreich, 0, "session-1");
        await history.AddAttemptAsync(attempt);
        await history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), item.Id, now, ExecutionEventSeverity.Information,
            "completed", "Auftrag abgeschlossen", attempt.Id, new Dictionary<string, string> { ["source"] = "test" }));
        var secondAttempt = new ExecutionAttempt(ExecutionAttemptId.New(), item.Id, 2, item.PlatformId,
            item.PlatformProfileId, item.ModelId,
            item.Effort, now.AddMinutes(2), now.AddMinutes(3), ExecutionAttemptResult.Fehlgeschlagen, 2);
        await history.AddAttemptAsync(secondAttempt);
        await history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), item.Id, now.AddMinutes(3), ExecutionEventSeverity.Error,
            "failed", "Zweiter Versuch fehlgeschlagen", secondAttempt.Id));

        var restartedItems = new SqliteWorkItemRepository(factory!);
        var restartedHistory = new SqliteExecutionHistoryRepository(factory!);
        var restored = await restartedItems.GetAsync(item.Id);
        var attempts = await restartedHistory.ListAttemptsAsync(item.Id);
        var events = await restartedHistory.ListEventsAsync(item.Id);

        Assert.IsNotNull(restored);
        Assert.AreEqual(WorkItemStatus.InWarteschlange, restored.Status);
        Assert.AreEqual("AP9: persistierte Nachricht", restored.CommitMessage);
        Assert.AreEqual(2, attempts.Count);
        Assert.AreEqual("session-1", attempts[0].SessionId);
        Assert.IsTrue(attempts.All(x => x.PlatformProfileId == item.PlatformProfileId));
        Assert.AreEqual(2, events.Count);
        Assert.AreEqual("test", events[0].Data["source"]);
    }

    [TestMethod]
    public async Task SettingsRoundTripRejectsSensitiveKeys()
    {
        var settings = new SqliteSettingsRepository(factory!);
        await settings.SetAsync("worker.pollIntervalSeconds", "15");

        Assert.AreEqual("15", await new SqliteSettingsRepository(factory!).GetAsync("worker.pollIntervalSeconds"));
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => settings.SetAsync("apiToken", "secret-value"));
    }

    [TestMethod]
    public async Task ProjectConfigurationIncludingTemplateAndValidationCommandIsPersisted()
    {
        var repository = new SqliteProjectRepository(factory!);
        var project = new ProjectDefinition(ProjectId.New(), "Konfiguriertes Projekt",
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), "develop",
            [new ValidationCommand("dotnet", ["test", "--no-restore"], required: true)], "webapi");

        await repository.SaveAsync(project);
        ProjectDefinition? restored = await new SqliteProjectRepository(factory!).GetAsync(project.Id);

        Assert.IsNotNull(restored);
        Assert.AreEqual("develop", restored.TargetBranch);
        Assert.AreEqual("webapi", restored.DefaultTemplate);
        Assert.AreEqual(1, restored.ValidationCommands.Count);
        Assert.IsTrue(restored.ValidationCommands[0].Required);
        CollectionAssert.AreEqual(new[] { "test", "--no-restore" },
            restored.ValidationCommands[0].Arguments.ToArray());
    }

    [TestMethod]
    public async Task UnusedProjectCanBeDeletedWithoutDeletingItsDirectory()
    {
        var repository = new SqliteProjectRepository(factory!);
        var root = Path.GetTempPath();
        var project = new ProjectDefinition(ProjectId.New(), "Löschbares Projekt", root);
        await repository.SaveAsync(project);

        Assert.IsTrue(await repository.DeleteAsync(project.Id));
        Assert.IsNull(await repository.GetAsync(project.Id));
        Assert.IsFalse(await repository.DeleteAsync(project.Id));
        Assert.IsTrue(Directory.Exists(root));
    }

    [TestMethod]
    public async Task PlatformActivationAndStatusBarPreferenceArePersisted()
    {
        var repository = new SqlitePlatformRepository(factory!);
        var platform = new PlatformDefinition(new PlatformId("codex"), "codex",
            [new PlatformModel(new ModelId("gpt"), [new EffortLevel("medium")])],
            enabled: false, showUsageInStatusBar: true);

        await repository.SaveAsync(platform);
        var restored = await new SqlitePlatformRepository(factory!).GetAsync(platform.Id);

        Assert.IsNotNull(restored);
        Assert.IsFalse(restored.Enabled);
        Assert.IsTrue(restored.ShowUsageInStatusBar);
    }

    [TestMethod]
    public async Task PlatformProfilesEnforceDefaultNameAndDirectoryUniquenessAndDisableById()
    {
        var repository = new SqlitePlatformProfileRepository(factory!);
        var root = Path.Combine(Path.GetTempPath(), $"profile-{Guid.NewGuid():N}");
        var second = new PlatformProfile(PlatformProfileId.New(), new("codex"), "work", "Arbeit", root);
        await repository.SaveAsync(second);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => repository.SaveAsync(
            new PlatformProfile(PlatformProfileId.New(), new("codex"), "WORK", "Duplikat",
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")))));
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => repository.SaveAsync(
            new PlatformProfile(PlatformProfileId.New(), new("codex"), "other", "Duplikat",
                root.ToUpperInvariant())));
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => repository.SaveAsync(
            new PlatformProfile(PlatformProfileId.New(), new("codex"), "third", "ARBEIT",
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")))));
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => repository.SaveAsync(
            PlatformProfile.CreateDefault(PlatformProfileId.New(), new("codex"),
                userProfileDirectory: Path.Combine(Path.GetTempPath(), "another-user"))));

        Assert.IsTrue(await repository.DisableAsync(second.Id));
        Assert.IsFalse((await repository.GetAsync(second.Id))!.Enabled);
        Assert.IsFalse(Directory.Exists(root), "Das Speichern oder Deaktivieren darf den Profilordner nicht anlegen.");
        Assert.AreEqual(1, (await repository.ListAsync(new("codex"))).Count(x => x.IsDefault));
    }

    [TestMethod]
    public async Task WorkItemRejectsProfileFromAnotherPlatform()
    {
        var item = new WorkItem(WorkItemId.New(), "Falsches Profil", new(50), new("codex"),
            claudeProfileId, new("gpt"), new("medium"), new("docs/AP.md"), false,
            DateTimeOffset.UtcNow);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            new SqliteWorkItemRepository(factory!).SaveAsync(item));
    }

    [TestMethod]
    public async Task MigrationAssignsExistingRowsToDefaultProfilesWithoutDataLoss()
    {
        await using var db = await factory!.CreateDbContextAsync();
        var codex = new PlatformDefinition(new("codex"), "codex",
            [new PlatformModel(new("gpt"), [new("medium")])], showUsageInStatusBar: true);
        await new SqlitePlatformRepository(factory).SaveAsync(codex);
        await new SqliteSettingsRepository(factory).SetAsync("migration.marker", "kept");

        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260912100000_AddPlatformUiOptions");
        var workItemId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);
        await db.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO "WorkItems"
                ("Id", "Title", "Priority", "PlatformId", "ModelId", "Effort", "PromptPath",
                 "AutoCommit", "CommitMessage", "ProjectId", "CreatedAtUtc", "FirstAttemptStartedAtUtc",
                 "HasExecutionStarted", "Status", "NormalRetryCount")
            VALUES ({{workItemId}}, 'Bestand', 50, 'codex', 'gpt', 'medium', 'docs/AP.md',
                    0, NULL, NULL, {{now}}, {{now}}, 1, {{(int)WorkItemStatus.InBearbeitung}}, 0);

            INSERT INTO "ExecutionAttempts"
                ("Id", "WorkItemId", "SequenceNumber", "PlatformId", "ModelId", "Effort",
                 "StartedAtUtc", "CompletedAtUtc", "Result", "ExitCode", "SessionId", "Diagnostic")
            VALUES ({{attemptId}}, {{workItemId}}, 1, 'codex', 'gpt', 'medium', {{now}}, {{now.AddMinutes(1)}},
                    {{(int)ExecutionAttemptResult.TechnischErfolgreich}}, 0, 'session-before-ap13', NULL);
            """);

        await migrator.MigrateAsync();

        var profiles = await new SqlitePlatformProfileRepository(factory).ListAsync();
        Assert.AreEqual(2, profiles.Count);
        Assert.IsTrue(profiles.All(x => x.IsDefault && x.Name == PlatformProfile.DefaultName));
        var codexProfile = profiles.Single(x => x.PlatformId.Value == "codex");
        Assert.IsTrue(codexProfile.ShowUsageInStatusBar);
        Assert.AreEqual(Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE")!, ".codex"),
            codexProfile.ConfigurationDirectory);
        Assert.AreEqual(codexProfile.Id, (await new SqliteWorkItemRepository(factory)
            .GetAsync(new(workItemId)))!.PlatformProfileId);
        Assert.AreEqual(codexProfile.Id, (await new SqliteExecutionHistoryRepository(factory)
            .ListAttemptsAsync(new(workItemId))).Single().PlatformProfileId);
        Assert.AreEqual("session-before-ap13", (await new SqliteExecutionHistoryRepository(factory)
            .ListAttemptsAsync(new(workItemId))).Single().SessionId);
        Assert.AreEqual("kept", await new SqliteSettingsRepository(factory).GetAsync("migration.marker"));

        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.IsFalse(await reader.ReadAsync(), "Die migrierte Datenbank darf keine verletzten Fremdschlüssel enthalten.");
    }

    [TestMethod]
    public async Task ConcurrentLeaseAttemptsReserveAWorkItemOnlyOnce()
    {
        var repository = new SqliteWorkItemRepository(factory!);
        var item = CreateQueuedWorkItem();
        await repository.SaveAsync(item);
        var now = DateTimeOffset.UtcNow;

        var results = await Task.WhenAll(
            repository.TryAcquireLeaseAsync(item.Id, "worker-a", now, TimeSpan.FromMinutes(1)),
            repository.TryAcquireLeaseAsync(item.Id, "worker-b", now, TimeSpan.FromMinutes(1)));

        Assert.AreEqual(1, results.Count(x => x is not null));
        Assert.AreEqual(1, results.Count(x => x is null));
        Assert.AreEqual(WorkItemStatus.Reserviert, (await repository.GetAsync(item.Id))!.Status);
    }

    [TestMethod]
    public async Task LeasesAtomicallyEnforcePlatformAndProjectCapacity()
    {
        var repository = new SqliteWorkItemRepository(factory!);
        var firstProject = ProjectId.New();
        var secondProject = ProjectId.New();
        var projects = new SqliteProjectRepository(factory!);
        await projects.SaveAsync(new ProjectDefinition(firstProject, "First", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), "master"));
        await projects.SaveAsync(new ProjectDefinition(secondProject, "Second", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), "master"));
        var first = CreateQueuedWorkItem(firstProject);
        var samePlatform = CreateQueuedWorkItem(secondProject);
        var sameProject = CreateQueuedWorkItem(firstProject, "claude");
        await repository.SaveAsync(first);
        await repository.SaveAsync(samePlatform);
        await repository.SaveAsync(sameProject);
        var now = DateTimeOffset.UtcNow;

        Assert.IsNotNull(await repository.TryAcquireLeaseAsync(first.Id, "worker-a", now, TimeSpan.FromMinutes(1)));
        Assert.IsNull(await repository.TryAcquireLeaseAsync(samePlatform.Id, "worker-b", now, TimeSpan.FromMinutes(1)));
        Assert.IsNull(await repository.TryAcquireLeaseAsync(sameProject.Id, "worker-c", now, TimeSpan.FromMinutes(1)));
    }

    [TestMethod]
    public async Task UsageExceededResultAndBothBlocksAreSavedAtomically()
    {
        var projects = new SqliteProjectRepository(factory!);
        var workItems = new SqliteWorkItemRepository(factory!);
        var project = new ProjectDefinition(ProjectId.New(), "Test", Path.GetTempPath(), "master");
        await projects.SaveAsync(project);
        var item = CreateQueuedWorkItem(project.Id);
        await workItems.SaveAsync(item);
        var lease = await workItems.TryAcquireLeaseAsync(item.Id, "worker", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        Assert.IsNotNull(lease);
        item = (await workItems.GetAsync(item.Id))!;
        item.TransitionTo(WorkItemStatus.InBearbeitung);
        item.MarkAttemptStarted(DateTimeOffset.UtcNow);
        item.CompleteCurrentAttempt(ExecutionAttemptResult.UsageExceeded);
        var now = DateTimeOffset.UtcNow;
        var attempt = new ExecutionAttempt(ExecutionAttemptId.New(), item.Id, 1, item.PlatformId,
            item.PlatformProfileId, item.ModelId,
            item.Effort, now.AddSeconds(-1), now, ExecutionAttemptResult.UsageExceeded, 1, "session-2");
        var executionEvent = new ExecutionEvent(Guid.NewGuid(), item.Id, now, ExecutionEventSeverity.Error,
            "usage_exceeded", "Usage-Limit erreicht", attempt.Id);
        var platformBlock = new PlatformUsageBlock(Guid.NewGuid(), item.PlatformId, item.Id, attempt.Id,
            "Usage-Limit erreicht", now);
        var projectHold = new ProjectExecutionHold(Guid.NewGuid(), project.Id, item.Id, item.PlatformId,
            "Möglicherweise teilweise ausgeführt", now);

        await new SqliteAtomicExecutionRepository(factory!).PersistUsageExceededAsync(
            new(item, attempt, executionEvent, platformBlock, projectHold));

        Assert.AreEqual(WorkItemStatus.WartetAufUsage, (await workItems.GetAsync(item.Id))!.Status);
        Assert.AreEqual(1, (await new SqliteExecutionHistoryRepository(factory!).ListAttemptsAsync(item.Id)).Count);
        var blocks = new SqliteExecutionBlockRepository(factory!);
        Assert.AreEqual(1, (await blocks.ListActivePlatformBlocksAsync()).Count);
        Assert.AreEqual(1, (await blocks.ListActiveProjectHoldsAsync()).Count);
    }

    [TestMethod]
    public async Task StartupRecoveryInterruptsOrphanedExecutionAndCreatesDiagnosticHold()
    {
        var projects = new SqliteProjectRepository(factory!);
        var workItems = new SqliteWorkItemRepository(factory!);
        var history = new SqliteExecutionHistoryRepository(factory!);
        var project = new ProjectDefinition(ProjectId.New(), "Recovery", Path.GetTempPath(), "master");
        await projects.SaveAsync(project);
        var item = CreateQueuedWorkItem(project.Id);
        await workItems.SaveAsync(item);
        var detectedAt = DateTimeOffset.UtcNow;
        var lease = await workItems.TryAcquireLeaseAsync(item.Id, "crashed-worker",
            detectedAt.AddMinutes(-2), TimeSpan.FromHours(1));
        Assert.IsNotNull(lease);
        item = (await workItems.GetAsync(item.Id))!;
        item.TransitionTo(WorkItemStatus.InBearbeitung);
        item.MarkAttemptStarted(detectedAt.AddMinutes(-1));
        await workItems.SaveAsync(item);

        var result = await new SqliteStartupRecoveryRepository(factory!)
            .RecoverInterruptedAsync(detectedAt, "restart-worker");

        Assert.AreEqual(1, result.InterruptedWorkItemCount);
        Assert.AreEqual(1, result.CreatedProjectHoldCount);
        Assert.AreEqual(1, result.RemovedLeaseCount);
        Assert.AreEqual(WorkItemStatus.Unterbrochen, (await workItems.GetAsync(item.Id))!.Status);
        var attempt = (await history.ListAttemptsAsync(item.Id)).Single();
        Assert.AreEqual(ExecutionAttemptResult.Unterbrochen, attempt.Result);
        var recoveryEvent = (await history.ListEventsAsync(item.Id)).Single(x => x.EventType == "recovery.interrupted");
        Assert.AreEqual("InBearbeitung", recoveryEvent.Data["previousStatus"]);
        Assert.AreEqual("codex", recoveryEvent.Data["platformId"]);
        Assert.IsTrue(recoveryEvent.Data.ContainsKey("logReference"));
        Assert.AreEqual(1, (await new SqliteExecutionBlockRepository(factory!)
            .ListActiveProjectHoldsAsync()).Count);

        var second = await new SqliteStartupRecoveryRepository(factory!)
            .RecoverInterruptedAsync(detectedAt.AddSeconds(1), "another-restart");
        Assert.AreEqual(0, second.InterruptedWorkItemCount);
    }

    [TestMethod]
    public void DatabaseWorkerLockAllowsOnlyOneOwnerForTheSameDatabase()
    {
        using var first = new DatabaseWorkerLock(databasePath!);
        using var second = new DatabaseWorkerLock(databasePath!);

        Assert.IsTrue(first.TryAcquire());
        Assert.IsFalse(second.TryAcquire());
        first.Dispose();
        Assert.IsTrue(second.TryAcquire());
    }

    [TestMethod]
    public async Task DatabaseMigrationIsIdempotentAndLeavesCurrentSchema()
    {
        await using (var firstStart = factory!.CreateDbContext())
        {
            await firstStart.Database.MigrateAsync();
            Assert.AreEqual(0, (await firstStart.Database.GetPendingMigrationsAsync()).Count());
        }

        await using (var restarted = factory!.CreateDbContext())
        {
            await restarted.Database.MigrateAsync();
            Assert.AreEqual(0, (await restarted.Database.GetPendingMigrationsAsync()).Count());
            Assert.IsFalse(restarted.Database.HasPendingModelChanges());
            CollectionAssert.IsSubsetOf(new[]
            {
                "WorkItems", "Projects", "ExecutionAttempts", "ExecutionEvents", "PlatformUsageBlocks",
                "ProjectExecutionHolds", "SchedulerLeases", "Settings", "PlatformProfiles"
            }, (await restarted.Database.SqlQueryRaw<string>(
                "SELECT name AS Value FROM sqlite_master WHERE type = 'table'").ToListAsync()).ToArray());
        }
    }

    private WorkItem CreateQueuedWorkItem(ProjectId? projectId = null, string platformId = "codex")
    {
        var profileId = platformId.Equals("claude", StringComparison.OrdinalIgnoreCase)
            ? claudeProfileId : codexProfileId;
        var item = new WorkItem(WorkItemId.New(), "AP", new(50), new(platformId), profileId,
            new("gpt"), new("medium"),
            new("docs/AP.md"), true, DateTimeOffset.UtcNow, projectId);
        item.TransitionTo(WorkItemStatus.InWarteschlange);
        return item;
    }

    private sealed class TestContextFactory(DbContextOptions<KischedulerDbContext> options)
        : IDbContextFactory<KischedulerDbContext>
    {
        public KischedulerDbContext CreateDbContext() => new(options);
    }
}
