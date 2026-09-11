using System.Collections.ObjectModel;

namespace KIScheduler.Core.Domain;

public sealed record ExecutionAttempt
{
    public ExecutionAttempt(ExecutionAttemptId id, WorkItemId workItemId, int sequenceNumber,
        PlatformId platformId, ModelId modelId, EffortLevel effort, DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc, ExecutionAttemptResult result, int? exitCode = null,
        string? sessionId = null, string? diagnostic = null)
    {
        DomainValidation.Id(id.Value, nameof(id));
        DomainValidation.Id(workItemId.Value, nameof(workItemId));
        if (sequenceNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber), "Die Versuchsnummer muss größer als null sein.");
        }

        Id = id;
        WorkItemId = workItemId;
        SequenceNumber = sequenceNumber;
        PlatformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
        ModelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
        Effort = effort ?? throw new ArgumentNullException(nameof(effort));
        StartedAtUtc = DomainValidation.Utc(startedAtUtc, nameof(startedAtUtc));
        CompletedAtUtc = DomainValidation.Utc(completedAtUtc, nameof(completedAtUtc));
        if (CompletedAtUtc < StartedAtUtc)
        {
            throw new ArgumentException("Das Ende eines Versuchs darf nicht vor seinem Start liegen.", nameof(completedAtUtc));
        }

        Result = result;
        ExitCode = exitCode;
        SessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId.Trim();
        Diagnostic = string.IsNullOrWhiteSpace(diagnostic) ? null : diagnostic.Trim();
    }

    public ExecutionAttemptId Id { get; }
    public WorkItemId WorkItemId { get; }
    public int SequenceNumber { get; }
    public PlatformId PlatformId { get; }
    public ModelId ModelId { get; }
    public EffortLevel Effort { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public DateTimeOffset CompletedAtUtc { get; }
    public ExecutionAttemptResult Result { get; }
    public int? ExitCode { get; }
    public string? SessionId { get; }
    public string? Diagnostic { get; }
    public bool ConsumesNormalRetry => Result == ExecutionAttemptResult.Fehlgeschlagen;
}

public sealed record ExecutionEvent
{
    public ExecutionEvent(Guid id, WorkItemId workItemId, DateTimeOffset occurredAtUtc,
        ExecutionEventSeverity severity, string eventType, string message, ExecutionAttemptId? attemptId = null,
        IReadOnlyDictionary<string, string>? data = null)
    {
        Id = DomainValidation.Id(id, nameof(id));
        DomainValidation.Id(workItemId.Value, nameof(workItemId));
        WorkItemId = workItemId;
        OccurredAtUtc = DomainValidation.Utc(occurredAtUtc, nameof(occurredAtUtc));
        Severity = severity;
        EventType = DomainValidation.Required(eventType, nameof(eventType));
        Message = DomainValidation.Required(message, nameof(message));
        if (attemptId.HasValue)
        {
            DomainValidation.Id(attemptId.Value.Value, nameof(attemptId));
        }

        AttemptId = attemptId;
        Data = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(data ??
            new Dictionary<string, string>(), StringComparer.Ordinal));
    }

    public Guid Id { get; }
    public WorkItemId WorkItemId { get; }
    public ExecutionAttemptId? AttemptId { get; }
    public DateTimeOffset OccurredAtUtc { get; }
    public ExecutionEventSeverity Severity { get; }
    public string EventType { get; }
    public string Message { get; }
    public IReadOnlyDictionary<string, string> Data { get; }
}
