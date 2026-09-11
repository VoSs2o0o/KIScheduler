using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;

namespace KIScheduler.Core.Scheduling;

public interface ISchedulerEngine : IAsyncDisposable
{
    bool IsPaused { get; }
    int RunningCount { get; }
    void Pause();
    void Resume();
    Task<bool> CancelAsync(WorkItemId workItemId, CancellationToken cancellationToken = default);
    Task<int> RunCycleAsync(CancellationToken cancellationToken = default);
    Task WaitForIdleAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Coordinates eligibility and starts work. Each cycle returns after dispatching so that a free
/// platform can receive more work on a later cycle while another platform is still running.
/// </summary>
public sealed class SchedulerEngine : ISchedulerEngine
{
    private static readonly WorkItemStatus[] DueStatuses =
        [WorkItemStatus.InWarteschlange, WorkItemStatus.WartetAufUsage];

    private readonly IWorkItemRepository workItems;
    private readonly IProjectRepository projects;
    private readonly IPlatformRepository platformDefinitions;
    private readonly IUsagePolicyRepository usagePolicies;
    private readonly IUsageSnapshotRepository usageSnapshots;
    private readonly IExecutionHistoryRepository history;
    private readonly IExecutionBlockRepository blocks;
    private readonly IAtomicExecutionRepository atomicExecution;
    private readonly IAiPlatformRegistry platforms;
    private readonly IUsageProviderRegistry usageProviders;
    private readonly IPlatformConfigurationValidator configurationValidator;
    private readonly IFileSystem fileSystem;
    private readonly IGitService git;
    private readonly IClock clock;
    private readonly SchedulerOptions options;
    private readonly UsagePolicyEvaluator usageEvaluator;
    private readonly SchedulerPriorityCalculator priorityCalculator;
    private readonly string ownerId;
    private readonly SemaphoreSlim cycleGate = new(1, 1);
    private readonly object runningGate = new();
    private readonly Dictionary<string, Task> runningByPlatform = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<WorkItemId, CancellationTokenSource> cancellationByWorkItem = [];
    private readonly HashSet<ProjectId> runningProjects = [];
    private readonly CancellationTokenSource shutdown = new();
    private volatile bool paused;
    private volatile bool stopped;

    public SchedulerEngine(IWorkItemRepository workItems, IProjectRepository projects,
        IPlatformRepository platformDefinitions, IUsagePolicyRepository usagePolicies,
        IUsageSnapshotRepository usageSnapshots, IExecutionHistoryRepository history,
        IExecutionBlockRepository blocks, IAtomicExecutionRepository atomicExecution,
        IAiPlatformRegistry platforms, IUsageProviderRegistry usageProviders,
        IPlatformConfigurationValidator configurationValidator, IFileSystem fileSystem,
        IGitService git, IClock clock, SchedulerOptions options, UsagePolicyEvaluator? usageEvaluator = null,
        SchedulerPriorityCalculator? priorityCalculator = null, string? ownerId = null)
    {
        this.workItems = workItems ?? throw new ArgumentNullException(nameof(workItems));
        this.projects = projects ?? throw new ArgumentNullException(nameof(projects));
        this.platformDefinitions = platformDefinitions ?? throw new ArgumentNullException(nameof(platformDefinitions));
        this.usagePolicies = usagePolicies ?? throw new ArgumentNullException(nameof(usagePolicies));
        this.usageSnapshots = usageSnapshots ?? throw new ArgumentNullException(nameof(usageSnapshots));
        this.history = history ?? throw new ArgumentNullException(nameof(history));
        this.blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
        this.atomicExecution = atomicExecution ?? throw new ArgumentNullException(nameof(atomicExecution));
        this.platforms = platforms ?? throw new ArgumentNullException(nameof(platforms));
        this.usageProviders = usageProviders ?? throw new ArgumentNullException(nameof(usageProviders));
        this.configurationValidator = configurationValidator ?? throw new ArgumentNullException(nameof(configurationValidator));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.git = git ?? throw new ArgumentNullException(nameof(git));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        options.Validate();
        this.usageEvaluator = usageEvaluator ?? new UsagePolicyEvaluator();
        this.priorityCalculator = priorityCalculator ?? new SchedulerPriorityCalculator();
        this.ownerId = string.IsNullOrWhiteSpace(ownerId)
            ? $"scheduler-{Environment.ProcessId}-{Guid.NewGuid():N}"
            : ownerId.Trim();
    }

