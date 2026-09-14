using System.Collections.ObjectModel;
using KIScheduler.Core.Domain;

namespace KIScheduler.Core.Contracts;

public interface IAiPlatform
{
    PlatformId PlatformId { get; }
    PlatformCapabilities Capabilities { get; }
    Task<PlatformHealth> CheckAvailabilityAsync(CancellationToken cancellationToken = default);
    Task<PlatformHealth> CheckAvailabilityAsync(PlatformProfileId profileId,
        CancellationToken cancellationToken = default) => CheckAvailabilityAsync(cancellationToken);
    Task<PlatformExecutionResult> ExecuteAsync(PlatformExecutionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PlatformCapabilities(
    bool SupportsStructuredOutput,
    bool ProvidesSessionId,
    bool SupportsResume);

public enum PlatformHealthStatus
{
    Available,
    ExecutableMissing,
    Misconfigured,
    Unavailable
}

public sealed class PlatformHealth
{
    public PlatformHealth(PlatformHealthStatus status, string? message = null,
        IEnumerable<PlatformModel>? supportedModels = null)
    {
        Status = status;
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        SupportedModels = new ReadOnlyCollection<PlatformModel>((supportedModels ?? []).ToList());
    }

    public PlatformHealthStatus Status { get; }
    public string? Message { get; }
    public IReadOnlyList<PlatformModel> SupportedModels { get; }
    public bool IsAvailable => Status == PlatformHealthStatus.Available;

    public static PlatformHealth Available(IEnumerable<PlatformModel>? supportedModels = null) =>
        new(PlatformHealthStatus.Available, supportedModels: supportedModels);
}

public sealed record PlatformExecutionRequest
{
    public PlatformExecutionRequest(PlatformId platformId, PlatformProfileId platformProfileId,
        ModelId modelId, EffortLevel effort,
        string prompt, string workingDirectory)
    {
        PlatformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
        DomainValidation.Id(platformProfileId.Value, nameof(platformProfileId));
        PlatformProfileId = platformProfileId;
        ModelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
        Effort = effort ?? throw new ArgumentNullException(nameof(effort));
        Prompt = DomainValidation.Required(prompt, nameof(prompt));
        WorkingDirectory = DomainValidation.Required(workingDirectory, nameof(workingDirectory));
    }

    public PlatformId PlatformId { get; }
    public PlatformProfileId PlatformProfileId { get; }
    public ModelId ModelId { get; }
    public EffortLevel Effort { get; }
    public string Prompt { get; }
    public string WorkingDirectory { get; }
    public string? SessionId { get; init; }
    public TimeSpan? Timeout { get; init; }
}

public enum PlatformExecutionOutcome
{
    Succeeded,
    Failed,
    Cancelled,
    TimedOut,
    HumanReviewRequired,
    UsageExceeded
}

public enum PlatformFailureKind
{
    None,
    Authentication,
    Network,
    Parse,
    Quota,
    ProcessStart,
    Unknown
}

public sealed record PlatformExecutionEvent(string Type, string Json);

public sealed record PlatformExecutionResult
{
    public PlatformExecutionResult(PlatformExecutionOutcome outcome, int? exitCode = null,
        string? sessionId = null, string? message = null, bool mayHavePartialChanges = false,
        PlatformFailureKind failureKind = PlatformFailureKind.None,
        IEnumerable<PlatformExecutionEvent>? events = null)
    {
        Outcome = outcome;
        ExitCode = exitCode;
        SessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId.Trim();
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        MayHavePartialChanges = mayHavePartialChanges;
        FailureKind = failureKind;
        Events = new ReadOnlyCollection<PlatformExecutionEvent>((events ?? []).ToList());
    }

    public PlatformExecutionOutcome Outcome { get; }
    public int? ExitCode { get; }
    public string? SessionId { get; }
    public string? Message { get; }
    public bool MayHavePartialChanges { get; }
    public PlatformFailureKind FailureKind { get; }
    public IReadOnlyList<PlatformExecutionEvent> Events { get; }
    public bool Succeeded => Outcome == PlatformExecutionOutcome.Succeeded;
}
