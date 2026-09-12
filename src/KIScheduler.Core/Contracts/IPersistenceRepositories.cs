using KIScheduler.Core.Domain;

namespace KIScheduler.Core.Contracts;

public interface IProjectRepository
{
    Task<ProjectDefinition?> GetAsync(ProjectId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectDefinition>> ListAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ProjectDefinition project, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(ProjectId id, CancellationToken cancellationToken = default);
}

public interface IPlatformRepository
{
    Task<PlatformDefinition?> GetAsync(PlatformId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlatformDefinition>> ListAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PlatformDefinition platform, CancellationToken cancellationToken = default);
}

public interface IUsagePolicyRepository
{
    Task<IReadOnlyList<UsagePolicy>> ListAsync(PlatformId? platformId = null,
        CancellationToken cancellationToken = default);
    Task ReplaceAsync(IReadOnlyCollection<UsagePolicy> policies,
        CancellationToken cancellationToken = default);
}

public interface IUsageSnapshotRepository
{
    Task<UsageSnapshot?> GetLatestAsync(PlatformId platformId,
        CancellationToken cancellationToken = default);
    Task SaveAsync(UsageSnapshot snapshot, CancellationToken cancellationToken = default);
    Task InvalidateAsync(PlatformId platformId, CancellationToken cancellationToken = default);
}

public interface IExecutionHistoryRepository
{
    Task AddAttemptAsync(ExecutionAttempt attempt, CancellationToken cancellationToken = default);
    Task AddEventAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecutionAttempt>> ListAttemptsAsync(WorkItemId workItemId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecutionEvent>> ListEventsAsync(WorkItemId workItemId,
        CancellationToken cancellationToken = default);
}

public interface IExecutionBlockRepository
{
    Task<IReadOnlyList<PlatformUsageBlock>> ListActivePlatformBlocksAsync(
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectExecutionHold>> ListActiveProjectHoldsAsync(
        CancellationToken cancellationToken = default);
    Task SaveAsync(PlatformUsageBlock block, CancellationToken cancellationToken = default);
    Task SaveAsync(ProjectExecutionHold hold, CancellationToken cancellationToken = default);
}

public interface ISettingsRepository
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);
}

public sealed record UsageExceededPersistenceRequest(
    WorkItem WorkItem,
    ExecutionAttempt Attempt,
    ExecutionEvent Event,
    PlatformUsageBlock PlatformBlock,
    ProjectExecutionHold ProjectHold);

public interface IAtomicExecutionRepository
{
    Task PersistUsageExceededAsync(UsageExceededPersistenceRequest request,
        CancellationToken cancellationToken = default);
}
