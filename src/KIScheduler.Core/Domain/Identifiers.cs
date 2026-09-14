namespace KIScheduler.Core.Domain;

public readonly record struct WorkItemId
{
    public WorkItemId(Guid value) => Value = DomainValidation.Id(value, nameof(value));
    public Guid Value { get; }
    public static WorkItemId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public readonly record struct ExecutionAttemptId
{
    public ExecutionAttemptId(Guid value) => Value = DomainValidation.Id(value, nameof(value));
    public Guid Value { get; }
    public static ExecutionAttemptId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public readonly record struct PlatformProfileId
{
    public PlatformProfileId(Guid value) => Value = DomainValidation.Id(value, nameof(value));
    public Guid Value { get; }
    public static PlatformProfileId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public readonly record struct ProjectId
{
    public ProjectId(Guid value) => Value = DomainValidation.Id(value, nameof(value));
    public Guid Value { get; }
    public static ProjectId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public readonly record struct SchedulerLeaseId
{
    public SchedulerLeaseId(Guid value) => Value = DomainValidation.Id(value, nameof(value));
    public Guid Value { get; }
    public static SchedulerLeaseId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}
