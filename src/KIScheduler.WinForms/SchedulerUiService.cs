using System.Text.RegularExpressions;
using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using KIScheduler.Core.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace KIScheduler.WinForms;

public sealed record WorkItemEditModel(string Title, int Priority, string PlatformId, string ModelId,
    string Effort, string PromptPath, bool AutoCommit, string? CommitMessage, string? ConfirmedProjectRoot = null);

public sealed record QueueRow(WorkItem Item, string Project, string Usage, string Status, string Reason);
public sealed record PlatformRow(PlatformDefinition Definition, PlatformHealth Health, UsageSnapshot? Usage,
    string EffectiveLimits, string? UsageMessage);
public sealed record DashboardData(IReadOnlyList<QueueRow> Queue, IReadOnlyList<PlatformRow> Platforms,
    IReadOnlyList<PlatformUsageBlock> PlatformBlocks, IReadOnlyList<ProjectExecutionHold> ProjectHolds,
    IReadOnlyList<UsagePolicy> Policies, IReadOnlyDictionary<ProjectId, ProjectDefinition> Projects);

public sealed class ProjectRootRequiredException(ProjectRootResolution resolution) : InvalidOperationException(resolution.Message)
{
    public ProjectRootResolution Resolution { get; } = resolution;
}

public sealed class SchedulerUiService(
    IWorkItemRepository workItems,
    IProjectRepository projects,
    IPlatformRepository platforms,
    IUsagePolicyRepository policies,
    IUsageSnapshotRepository snapshots,
    IExecutionHistoryRepository history,
    IExecutionBlockRepository blocks,
    IProjectRootResolver rootResolver,
    IProjectCreationService projectCreation,
    IAiPlatformRegistry platformRegistry,
    IUsageProviderRegistry usageRegistry,
    UsagePolicyEvaluator usageEvaluator,
    IClock clock,
    ISchedulerEngine scheduler,
    ISettingsRepository settings)
{
    private readonly Dictionary<string, (DateTimeOffset At, PlatformHealth Health)> healthCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> usageMessages = new(StringComparer.OrdinalIgnoreCase);

    public async Task<DashboardData> LoadAsync(bool refreshProviders = false,
        CancellationToken cancellationToken = default)
    {
        var allStatuses = Enum.GetValues<WorkItemStatus>();
        var itemListTask = workItems.ListByStatusAsync(allStatuses, cancellationToken);
        var projectListTask = projects.ListAsync(cancellationToken);
        var platformListTask = platforms.ListAsync(cancellationToken);
        var policyListTask = policies.ListAsync(cancellationToken: cancellationToken);
        var platformBlocksTask = blocks.ListActivePlatformBlocksAsync(cancellationToken);
        var projectHoldsTask = blocks.ListActiveProjectHoldsAsync(cancellationToken);
        await Task.WhenAll(itemListTask, projectListTask, platformListTask, policyListTask,
            platformBlocksTask, projectHoldsTask).ConfigureAwait(false);

        var projectMap = projectListTask.Result.ToDictionary(x => x.Id);
        var holds = projectHoldsTask.Result;
        var platformRows = await Task.WhenAll(platformListTask.Result.Select(async definition =>
        {
            var health = await GetHealthAsync(definition, refreshProviders, cancellationToken).ConfigureAwait(false);
            UsageSnapshot? snapshot = null;
            string? usageMessage = usageMessages.GetValueOrDefault(definition.Id.Value);
            if (refreshProviders && usageRegistry.TryGet(definition.Id, out var provider) && provider is not null)
            {
                try
                {
                    var read = await provider.ReadAsync(true, cancellationToken).ConfigureAwait(false);
                    usageMessage = read.Message;
                    usageMessages[definition.Id.Value] = usageMessage;
                    if (read.Snapshot is not null)
                    {
                        snapshot = read.Snapshot;
                        await snapshots.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    usageMessage = exception.Message;
                    usageMessages[definition.Id.Value] = usageMessage;
                }
            }
            snapshot ??= await snapshots.GetLatestAsync(definition.Id, cancellationToken).ConfigureAwait(false);
            return new PlatformRow(definition, health, snapshot,
                DescribeLimits(definition, policyListTask.Result, snapshot), usageMessage);
        })).ConfigureAwait(false);

        var queue = itemListTask.Result.Select(item =>
        {
            var held = item.ProjectId is { } id && holds.Any(x => x.ProjectId == id);
            var project = item.ProjectId is { } projectId && projectMap.TryGetValue(projectId, out var value)
                ? value.Name : "—";
            var platform = platformRows.FirstOrDefault(x => PlatformEquals(x.Definition.Id, item.PlatformId));
            var usage = platform?.Usage is null ? "unbekannt" : string.Join(", ",
                platform.Usage.Windows.Select(x => $"{x.Name}: {x.UsedPercent}"));
            var reason = LatestBlockingReason(item, held, platformBlocksTask.Result, platform?.Usage);
            return new QueueRow(item, project, usage, item.GetDisplayStatus(held).ToString(), reason);
        }).ToList();
        return new DashboardData(queue, platformRows, platformBlocksTask.Result, holds,
            policyListTask.Result, projectMap);
    }

    public async Task<WorkItem> SaveWorkItemAsync(WorkItemEditModel model, WorkItem? existing = null,
        CancellationToken cancellationToken = default)
    {
        ValidateWorkItem(model);
        var definition = await platforms.GetAsync(new PlatformId(model.PlatformId), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Plattform '{model.PlatformId}' ist nicht konfiguriert.");
        var platformId = definition.Id;
        var modelId = new ModelId(model.ModelId);
        var effort = new EffortLevel(model.Effort);
        if (!definition.Supports(modelId, effort))
            throw new InvalidOperationException("Das gewählte Modell unterstützt die Effort-Stufe nicht.");

        var resolution = rootResolver.Resolve(model.PromptPath);
        string root;
        if (resolution.IsResolved) root = resolution.ProjectRoot!;
        else if (!string.IsNullOrWhiteSpace(model.ConfirmedProjectRoot))
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(model.ConfirmedProjectRoot));
        else throw new ProjectRootRequiredException(resolution);

        var knownProjects = await projects.ListAsync(cancellationToken).ConfigureAwait(false);
        var project = knownProjects.FirstOrDefault(x => PathEquals(x.RootPath, root));
        if (project is null)
        {
            project = new ProjectDefinition(ProjectId.New(), new DirectoryInfo(root).Name, root);
            await projects.SaveAsync(project, cancellationToken).ConfigureAwait(false);
        }
        var storedPrompt = Path.GetRelativePath(project.RootPath, Path.GetFullPath(model.PromptPath));
        var promptPath = new PromptPath(storedPrompt);

        WorkItem item;
        if (existing is null)
        {
            item = new WorkItem(WorkItemId.New(), model.Title, new WorkItemPriority(model.Priority),
                platformId, modelId, effort, promptPath, model.AutoCommit, clock.UtcNow, project.Id,
                model.CommitMessage);
            item.TransitionTo(WorkItemStatus.InWarteschlange);
        }
        else
        {
            existing.ChangeTitle(model.Title);
            existing.ChangePlanning(new WorkItemPriority(model.Priority), promptPath, model.AutoCommit, project.Id);
            existing.ChangeCommitMessage(model.CommitMessage);
            if (!existing.HasExecutionStarted || existing.PlatformId != platformId
                || existing.ModelId != modelId || existing.Effort != effort)
                existing.ChangeExecutionConfiguration(platformId, modelId, effort);
            item = existing;
        }
        await workItems.SaveAsync(item, cancellationToken).ConfigureAwait(false);
        return item;
    }

    public async Task SetPausedAsync(WorkItem item, bool paused, CancellationToken cancellationToken = default)
    {
        var target = paused ? WorkItemStatus.Pausiert : WorkItemStatus.InWarteschlange;
        if (!WorkItemStateMachine.CanTransition(item.Status, target))
            throw new InvalidOperationException($"'{item.Status}' kann nicht in '{target}' geändert werden.");
        item.TransitionTo(target);
        await workItems.SaveAsync(item, cancellationToken).ConfigureAwait(false);
    }

    public async Task RequeueAsync(WorkItem item, CancellationToken cancellationToken = default)
    {
        if (WorkItemStateMachine.CanTransition(item.Status, WorkItemStatus.InWarteschlange))
        {
            item.TransitionTo(WorkItemStatus.InWarteschlange);
            await workItems.SaveAsync(item, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (item.Status is not (WorkItemStatus.Fehlgeschlagen or WorkItemStatus.TechnischErfolgreich
            or WorkItemStatus.ErfolgreichMitWarnung or WorkItemStatus.Abgebrochen))
            throw new InvalidOperationException($"'{item.Status}' kann nicht erneut eingereiht werden.");
        var replacement = new WorkItem(WorkItemId.New(), item.Title, item.Priority, item.PlatformId,
            item.ModelId, item.Effort, item.PromptPath, item.AutoCommit, clock.UtcNow, item.ProjectId,
            item.CommitMessage);
        replacement.TransitionTo(WorkItemStatus.InWarteschlange);
        await workItems.SaveAsync(replacement, cancellationToken).ConfigureAwait(false);
        await history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), replacement.Id, clock.UtcNow,
            ExecutionEventSeverity.Information, "work_item.requeued", "Auftrag wurde aus einem abgeschlossenen Auftrag neu eingereiht.", data:
            new Dictionary<string, string> { ["sourceWorkItemId"] = item.Id.ToString() }), cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<bool> CancelAsync(WorkItem item, CancellationToken cancellationToken = default) =>
        scheduler.CancelAsync(item.Id, cancellationToken);

    public async Task ReleaseHoldAsync(ProjectExecutionHold hold, CancellationToken cancellationToken = default)
    {
        var trigger = await workItems.GetAsync(hold.TriggeringWorkItemId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Der auslösende Auftrag wurde nicht gefunden.");
        hold.Release(clock.UtcNow, "Bewusst manuell in der Oberfläche freigegeben.", true, trigger.Status);
        await blocks.SaveAsync(hold, cancellationToken).ConfigureAwait(false);
        await history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), trigger.Id, clock.UtcNow,
            ExecutionEventSeverity.Warning, "project.hold_released", "Projekt-Hold wurde manuell freigegeben.", data:
            new Dictionary<string, string> { ["reasonCode"] = "project.hold_manual_release" }), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<ExecutionAttempt> Attempts, IReadOnlyList<ExecutionEvent> Events)>
        GetHistoryAsync(WorkItemId id, CancellationToken cancellationToken = default)
    {
        var attempts = history.ListAttemptsAsync(id, cancellationToken);
        var events = history.ListEventsAsync(id, cancellationToken);
        await Task.WhenAll(attempts, events).ConfigureAwait(false);
        return (attempts.Result, events.Result);
    }

    public Task SavePoliciesAsync(IReadOnlyCollection<UsagePolicy> values,
        CancellationToken cancellationToken = default) => policies.ReplaceAsync(values, cancellationToken);

    public async Task SavePlatformAsync(PlatformDefinition definition, CancellationToken cancellationToken = default)
    {
        platformRegistry.GetRequired(definition.Id);
        await platforms.SaveAsync(definition, cancellationToken).ConfigureAwait(false);
        await settings.SetAsync($"{definition.Id.Value}.Executable", definition.Executable, cancellationToken)
            .ConfigureAwait(false);
        healthCache.Remove(definition.Id.Value);
    }

    public Task SaveSettingAsync(string key, string value, CancellationToken cancellationToken = default) =>
        settings.SetAsync(key, value, cancellationToken);

    public Task<ProjectCreationResult> CreateProjectAsync(ProjectDefinition project,
        CancellationToken cancellationToken = default) =>
        projectCreation.CreateAsync(new ProjectCreationRequest(project, true), cancellationToken);

    public static void ValidateRegex(string pattern) => _ = new Regex(pattern,
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(500));

    private async Task<PlatformHealth> GetHealthAsync(PlatformDefinition definition, bool force,
        CancellationToken cancellationToken)
    {
        if (!force && healthCache.TryGetValue(definition.Id.Value, out var cached)
            && clock.UtcNow - cached.At < TimeSpan.FromSeconds(30)) return cached.Health;
        PlatformHealth health;
        try { health = await platformRegistry.GetRequired(definition.Id).CheckAvailabilityAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        { health = new PlatformHealth(PlatformHealthStatus.Unavailable, exception.Message); }
        healthCache[definition.Id.Value] = (clock.UtcNow, health);
        return health;
    }

    private string DescribeLimits(PlatformDefinition definition, IReadOnlyList<UsagePolicy> configured,
        UsageSnapshot? snapshot)
    {
        var model = definition.Models.First();
        var decision = usageEvaluator.Evaluate(definition.Id, model.Id, configured, snapshot, clock.UtcNow);
        return decision.Windows.Count == 0 ? DescribeDecision(decision.ReasonCode) : string.Join(", ", decision.Windows.Select(x =>
            $"{x.WindowName}: {x.UsedPercent:0.##} % < {x.EffectiveLimit:0.##} %" + (x.EndSprintActive ? " (Endspurt)" : "")));
    }

    private static string DescribeDecision(string reasonCode) => reasonCode switch
    {
        SchedulerReasonCodes.UnknownUsageBlocked => "Blockiert: Verbrauch unbekannt",
        SchedulerReasonCodes.UnknownUsageAllowed => "Erlaubt: Verbrauch unbekannt",
        SchedulerReasonCodes.StaleUsageBlocked => "Blockiert: Verbrauchsdaten veraltet",
        SchedulerReasonCodes.StaleUsageAllowed => "Erlaubt: Verbrauchsdaten veraltet",
        SchedulerReasonCodes.OutsideSchedule => "Blockiert: keine aktive Zeitregel",
        SchedulerReasonCodes.ServerLimitReached => "Blockiert: serverseitiges Limit erreicht",
        SchedulerReasonCodes.PercentLimitReached => "Blockiert: Verbrauchsgrenze erreicht",
        _ => reasonCode
    };

    private static string LatestBlockingReason(WorkItem item, bool held,
        IReadOnlyList<PlatformUsageBlock> platformBlocks, UsageSnapshot? snapshot)
    {
        if (held) return "Projekt-Hold durch einen möglicherweise teilweise ausgeführten Auftrag";
        var block = platformBlocks.FirstOrDefault(x => PlatformEquals(x.PlatformId, item.PlatformId));
        if (block is not null) return block.Reason;
        var server = snapshot?.Windows.FirstOrDefault(x => x.IsServerLimitReached);
        if (server is not null) return $"Serverlimit: {server.RateLimitReachedType}";
        return "";
    }

    private static void ValidateWorkItem(WorkItemEditModel model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(model.PlatformId);
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(model.Effort);
        ArgumentException.ThrowIfNullOrWhiteSpace(model.PromptPath);
        _ = new WorkItemPriority(model.Priority);
        if (!File.Exists(model.PromptPath)) throw new FileNotFoundException("Die Prompt-Datei wurde nicht gefunden.", model.PromptPath);
    }

    private static bool PlatformEquals(PlatformId left, PlatformId right) =>
        string.Equals(left.Value, right.Value, StringComparison.OrdinalIgnoreCase);
    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);
}

internal sealed class UiDefaultsInitializer(IPlatformRepository platforms, IUsagePolicyRepository policies,
    IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var configured = await platforms.ListAsync(cancellationToken);
        if (!configured.Any(x => x.Id.Value.Equals("codex", StringComparison.OrdinalIgnoreCase)))
        {
            await platforms.SaveAsync(new PlatformDefinition(new PlatformId("codex"),
                configuration["Codex:Executable"] ?? "codex",
                [new PlatformModel(new ModelId("gpt-5.6-sol"), [new EffortLevel("low"), new EffortLevel("medium"), new EffortLevel("high")])]), cancellationToken);
        }
        if (!configured.Any(x => x.Id.Value.Equals("claude", StringComparison.OrdinalIgnoreCase)))
        {
            await platforms.SaveAsync(new PlatformDefinition(new PlatformId("claude"),
                configuration["Claude:Executable"] ?? "claude",
                [new PlatformModel(new ModelId("sonnet"), [new EffortLevel("low"), new EffortLevel("medium"), new EffortLevel("high")])]), cancellationToken);
        }
        if ((await policies.ListAsync(cancellationToken: cancellationToken)).Count == 0)
        {
            var days = Enum.GetValues<DayOfWeek>();
            var zone = TimeZoneInfo.Local.Id;
            var defaults = (await platforms.ListAsync(cancellationToken)).Select(x => new UsagePolicy(x.Id, days,
                TimeOnly.MinValue, new TimeOnly(23, 59, 59, 999), zone, new UsagePercent(100),
                UnknownUsageBehavior.Blockieren, TimeSpan.FromMinutes(1), endSprintDuration: TimeSpan.FromMinutes(30),
                endSprintMaxUsedPercent: new UsagePercent(100))).ToList();
            await policies.ReplaceAsync(defaults, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
