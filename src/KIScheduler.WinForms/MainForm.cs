using KIScheduler.Core.Domain;
using KIScheduler.Core.Scheduling;
using KIScheduler.Platforms.Claude;
using Microsoft.Extensions.Configuration;

namespace KIScheduler.WinForms;

public sealed partial class MainForm : Form
{
    private static readonly Image pauseIcon = UiIcons.Create(UiIcon.Pause, 64);
    private static readonly Image playIcon = UiIcons.Create(UiIcon.Play, 64);
    private readonly SchedulerUiService ui = null!;
    private readonly ISchedulerEngine scheduler = null!;
    private readonly SchedulerOptions schedulerOptions = null!;
    private readonly IConfiguration configuration = null!;
    // The visible controls are declared and created in MainForm.Designer.cs.
    private readonly ToolStripMenuItem schedulerPauseMenuItem = new();
    private readonly NotifyIcon tray = new() { Icon = SystemIcons.Application, Text = "KIScheduler" };
    private readonly System.Windows.Forms.Timer refreshTimer = new() { Interval = 2500 };
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private string? currentResumeCommand;
    private DashboardData? data;
    private bool allowClose;
    private bool minimizeToTray;
    private bool restoringQueueSelection;
    private int historyLoadVersion;

    public MainForm()
    {
        InitializeComponent();
    }

