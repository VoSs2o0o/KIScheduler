using KIScheduler.Core.Domain;
using KIScheduler.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Persistence;

[TestClass]
public sealed class SqlitePersistenceTests
{
    private string? databasePath;
    private IDbContextFactory<KischedulerDbContext>? factory;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        databasePath = Path.Combine(Path.GetTempPath(), $"kischeduler-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<KischedulerDbContext>()
            .UseSqlite($"Data Source={databasePath};Foreign Keys=True;Default Timeout=5").Options;
        factory = new TestContextFactory(options);
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
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
        await workItems.SaveAsync(item);
        var now = DateTimeOffset.UtcNow;
        var attempt = new ExecutionAttempt(ExecutionAttemptId.New(), item.Id, 1, item.PlatformId, item.ModelId,
            item.Effort, now, now.AddMinutes(1), ExecutionAttemptResult.TechnischErfolgreich, 0, "session-1");
        await history.AddAttemptAsync(attempt);
        await history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), item.Id, now, ExecutionEventSeverity.Information,
            "completed", "Auftrag abgeschlossen", attempt.Id, new Dictionary<string, string> { ["source"] = "test" }));
        var secondAttempt = new ExecutionAttempt(ExecutionAttemptId.New(), item.Id, 2, item.PlatformId, item.ModelId,
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
        Assert.AreEqual(2, attempts.Count);
        Assert.AreEqual("session-1", attempts[0].SessionId);
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
        var attempt = new ExecutionAttempt(ExecutionAttemptId.New(), item.Id, 1, item.PlatformId, item.ModelId,
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

    private static WorkItem CreateQueuedWorkItem(ProjectId? projectId = null)
    {
        var item = new WorkItem(WorkItemId.New(), "AP", new(50), new("codex"), new("gpt"), new("medium"),
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
