namespace KIScheduler.Core.Domain;

public sealed class PlatformUsageBlock
{
    public PlatformUsageBlock(Guid id, PlatformId platformId, WorkItemId triggeringWorkItemId,
        ExecutionAttemptId? triggeringAttemptId, string reason, DateTimeOffset createdAtUtc,
        PlatformBlockReleaseRule releaseRule = PlatformBlockReleaseRule.FrischerZulaessigerUsageSnapshot)
    {
        Id = DomainValidation.Id(id, nameof(id));
        PlatformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
        DomainValidation.Id(triggeringWorkItemId.Value, nameof(triggeringWorkItemId));
        TriggeringWorkItemId = triggeringWorkItemId;
        if (triggeringAttemptId.HasValue)
        {
            DomainValidation.Id(triggeringAttemptId.Value.Value, nameof(triggeringAttemptId));
        }

        TriggeringAttemptId = triggeringAttemptId;
        Reason = DomainValidation.Required(reason, nameof(reason));
        CreatedAtUtc = DomainValidation.Utc(createdAtUtc, nameof(createdAtUtc));
        ReleaseRule = releaseRule;
    }

    public Guid Id { get; }
    public PlatformId PlatformId { get; }
    public WorkItemId TriggeringWorkItemId { get; }
    public ExecutionAttemptId? TriggeringAttemptId { get; }
    public string Reason { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public PlatformBlockReleaseRule ReleaseRule { get; }
    public DateTimeOffset? ReleasedAtUtc { get; private set; }
    public string? ReleaseReason { get; private set; }
    public bool IsActive => ReleasedAtUtc is null;

    public void Release(DateTimeOffset releasedAtUtc, string reason, bool isManual, bool hasFreshPermissibleSnapshot)
    {
        EnsureActive();
        releasedAtUtc = DomainValidation.Utc(releasedAtUtc, nameof(releasedAtUtc));
        if (releasedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentException("Die Freigabe darf nicht vor der Sperre liegen.", nameof(releasedAtUtc));
        }

        var allowed = ReleaseRule switch
        {
            PlatformBlockReleaseRule.NurManuell => isManual,
            PlatformBlockReleaseRule.FrischerZulaessigerUsageSnapshot => isManual || hasFreshPermissibleSnapshot,
            _ => false
        };
        if (!allowed)
        {
            throw new InvalidOperationException("Die Freigaberegel der Plattformsperre ist nicht erfüllt.");
        }

        ReleasedAtUtc = releasedAtUtc;
        ReleaseReason = DomainValidation.Required(reason, nameof(reason));
    }

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Die Plattformsperre wurde bereits aufgehoben.");
        }
    }
}

public sealed class ProjectExecutionHold
{
    public ProjectExecutionHold(Guid id, ProjectId projectId, WorkItemId triggeringWorkItemId,
        PlatformId platformId, string reason, DateTimeOffset createdAtUtc,
        ProjectHoldReleaseRule releaseRule = ProjectHoldReleaseRule.ErfolgreicherAbschlussOderExpliziterAbbruch)
    {
        Id = DomainValidation.Id(id, nameof(id));
        DomainValidation.Id(projectId.Value, nameof(projectId));
        DomainValidation.Id(triggeringWorkItemId.Value, nameof(triggeringWorkItemId));
        ProjectId = projectId;
        TriggeringWorkItemId = triggeringWorkItemId;
        PlatformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
        Reason = DomainValidation.Required(reason, nameof(reason));
        CreatedAtUtc = DomainValidation.Utc(createdAtUtc, nameof(createdAtUtc));
        ReleaseRule = releaseRule;
    }

    public Guid Id { get; }
    public ProjectId ProjectId { get; }
    public WorkItemId TriggeringWorkItemId { get; }
    public PlatformId PlatformId { get; }
    public string Reason { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public ProjectHoldReleaseRule ReleaseRule { get; }
    public DateTimeOffset? ReleasedAtUtc { get; private set; }
    public string? ReleaseReason { get; private set; }
    public bool IsActive => ReleasedAtUtc is null;

    public void Release(DateTimeOffset releasedAtUtc, string reason, bool isManual, WorkItemStatus triggeringWorkItemStatus)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Der Projekt-Hold wurde bereits aufgehoben.");
        }

        releasedAtUtc = DomainValidation.Utc(releasedAtUtc, nameof(releasedAtUtc));
        if (releasedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentException("Die Freigabe darf nicht vor dem Hold liegen.", nameof(releasedAtUtc));
        }

        var completedOrCancelled = triggeringWorkItemStatus is WorkItemStatus.TechnischErfolgreich
            or WorkItemStatus.ErfolgreichMitWarnung or WorkItemStatus.Abgebrochen;
        var allowed = ReleaseRule switch
        {
            ProjectHoldReleaseRule.NurManuell => isManual,
            ProjectHoldReleaseRule.ErfolgreicherAbschlussOderExpliziterAbbruch => isManual || completedOrCancelled,
            _ => false
        };
        if (!allowed)
        {
            throw new InvalidOperationException("Die Freigaberegel des Projekt-Holds ist nicht erfüllt.");
        }

        ReleasedAtUtc = releasedAtUtc;
        ReleaseReason = DomainValidation.Required(reason, nameof(reason));
    }
}

public sealed record SchedulerLease
{
    public SchedulerLease(SchedulerLeaseId id, WorkItemId workItemId, string ownerId,
        DateTimeOffset acquiredAtUtc, DateTimeOffset expiresAtUtc)
    {
        DomainValidation.Id(id.Value, nameof(id));
        DomainValidation.Id(workItemId.Value, nameof(workItemId));
        Id = id;
        WorkItemId = workItemId;
        OwnerId = DomainValidation.Required(ownerId, nameof(ownerId));
        AcquiredAtUtc = DomainValidation.Utc(acquiredAtUtc, nameof(acquiredAtUtc));
        ExpiresAtUtc = DomainValidation.Utc(expiresAtUtc, nameof(expiresAtUtc));
        if (ExpiresAtUtc <= AcquiredAtUtc)
        {
            throw new ArgumentException("Eine Lease muss nach ihrer Erstellung ablaufen.", nameof(expiresAtUtc));
        }
    }

    public SchedulerLeaseId Id { get; }
    public WorkItemId WorkItemId { get; }
    public string OwnerId { get; }
    public DateTimeOffset AcquiredAtUtc { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public bool IsExpired(DateTimeOffset nowUtc) => DomainValidation.Utc(nowUtc, nameof(nowUtc)) >= ExpiresAtUtc;
}
