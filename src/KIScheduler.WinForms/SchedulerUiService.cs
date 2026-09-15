using System.Text.RegularExpressions;
using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using KIScheduler.Core.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace KIScheduler.WinForms;

public sealed record WorkItemEditModel(string Title, int Priority, string PlatformId, string ModelId,
    string Effort, string PromptPath, bool AutoCommit, string? CommitMessage, ProjectId? ProjectId = null,
    PlatformProfileId? ProfileId = null);

public sealed record ProfileRow(PlatformProfile Profile, PlatformHealth Health, UsageSnapshot? Usage,
    string? UsageMessage);
public sealed record QueueRow(WorkItem Item, string Profile, string Project, string Usage, string Status, string Reason);
public sealed record PlatformRow(PlatformDefinition Definition, PlatformHealth Health, UsageSnapshot? Usage,
    string EffectiveLimits, string? UsageMessage, PlatformProfile? DefaultProfile,
    IReadOnlyList<ProfileRow> Profiles);
public sealed record HumanReviewDetails(string Platform, string Profile, string Model, string Project, string? ProjectRoot,
    string? SessionId, string FailureReason, string LogReference, string? ResumeCommand);
public sealed record DashboardData(IReadOnlyList<QueueRow> Queue, IReadOnlyList<PlatformRow> Platforms,
    IReadOnlyList<PlatformUsageBlock> PlatformBlocks, IReadOnlyList<ProjectExecutionHold> ProjectHolds,
    IReadOnlyList<UsagePolicy> Policies, IReadOnlyDictionary<ProjectId, ProjectDefinition> Projects);

