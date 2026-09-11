namespace KIScheduler.Core.Domain;

public sealed class WorkItem
{
    public WorkItem(WorkItemId id, string title, WorkItemPriority priority, PlatformId platformId,
        ModelId modelId, EffortLevel effort, PromptPath promptPath, bool autoCommit,
        DateTimeOffset createdAtUtc, ProjectId? projectId = null)
    {
        DomainValidation.Id(id.Value, nameof(id));
        Id = id;
        Title = DomainValidation.Required(title, nameof(title));
        Priority = priority;
        PlatformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
        ModelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
        Effort = effort ?? throw new ArgumentNullException(nameof(effort));
        PromptPath = promptPath ?? throw new ArgumentNullException(nameof(promptPath));
        AutoCommit = autoCommit;
        CreatedAtUtc = DomainValidation.Utc(createdAtUtc, nameof(createdAtUtc));
        ProjectId = projectId;
        Status = WorkItemStatus.Entwurf;
    }

    public WorkItemId Id { get; }
    public string Title { get; private set; }
    public WorkItemPriority Priority { get; private set; }
    public PlatformId PlatformId { get; private set; }
    public ModelId ModelId { get; private set; }
    public EffortLevel Effort { get; private set; }
    public PromptPath PromptPath { get; private set; }
    public bool AutoCommit { get; private set; }
    public ProjectId? ProjectId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset? FirstAttemptStartedAtUtc { get; private set; }
    public bool HasExecutionStarted { get; private set; }
    public WorkItemStatus Status { get; private set; }
    public int NormalRetryCount { get; private set; }

    public static WorkItem Rehydrate(WorkItemId id, string title, WorkItemPriority priority,
        PlatformId platformId, ModelId modelId, EffortLevel effort, PromptPath promptPath,
        bool autoCommit, DateTimeOffset createdAtUtc, ProjectId? projectId, WorkItemStatus status,
        DateTimeOffset? firstAttemptStartedAtUtc, bool hasExecutionStarted, int normalRetryCount)
    {
        if (firstAttemptStartedAtUtc.HasValue)
        {
            DomainValidation.Utc(firstAttemptStartedAtUtc.Value, nameof(firstAttemptStartedAtUtc));
        }

        if (normalRetryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(normalRetryCount));
        }

        var item = new WorkItem(id, title, priority, platformId, modelId, effort, promptPath,
            autoCommit, createdAtUtc, projectId)
        {
            Status = status,
            FirstAttemptStartedAtUtc = firstAttemptStartedAtUtc,
            HasExecutionStarted = hasExecutionStarted,
            NormalRetryCount = normalRetryCount
        };
        return item;
    }

    public void TransitionTo(WorkItemStatus target)
    {
        WorkItemStateMachine.EnsureAllowed(Status, target);
        Status = target;
        if (target == WorkItemStatus.InBearbeitung)
        {
            HasExecutionStarted = true;
        }
    }

    public void MarkAttemptStarted(DateTimeOffset startedAtUtc)
    {
        if (Status != WorkItemStatus.InBearbeitung)
        {
            throw new InvalidOperationException("Ein Versuch kann nur im Status 'InBearbeitung' gestartet werden.");
        }

        startedAtUtc = DomainValidation.Utc(startedAtUtc, nameof(startedAtUtc));
        FirstAttemptStartedAtUtc ??= startedAtUtc;
    }

    public void RegisterAttemptResult(ExecutionAttemptResult result)
    {
        if (result == ExecutionAttemptResult.Fehlgeschlagen)
        {
            NormalRetryCount++;
        }
    }

    public void CompleteCurrentAttempt(ExecutionAttemptResult result)
    {
        if (Status != WorkItemStatus.InBearbeitung)
        {
            throw new InvalidOperationException("Ein Versuch kann nur im Status 'InBearbeitung' abgeschlossen werden.");
        }

        var target = result switch
        {
            ExecutionAttemptResult.TechnischErfolgreich => WorkItemStatus.TechnischErfolgreich,
            ExecutionAttemptResult.ErfolgreichMitWarnung => WorkItemStatus.ErfolgreichMitWarnung,
            ExecutionAttemptResult.Fehlgeschlagen => WorkItemStatus.Fehlgeschlagen,
            ExecutionAttemptResult.Abgebrochen => WorkItemStatus.Abgebrochen,
            ExecutionAttemptResult.Unterbrochen => WorkItemStatus.Unterbrochen,
            ExecutionAttemptResult.MenschlichePruefung => WorkItemStatus.MenschlichePruefung,
            ExecutionAttemptResult.UsageExceeded => WorkItemStatus.WartetAufUsage,
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };

        RegisterAttemptResult(result);
        TransitionTo(target);
    }

    public void ChangeExecutionConfiguration(PlatformId platformId, ModelId modelId, EffortLevel effort)
    {
        if (HasExecutionStarted)
        {
            throw new InvalidOperationException("Plattform, Modell und Effort können nach dem Start eines Versuchs nicht geändert werden.");
        }

        PlatformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
        ModelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
        Effort = effort ?? throw new ArgumentNullException(nameof(effort));
    }

    public void ChangePlanning(WorkItemPriority priority, PromptPath promptPath, bool autoCommit, ProjectId? projectId)
    {
        Priority = priority;
        PromptPath = promptPath ?? throw new ArgumentNullException(nameof(promptPath));
        AutoCommit = autoCommit;
        ProjectId = projectId;
    }

    public WorkItemDisplayStatus GetDisplayStatus(bool projectHasExecutionHold)
    {
        if (projectHasExecutionHold && Status is WorkItemStatus.InWarteschlange or WorkItemStatus.WartetAufUsage)
        {
            return WorkItemDisplayStatus.ProjektAngehalten;
        }

        return Enum.Parse<WorkItemDisplayStatus>(Status.ToString());
    }
}