    public MainForm(SchedulerUiService ui, ISchedulerEngine scheduler, SchedulerOptions schedulerOptions,
        IConfiguration configuration) : this()
    {
        this.ui = ui;
        this.scheduler = scheduler;
        this.schedulerOptions = schedulerOptions;
        this.configuration = configuration;
        minimizeToTray = bool.TryParse(configuration["UI:MinimizeToTray"], out var configuredMinimizeToTray)
            && configuredMinimizeToTray;
        ConfigurePageActions();
        ConfigureSettingsPage();

        ConfigureTray();
        tray.Visible = true;
        UpdateSchedulerPauseUi();
        Shown += async (_, _) => { await RefreshAsync(true); refreshTimer.Start(); };
        refreshTimer.Tick += async (_, _) => await RefreshAsync();
        queue.SelectionChanged += async (_, _) =>
        {
            UpdateQueueActionStates();
            if (!restoringQueueSelection) await LoadHistoryAsync();
        };
        queue.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditSelectedAsync(); };
        projectGrid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditSelectedProjectAsync(); };
        platformGrid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditPlatformAsync(); };
        policyGrid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditSelectedPolicyAsync(); };
        filter.TextChanged += (_, _) => ApplyQueueRows();
        statusFilter.SelectedIndexChanged += (_, _) => ApplyQueueRows();
        FormClosing += OnFormClosing;
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized && minimizeToTray) Hide(); };
        FormClosed += (_, _) => { refreshTimer.Dispose(); tray.Dispose(); refreshGate.Dispose(); };
    }

    private void ConfigurePageActions()
    {
        ConfigureQueuePage();
        ConfigureProjectsPage();
        ConfigurePlatformPage();
        ConfigureBlocksPage();
        ConfigureHistoryPage();
        ConfigurePoliciesPage();
    }

    private void ConfigureQueuePage()
    {
        newItemButton.Click += async (_, _) => await EditWorkItemAsync(null);
        newProjectButton.Click += async (_, _) => await CreateProjectAsync();
        editItemButton.Click += async (_, _) => await EditSelectedAsync();
        duplicateItemButton.Click += async (_, _) => await DuplicateSelectedAsync();
        historyButton.Click += async (_, _) => await ShowSelectedHistoryAsync();
        priorityIncreaseButton.Click += async (_, _) => await ChangePriorityAsync(5);
        priorityDecreaseButton.Click += async (_, _) => await ChangePriorityAsync(-5);
        itemPauseButton.Click += async (_, _) => await ToggleItemPauseAsync();
        cancelButton.Click += async (_, _) => await CancelSelectedAsync();
        requeueButton.Click += async (_, _) => await RequeueSelectedAsync();
        humanReviewButton.Click += async (_, _) => await MarkSelectedForHumanReviewAsync();
        usageRefreshButton.Click += async (_, _) => await RefreshAsync(true);
        schedulerPauseButton.Click += (_, _) => ToggleSchedulerPause();
        schedulerPauseButton.ToolTipText = "Pausiert neue Starts; bereits laufende Aufträge laufen kontrolliert weiter.";
        ConfigureToolbar(queueTools, (newItemButton, UiIcon.Add), (newProjectButton, UiIcon.Project),
            (editItemButton, UiIcon.Edit), (duplicateItemButton, UiIcon.Duplicate),
            (historyButton, UiIcon.History), (priorityIncreaseButton, UiIcon.PriorityUp),
            (priorityDecreaseButton, UiIcon.PriorityDown), (itemPauseButton, UiIcon.Pause),
            (cancelButton, UiIcon.Cancel), (humanReviewButton, UiIcon.Review),
            (requeueButton, UiIcon.Requeue), (usageRefreshButton, UiIcon.Refresh),
            (schedulerPauseButton, UiIcon.Pause));
        statusFilter.Items.Add("Alle Status");
        foreach (var value in Enum.GetValues<WorkItemDisplayStatus>()) statusFilter.Items.Add(value.ToString());
        statusFilter.SelectedIndex = 0;
    }

    private void ConfigureProjectsPage()
    {
        createProjectButton.Click += async (_, _) => await EditProjectAsync(null);
        editProjectButton.Click += async (_, _) => await EditSelectedProjectAsync();
        deleteProjectButton.Click += async (_, _) => await DeleteSelectedProjectAsync();
        ConfigureToolbar(projectTools, (createProjectButton, UiIcon.Project),
            (editProjectButton, UiIcon.Edit), (deleteProjectButton, UiIcon.Delete));
    }

    private void ConfigurePlatformPage()
    {
        checkPlatformsButton.Click += async (_, _) => await RefreshAsync(true);
        managePlatformsButton.Click += async (_, _) => await EditPlatformAsync();
        ConfigureToolbar(platformTools, (checkPlatformsButton, UiIcon.Refresh),
            (managePlatformsButton, UiIcon.Platform));
    }

    private void ConfigureBlocksPage()
    {
        releaseHoldButton.Click += async (_, _) => await ReleaseSelectedHoldAsync();
        ConfigureToolbar(blockTools, (releaseHoldButton, UiIcon.Unlock));
    }

    private void ConfigureHistoryPage()
    {
        copyResumeButton.Click += (_, _) => CopyResumeCommand();
        ConfigureToolbar(historyTools, (copyResumeButton, UiIcon.Copy));
    }

    private void ConfigurePoliciesPage()
    {
        addPolicyButton.Click += async (_, _) => await EditPolicyAsync(null);
        editPolicyButton.Click += async (_, _) => await EditSelectedPolicyAsync();
        removePolicyButton.Click += async (_, _) => await RemoveSelectedPolicyAsync();
        ConfigureToolbar(policyTools, (addPolicyButton, UiIcon.Policy),
            (editPolicyButton, UiIcon.Edit), (removePolicyButton, UiIcon.Delete));
    }

    private static void ConfigureToolbar(ToolStrip toolbar, params (ToolStripButton Button, UiIcon Icon)[] buttons)
    {
        toolbar.Renderer = new ToolStripProfessionalRenderer(new ToolbarColorTable());
        foreach (var (button, icon) in buttons) StyleButton(button, icon);
    }

    private void ConfigureSettingsPage()
    {
        poll.Text = schedulerOptions.PollInterval.ToString("c");
        timeout.Text = schedulerOptions.ExecutionTimeout.ToString("c");
        usageHistoryInterval.Text = schedulerOptions.HistoryUsageEventInterval.ToString("c");
        maximumAttempts.Text = schedulerOptions.MaximumAttempts.ToString();
        retryBackoff.Text = schedulerOptions.RetryBackoff.ToString("c");
        maximumRetryBackoff.Text = schedulerOptions.MaximumRetryBackoff.ToString("c");
        regex.Text = configuration["Claude:Usage:Pattern"] ?? ClaudeUsageOptions.DefaultPattern;
        weeklyRegex.Text = configuration["Claude:Usage:WeeklyPattern"] ?? ClaudeUsageOptions.DefaultWeeklyPattern;
        costRegex.Text = configuration["Claude:Usage:CostPattern"] ?? ClaudeUsageOptions.DefaultCostPattern;
        freeRegex.Text = configuration["Claude:Usage:FreeAccountPattern"] ?? ClaudeUsageOptions.DefaultFreeAccountPattern;
        minimizeTarget.SelectedIndex = minimizeToTray ? 1 : 0;
        var regexFields = new[] { regex, weeklyRegex, costRegex, freeRegex };
        var defaultExampleOutputs = new[] { "Current session: 42% used",
            "Current week (all models): 76% used", "Total cost: $0.0000", "Free account" };
        var exampleOutputs = (string[])defaultExampleOutputs.Clone();
        var previousChoice = -1;
        regexChoice.SelectedIndexChanged += (_, _) =>
        {
            if (previousChoice >= 0) exampleOutputs[previousChoice] = sample.Text;
            previousChoice = regexChoice.SelectedIndex;
            sample.Text = exampleOutputs[previousChoice];
            testResult.Text = "Noch nicht getestet.";
            testResult.ForeColor = SystemColors.ControlText;
        };
        regexChoice.SelectedIndex = 0;
        sample.TextChanged += (_, _) => testResult.Text = "Noch nicht getestet.";
        foreach (var field in regexFields)
            field.TextChanged += (_, _) => testResult.Text = "Noch nicht getestet.";
        testButton.Click += (_, _) =>
        {
            try
            {
                var selected = regexChoice.SelectedIndex;
                var match = System.Text.RegularExpressions.Regex.Match(sample.Text, regexFields[selected].Text,
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant
                    | System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));
                if (!match.Success)
                {
                    testResult.Text = "Kein Treffer in der Beispielausgabe.";
                    testResult.ForeColor = Color.Firebrick;
                    return;
                }
                if (selected == 3)
                {
                    testResult.Text = $"Treffer: {match.Value}";
                }
                else
                {
                    var groupName = selected == 2 ? "cost" : "used";
                    var group = match.Groups[groupName];
                    if (!group.Success)
                    {
                        testResult.Text = $"Treffer ohne benötigte Gruppe '{groupName}'.";
                        testResult.ForeColor = Color.Firebrick;
                        return;
                    }
                    testResult.Text = $"Treffer: {groupName} = {group.Value}";
                }
                testResult.ForeColor = Color.DarkGreen;
            }
            catch (Exception exception) when (exception is ArgumentException
                or System.Text.RegularExpressions.RegexMatchTimeoutException)
            {
                testResult.Text = $"RegEx-Test fehlgeschlagen: {exception.Message}";
                testResult.ForeColor = Color.Firebrick;
            }
        };
        var defaultAppServerArguments = configuration.GetSection("Codex:AppServerArguments").Get<string[]>()
            ?? ["app-server", "--listen", "stdio://"];
        appServer.Text = System.Text.Json.JsonSerializer.Serialize(defaultAppServerArguments);
        saveSettingsButton.Image = UiIcons.Create(UiIcon.Save, 26);
        saveSettingsButton.TextImageRelation = TextImageRelation.ImageBeforeText;
        saveSettingsButton.Click += async (_, _) => await UiAction(async () =>
        {
            if (!TimeSpan.TryParse(poll.Text, out var p) || p <= TimeSpan.Zero) throw new InvalidOperationException("Das Polling-Intervall ist ungültig.");
            if (!TimeSpan.TryParse(timeout.Text, out var t) || t <= TimeSpan.Zero) throw new InvalidOperationException("Das Auftrags-Timeout ist ungültig.");
            if (!TimeSpan.TryParse(usageHistoryInterval.Text, out var h) || h <= TimeSpan.Zero)
                throw new InvalidOperationException("Der Mindestabstand für Usage-Ereignisse ist ungültig.");
            if (!int.TryParse(maximumAttempts.Text, out var attempts) || attempts <= 0)
                throw new InvalidOperationException("Die maximale Versuchszahl muss größer als null sein.");
            if (!TimeSpan.TryParse(retryBackoff.Text, out var backoff) || backoff <= TimeSpan.Zero)
                throw new InvalidOperationException("Der Retry-Backoff ist ungültig.");
            if (!TimeSpan.TryParse(maximumRetryBackoff.Text, out var maximumBackoff) || maximumBackoff < backoff)
                throw new InvalidOperationException("Der maximale Retry-Backoff darf nicht kleiner als der Retry-Backoff sein.");
            SchedulerUiService.ValidateRegex(regex.Text);
            SchedulerUiService.ValidateRegex(weeklyRegex.Text);
            SchedulerUiService.ValidateRegex(costRegex.Text);
            SchedulerUiService.ValidateRegex(freeRegex.Text);
            _ = System.Text.Json.JsonSerializer.Deserialize<string[]>(appServer.Text)
                ?? throw new InvalidOperationException("Die App-Server-Argumente sind kein gültiges JSON-Array.");
            await ui.SaveSettingAsync("Scheduler.PollInterval", p.ToString("c"));
            await ui.SaveSettingAsync("Scheduler.ExecutionTimeout", t.ToString("c"));
            await ui.SaveSettingAsync("Scheduler.HistoryUsageEventInterval", h.ToString("c"));
            await ui.SaveSettingAsync("Scheduler.MaximumAttempts", attempts.ToString());
            await ui.SaveSettingAsync("Scheduler.RetryBackoff", backoff.ToString("c"));
            await ui.SaveSettingAsync("Scheduler.MaximumRetryBackoff", maximumBackoff.ToString("c"));
            await ui.SaveSettingAsync("Claude.Usage.Pattern", regex.Text);
            await ui.SaveSettingAsync("Claude.Usage.WeeklyPattern", weeklyRegex.Text);
            await ui.SaveSettingAsync("Claude.Usage.CostPattern", costRegex.Text);
            await ui.SaveSettingAsync("Claude.Usage.FreeAccountPattern", freeRegex.Text);
            await ui.SaveSettingAsync("Codex.AppServerArguments", appServer.Text);
            await ui.SaveSettingAsync("UI.MinimizeToTray", (minimizeTarget.SelectedIndex == 1).ToString());
            minimizeToTray = minimizeTarget.SelectedIndex == 1;
            schedulerOptions.HistoryUsageEventInterval = h;
            await LoadHistoryAsync();
            MessageBox.Show(this, "Die Einstellungen wurden gespeichert. Das Minimierungsziel gilt sofort; die übrigen Änderungen gelten nach dem nächsten Start.", "Einstellungen");
        });

        async Task<string> LoadCurrentValueAsync(string key, string configurationKey, string fallback) =>
            await ui.LoadSettingAsync(key) ?? configuration[configurationKey] ?? fallback;

        async Task ReloadSettingsAsync()
        {
            poll.Text = await LoadCurrentValueAsync("Scheduler.PollInterval", "Scheduler:PollInterval",
                schedulerOptions.PollInterval.ToString("c"));
            timeout.Text = await LoadCurrentValueAsync("Scheduler.ExecutionTimeout", "Scheduler:ExecutionTimeout",
                schedulerOptions.ExecutionTimeout.ToString("c"));
            usageHistoryInterval.Text = await LoadCurrentValueAsync("Scheduler.HistoryUsageEventInterval",
                "Scheduler:HistoryUsageEventInterval", schedulerOptions.HistoryUsageEventInterval.ToString("c"));
            maximumAttempts.Text = await LoadCurrentValueAsync("Scheduler.MaximumAttempts", "Scheduler:MaximumAttempts",
                schedulerOptions.MaximumAttempts.ToString());
            retryBackoff.Text = await LoadCurrentValueAsync("Scheduler.RetryBackoff", "Scheduler:RetryBackoff",
                schedulerOptions.RetryBackoff.ToString("c"));
            maximumRetryBackoff.Text = await LoadCurrentValueAsync("Scheduler.MaximumRetryBackoff",
                "Scheduler:MaximumRetryBackoff", schedulerOptions.MaximumRetryBackoff.ToString("c"));
            regex.Text = await LoadCurrentValueAsync("Claude.Usage.Pattern", "Claude:Usage:Pattern",
                ClaudeUsageOptions.DefaultPattern);
            weeklyRegex.Text = await LoadCurrentValueAsync("Claude.Usage.WeeklyPattern", "Claude:Usage:WeeklyPattern",
                ClaudeUsageOptions.DefaultWeeklyPattern);
            costRegex.Text = await LoadCurrentValueAsync("Claude.Usage.CostPattern", "Claude:Usage:CostPattern",
                ClaudeUsageOptions.DefaultCostPattern);
            freeRegex.Text = await LoadCurrentValueAsync("Claude.Usage.FreeAccountPattern",
                "Claude:Usage:FreeAccountPattern", ClaudeUsageOptions.DefaultFreeAccountPattern);
            appServer.Text = await ui.LoadSettingAsync("Codex.AppServerArguments")
                ?? System.Text.Json.JsonSerializer.Serialize(defaultAppServerArguments);
            var minimizeValue = await LoadCurrentValueAsync("UI.MinimizeToTray", "UI:MinimizeToTray", "False");
            minimizeTarget.SelectedIndex = bool.TryParse(minimizeValue, out var savedMinimizeToTray)
                && savedMinimizeToTray ? 1 : 0;
            minimizeToTray = minimizeTarget.SelectedIndex == 1;

            Array.Copy(defaultExampleOutputs, exampleOutputs, defaultExampleOutputs.Length);
            previousChoice = -1;
            regexChoice.SelectedIndex = 0;
            previousChoice = 0;
            sample.Text = defaultExampleOutputs[0];
            testResult.Text = "Noch nicht getestet.";
            testResult.ForeColor = SystemColors.ControlText;
        }

        cancelSettingsButton.Image = UiIcons.Create(UiIcon.Cancel, 26);
        cancelSettingsButton.TextImageRelation = TextImageRelation.ImageBeforeText;
        cancelSettingsButton.Click += async (_, _) => await UiAction(ReloadSettingsAsync);
        const string projectUrl = "https://github.com/VoSs2o0o/KIScheduler";
        projectLink.Links.Add(0, projectUrl.Length, projectUrl);
        projectLink.LinkClicked += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(projectUrl)
                    { UseShellExecute = true });
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, $"Der GitHub-Link konnte nicht geöffnet werden: {exception.Message}",
                    "GitHub-Link", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        versionLabel.Text = $"Version {Application.ProductVersion}";
        ConfigureNavigationButton(runtimeNavigationButton, runtimeHeading, UiIcon.Settings);
        ConfigureNavigationButton(claudeNavigationButton, claudeHeading, UiIcon.Pattern);
        ConfigureNavigationButton(testNavigationButton, testHeading, UiIcon.Test);
    }

    private void ConfigureNavigationButton(Button button, Label heading, UiIcon icon)
    {
        button.Image = UiIcons.Create(icon, 26);
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.AccessibleDescription = heading.Text;
        button.Click += (_, _) =>
            settingsScroll.AutoScrollPosition = new Point(0, Math.Max(0, heading.Top - 8));
    }

    private async Task RefreshAsync(bool providerRefresh = false)
    {
        if (!await refreshGate.WaitAsync(0)) return;
        try
        {
            data = await ui.LoadAsync(providerRefresh);
            if (IsDisposed) return;
            ApplyQueueRows(); ApplyProjectRows(); ApplyPlatformRows(); ApplyBlockRows(); ApplyPolicyRows();
            UpdateSchedulerPauseUi();
            var usage = data.Platforms
                .SelectMany(x => x.Profiles.Select(p => new ProfileUsageStatus(x.Definition.Id,
                    p.Profile.DisplayName, p.Usage, x.Definition.Enabled, p.Profile.Enabled,
                    p.Profile.ShowUsageInStatusBar, p.UsageMessage)))
                .ToList();
            var usageText = ProfileUsageStatusFormatter.Format(usage, DateTimeOffset.UtcNow);
            usageState.Text = $"KI-Usage: {usageText}";
            usageState.Visible = usageText.Length > 0;
            var waitingCount = data.Queue.Count(row => row.Item.Status is
                WorkItemStatus.InWarteschlange or WorkItemStatus.WartetAufUsage);
            var busyCount = data.Queue.Count(row => row.Item.Status is
                WorkItemStatus.Reserviert or WorkItemStatus.InBearbeitung);
            runningState.Text = $"Auftragsstatus: wartet {waitingCount} · beschäftigt {busyCount}";
        }
        catch (Exception exception) { workerState.Text = $"Aktualisierung fehlgeschlagen: {exception.Message}"; }
        finally { refreshGate.Release(); }
    }

    private void ApplyQueueRows()
    {
        var selected = SelectedItem()?.Id;
        var firstDisplayedRow = queue.Rows.Count == 0 ? -1 : queue.FirstDisplayedScrollingRowIndex;
        DataGridViewRow? selectedRow = null;
        restoringQueueSelection = true;
        try
        {
            queue.Rows.Clear(); if (data is null) return;
            var term = filter.Text.Trim();
            var status = statusFilter.SelectedIndex <= 0 ? null : statusFilter.SelectedItem?.ToString();
            foreach (var row in data.Queue.Where(x => (status is null || x.Status == status) &&
                (term.Length == 0 || $"{x.Item.Title} {x.Project} {x.Reason}".Contains(term, StringComparison.CurrentCultureIgnoreCase))))
            {
                var index = queue.Rows.Add(row.Item.Priority.Value, row.Item.PlatformId.Value, row.Profile, row.Item.ModelId.Value,
                    row.Item.Effort.Value, row.Project,
                    row.Item.ScheduledStartAtUtc?.ToLocalTime().ToString("g") ?? "—",
                    row.Usage, row.Status, row.Item.Title, row.Reason);
                queue.Rows[index].Tag = row.Item;
                if (row.Item.Id == selected) selectedRow = queue.Rows[index];
                if (row.Item.Status == WorkItemStatus.WartetAufUsage
                    || row.Status == WorkItemDisplayStatus.ProjektAngehalten.ToString())
                    queue.Rows[index].DefaultCellStyle.BackColor = Color.MistyRose;
                else if (!row.Item.IsScheduledStartDue(DateTimeOffset.UtcNow))
                    queue.Rows[index].DefaultCellStyle.BackColor = Color.LemonChiffon;
            }
            if (selectedRow is not null)
            {
                queue.ClearSelection();
                queue.CurrentCell = selectedRow.Cells[0];
                selectedRow.Selected = true;
            }
            RestoreScrollPosition(queue, firstDisplayedRow);
        }
        finally { restoringQueueSelection = false; }
        UpdateQueueActionStates();
        _ = LoadHistoryAsync();
    }

    private void ApplyPlatformRows()
    {
        var selected = (platformGrid.CurrentRow?.Tag as PlatformDefinition)?.Id.Value;
        platformGrid.Rows.Clear(); if (data is null) return;
        foreach (var row in data.Platforms)
        {
            var usage = row.Usage is null ? "Usage unbekannt" : string.Join(" | ", row.Usage.Windows.Select(x =>
                $"{x.Name}: {x.UsedPercent}; Reset: {(x.ResetAtUtc?.ToLocalTime().ToString("g") ?? "unbekannt")}; Limitstatus: {x.RateLimitReachedType ?? "—"}"));
            if (!string.IsNullOrWhiteSpace(row.UsageMessage)) usage += $" — {row.UsageMessage}";
            var health = row.Definition.Enabled ? row.Health.Status.ToString() : "Deaktiviert";
            var index = platformGrid.Rows.Add(row.Definition.Id.Value, row.Definition.Enabled ? "Ja" : "Nein",
                row.DefaultProfile?.DisplayName ?? "—", row.Profiles.Count, health, row.Health.Message ?? "", usage,
                row.EffectiveLimits);
            platformGrid.Rows[index].Tag = row.Definition;
            if (row.Definition.Id.Value.Equals(selected, StringComparison.OrdinalIgnoreCase))
                platformGrid.CurrentCell = platformGrid.Rows[index].Cells[0];
        }
    }

    private void ApplyProjectRows()
    {
        var selected = (projectGrid.CurrentRow?.Tag as ProjectDefinition)?.Id;
        projectGrid.Rows.Clear(); if (data is null) return;
        foreach (var project in data.Projects.Values.OrderBy(x => x.Name))
        {
            var index = projectGrid.Rows.Add(project.Name, project.RootPath, project.TargetBranch);
            projectGrid.Rows[index].Tag = project;
            if (project.Id == selected) projectGrid.CurrentCell = projectGrid.Rows[index].Cells[0];
        }
    }

    private void ApplyBlockRows()
    {
        blockGrid.Rows.Clear(); if (data is null) return;
        foreach (var block in data.PlatformBlocks)
        { var i = blockGrid.Rows.Add("Plattformsperre", block.PlatformId.Value, WorkItemName(block.TriggeringWorkItemId), block.CreatedAtUtc.ToLocalTime().ToString("g"), block.ReleaseRule, block.Reason); blockGrid.Rows[i].Tag = block; }
        foreach (var hold in data.ProjectHolds)
        { var project = data.Projects.TryGetValue(hold.ProjectId, out var value) ? value.Name : hold.ProjectId.ToString(); var i = blockGrid.Rows.Add("Projekt-Hold", project, WorkItemName(hold.TriggeringWorkItemId), hold.CreatedAtUtc.ToLocalTime().ToString("g"), hold.ReleaseRule, hold.Reason); blockGrid.Rows[i].Tag = hold; }
    }

    private void ApplyPolicyRows()
    {
        PolicySelectionKey? selected = policyGrid.CurrentRow?.Tag is UsagePolicy policy
            ? PolicyIdentity(policy) : null;
        var firstDisplayedRow = policyGrid.Rows.Count == 0 ? -1 : policyGrid.FirstDisplayedScrollingRowIndex;
        DataGridViewRow? selectedRow = null;
        policyGrid.Rows.Clear(); if (data is null) return;
        foreach (var p in data.Policies)
        { var sprint = p.EndSprintDuration.HasValue ? $"{p.EndSprintDuration:g} → {p.EndSprintMaxUsedPercent}" : "—"; var i = policyGrid.Rows.Add(p.PlatformId.Value, p.ModelId?.Value ?? "alle", string.Join(", ", p.Days.Select(x => x.ToString()[..2])), $"{p.LocalStart:HH:mm}–{p.LocalEnd:HH:mm}", p.MaxUsedPercent, sprint, p.UnknownUsageBehavior, p.RefreshInterval, p.TimeZoneId); policyGrid.Rows[i].Tag = p; if (PolicyIdentity(p) == selected) selectedRow = policyGrid.Rows[i]; }
        if (selectedRow is not null)
        {
            policyGrid.ClearSelection();
            policyGrid.CurrentCell = selectedRow.Cells[0];
            selectedRow.Selected = true;
        }
        RestoreScrollPosition(policyGrid, firstDisplayedRow);
    }

    private async Task LoadHistoryAsync()
    {
        var version = ++historyLoadVersion;
        var item = SelectedItem();
        if (item is null)
        {
            attemptsGrid.Rows.Clear(); eventsGrid.Rows.Clear(); reviewDetails.Clear();
            currentResumeCommand = null; historyButton.Enabled = false; return;
        }
        try
        {
            ExecutionAttemptId? selectedAttemptId = attemptsGrid.CurrentRow?.Tag is ExecutionAttempt attempt
                ? attempt.Id : null;
            var selectedEventId = eventsGrid.CurrentRow?.Tag is ExecutionEvent executionEvent
                ? executionEvent.Id : (Guid?)null;
            var firstDisplayedAttempt = attemptsGrid.Rows.Count == 0 ? -1
                : attemptsGrid.FirstDisplayedScrollingRowIndex;
            var firstDisplayedEvent = eventsGrid.Rows.Count == 0 ? -1
                : eventsGrid.FirstDisplayedScrollingRowIndex;
            var result = await ui.GetHistoryAsync(item.Id);
            var review = await ui.GetHumanReviewDetailsAsync(item);
            if (version != historyLoadVersion || SelectedItem()?.Id != item.Id) return;
            attemptsGrid.Rows.Clear(); eventsGrid.Rows.Clear();
            DataGridViewRow? selectedAttemptRow = null;
            foreach (var x in result.Attempts)
            {
                var profile = data?.Platforms.SelectMany(p => p.Profiles)
                    .FirstOrDefault(p => p.Profile.Id == x.PlatformProfileId)?.Profile.DisplayName
                    ?? x.PlatformProfileId.ToString();
                var index = attemptsGrid.Rows.Add(x.SequenceNumber, profile, x.StartedAtUtc.ToLocalTime().ToString("g"),
                    x.CompletedAtUtc.ToLocalTime().ToString("g"), x.Result, x.ExitCode, x.SessionId, x.Diagnostic);
                attemptsGrid.Rows[index].Tag = x;
                if (x.Id == selectedAttemptId) selectedAttemptRow = attemptsGrid.Rows[index];
            }
            DataGridViewRow? selectedEventRow = null;
            var visibleEvents = HistoryEventFilter.ThrottleUsageEvents(result.Events,
                schedulerOptions.HistoryUsageEventInterval);
            foreach (var x in visibleEvents.Reverse())
            {
                var profile = data?.Platforms.SelectMany(p => p.Profiles)
                    .FirstOrDefault(p => p.Profile.Id == x.PlatformProfileId)?.Profile.DisplayName
                    ?? x.PlatformProfileId.ToString();
                var index = eventsGrid.Rows.Add(x.OccurredAtUtc.ToLocalTime().ToString("g"), profile, x.Severity,
                    x.EventType, x.Message);
                eventsGrid.Rows[index].Tag = x;
                if (x.Id == selectedEventId) selectedEventRow = eventsGrid.Rows[index];
            }
            RestoreGridSelection(attemptsGrid, selectedAttemptRow, firstDisplayedAttempt);
            RestoreGridSelection(eventsGrid, selectedEventRow, firstDisplayedEvent);
            historyButton.Enabled = result.Attempts.Count > 0 || result.Events.Count > 0;
            currentResumeCommand = review?.ResumeCommand;
            reviewDetails.Visible = review is not null;
            reviewDetails.Text = review is null ? "" :
                $"MENSCHLICHE PRÜFUNG\r\nPlattform/Profil/Modell: {review.Platform} / {review.Profile} / {review.Model}   Projekt: {review.Project}\r\n" +
                $"Projektroot: {review.ProjectRoot ?? "—"}\r\nSitzung: {review.SessionId ?? "—"}   Logs: {review.LogReference}\r\n" +
                $"Fehlergrund: {review.FailureReason}\r\nFortsetzungsbefehl: {review.ResumeCommand ?? "nicht sicher verfügbar"}";
        }
        catch (Exception exception) { workerState.Text = exception.Message; }
    }

    private async Task ShowSelectedHistoryAsync()
    {
        await LoadHistoryAsync();
        if (historyButton.Enabled) tabs.SelectedTab = historyPage;
    }

    private async Task EditWorkItemAsync(WorkItem? item, bool duplicate = false)
    {
        if (data is null) return;
        var selectablePlatforms = data.Platforms
            .Where(x => x.Definition.Enabled || (!duplicate && item is not null
                && string.Equals(x.Definition.Id.Value, item.PlatformId.Value, StringComparison.OrdinalIgnoreCase))).ToList();
        using var dialog = new WorkItemDialog(selectablePlatforms, item, data.Projects, duplicate);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await UiAction(async () =>
        {
            await ui.SaveWorkItemAsync(dialog.Value, duplicate ? null : item);
            await RefreshAsync();
        });
    }

    private Task EditSelectedAsync() => SelectedItem() is { } item ? EditWorkItemAsync(item) : Task.CompletedTask;
    private Task DuplicateSelectedAsync() => SelectedItem() is { } item
        ? EditWorkItemAsync(item, duplicate: true) : Task.CompletedTask;
    private async Task ChangePriorityAsync(int delta)
    { var item = SelectedItem(); if (item is null || data is null || !item.CanEdit) return; var projectRoot = item.ProjectId is { } id && data.Projects.TryGetValue(id, out var p) ? p.RootPath : null; var prompt = projectRoot is null ? item.PromptPath.Value : Path.GetFullPath(item.PromptPath.Value, projectRoot); var model = new WorkItemEditModel(item.Title, Math.Clamp(item.Priority.Value + delta, 0, 100), item.PlatformId.Value, item.ModelId.Value, item.Effort.Value, prompt, item.AutoCommit, item.CommitMessage, item.ProjectId, item.PlatformProfileId, item.ScheduledStartAtUtc); await UiAction(async () => { await ui.SaveWorkItemAsync(model, item); await RefreshAsync(); }); }
    private async Task ToggleItemPauseAsync() { var item = SelectedItem(); if (item is null) return; await UiAction(async () => { await ui.SetPausedAsync(item, item.Status != WorkItemStatus.Pausiert); await RefreshAsync(); }); }
    private async Task CancelSelectedAsync() { var item = SelectedItem(); if (item is null || MessageBox.Show(this, $"Auftrag '{item.Title}' kontrolliert abbrechen?", "Abbrechen bestätigen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; await UiAction(async () => { if (!await ui.CancelAsync(item)) throw new InvalidOperationException("Der Auftrag kann nicht abgebrochen werden."); await RefreshAsync(); }); }
    private async Task RequeueSelectedAsync()
    {
        var item = SelectedItem(); if (item is null) return;
        var itemHolds = data?.ProjectHolds.Where(x => x.TriggeringWorkItemId == item.Id).ToList() ?? [];
        if (itemHolds.Count > 0 && MessageBox.Show(this,
            "Für diesen Auftrag besteht ein Projekt-Hold. Haben Sie die externe Bearbeitung und den Arbeitsbaum geprüft und möchten Sie den Hold bewusst freigeben und den Auftrag neu einreihen?",
            "Prüfung bestätigen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        await UiAction(async () =>
        {
            foreach (var hold in itemHolds) await ui.ReleaseHoldAsync(hold);
            await ui.RequeueAsync(item);
            await RefreshAsync();
        });
    }
    private async Task MarkSelectedForHumanReviewAsync() { var item = SelectedItem(); if (item is null) return; await UiAction(async () => { await ui.MarkForHumanReviewAsync(item); await RefreshAsync(); await ShowSelectedHistoryAsync(); }); }
    private void ToggleSchedulerPause()
    {
        if (scheduler.IsPaused) scheduler.Resume(); else scheduler.Pause();
        UpdateSchedulerPauseUi();
    }

    private void UpdateSchedulerPauseUi()
    {
        var paused = scheduler.IsPaused;
        workerState.Text = paused
            ? "⏸ VERARBEITUNG PAUSIERT – laufende Aufträge laufen weiter"
            : "Verarbeitung: aktiv";
        workerState.BackColor = paused ? Color.Firebrick : Color.Honeydew;
        workerState.ForeColor = paused ? Color.White : Color.DarkGreen;
        schedulerPauseButton.Text = paused ? "Fortsetzen" : "Pausieren";
        schedulerPauseButton.ToolTipText = paused ? "Verarbeitung fortsetzen"
            : "Pausiert neue Starts; bereits laufende Aufträge laufen kontrolliert weiter.";
        schedulerPauseButton.Image = paused ? playIcon : pauseIcon;
        schedulerPauseButton.BackColor = paused ? Color.MistyRose : Color.White;
        schedulerPauseButton.ForeColor = SystemColors.ControlText;
        schedulerPauseMenuItem.Text = paused ? "Verarbeitung fortsetzen" : "Verarbeitung pausieren";
        tray.Text = paused ? "KIScheduler – Verarbeitung pausiert"
            : $"KIScheduler – {scheduler.RunningCount} laufend";
    }

    private async Task ReleaseSelectedHoldAsync()
    { if (blockGrid.CurrentRow?.Tag is not ProjectExecutionHold hold) { MessageBox.Show(this, "Bitte einen Projekt-Hold auswählen."); return; } if (MessageBox.Show(this, "Der Arbeitsbaum kann teilweise verändert sein. Haben Sie ihn geprüft und möchten Sie den Hold bewusst freigeben?", "Manuelle Freigabe", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; await UiAction(async () => { await ui.ReleaseHoldAsync(hold); await RefreshAsync(); }); }
    private async Task EditPlatformAsync()
    {
        if (data is null || platformGrid.CurrentRow?.Tag is not PlatformDefinition platform) return;
        while (true)
        {
            var row = data.Platforms.FirstOrDefault(x => x.Definition.Id == platform.Id);
            if (row is null) return;
            using var dialog = new PlatformDialog(row.Definition, row.Profiles);
            var result = dialog.ShowDialog(this);
            if (result == DialogResult.Cancel) return;
            if (result == DialogResult.OK)
            {
                await UiAction(async () => { await ui.SavePlatformAsync(dialog.Value); await RefreshAsync(true); });
                return;
            }

            if (dialog.RequestedAction == PlatformDialog.ProfileAction.Hinzufuegen)
            {
                using var profileDialog = new PlatformProfileDialog(platform.Id, null);
                if (profileDialog.ShowDialog(this) == DialogResult.OK)
                    await UiAction(async () => { await ui.SaveProfileAsync(profileDialog.Value); await RefreshAsync(true); });
            }
            else if (dialog.SelectedProfile is { } selectedProfile
                && dialog.RequestedAction == PlatformDialog.ProfileAction.Bearbeiten)
            {
                using var profileDialog = new PlatformProfileDialog(platform.Id, selectedProfile);
                if (profileDialog.ShowDialog(this) == DialogResult.OK)
                    await UiAction(async () => { await ui.SaveProfileAsync(profileDialog.Value); await RefreshAsync(true); });
            }
            else if (dialog.SelectedProfile is { } profileToMakeDefault
                && dialog.RequestedAction == PlatformDialog.ProfileAction.AlsStandard)
            {
                if (profileToMakeDefault.IsDefault)
                {
                    MessageBox.Show(this, $"'{profileToMakeDefault.DisplayName}' ist bereits das Standardprofil.");
                    continue;
                }
                if (!profileToMakeDefault.Enabled)
                {
                    MessageBox.Show(this, "Nur ein aktives Profil kann als Standard festgelegt werden.",
                        "Standardprofil", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }
                if (MessageBox.Show(this,
                        $"Profil '{profileToMakeDefault.DisplayName}' als Standard festlegen?",
                        "Standardprofil ändern", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    continue;
                await UiAction(async () =>
                {
                    await ui.SetDefaultProfileAsync(profileToMakeDefault.Id);
                    await RefreshAsync(true);
                });
            }
            else if (dialog.SelectedProfile is { } profile
                && dialog.RequestedAction == PlatformDialog.ProfileAction.Pruefen)
            {
                await UiAction(async () =>
                {
                    var result = await ui.CheckProfileAsync(profile.Id);
                    await RefreshAsync();
                    MessageBox.Show(this, $"Anmeldung/Usage für '{profile.DisplayName}' wurde geprüft.\r\n"
                        + $"Health: {result.Health.Status}\r\n"
                        + $"Usage: {(result.Usage is null ? "unbekannt" : "Snapshot vorhanden")}", "Profilprüfung",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }
            else if (dialog.SelectedProfile is { } profileToDisable
                && dialog.RequestedAction == PlatformDialog.ProfileAction.Deaktivieren)
            {
                var impacts = await ui.GetProfileDeactivationImpactsAsync(profileToDisable);
                if (impacts.Count > 0)
                {
                    MessageBox.Show(this,
                        $"'{profileToDisable.DisplayName}' kann noch nicht deaktiviert werden.\r\n\r\n"
                        + "Wartende/bearbeitbare Aufträge:\r\n"
                        + string.Join("\r\n", impacts.Select(x => $"• {x.Title} ({x.Status})"))
                        + "\r\n\r\nBitte diese Aufträge zuerst einem anderen aktiven Profil zuordnen.",
                        "Profil wird verwendet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }
                PlatformProfile? replacement = null;
                if (profileToDisable.IsDefault)
                {
                    using var replacementDialog = new ProfileSelectionDialog(
                        row.Profiles.Select(x => x.Profile).ToList(), "Ersatzprofil für Standard auswählen");
                    if (replacementDialog.ShowDialog(this) != DialogResult.OK) continue;
                    replacement = replacementDialog.Value;
                }
                if (MessageBox.Show(this,
                        $"Profil '{profileToDisable.DisplayName}' deaktivieren?\r\n\r\n"
                        + "Es werden keine Dateien und keine Anmeldedaten gelöscht. Historische Aufträge bleiben erhalten.",
                        "Deaktivierung bestätigen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    continue;
                await UiAction(async () => { await ui.DisableProfileAsync(profileToDisable, replacement); await RefreshAsync(true); });
            }
        }
    }
    private Task CreateProjectAsync() => EditProjectAsync(null);
    private async Task EditProjectAsync(ProjectDefinition? project)
    {
        using var dialog = new ProjectDialog(project);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await UiAction(async () => { await ui.SaveProjectAsync(dialog.Value); await RefreshAsync(); });
    }
    private Task EditSelectedProjectAsync() => projectGrid.CurrentRow?.Tag is ProjectDefinition project
        ? EditProjectAsync(project) : Task.CompletedTask;
    private async Task DeleteSelectedProjectAsync()
    {
        if (projectGrid.CurrentRow?.Tag is not ProjectDefinition project) return;
        if (MessageBox.Show(this,
                $"Projekt '{project.Name}' aus KIScheduler löschen? Der Ordner und seine Dateien bleiben erhalten.",
                "Projekt löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        await UiAction(async () => { await ui.DeleteProjectAsync(project); await RefreshAsync(); });
    }
    private async Task EditPolicyAsync(UsagePolicy? policy) { if (data is null) return; using var dialog = new UsagePolicyDialog(data.Platforms.Select(x => x.Definition).ToList(), policy); if (dialog.ShowDialog(this) != DialogResult.OK) return; var values = data.Policies.ToList(); if (policy is not null) values.Remove(policy); values.Add(dialog.Value); await UiAction(async () => { await ui.SavePoliciesAsync(values); await RefreshAsync(); }); }
    private Task EditSelectedPolicyAsync() => policyGrid.CurrentRow?.Tag is UsagePolicy p ? EditPolicyAsync(p) : Task.CompletedTask;
    private async Task RemoveSelectedPolicyAsync() { if (data is null || policyGrid.CurrentRow?.Tag is not UsagePolicy p) return; await UiAction(async () => { await ui.SavePoliciesAsync(data.Policies.Where(x => !ReferenceEquals(x, p)).ToList()); await RefreshAsync(); }); }

    private void CopyResumeCommand()
    { if (string.IsNullOrWhiteSpace(currentResumeCommand)) { MessageBox.Show(this, "Für diesen Auftrag ist kein sicherer Fortsetzungsbefehl verfügbar."); return; } Clipboard.SetText(currentResumeCommand); workerState.Text = "Fortsetzungsbefehl wurde kopiert; er wurde nicht ausgeführt."; }

    private void ConfigureTray()
    {
        var menu = new ContextMenuStrip(); menu.Items.Add("Öffnen", null, (_, _) => RestoreFromTray()); schedulerPauseMenuItem.Click += (_, _) => ToggleSchedulerPause(); menu.Items.Add(schedulerPauseMenuItem); var running = new ToolStripMenuItem("Laufende Aufträge"); menu.Items.Add(running);
        menu.Opening += (_, _) => { running.DropDownItems.Clear(); var items = data?.Queue.Where(x => x.Item.Status is WorkItemStatus.Reserviert or WorkItemStatus.InBearbeitung).ToList() ?? []; if (items.Count == 0) running.DropDownItems.Add("Keine").Enabled = false; foreach (var x in items) running.DropDownItems.Add($"{x.Item.PlatformId}: {x.Item.Title}").Enabled = false; };
        menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Beenden", null, async (_, _) => await ExitAsync()); tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => RestoreFromTray();
    }

    internal void RestoreAndActivate()
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(RestoreAndActivate);
            return;
        }

        Show();
        WindowState = FormWindowState.Normal;
        BringToFront();
        Activate();
    }

    private void RestoreFromTray() => RestoreAndActivate();
    private void OnFormClosing(object? sender, FormClosingEventArgs e) { if (allowClose) return; e.Cancel = true; Hide(); tray.ShowBalloonTip(1500, "KIScheduler", "Die Verarbeitung läuft im Infobereich weiter.", ToolTipIcon.Info); }
    private async Task ExitAsync() { if (scheduler.RunningCount > 0 && MessageBox.Show(this, $"{scheduler.RunningCount} Auftrag/Aufträge laufen. Beim Beenden werden die Prozessbäume kontrolliert beendet. Fortfahren?", "KIScheduler beenden", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; allowClose = true; refreshTimer.Stop(); Enabled = false; using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35)); try { await scheduler.StopAsync(timeout.Token); } catch (OperationCanceledException) { MessageBox.Show(this, "Die Prozesse konnten nicht rechtzeitig beendet werden.", "Beenden", MessageBoxButtons.OK, MessageBoxIcon.Error); allowClose = false; Enabled = true; refreshTimer.Start(); return; } tray.Visible = false; Close(); }

    private WorkItem? SelectedItem() => queue.CurrentRow?.Tag as WorkItem;
    private void UpdateQueueActionStates()
    {
        var item = SelectedItem();
        var canChangePriority = item?.CanEdit == true;
        priorityIncreaseButton.Enabled = canChangePriority;
        priorityDecreaseButton.Enabled = canChangePriority;
        itemPauseButton.Enabled = item is not null && (item.Status == WorkItemStatus.Pausiert
            ? WorkItemStateMachine.CanTransition(item.Status, WorkItemStatus.InWarteschlange)
            : WorkItemStateMachine.CanTransition(item.Status, WorkItemStatus.Pausiert));
        itemPauseButton.Text = item?.Status == WorkItemStatus.Pausiert ? "Fortsetzen" : "Pausieren";
        itemPauseButton.Image = item?.Status == WorkItemStatus.Pausiert ? playIcon : pauseIcon;
        cancelButton.Enabled = item is not null
            && WorkItemStateMachine.CanTransition(item.Status, WorkItemStatus.Abgebrochen);
        requeueButton.Enabled = item?.Status is WorkItemStatus.TechnischErfolgreich
            or WorkItemStatus.ErfolgreichMitWarnung or WorkItemStatus.Fehlgeschlagen
            or WorkItemStatus.Abgebrochen or WorkItemStatus.Unterbrochen
            or WorkItemStatus.MenschlichePruefung;
        humanReviewButton.Enabled = item is not null && (item.Status == WorkItemStatus.MenschlichePruefung
            || WorkItemStateMachine.CanTransition(item.Status, WorkItemStatus.MenschlichePruefung));
    }
    private string WorkItemName(WorkItemId id) => data?.Queue.FirstOrDefault(x => x.Item.Id == id)?.Item.Title ?? id.ToString();
    private static PolicySelectionKey PolicyIdentity(UsagePolicy policy) => new(
        policy.PlatformId.Value, policy.ModelId?.Value, policy.Days.Aggregate(0, (mask, day) => mask | 1 << (int)day),
        policy.LocalStart.Ticks, policy.LocalEnd.Ticks, policy.TimeZoneId, policy.MaxUsedPercent.Value,
        policy.UnknownUsageBehavior, policy.RefreshInterval.Ticks, policy.EndSprintDuration?.Ticks,
        policy.EndSprintMaxUsedPercent?.Value);
    private static void RestoreScrollPosition(DataGridView grid, int rowIndex)
    {
        if (rowIndex >= 0 && rowIndex < grid.Rows.Count && grid.Rows[rowIndex].Visible)
            grid.FirstDisplayedScrollingRowIndex = rowIndex;
    }
    private static void RestoreGridSelection(DataGridView grid, DataGridViewRow? selectedRow,
        int firstDisplayedRow)
    {
        if (selectedRow is not null)
        {
            grid.ClearSelection();
            grid.CurrentCell = selectedRow.Cells[0];
            selectedRow.Selected = true;
        }
        RestoreScrollPosition(grid, firstDisplayedRow);
    }
    private async Task UiAction(Func<Task> action) { try { await action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "KIScheduler", MessageBoxButtons.OK, MessageBoxIcon.Error); } }
    private static ToolStripButton StyleButton(ToolStripButton button, UiIcon icon)
    {
        var fullText = button.Text ?? string.Empty;
        button.ToolTipText = string.IsNullOrEmpty(button.ToolTipText) ? fullText : button.ToolTipText;
        button.Image = icon == UiIcon.Pause ? pauseIcon : UiIcons.Create(icon, 64);
        button.ImageScaling = ToolStripItemImageScaling.None;
        return button;
    }

    private readonly record struct PolicySelectionKey(string PlatformId, string? ModelId, int Days,
        long LocalStartTicks, long LocalEndTicks, string TimeZoneId, decimal MaxUsedPercent,
        UnknownUsageBehavior UnknownUsageBehavior, long RefreshIntervalTicks, long? EndSprintDurationTicks,
        decimal? EndSprintMaxUsedPercent);
}