public sealed class SchedulerUiService(
    IWorkItemRepository workItems,
    IProjectRepository projects,
    IPlatformRepository platforms,
    IPlatformProfileRepository profiles,
    IUsagePolicyRepository policies,
    IUsageSnapshotRepository snapshots,
    IExecutionHistoryRepository history,
    IExecutionBlockRepository blocks,
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
        var profileListTask = profiles.ListAsync(cancellationToken: cancellationToken);
        var policyListTask = policies.ListAsync(cancellationToken: cancellationToken);
        var platformBlocksTask = blocks.ListActivePlatformBlocksAsync(cancellationToken);
        var projectHoldsTask = blocks.ListActiveProjectHoldsAsync(cancellationToken);
        await Task.WhenAll(itemListTask, projectListTask, platformListTask, policyListTask,
            profileListTask, platformBlocksTask, projectHoldsTask).ConfigureAwait(false);

        var projectMap = projectListTask.Result.ToDictionary(x => x.Id);
        var holds = projectHoldsTask.Result;
        var profileMap = profileListTask.Result.ToDictionary(x => x.Id);
        var platformRows = await Task.WhenAll(platformListTask.Result.Select(async definition =>
        {
            var configuredProfiles = profileListTask.Result.Where(x => PlatformEquals(x.PlatformId, definition.Id)).ToList();
            var profileRows = await Task.WhenAll(configuredProfiles.Select(profile =>
                LoadProfileAsync(definition, profile, refreshProviders, cancellationToken))).ConfigureAwait(false);
            var defaultProfile = configuredProfiles.FirstOrDefault(x => x.IsDefault
                && PlatformEquals(x.PlatformId, definition.Id));
            var defaultRow = profileRows.FirstOrDefault(x => defaultProfile is not null && x.Profile.Id == defaultProfile.Id);
            var health = definition.Enabled && defaultRow is { Profile.Enabled: true }
                ? defaultRow.Health
                : new PlatformHealth(PlatformHealthStatus.Unavailable,
                    definition.Enabled ? "Kein aktives Standardprofil." : "Plattform deaktiviert.");
            var snapshot = defaultRow?.Usage;
            return new PlatformRow(definition, health, snapshot, definition.Enabled
                ? DescribeLimits(definition, policyListTask.Result, snapshot) : "Deaktiviert",
                defaultRow?.UsageMessage, defaultProfile, profileRows);
        })).ConfigureAwait(false);

        var queue = await Task.WhenAll(itemListTask.Result.Select(async item =>
        {
            var held = item.ProjectId is { } id && holds.Any(x => x.ProjectId == id);
            var project = item.ProjectId is { } projectId && projectMap.TryGetValue(projectId, out var value)
                ? value.Name : "—";
            var platform = platformRows.FirstOrDefault(x => PlatformEquals(x.Definition.Id, item.PlatformId));
            profileMap.TryGetValue(item.PlatformProfileId, out var profile);
            var snapshot = profile is null ? null
                : await snapshots.GetLatestAsync(item.PlatformProfileId, cancellationToken).ConfigureAwait(false);
            var usage = snapshot is null ? "unbekannt" : string.Join(", ",
                snapshot.Windows.Select(x => $"{x.Name}: {x.UsedPercent}"));
            var reason = platform is { Definition.Enabled: false } ? "Plattform deaktiviert"
                : profile is null ? "Plattformprofil fehlt"
                : !PlatformEquals(profile.PlatformId, item.PlatformId) ? "Plattformprofil gehört zu einer anderen Plattform"
                : !profile.Enabled ? "Plattformprofil deaktiviert"
                : LatestBlockingReason(item, held, platformBlocksTask.Result, snapshot);
            return new QueueRow(item, profile?.DisplayName ?? item.PlatformProfileId.ToString(), project, usage,
                item.GetDisplayStatus(held).ToString(), reason);
        })).ConfigureAwait(false);
        return new DashboardData(queue, platformRows, platformBlocksTask.Result, holds,
            policyListTask.Result, projectMap);
    }

    public async Task<WorkItem> SaveWorkItemAsync(WorkItemEditModel model, WorkItem? existing = null,
        CancellationToken cancellationToken = default)
    {
        if (existing is { CanEdit: false })
            throw new InvalidOperationException("Ein laufender oder bereits ausgeführter Auftrag kann nicht mehr gespeichert werden.");
        ValidateWorkItem(model);
        var definition = await platforms.GetAsync(new PlatformId(model.PlatformId), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Plattform '{model.PlatformId}' ist nicht konfiguriert.");
        if (!definition.Enabled && (existing is null || !PlatformEquals(definition.Id, existing.PlatformId)))
            throw new InvalidOperationException($"Plattform '{model.PlatformId}' ist deaktiviert.");
        var platformId = definition.Id;
        var requestedProfileId = model.ProfileId
            ?? (existing is not null && PlatformEquals(definition.Id, existing.PlatformId)
                ? existing.PlatformProfileId : (PlatformProfileId?)null);
        var platformProfile = requestedProfileId is { } selectedProfileId
            ? await profiles.GetAsync(selectedProfileId, cancellationToken).ConfigureAwait(false)
            : await profiles.GetDefaultAsync(platformId, cancellationToken).ConfigureAwait(false);
        if (platformProfile is null || !platformProfile.Enabled || !PlatformEquals(platformProfile.PlatformId, platformId))
            throw new InvalidOperationException($"Für Plattform '{platformId.Value}' ist kein gültiges aktives Profil ausgewählt.");
        if (existing is { HasExecutionStarted: true } && existing.PlatformProfileId != platformProfile.Id)
            throw new InvalidOperationException("Das Profil eines Auftrags darf nach dem ersten Versuch nicht mehr geändert werden.");
        var modelId = new ModelId(model.ModelId);
        var effort = new EffortLevel(model.Effort);
        if (!definition.Supports(modelId, effort))
            throw new InvalidOperationException("Das gewählte Modell unterstützt die Effort-Stufe nicht.");

        var project = await projects.GetAsync(model.ProjectId!.Value, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Das gewählte Projekt wurde nicht gefunden.");
        if (!IsPathWithinRoot(model.PromptPath, project.RootPath))
            throw new InvalidOperationException("Die Prompt-Datei muss innerhalb des gewählten Projektroots liegen.");
        var storedPrompt = Path.GetRelativePath(project.RootPath, Path.GetFullPath(model.PromptPath));
        var promptPath = new PromptPath(storedPrompt);

        WorkItem item;
        if (existing is null)
        {
            item = new WorkItem(WorkItemId.New(), model.Title, new WorkItemPriority(model.Priority),
                platformId, platformProfile.Id, modelId, effort, promptPath, model.AutoCommit, clock.UtcNow, project.Id,
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
                existing.ChangeExecutionConfiguration(platformId, platformProfile.Id, modelId, effort);
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
        if (item.Status != WorkItemStatus.Fehlgeschlagen
            && WorkItemStateMachine.CanTransition(item.Status, WorkItemStatus.InWarteschlange))
        {
            var previous = item.Status;
            item.TransitionTo(WorkItemStatus.InWarteschlange);
            await workItems.SaveAsync(item, cancellationToken).ConfigureAwait(false);
            await history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), item.Id, item.PlatformProfileId, clock.UtcNow,
                ExecutionEventSeverity.Warning, "work_item.requeued",
                "Auftrag wurde nach manueller Prüfung erneut eingereiht.", data:
                new Dictionary<string, string>
                {
                    ["reasonCode"] = "work_item.manual_requeue",
                    ["previousStatus"] = previous.ToString()
                }), cancellationToken).ConfigureAwait(false);
            return;
        }
        if (item.Status is not (WorkItemStatus.Fehlgeschlagen or WorkItemStatus.TechnischErfolgreich
            or WorkItemStatus.ErfolgreichMitWarnung or WorkItemStatus.Abgebrochen))
            throw new InvalidOperationException($"'{item.Status}' kann nicht erneut eingereiht werden.");
        var replacement = new WorkItem(WorkItemId.New(), item.Title, item.Priority, item.PlatformId,
            item.PlatformProfileId, item.ModelId, item.Effort, item.PromptPath, item.AutoCommit, clock.UtcNow, item.ProjectId,
            item.CommitMessage);
        replacement.TransitionTo(WorkItemStatus.InWarteschlange);
        await workItems.SaveAsync(replacement, cancellationToken).ConfigureAwait(false);
        await history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), replacement.Id,
            replacement.PlatformProfileId, clock.UtcNow,
            ExecutionEventSeverity.Information, "work_item.requeued", "Auftrag wurde aus einem abgeschlossenen Auftrag neu eingereiht.", data:
            new Dictionary<string, string> { ["sourceWorkItemId"] = item.Id.ToString() }), cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<bool> CancelAsync(WorkItem item, CancellationToken cancellationToken = default) =>
        scheduler.CancelAsync(item.Id, cancellationToken);

    public async Task MarkForHumanReviewAsync(WorkItem item,
        CancellationToken cancellationToken = default)
    {
        if (item.Status != WorkItemStatus.MenschlichePruefung)
        {
            if (!WorkItemStateMachine.CanTransition(item.Status, WorkItemStatus.MenschlichePruefung))
                throw new InvalidOperationException($"'{item.Status}' kann nicht extern geprüft werden.");
            item.TransitionTo(WorkItemStatus.MenschlichePruefung);
            await workItems.SaveAsync(item, cancellationToken).ConfigureAwait(false);
        }
        await history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), item.Id, item.PlatformProfileId, clock.UtcNow,
            ExecutionEventSeverity.Warning, "human_review.requested",
            "Der Auftrag wurde bewusst zur externen menschlichen Prüfung markiert.", data:
            new Dictionary<string, string> { ["reasonCode"] = "human_review.requested_by_user" }),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<HumanReviewDetails?> GetHumanReviewDetailsAsync(WorkItem item,
        CancellationToken cancellationToken = default)
    {
        if (item.Status is not (WorkItemStatus.MenschlichePruefung or WorkItemStatus.Unterbrochen
            or WorkItemStatus.WartetAufUsage)) return null;
        var attemptsTask = history.ListAttemptsAsync(item.Id, cancellationToken);
        var eventsTask = history.ListEventsAsync(item.Id, cancellationToken);
        var projectTask = item.ProjectId is { } projectId
            ? projects.GetAsync(projectId, cancellationToken) : Task.FromResult<ProjectDefinition?>(null);
        await Task.WhenAll(attemptsTask, eventsTask, projectTask).ConfigureAwait(false);
        var attempts = attemptsTask.Result;
        var events = eventsTask.Result;
        var latestAttempt = attempts.LastOrDefault();
        var session = attempts.LastOrDefault(x => !string.IsNullOrWhiteSpace(x.SessionId))?.SessionId;
        var latestFailure = events.LastOrDefault(x => x.Severity == ExecutionEventSeverity.Error)?.Message
            ?? attempts.LastOrDefault(x => !string.IsNullOrWhiteSpace(x.Diagnostic))?.Diagnostic
            ?? "Kein genauer Fehlergrund gespeichert.";
        var canResume = latestAttempt is not null && !string.IsNullOrWhiteSpace(latestAttempt.SessionId)
            && platformRegistry.GetRequired(item.PlatformId).Capabilities.SupportsResume;
        var command = canResume ? BuildResumeCommand(item.PlatformId, latestAttempt!.SessionId!) : null;
        var project = projectTask.Result;
        var profile = await profiles.GetAsync(item.PlatformProfileId, cancellationToken).ConfigureAwait(false);
        return new HumanReviewDetails(item.PlatformId.Value, profile?.DisplayName ?? item.PlatformProfileId.ToString(), item.ModelId.Value,
            project?.Name ?? item.ProjectId?.ToString() ?? "—", project?.RootPath, session,
            latestFailure, $"{events.Count} Ereignis(se), {attempts.Count} Versuch/Versuche in der Historie",
            command);
    }

    public async Task ReleaseHoldAsync(ProjectExecutionHold hold, CancellationToken cancellationToken = default)
    {
        var trigger = await workItems.GetAsync(hold.TriggeringWorkItemId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Der auslösende Auftrag wurde nicht gefunden.");
        hold.Release(clock.UtcNow, "Bewusst manuell in der Oberfläche freigegeben.", true, trigger.Status);
        await blocks.SaveAsync(hold, cancellationToken).ConfigureAwait(false);
        await history.AddEventAsync(new ExecutionEvent(Guid.NewGuid(), trigger.Id, trigger.PlatformProfileId, clock.UtcNow,
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

    public Task<IReadOnlyList<WorkItem>> GetProfileDeactivationImpactsAsync(PlatformProfile profile,
        CancellationToken cancellationToken = default) =>
        GetProfileDeactivationImpactsCoreAsync(profile, cancellationToken);

    public async Task<ProfileRow> CheckProfileAsync(PlatformProfileId profileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await profiles.GetAsync(profileId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Das Profil wurde nicht gefunden.");
        var platform = await platforms.GetAsync(profile.PlatformId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Plattform '{profile.PlatformId.Value}' wurde nicht gefunden.");
        return await LoadProfileAsync(platform, profile, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveProfileAsync(PlatformProfile profile,
        CancellationToken cancellationToken = default)
    {
        var platform = await platforms.GetAsync(profile.PlatformId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Plattform '{profile.PlatformId.Value}' wurde nicht gefunden.");
        if (!platform.Enabled && profile.Enabled)
            throw new InvalidOperationException("Ein Profil einer deaktivierten Plattform kann nicht aktiviert werden.");

        var existing = await profiles.GetAsync(profile.Id, cancellationToken).ConfigureAwait(false);
        if (existing is not null && !existing.Name.Equals(profile.Name, StringComparison.Ordinal))
            throw new InvalidOperationException("Der interne Name eines bestehenden Profils kann nicht geändert werden.");
        if (existing is not null && existing.IsDefault != profile.IsDefault)
            throw new InvalidOperationException("Bitte verwenden Sie zum Wechseln die Aktion 'Als Standard'.");
        await profiles.SaveAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetDefaultProfileAsync(PlatformProfileId profileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await profiles.GetAsync(profileId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Das Profil wurde nicht gefunden.");
        if (!profile.Enabled)
            throw new InvalidOperationException("Nur ein aktives Profil kann als Standard festgelegt werden.");
        await profiles.SetDefaultAsync(profileId, cancellationToken).ConfigureAwait(false);
        healthCache.Clear();
    }

    public async Task DisableProfileAsync(PlatformProfile profile, PlatformProfile? replacement = null,
        CancellationToken cancellationToken = default)
    {
        var stored = await profiles.GetAsync(profile.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Das Profil wurde nicht gefunden.");
        if (!stored.Enabled) return;
        var impacts = await GetProfileDeactivationImpactsCoreAsync(stored, cancellationToken).ConfigureAwait(false);
        if (impacts.Count > 0)
            throw new InvalidOperationException("Das Profil wird noch von wartenden oder bearbeitbaren Aufträgen verwendet: "
                + string.Join(", ", impacts.Select(x => $"'{x.Title}'"))
                + ". Bitte diese Aufträge zuerst einem anderen aktiven Profil zuordnen.");
        if (stored.IsDefault)
        {
            if (replacement is null || replacement.Id == stored.Id || !replacement.Enabled
                || !PlatformEquals(replacement.PlatformId, stored.PlatformId) || replacement.IsDefault)
                throw new InvalidOperationException("Das Standardprofil kann nur nach Auswahl eines aktiven Ersatzprofils deaktiviert werden.");
            await profiles.SetDefaultAsync(replacement.Id, cancellationToken).ConfigureAwait(false);
        }
        await profiles.DisableAsync(stored.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task SavePlatformAsync(PlatformDefinition definition, CancellationToken cancellationToken = default)
    {
        platformRegistry.GetRequired(definition.Id);
        await platforms.SaveAsync(definition, cancellationToken).ConfigureAwait(false);
        await settings.SetAsync($"{definition.Id.Value}.Executable", definition.Executable, cancellationToken)
            .ConfigureAwait(false);
        healthCache.Clear();
    }

    public Task SaveSettingAsync(string key, string value, CancellationToken cancellationToken = default) =>
        settings.SetAsync(key, value, cancellationToken);

    public async Task SaveProjectAsync(ProjectDefinition project,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(project.RootPath))
            throw new DirectoryNotFoundException($"Das Projektroot wurde nicht gefunden: {project.RootPath}");
        var configured = await projects.ListAsync(cancellationToken).ConfigureAwait(false);
        if (configured.Any(x => x.Id != project.Id && PathEquals(x.RootPath, project.RootPath)))
            throw new InvalidOperationException("Dieses Projektroot ist bereits einem anderen Projekt zugeordnet.");

        var previous = configured.FirstOrDefault(x => x.Id == project.Id);
        if (previous is not null && !PathEquals(previous.RootPath, project.RootPath))
        {
            var referencedItems = (await workItems.ListByStatusAsync(Enum.GetValues<WorkItemStatus>(), cancellationToken)
                .ConfigureAwait(false)).Where(x => x.ProjectId == project.Id).ToList();
            if (referencedItems.Any(x =>
                {
                    var promptPath = Path.GetFullPath(x.PromptPath.Value, project.RootPath);
                    return !File.Exists(promptPath) || !IsPathWithinRoot(promptPath, project.RootPath);
                }))
                throw new InvalidOperationException(
                    "Das Projektroot kann nicht geändert werden, weil dort nicht alle Prompt-Dateien der vorhandenen Aufträge liegen.");
        }
        await projects.SaveAsync(project, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteProjectAsync(ProjectDefinition project,
        CancellationToken cancellationToken = default)
    {
        var referenced = (await workItems.ListByStatusAsync(Enum.GetValues<WorkItemStatus>(), cancellationToken)
            .ConfigureAwait(false)).Any(x => x.ProjectId == project.Id);
        if (referenced)
            throw new InvalidOperationException("Das Projekt kann nicht gelöscht werden, solange Aufträge darauf verweisen.");
        if (!await projects.DeleteAsync(project.Id, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Das Projekt wurde nicht gefunden.");
    }

    public static void ValidateRegex(string pattern) => _ = new Regex(pattern,
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(500));

    private async Task<ProfileRow> LoadProfileAsync(PlatformDefinition definition, PlatformProfile profile, bool force,
        CancellationToken cancellationToken)
    {
        var health = definition.Enabled && profile.Enabled
            ? await GetHealthAsync(definition, profile, force, cancellationToken).ConfigureAwait(false)
            : new PlatformHealth(PlatformHealthStatus.Unavailable,
                definition.Enabled ? "Profil deaktiviert." : "Plattform deaktiviert.");
        UsageSnapshot? snapshot = await snapshots.GetLatestAsync(profile.Id, cancellationToken).ConfigureAwait(false);
        string? usageMessage = usageMessages.GetValueOrDefault(profile.Id.ToString());
        if (definition.Enabled && profile.Enabled && force
            && usageRegistry.TryGet(definition.Id, out var provider) && provider is not null)
        {
            try
            {
                var read = await provider.ReadAsync(profile.Id, true, cancellationToken).ConfigureAwait(false);
                usageMessage = read.Message;
                usageMessages[profile.Id.ToString()] = usageMessage;
                if (read.Snapshot is not null)
                {
                    snapshot = read.Snapshot;
                    await snapshots.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                usageMessage = exception.Message;
                usageMessages[profile.Id.ToString()] = usageMessage;
            }
        }
        return new ProfileRow(profile, health, snapshot, usageMessage);
    }

    private async Task<PlatformHealth> GetHealthAsync(PlatformDefinition definition, PlatformProfile profile, bool force,
        CancellationToken cancellationToken)
    {
        var cacheKey = profile.Id.ToString();
        if (!force && healthCache.TryGetValue(cacheKey, out var cached)
            && clock.UtcNow - cached.At < TimeSpan.FromSeconds(30)) return cached.Health;
        PlatformHealth health;
        try { health = await platformRegistry.GetRequired(definition.Id).CheckAvailabilityAsync(profile.Id, cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        { health = new PlatformHealth(PlatformHealthStatus.Unavailable, exception.Message); }
        healthCache[cacheKey] = (clock.UtcNow, health);
        return health;
    }

    private async Task<IReadOnlyList<WorkItem>> GetProfileDeactivationImpactsCoreAsync(PlatformProfile profile,
        CancellationToken cancellationToken)
    {
        var items = await workItems.ListByStatusAsync(Enum.GetValues<WorkItemStatus>(), cancellationToken)
            .ConfigureAwait(false);
        return items.Where(x => x.PlatformProfileId == profile.Id && x.CanEdit).ToList();
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
        var block = platformBlocks.FirstOrDefault(x => x.PlatformProfileId == item.PlatformProfileId);
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
        if (model.ProjectId is null) throw new ArgumentException("Ein Projekt muss ausgewählt werden.", nameof(model));
        _ = new WorkItemPriority(model.Priority);
        if (!File.Exists(model.PromptPath)) throw new FileNotFoundException("Die Prompt-Datei wurde nicht gefunden.", model.PromptPath);
    }

    private static bool PlatformEquals(PlatformId left, PlatformId right) =>
        string.Equals(left.Value, right.Value, StringComparison.OrdinalIgnoreCase);
    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);

    private static bool IsPathWithinRoot(string path, string root)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        return !Path.IsPathRooted(relative) && relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string BuildResumeCommand(PlatformId platformId, string sessionId)
    {
        var safeSession = sessionId.Replace("\"", "\\\"", StringComparison.Ordinal);
        return platformId.Value.Equals("codex", StringComparison.OrdinalIgnoreCase)
            ? $"codex exec resume \"{safeSession}\" -"
            : platformId.Value.Equals("claude", StringComparison.OrdinalIgnoreCase)
                ? $"claude --resume \"{safeSession}\""
                : $"{platformId.Value} --resume \"{safeSession}\"";
    }
}

internal sealed class UiDefaultsInitializer(IPlatformRepository platforms, IPlatformProfileRepository profiles,
    IUsagePolicyRepository policies,
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
        configured = await platforms.ListAsync(cancellationToken);
        foreach (var platform in configured)
        {
            if (await profiles.GetDefaultAsync(platform.Id, cancellationToken) is null)
                await profiles.SaveAsync(PlatformProfile.CreateDefault(PlatformProfileId.New(), platform.Id,
                    platform.ShowUsageInStatusBar), cancellationToken);
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
