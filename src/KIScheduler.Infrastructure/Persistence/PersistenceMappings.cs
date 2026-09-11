using System.Text.Json;
using KIScheduler.Core.Domain;

namespace KIScheduler.Infrastructure.Persistence;

internal static class PersistenceMappings
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static WorkItemRow ToRow(WorkItem value) => new()
    {
        Id = value.Id.Value,
        Title = value.Title,
        Priority = value.Priority.Value,
        PlatformId = value.PlatformId.Value,
        ModelId = value.ModelId.Value,
        Effort = value.Effort.Value,
        PromptPath = value.PromptPath.Value,
        AutoCommit = value.AutoCommit,
        ProjectId = value.ProjectId?.Value,
        CreatedAtUtc = value.CreatedAtUtc,
        FirstAttemptStartedAtUtc = value.FirstAttemptStartedAtUtc,
        HasExecutionStarted = value.HasExecutionStarted,
        Status = (int)value.Status,
        NormalRetryCount = value.NormalRetryCount
    };

    public static WorkItem ToDomain(WorkItemRow value) => WorkItem.Rehydrate(
        new(value.Id), value.Title, new(value.Priority), new(value.PlatformId), new(value.ModelId),
        new(value.Effort), new(value.PromptPath), value.AutoCommit, value.CreatedAtUtc,
        value.ProjectId.HasValue ? new ProjectId(value.ProjectId.Value) : null,
        (WorkItemStatus)value.Status, value.FirstAttemptStartedAtUtc, value.HasExecutionStarted,
        value.NormalRetryCount);

    public static ExecutionAttemptRow ToRow(ExecutionAttempt value) => new()
    {
        Id = value.Id.Value,
        WorkItemId = value.WorkItemId.Value,
        SequenceNumber = value.SequenceNumber,
        PlatformId = value.PlatformId.Value,
        ModelId = value.ModelId.Value,
        Effort = value.Effort.Value,
        StartedAtUtc = value.StartedAtUtc,
        CompletedAtUtc = value.CompletedAtUtc,
        Result = (int)value.Result,
        ExitCode = value.ExitCode,
        SessionId = value.SessionId,
        Diagnostic = value.Diagnostic
    };

    public static ExecutionAttempt ToDomain(ExecutionAttemptRow value) => new(new(value.Id), new(value.WorkItemId),
        value.SequenceNumber, new(value.PlatformId), new(value.ModelId), new(value.Effort), value.StartedAtUtc,
        value.CompletedAtUtc, (ExecutionAttemptResult)value.Result, value.ExitCode, value.SessionId, value.Diagnostic);

    public static ExecutionEventRow ToRow(ExecutionEvent value) => new()
    {
        Id = value.Id,
        WorkItemId = value.WorkItemId.Value,
        AttemptId = value.AttemptId?.Value,
        OccurredAtUtc = value.OccurredAtUtc,
        Severity = (int)value.Severity,
        EventType = value.EventType,
        Message = value.Message,
        DataJson = JsonSerializer.Serialize(value.Data, JsonOptions)
    };

    public static ExecutionEvent ToDomain(ExecutionEventRow value) => new(value.Id, new(value.WorkItemId),
        value.OccurredAtUtc, (ExecutionEventSeverity)value.Severity, value.EventType, value.Message,
        value.AttemptId.HasValue ? new ExecutionAttemptId(value.AttemptId.Value) : null,
        JsonSerializer.Deserialize<Dictionary<string, string>>(value.DataJson, JsonOptions));

    public static PlatformUsageBlockRow ToRow(PlatformUsageBlock value) => new()
    {
        Id = value.Id,
        PlatformId = value.PlatformId.Value,
        TriggeringWorkItemId = value.TriggeringWorkItemId.Value,
        TriggeringAttemptId = value.TriggeringAttemptId?.Value,
        Reason = value.Reason,
        CreatedAtUtc = value.CreatedAtUtc,
        ReleaseRule = (int)value.ReleaseRule,
        ReleasedAtUtc = value.ReleasedAtUtc,
        ReleaseReason = value.ReleaseReason
    };

    public static PlatformUsageBlock ToDomain(PlatformUsageBlockRow value)
    {
        var result = new PlatformUsageBlock(value.Id, new(value.PlatformId), new(value.TriggeringWorkItemId),
            value.TriggeringAttemptId.HasValue ? new ExecutionAttemptId(value.TriggeringAttemptId.Value) : null,
            value.Reason, value.CreatedAtUtc, (PlatformBlockReleaseRule)value.ReleaseRule);
        if (value.ReleasedAtUtc.HasValue)
        {
            result.Release(value.ReleasedAtUtc.Value, value.ReleaseReason!, true, false);
        }
        return result;
    }

    public static ProjectExecutionHoldRow ToRow(ProjectExecutionHold value) => new()
    {
        Id = value.Id,
        ProjectId = value.ProjectId.Value,
        TriggeringWorkItemId = value.TriggeringWorkItemId.Value,
        PlatformId = value.PlatformId.Value,
        Reason = value.Reason,
        CreatedAtUtc = value.CreatedAtUtc,
        ReleaseRule = (int)value.ReleaseRule,
        ReleasedAtUtc = value.ReleasedAtUtc,
        ReleaseReason = value.ReleaseReason
    };

    public static ProjectExecutionHold ToDomain(ProjectExecutionHoldRow value)
    {
        var result = new ProjectExecutionHold(value.Id, new(value.ProjectId), new(value.TriggeringWorkItemId),
            new(value.PlatformId), value.Reason, value.CreatedAtUtc, (ProjectHoldReleaseRule)value.ReleaseRule);
        if (value.ReleasedAtUtc.HasValue)
        {
            result.Release(value.ReleasedAtUtc.Value, value.ReleaseReason!, true, WorkItemStatus.Abgebrochen);
        }
        return result;
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    public static T? Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions);
}
