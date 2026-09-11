using KIScheduler.Core.Domain;

namespace KIScheduler.Core.Contracts;

public interface IWorkItemRepository
{
    Task<WorkItem?> GetAsync(WorkItemId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkItem>> ListByStatusAsync(
        IReadOnlyCollection<WorkItemStatus> statuses,
        CancellationToken cancellationToken = default);

    Task SaveAsync(WorkItem workItem, CancellationToken cancellationToken = default);

    Task<SchedulerLease?> TryAcquireLeaseAsync(
        WorkItemId workItemId,
        string ownerId,
        DateTimeOffset acquiredAtUtc,
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    Task ReleaseLeaseAsync(SchedulerLeaseId leaseId, CancellationToken cancellationToken = default);
}