    public bool IsPaused => paused;
    public int RunningCount { get { lock (runningGate) return runningByPlatform.Count; } }

    public void Pause() => paused = true;

    public void Resume()
    {
        ObjectDisposedException.ThrowIf(stopped, this);
        paused = false;
    }

    public async Task<bool> CancelAsync(WorkItemId workItemId, CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? runningCancellation;
        lock (runningGate) cancellationByWorkItem.TryGetValue(workItemId, out runningCancellation);
        if (runningCancellation is not null)
        {
            await runningCancellation.CancelAsync().ConfigureAwait(false);
            return true;
        }

        var item = await workItems.GetAsync(workItemId, cancellationToken).ConfigureAwait(false);
        if (item is null || !WorkItemStateMachine.CanTransition(item.Status, WorkItemStatus.Abgebrochen)) return false;
        item.TransitionTo(WorkItemStatus.Abgebrochen);
        await workItems.SaveAsync(item, cancellationToken).ConfigureAwait(false);
        await history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), item.Id, clock.UtcNow,
            ExecutionEventSeverity.Information, "execution.cancelled", "Auftrag wurde manuell abgebrochen.", data:
            new Dictionary<string, string> { ["reasonCode"] = "execution.cancelled_by_user" }), cancellationToken)
            .ConfigureAwait(false);
        await ReleaseProjectHoldsAsync(item, clock.UtcNow).ConfigureAwait(false);
        return true;
    }

    public async Task<int> RunCycleAsync(CancellationToken cancellationToken = default)
    {
        if (paused || stopped) return 0;
        await cycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (paused || stopped) return 0;
            var now = clock.UtcNow;
            var candidates = await workItems.ListByStatusAsync(DueStatuses, cancellationToken).ConfigureAwait(false);
            if (candidates.Count == 0) return 0;

            var allPolicies = await usagePolicies.ListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var activePlatformBlocks = (await blocks.ListActivePlatformBlocksAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var activeProjectHolds = await blocks.ListActiveProjectHoldsAsync(cancellationToken).ConfigureAwait(false);
            var usageByPlatform = await ReadFreshUsageAsync(candidates, cancellationToken).ConfigureAwait(false);
            await ReleaseEligiblePlatformBlocksAsync(activePlatformBlocks, allPolicies, usageByPlatform, now, cancellationToken)
                .ConfigureAwait(false);
            activePlatformBlocks = activePlatformBlocks.Where(block => block.IsActive).ToList();

            var resumeIds = activeProjectHolds.Select(hold => hold.TriggeringWorkItemId).ToHashSet();
            var ordered = candidates
                .OrderByDescending(item => resumeIds.Contains(item.Id))
                .ThenByDescending(item => priorityCalculator.Calculate(item.Priority.Value, item.CreatedAtUtc, now,
                    options.AgingInterval, options.AgingBonusPerInterval))
                .ThenBy(item => item.CreatedAtUtc)
                .ThenBy(item => item.Id.Value)
                .ToList();

            var dispatched = 0;
            foreach (var item in ordered)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (paused || stopped) break;
                if (IsPlatformRunning(item.PlatformId))
                {
                    await RecordDecisionAsync(item, SchedulerReasonCodes.PlatformBusy, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                if (item.ProjectId is { } runningProject && IsProjectRunning(runningProject))
                {
                    await RecordDecisionAsync(item, SchedulerReasonCodes.ProjectBusy, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var conflictingHold = item.ProjectId is { } projectId
                    ? activeProjectHolds.FirstOrDefault(hold => hold.ProjectId == projectId
                        && hold.TriggeringWorkItemId != item.Id)
                    : null;
                if (conflictingHold is not null)
                {
                    await RecordDecisionAsync(item, SchedulerReasonCodes.ProjectHeld, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (activePlatformBlocks.Any(block => PlatformEquals(block.PlatformId, item.PlatformId)))
                {
                    await MoveToWaitingForUsageAsync(item, SchedulerReasonCodes.PlatformBlocked, cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                usageByPlatform.TryGetValue(item.PlatformId.Value, out var usage);
                var usageDecision = usageEvaluator.Evaluate(item.PlatformId, item.ModelId, allPolicies,
                    usage?.Snapshot, now);
                if (!usageDecision.IsAllowed)
                {
                    if (usageDecision.HasApplicablePolicy)
                        await MoveToWaitingForUsageAsync(item, usageDecision.ReasonCode, cancellationToken).ConfigureAwait(false);
                    else
                        await RecordDecisionAsync(item, usageDecision.ReasonCode, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (item.ProjectId is null)
                {
                    item.TransitionTo(WorkItemStatus.ProjektFehlt);
                    await workItems.SaveAsync(item, cancellationToken).ConfigureAwait(false);
                    await RecordDecisionAsync(item, "project.missing", cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var resume = await GetResumeSessionAsync(item, activeProjectHolds, cancellationToken).ConfigureAwait(false);
                if (resume.IsRequired && !resume.IsAvailable)
                {
                    item.TransitionTo(WorkItemStatus.MenschlichePruefung);
                    await workItems.SaveAsync(item, cancellationToken).ConfigureAwait(false);
                    await RecordDecisionAsync(item, SchedulerReasonCodes.ResumeUnavailable, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var lease = await workItems.TryAcquireLeaseAsync(item.Id, ownerId, now,
                    options.LeaseDuration, cancellationToken).ConfigureAwait(false);
                if (lease is null)
                {
                    await RecordDecisionAsync(item, SchedulerReasonCodes.ReservationConflict, cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                item.TransitionTo(WorkItemStatus.Reserviert);
                var itemCancellation = ReserveRunning(item);
                var execution = ExecuteReservedAsync(item, lease, resume.SessionId, itemCancellation.Token);
                TrackExecution(item, execution, itemCancellation);
                dispatched++;
            }

            return dispatched;
        }
        finally
        {
            cycleGate.Release();
        }
    }

    public async Task WaitForIdleAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            Task[] tasks;
            lock (runningGate) tasks = runningByPlatform.Values.ToArray();
            if (tasks.Length == 0) return;
            await Task.WhenAll(tasks).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!stopped)
        {
            stopped = true;
            paused = true;
            await shutdown.CancelAsync().ConfigureAwait(false);
        }
        await WaitForIdleAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (!stopped) await StopAsync().ConfigureAwait(false);
        shutdown.Dispose();
        cycleGate.Dispose();
    }

    private async Task<Dictionary<string, UsageReadResult>> ReadFreshUsageAsync(
        IReadOnlyCollection<WorkItem> candidates, CancellationToken cancellationToken)
    {
        var result = new ConcurrentDictionary<string, UsageReadResult>(StringComparer.OrdinalIgnoreCase);
        var ids = candidates.Select(item => item.PlatformId).DistinctBy(id => id.Value, StringComparer.OrdinalIgnoreCase);
        await Task.WhenAll(ids.Select(async id =>
        {
            var provider = usageProviders.GetRequired(id);
            UsageReadResult read;
            try
            {
                read = await provider.ReadAsync(forceRefresh: true, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                read = new UsageReadResult(UsageReadStatus.ProviderUnavailable, message: exception.Message);
            }
            if (read.Snapshot is { } snapshot)
                await usageSnapshots.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
            result[id.Value] = read;
        })).ConfigureAwait(false);
        return result.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private async Task ReleaseEligiblePlatformBlocksAsync(List<PlatformUsageBlock> activeBlocks,
        IReadOnlyCollection<UsagePolicy> policies, IReadOnlyDictionary<string, UsageReadResult> usageByPlatform,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var block in activeBlocks)
        {
            if (!usageByPlatform.TryGetValue(block.PlatformId.Value, out var read) || read.Snapshot is null) continue;
            var trigger = await workItems.GetAsync(block.TriggeringWorkItemId, cancellationToken).ConfigureAwait(false);
            if (trigger is null) continue;
            var decision = usageEvaluator.Evaluate(trigger.PlatformId, trigger.ModelId, policies, read.Snapshot, now);
            if (!decision.IsAllowed || !decision.IsFreshSnapshot) continue;
            block.Release(now, "Frischer zulässiger Usage-Snapshot.", isManual: false,
                hasFreshPermissibleSnapshot: true);
            await blocks.SaveAsync(block, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<(bool IsRequired, bool IsAvailable, string? SessionId)> GetResumeSessionAsync(
        WorkItem item, IReadOnlyCollection<ProjectExecutionHold> activeProjectHolds,
        CancellationToken cancellationToken)
    {
        var requiresResume = activeProjectHolds.Any(hold => hold.TriggeringWorkItemId == item.Id);
        if (!requiresResume) return (false, true, null);
        var adapter = platforms.GetRequired(item.PlatformId);
        var attempts = await history.ListAttemptsAsync(item.Id, cancellationToken).ConfigureAwait(false);
        var interrupted = attempts.LastOrDefault(attempt => attempt.Result == ExecutionAttemptResult.UsageExceeded);
        var available = adapter.Capabilities.SupportsResume && !string.IsNullOrWhiteSpace(interrupted?.SessionId);
        return (true, available, available ? interrupted!.SessionId : null);
    }

    private async Task ExecuteReservedAsync(WorkItem item, SchedulerLease lease, string? resumeSessionId,
        CancellationToken itemCancellationToken)
    {
        var startedAt = clock.UtcNow;
        ExecutionAttempt? attempt = null;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token, itemCancellationToken);
            linked.CancelAfter(options.ExecutionTimeout);
            var cancellationToken = linked.Token;

            var project = await projects.GetAsync(item.ProjectId!.Value, cancellationToken).ConfigureAwait(false);
            if (project is null)
            {
                item.TransitionTo(WorkItemStatus.ProjektFehlt);
                await workItems.SaveAsync(item, CancellationToken.None).ConfigureAwait(false);
                await RecordDecisionAsync(item, "project.not_found", CancellationToken.None).ConfigureAwait(false);
                return;
            }

            var promptPath = Path.IsPathRooted(item.PromptPath.Value)
                ? Path.GetFullPath(item.PromptPath.Value)
                : Path.GetFullPath(item.PromptPath.Value, project.RootPath);
            if (!fileSystem.FileExists(promptPath))
            {
                item.TransitionTo(WorkItemStatus.ProjektFehlt);
                await workItems.SaveAsync(item, CancellationToken.None).ConfigureAwait(false);
                await RecordDecisionAsync(item, "prompt.not_found", CancellationToken.None).ConfigureAwait(false);
                return;
            }

            GitInspectionResult gitBefore = await git.InspectAsync(new GitInspectionRequest(
                project.RootPath, project.TargetBranch, RequireCleanWorkingTree: item.AutoCommit), cancellationToken)
                .ConfigureAwait(false);
            await RecordGitStatusAsync(item, "git.before", gitBefore, CancellationToken.None).ConfigureAwait(false);
            if (item.AutoCommit && !gitBefore.IsReady)
            {
                item.TransitionTo(WorkItemStatus.MenschlichePruefung);
                await workItems.SaveAsync(item, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            var definition = await platformDefinitions.GetAsync(item.PlatformId, cancellationToken).ConfigureAwait(false)
                ?? throw new PlatformConfigurationException($"Plattform '{item.PlatformId.Value}' ist nicht konfiguriert.");
            var adapter = platforms.GetRequired(item.PlatformId);
            var prompt = await fileSystem.ReadAllTextAsync(promptPath, cancellationToken).ConfigureAwait(false);
            var request = new PlatformExecutionRequest(item.PlatformId, item.ModelId, item.Effort, prompt, project.RootPath)
            {
                SessionId = resumeSessionId,
                Timeout = options.ExecutionTimeout
            };
            configurationValidator.ValidateExecution(definition, request);

            item.TransitionTo(WorkItemStatus.InBearbeitung);
            item.MarkAttemptStarted(startedAt);
            await workItems.SaveAsync(item, CancellationToken.None).ConfigureAwait(false);
            await RecordDecisionAsync(item, SchedulerReasonCodes.Reserved, CancellationToken.None).ConfigureAwait(false);

            PlatformExecutionResult result;
            try
            {
                result = await adapter.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
            {
                result = new PlatformExecutionResult(PlatformExecutionOutcome.Cancelled,
                    message: "Ausführung wegen kontrolliertem Herunterfahren unterbrochen.", mayHavePartialChanges: true);
            }
            catch (OperationCanceledException) when (itemCancellationToken.IsCancellationRequested)
            {
                result = new PlatformExecutionResult(PlatformExecutionOutcome.Cancelled,
                    message: "Ausführung wurde manuell abgebrochen.", mayHavePartialChanges: true);
            }
            catch (OperationCanceledException)
            {
                result = new PlatformExecutionResult(PlatformExecutionOutcome.TimedOut,
                    message: "Zeitlimit der Ausführung überschritten.", mayHavePartialChanges: true);
            }
            catch (Exception exception)
            {
                result = new PlatformExecutionResult(PlatformExecutionOutcome.HumanReviewRequired,
                    message: exception.Message, mayHavePartialChanges: true);
            }

            foreach (var platformEvent in result.Events)
            {
                await history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), item.Id, clock.UtcNow,
                    ExecutionEventSeverity.Information, $"platform.{platformEvent.Type}",
                    RedactSensitiveOutput(platformEvent.Json), data: new Dictionary<string, string>
                    {
                        ["reasonCode"] = "platform.output",
                        ["stream"] = "structured"
                    }), CancellationToken.None).ConfigureAwait(false);
            }

            if (!item.AutoCommit || result.Outcome != PlatformExecutionOutcome.Succeeded)
            {
                GitInspectionResult gitAfter = await git.InspectAsync(new GitInspectionRequest(
                    project.RootPath, project.TargetBranch, RequireCleanWorkingTree: false), CancellationToken.None)
                    .ConfigureAwait(false);
                await RecordGitStatusAsync(item, "git.after", gitAfter, CancellationToken.None).ConfigureAwait(false);
            }

            var completedAt = clock.UtcNow;
            var attemptResult = MapResult(result, shutdown.IsCancellationRequested);
            string? diagnostic = result.Message;
            GitCommitResult? commitResult = null;
            if (attemptResult == ExecutionAttemptResult.TechnischErfolgreich && item.AutoCommit)
            {
                commitResult = await git.CommitAllAsync(new GitCommitRequest(
                    project.RootPath, project.TargetBranch, item.ResolveCommitMessage()), CancellationToken.None)
                    .ConfigureAwait(false);
                await RecordGitCommitAsync(item, commitResult, CancellationToken.None).ConfigureAwait(false);
                diagnostic = commitResult.Message;
                attemptResult = commitResult.Status switch
                {
                    GitCommitStatus.Committed => ExecutionAttemptResult.TechnischErfolgreich,
                    GitCommitStatus.NoChanges => ExecutionAttemptResult.ErfolgreichMitWarnung,
                    _ => ExecutionAttemptResult.MenschlichePruefung
                };
            }
            var sequence = (await history.ListAttemptsAsync(item.Id, CancellationToken.None).ConfigureAwait(false)).Count + 1;
            attempt = new ExecutionAttempt(ExecutionAttemptId.New(), item.Id, sequence, item.PlatformId,
                item.ModelId, item.Effort, startedAt, completedAt, attemptResult, result.ExitCode,
                result.SessionId ?? resumeSessionId, diagnostic);
            item.CompleteCurrentAttempt(attemptResult);

            if (attemptResult == ExecutionAttemptResult.UsageExceeded)
            {
                await PersistUsageExceededAsync(item, attempt, result, completedAt).ConfigureAwait(false);
                return;
            }

            await history.AddAttemptAsync(attempt, CancellationToken.None).ConfigureAwait(false);
            await workItems.SaveAsync(item, CancellationToken.None).ConfigureAwait(false);
            await history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), item.Id, completedAt,
                attemptResult is ExecutionAttemptResult.TechnischErfolgreich or ExecutionAttemptResult.ErfolgreichMitWarnung
                    ? ExecutionEventSeverity.Information : ExecutionEventSeverity.Error,
                "execution.completed", diagnostic ?? attemptResult.ToString(), attempt.Id,
                new Dictionary<string, string>
                {
                    ["reasonCode"] = $"execution.{attemptResult.ToString().ToLowerInvariant()}",
                    ["outcome"] = result.Outcome.ToString()
                }), CancellationToken.None).ConfigureAwait(false);

            if (item.Status is WorkItemStatus.TechnischErfolgreich or WorkItemStatus.ErfolgreichMitWarnung
                or WorkItemStatus.Abgebrochen)
                await ReleaseProjectHoldsAsync(item, completedAt).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (itemCancellationToken.IsCancellationRequested)
        {
            if (item.Status == WorkItemStatus.Reserviert) item.TransitionTo(WorkItemStatus.Abgebrochen);
            else if (item.Status == WorkItemStatus.InBearbeitung)
                item.CompleteCurrentAttempt(ExecutionAttemptResult.Abgebrochen);
            await workItems.SaveAsync(item, CancellationToken.None).ConfigureAwait(false);
            await history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), item.Id, clock.UtcNow,
                ExecutionEventSeverity.Information, "execution.cancelled", "Auftrag wurde manuell abgebrochen.", data:
                new Dictionary<string, string> { ["reasonCode"] = "execution.cancelled_by_user" }),
                CancellationToken.None).ConfigureAwait(false);
            await ReleaseProjectHoldsAsync(item, clock.UtcNow).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            if (item.Status == WorkItemStatus.Reserviert) item.TransitionTo(WorkItemStatus.Unterbrochen);
            else if (item.Status == WorkItemStatus.InBearbeitung)
                item.CompleteCurrentAttempt(ExecutionAttemptResult.Unterbrochen);
            await workItems.SaveAsync(item, CancellationToken.None).ConfigureAwait(false);
            await history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), item.Id, clock.UtcNow,
                ExecutionEventSeverity.Warning, "execution.interrupted",
                "Auftrag wurde beim kontrollierten Herunterfahren unterbrochen.", data:
                new Dictionary<string, string> { ["reasonCode"] = "execution.shutdown" }),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (item.Status == WorkItemStatus.Reserviert)
            {
                item.TransitionTo(WorkItemStatus.MenschlichePruefung);
                await workItems.SaveAsync(item, CancellationToken.None).ConfigureAwait(false);
            }
            else if (item.Status == WorkItemStatus.InBearbeitung)
            {
                item.CompleteCurrentAttempt(ExecutionAttemptResult.MenschlichePruefung);
                await workItems.SaveAsync(item, CancellationToken.None).ConfigureAwait(false);
            }
            await history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), item.Id, clock.UtcNow,
                ExecutionEventSeverity.Error, "execution.preparation_failed", exception.Message, attempt?.Id,
                new Dictionary<string, string> { ["reasonCode"] = "execution.preparation_failed" }),
                CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            await workItems.ReleaseLeaseAsync(lease.Id, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task PersistUsageExceededAsync(WorkItem item, ExecutionAttempt attempt,
        PlatformExecutionResult result, DateTimeOffset now)
    {
        var message = result.Message ?? "Usage-Limit während der Ausführung erreicht.";
        var executionEvent = new ExecutionEvent(Guid.NewGuid(), item.Id, now, ExecutionEventSeverity.Error,
            "usage_exceeded", message, attempt.Id, new Dictionary<string, string>
            {
                ["reasonCode"] = SchedulerReasonCodes.ServerLimitReached,
                ["platformId"] = item.PlatformId.Value,
                ["autoCommit"] = "skipped"
            });
        var platformBlock = new PlatformUsageBlock(Guid.NewGuid(), item.PlatformId, item.Id, attempt.Id, message, now);
        var projectHold = new ProjectExecutionHold(Guid.NewGuid(), item.ProjectId!.Value, item.Id,
            item.PlatformId, "Auftrag kann den Projektarbeitsbaum teilweise verändert haben.", now);
        await atomicExecution.PersistUsageExceededAsync(
            new UsageExceededPersistenceRequest(item, attempt, executionEvent, platformBlock, projectHold),
            CancellationToken.None).ConfigureAwait(false);
        await usageSnapshots.InvalidateAsync(item.PlatformId, CancellationToken.None).ConfigureAwait(false);
        try
        {
            var fresh = await usageProviders.GetRequired(item.PlatformId).ReadAsync(forceRefresh: true, CancellationToken.None)
                .ConfigureAwait(false);
            if (fresh.Snapshot is not null)
                await usageSnapshots.SaveAsync(fresh.Snapshot, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // The persisted block remains active. A later cycle will retry the provider.
        }
    }

    private async Task ReleaseProjectHoldsAsync(WorkItem item, DateTimeOffset now)
    {
        var holds = await blocks.ListActiveProjectHoldsAsync(CancellationToken.None).ConfigureAwait(false);
        foreach (var hold in holds.Where(hold => hold.TriggeringWorkItemId == item.Id))
        {
            hold.Release(now, "Auslösender Auftrag abgeschlossen.", isManual: false, item.Status);
            await blocks.SaveAsync(hold, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task MoveToWaitingForUsageAsync(WorkItem item, string reasonCode,
        CancellationToken cancellationToken)
    {
        if (item.Status == WorkItemStatus.InWarteschlange)
        {
            item.TransitionTo(WorkItemStatus.WartetAufUsage);
            await workItems.SaveAsync(item, cancellationToken).ConfigureAwait(false);
        }
        await RecordDecisionAsync(item, reasonCode, cancellationToken).ConfigureAwait(false);
    }

    private Task RecordDecisionAsync(WorkItem item, string reasonCode, CancellationToken cancellationToken) =>
        history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), item.Id, clock.UtcNow,
            ExecutionEventSeverity.Information, "scheduler.decision", reasonCode, data:
            new Dictionary<string, string> { ["reasonCode"] = reasonCode }), cancellationToken);

    private Task RecordGitStatusAsync(WorkItem item, string eventType, GitInspectionResult result,
        CancellationToken cancellationToken)
    {
        var data = new Dictionary<string, string>
        {
            ["reasonCode"] = $"git.{result.Status.ToString().ToLowerInvariant()}",
            ["status"] = result.Status.ToString(),
            ["autoCommit"] = item.AutoCommit.ToString()
        };
        if (result.Snapshot is { } snapshot)
        {
            data["repositoryRoot"] = snapshot.RepositoryRoot;
            data["branch"] = snapshot.CurrentBranch ?? "";
            data["isClean"] = snapshot.IsClean.ToString();
            data["changeCount"] = snapshot.StatusLines.Count.ToString();
        }
        return history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), item.Id, clock.UtcNow,
            result.IsReady ? ExecutionEventSeverity.Information :
                item.AutoCommit ? ExecutionEventSeverity.Error : ExecutionEventSeverity.Warning,
            eventType, result.Message, data: data), cancellationToken);
    }

    private Task RecordGitCommitAsync(WorkItem item, GitCommitResult result,
        CancellationToken cancellationToken)
    {
        var data = new Dictionary<string, string>
        {
            ["reasonCode"] = $"git.{result.Status.ToString().ToLowerInvariant()}",
            ["status"] = result.Status.ToString(),
            ["beforeChangeCount"] = result.Before.StatusLines.Count.ToString(),
            ["afterChangeCount"] = result.After?.StatusLines.Count.ToString() ?? "unknown"
        };
        if (result.CommitId is not null) data["commitId"] = result.CommitId;
        return history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), item.Id, clock.UtcNow,
            result.Status switch
            {
                GitCommitStatus.Committed => ExecutionEventSeverity.Information,
                GitCommitStatus.NoChanges => ExecutionEventSeverity.Warning,
                _ => ExecutionEventSeverity.Error
            }, "git.after", result.Message, data: data), cancellationToken);
    }

    private CancellationTokenSource ReserveRunning(WorkItem item)
    {
        lock (runningGate)
        {
            if (runningByPlatform.ContainsKey(item.PlatformId.Value)
                || (item.ProjectId is { } projectId && runningProjects.Contains(projectId)))
                throw new InvalidOperationException("Die lokale Scheduler-Kapazität wurde gleichzeitig belegt.");
            if (item.ProjectId is { } id) runningProjects.Add(id);
            var cancellation = new CancellationTokenSource();
            cancellationByWorkItem[item.Id] = cancellation;
            return cancellation;
        }
    }

    private void TrackExecution(WorkItem item, Task execution, CancellationTokenSource itemCancellation)
    {
        lock (runningGate) runningByPlatform[item.PlatformId.Value] = execution;
        _ = execution.ContinueWith(_ =>
        {
            lock (runningGate)
            {
                runningByPlatform.Remove(item.PlatformId.Value);
                cancellationByWorkItem.Remove(item.Id);
                if (item.ProjectId is { } projectId) runningProjects.Remove(projectId);
            }
            itemCancellation.Dispose();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private bool IsPlatformRunning(PlatformId id)
    {
        lock (runningGate) return runningByPlatform.ContainsKey(id.Value);
    }

    private bool IsProjectRunning(ProjectId id)
    {
        lock (runningGate) return runningProjects.Contains(id);
    }

    private static ExecutionAttemptResult MapResult(PlatformExecutionResult result, bool shuttingDown) => result.Outcome switch
    {
        PlatformExecutionOutcome.Succeeded => ExecutionAttemptResult.TechnischErfolgreich,
        PlatformExecutionOutcome.Failed => ExecutionAttemptResult.Fehlgeschlagen,
        PlatformExecutionOutcome.Cancelled when shuttingDown => ExecutionAttemptResult.Unterbrochen,
        PlatformExecutionOutcome.Cancelled => ExecutionAttemptResult.Abgebrochen,
        PlatformExecutionOutcome.TimedOut => ExecutionAttemptResult.MenschlichePruefung,
        PlatformExecutionOutcome.HumanReviewRequired => ExecutionAttemptResult.MenschlichePruefung,
        PlatformExecutionOutcome.UsageExceeded => ExecutionAttemptResult.UsageExceeded,
        _ => throw new ArgumentOutOfRangeException(nameof(result))
    };

    private static bool PlatformEquals(PlatformId left, PlatformId right) =>
        string.Equals(left.Value, right.Value, StringComparison.OrdinalIgnoreCase);

    private static string RedactSensitiveOutput(string value)
    {
        value = Regex.Replace(value,
            "(?i)(\\\"?(?:authorization|access_token|api_key|secret|token)\\\"?\\s*[:=]\\s*\\\")[^\\\"]*(\\\")",
            "$1<redacted>$2", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return Regex.Replace(value, "(?i)Bearer\\s+[A-Za-z0-9._~+/-]+=*", "Bearer <redacted>",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }
}
