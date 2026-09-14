using KIScheduler.Core.Domain;
using KIScheduler.Core.Scheduling;

namespace KIScheduler.WinForms;

public sealed class MainForm : Form
{
    private readonly SchedulerUiService ui;
    private readonly ISchedulerEngine scheduler;
    private readonly SchedulerOptions schedulerOptions;
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private readonly TabPage historyPage;
    private readonly DataGridView queue = Grid();
    private readonly DataGridView projectGrid = Grid();
    private readonly DataGridView platformGrid = Grid();
    private readonly DataGridView blockGrid = Grid();
    private readonly DataGridView attemptsGrid = Grid();
    private readonly DataGridView eventsGrid = Grid();
    private readonly DataGridView policyGrid = Grid();
    private readonly TextBox filter = new() { PlaceholderText = "Titel, Projekt oder Begründung filtern …", Dock = DockStyle.Fill };
    private readonly ComboBox statusFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly ToolStripStatusLabel workerState = new()
        { BorderSides = ToolStripStatusLabelBorderSides.All, Padding = new Padding(6, 2, 6, 2) };
    private readonly ToolStripStatusLabel usageState = new()
        { BorderSides = ToolStripStatusLabelBorderSides.Left, Visible = false };
    private readonly ToolStripStatusLabel runningState = new();
    private readonly ToolStripButton priorityIncreaseButton = new() { Text = "Priorität +", Enabled = false };
    private readonly ToolStripButton priorityDecreaseButton = new() { Text = "Priorität −", Enabled = false };
    private readonly ToolStripButton itemPauseButton = new() { Text = "Pausieren/Fortsetzen", Enabled = false };
    private readonly ToolStripButton cancelButton = new() { Text = "Abbrechen", Enabled = false };
    private readonly ToolStripButton requeueButton = new() { Text = "Erneut einreihen", Enabled = false };
    private readonly ToolStripButton humanReviewButton = new() { Text = "Extern prüfen", Enabled = false };
    private readonly ToolStripButton historyButton = new() { Text = "Verlauf", Enabled = false };
    private readonly ToolStripButton schedulerPauseButton = new();
    private readonly ToolStripMenuItem schedulerPauseMenuItem = new();
    private readonly NotifyIcon tray = new() { Icon = SystemIcons.Application, Text = "KIScheduler", Visible = true };
    private readonly System.Windows.Forms.Timer refreshTimer = new() { Interval = 2500 };
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly TextBox reviewDetails = new() { ReadOnly = true, Multiline = true, Height = 118,
        Dock = DockStyle.Top, ScrollBars = ScrollBars.Vertical, BackColor = SystemColors.Info };
    private string? currentResumeCommand;
    private DashboardData? data;
    private bool allowClose;
    private bool restoringQueueSelection;
    private int historyLoadVersion;

    public MainForm(SchedulerUiService ui, ISchedulerEngine scheduler, SchedulerOptions schedulerOptions)
    {
        this.ui = ui;
        this.scheduler = scheduler;
        this.schedulerOptions = schedulerOptions;
        Text = "KIScheduler";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 650);
        ClientSize = new Size(1320, 780);
        Icon = SystemIcons.Application;

        historyPage = BuildHistoryPage();
        tabs.TabPages.Add(BuildQueuePage());
        tabs.TabPages.Add(BuildProjectsPage());
        tabs.TabPages.Add(BuildPlatformPage());
        tabs.TabPages.Add(BuildBlocksPage());
        tabs.TabPages.Add(historyPage);
        tabs.TabPages.Add(BuildPoliciesPage());
        tabs.TabPages.Add(BuildSettingsPage());
        var status = new StatusStrip();
        status.Items.AddRange([workerState, new ToolStripStatusLabel { Spring = true }, usageState, runningState]);
        Controls.Add(tabs);
        Controls.Add(status);

        ConfigureTray();
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
        FormClosed += (_, _) => { refreshTimer.Dispose(); tray.Dispose(); refreshGate.Dispose(); };
    }

    private TabPage BuildQueuePage()
    {
        queue.Columns.AddRange(
            TextColumn("Priorität", "Priority", 75), TextColumn("Plattform", "Platform", 85),
            TextColumn("Profil", "Profile", 150),
            TextColumn("Modell", "Model", 135), TextColumn("Effort", "Effort", 70),
            TextColumn("Projekt", "Project", 150), TextColumn("Usage", "Usage", 190),
            TextColumn("Status", "Status", 150), TextColumn("Titel", "Title", 230),
            TextColumn("Begründung", "Reason", 340));
        var tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        tools.Items.Add(Button("Neu", async () => await EditWorkItemAsync(null)));
        tools.Items.Add(Button("Neues Projekt", async () => await CreateProjectAsync()));
        tools.Items.Add(Button("Bearbeiten", async () => await EditSelectedAsync()));
        tools.Items.Add(Button("Duplizieren", async () => await DuplicateSelectedAsync()));
        historyButton.Click += async (_, _) => await ShowSelectedHistoryAsync();
        tools.Items.Add(historyButton);
        tools.Items.Add(new ToolStripSeparator());
        priorityIncreaseButton.Click += async (_, _) => await ChangePriorityAsync(5);
        priorityDecreaseButton.Click += async (_, _) => await ChangePriorityAsync(-5);
        tools.Items.Add(priorityIncreaseButton);
        tools.Items.Add(priorityDecreaseButton);
        itemPauseButton.Click += async (_, _) => await ToggleItemPauseAsync();
        cancelButton.Click += async (_, _) => await CancelSelectedAsync();
        requeueButton.Click += async (_, _) => await RequeueSelectedAsync();
        tools.Items.Add(itemPauseButton);
        tools.Items.Add(cancelButton);
        humanReviewButton.Click += async (_, _) => await MarkSelectedForHumanReviewAsync();
        tools.Items.Add(humanReviewButton);
        tools.Items.Add(requeueButton);
        tools.Items.Add(new ToolStripSeparator());
        tools.Items.Add(Button("Usage aktualisieren", async () => await RefreshAsync(true)));
        schedulerPauseButton.Click += (_, _) => ToggleSchedulerPause();
        schedulerPauseButton.ToolTipText = "Pausiert neue Starts; bereits laufende Aufträge laufen kontrolliert weiter.";
        tools.Items.Add(schedulerPauseButton);
        statusFilter.Items.Add("Alle Status");
        foreach (var value in Enum.GetValues<WorkItemDisplayStatus>()) statusFilter.Items.Add(value.ToString());
        statusFilter.SelectedIndex = 0;
        var filters = new TableLayoutPanel { Dock = DockStyle.Top, Height = 36, ColumnCount = 2, Padding = new Padding(4) };
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        filters.Controls.Add(filter, 0, 0); filters.Controls.Add(statusFilter, 1, 0);
        return Page("Warteschlange", queue, tools, filters);
    }

    private TabPage BuildPlatformPage()
    {
        platformGrid.Columns.AddRange(TextColumn("Plattform", "Platform", 110), TextColumn("Aktiv", "Enabled", 65),
            TextColumn("Standardprofil", "DefaultProfile", 150), TextColumn("Profile", "Profiles", 75),
            TextColumn("Zustand", "Health", 130),
            TextColumn("Details", "Details", 260), TextColumn("Verbrauch und Reset", "Usage", 430),
            TextColumn("Wirksame Grenzwerte", "Limits", 400));
        var tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        tools.Items.Add(Button("Jetzt prüfen", async () => await RefreshAsync(true)));
        tools.Items.Add(Button("Plattform und Profile verwalten", async () => await EditPlatformAsync()));
        return Page("Plattformen & Usage", platformGrid, tools);
    }

    private TabPage BuildProjectsPage()
    {
        projectGrid.Columns.AddRange(TextColumn("Name", "Name", 240),
            TextColumn("Projektroot", "Root", 650), TextColumn("Zielbranch", "Branch", 180));
        var tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        tools.Items.Add(Button("Projekt anlegen", async () => await EditProjectAsync(null)));
        tools.Items.Add(Button("Projekt bearbeiten", async () => await EditSelectedProjectAsync()));
        tools.Items.Add(Button("Projekt löschen", async () => await DeleteSelectedProjectAsync()));
        return Page("Projekte", projectGrid, tools);
    }

    private TabPage BuildBlocksPage()
    {
        blockGrid.Columns.AddRange(TextColumn("Typ", "Type", 130), TextColumn("Plattform/Projekt", "Target", 180),
            TextColumn("Auslösender Auftrag", "Trigger", 230), TextColumn("Seit", "Since", 145),
            TextColumn("Freigaberegel", "Rule", 260), TextColumn("Ursache", "Reason", 450));
        var tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        tools.Items.Add(Button("Projekt-Hold bewusst freigeben", async () => await ReleaseSelectedHoldAsync()));
        return Page("Sperren", blockGrid, tools);
    }

    private TabPage BuildHistoryPage()
    {
        attemptsGrid.Columns.AddRange(TextColumn("Nr.", "Number", 50), TextColumn("Profil", "Profile", 145), TextColumn("Start", "Start", 145),
            TextColumn("Ende", "End", 145), TextColumn("Ergebnis", "Result", 170), TextColumn("Exit", "Exit", 55),
            TextColumn("Sitzung", "Session", 240), TextColumn("Diagnose", "Diagnostic", 430));
        eventsGrid.Columns.AddRange(TextColumn("Zeit", "Time", 145), TextColumn("Profil", "Profile", 145), TextColumn("Stufe", "Severity", 90),
            TextColumn("Typ/Stream", "Type", 200), TextColumn("Meldung", "Message", 700));
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 220 };
        split.Panel1.Controls.Add(attemptsGrid); split.Panel2.Controls.Add(eventsGrid);
        var tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        tools.Items.Add(new ToolStripLabel("Versuche und Ereignisse des markierten Auftrags"));
        tools.Items.Add(Button("Fortsetzungsbefehl kopieren", CopyResumeCommand));
        return Page("Historie", split, tools, reviewDetails);
    }

    private TabPage BuildPoliciesPage()
    {
        policyGrid.Columns.AddRange(TextColumn("Plattform", "Platform", 100), TextColumn("Modell", "Model", 130),
            TextColumn("Tage", "Days", 210), TextColumn("Zeitraum", "Range", 130), TextColumn("Grenze", "Limit", 80),
            TextColumn("Endspurt", "Sprint", 170), TextColumn("Unbekannt", "Unknown", 100),
            TextColumn("Polling", "Polling", 100), TextColumn("Zeitzone", "Zone", 220));
        var tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        tools.Items.Add(Button("Regel hinzufügen", async () => await EditPolicyAsync(null)));
        tools.Items.Add(Button("Regel bearbeiten", async () => await EditSelectedPolicyAsync()));
        tools.Items.Add(Button("Regel entfernen", async () => await RemoveSelectedPolicyAsync()));
        return Page("Usage-Regeln", policyGrid, tools);
    }

    private TabPage BuildSettingsPage()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(20) };
        panel.Controls.Add(new Label { AutoSize = true, Font = new Font(Font, FontStyle.Bold), Text = "Laufzeit- und Provider-Einstellungen" });
        panel.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(900, 0), Text =
            "Änderungen werden validiert und sicher für den nächsten Anwendungsstart gespeichert. Zugangsdaten werden hier bewusst nicht abgelegt." });
        var poll = Field(panel, "Scheduler-Polling (hh:mm:ss)", schedulerOptions.PollInterval.ToString("c"));
        var timeout = Field(panel, "Auftrags-Timeout (hh:mm:ss)", schedulerOptions.ExecutionTimeout.ToString("c"));
        var usageHistoryInterval = Field(panel, "Usage-Ereignisse in Historie (hh:mm:ss)",
            schedulerOptions.HistoryUsageEventInterval.ToString("c"));
        var maximumAttempts = Field(panel, "Maximale Versuche", schedulerOptions.MaximumAttempts.ToString());
        var retryBackoff = Field(panel, "Retry-Backoff (hh:mm:ss)", schedulerOptions.RetryBackoff.ToString("c"));
        var maximumRetryBackoff = Field(panel, "Maximaler Retry-Backoff (hh:mm:ss)",
            schedulerOptions.MaximumRetryBackoff.ToString("c"));
        var regex = Field(panel, "Claude Usage-RegEx", @"Current session:\s*(?<used>\d+)%\s*used", 720);
        var sample = Field(panel, "RegEx-Testausgabe", "Current session: 42% used", 720);
        var appServer = Field(panel, "Codex App-Server-Argumente (JSON)", "[\"app-server\",\"--listen\",\"stdio://\"]", 720);
        var save = new Button { Text = "Einstellungen prüfen und speichern", AutoSize = true };
        save.Click += async (_, _) => await UiAction(async () =>
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
            if (!System.Text.RegularExpressions.Regex.IsMatch(sample.Text, regex.Text,
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(500)))
                throw new InvalidOperationException("Der reguläre Ausdruck passt nicht auf die Testausgabe.");
            _ = System.Text.Json.JsonSerializer.Deserialize<string[]>(appServer.Text)
                ?? throw new InvalidOperationException("Die App-Server-Argumente sind kein gültiges JSON-Array.");
            await ui.SaveSettingAsync("Scheduler.PollInterval", p.ToString("c"));
            await ui.SaveSettingAsync("Scheduler.ExecutionTimeout", t.ToString("c"));
            await ui.SaveSettingAsync("Scheduler.HistoryUsageEventInterval", h.ToString("c"));
            await ui.SaveSettingAsync("Scheduler.MaximumAttempts", attempts.ToString());
            await ui.SaveSettingAsync("Scheduler.RetryBackoff", backoff.ToString("c"));
            await ui.SaveSettingAsync("Scheduler.MaximumRetryBackoff", maximumBackoff.ToString("c"));
            await ui.SaveSettingAsync("Claude.Usage.Pattern", regex.Text);
            await ui.SaveSettingAsync("Codex.AppServerArguments", appServer.Text);
            schedulerOptions.HistoryUsageEventInterval = h;
            await LoadHistoryAsync();
            MessageBox.Show(this, "Die gültigen Einstellungen wurden gespeichert und gelten nach dem nächsten Start.", "Einstellungen");
        });
        panel.Controls.Add(save); var page = new TabPage("Einstellungen"); page.Controls.Add(panel); return page;
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
                    p.Profile.ShowUsageInStatusBar)))
                .ToList();
            usageState.Text = ProfileUsageStatusFormatter.Format(usage, DateTimeOffset.UtcNow);
            usageState.Visible = usageState.Text.Length > 0;
            runningState.Text = $"Laufende Aufträge: {scheduler.RunningCount}";
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
                    row.Item.Effort.Value, row.Project, row.Usage, row.Status, row.Item.Title, row.Reason);
                queue.Rows[index].Tag = row.Item;
                if (row.Item.Id == selected) selectedRow = queue.Rows[index];
                if (row.Item.Status == WorkItemStatus.WartetAufUsage
                    || row.Status == WorkItemDisplayStatus.ProjektAngehalten.ToString())
                    queue.Rows[index].DefaultCellStyle.BackColor = Color.MistyRose;
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
    { var item = SelectedItem(); if (item is null || data is null || !item.CanEdit) return; var projectRoot = item.ProjectId is { } id && data.Projects.TryGetValue(id, out var p) ? p.RootPath : null; var prompt = projectRoot is null ? item.PromptPath.Value : Path.GetFullPath(item.PromptPath.Value, projectRoot); var model = new WorkItemEditModel(item.Title, Math.Clamp(item.Priority.Value + delta, 0, 100), item.PlatformId.Value, item.ModelId.Value, item.Effort.Value, prompt, item.AutoCommit, item.CommitMessage, item.ProjectId, item.PlatformProfileId); await UiAction(async () => { await ui.SaveWorkItemAsync(model, item); await RefreshAsync(); }); }
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
        schedulerPauseButton.Text = paused ? "Verarbeitung fortsetzen" : "Verarbeitung pausieren";
        schedulerPauseButton.BackColor = paused ? Color.Firebrick : SystemColors.Control;
        schedulerPauseButton.ForeColor = paused ? Color.White : SystemColors.ControlText;
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

    private void RestoreFromTray() { Show(); WindowState = FormWindowState.Normal; Activate(); }
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
    private static ToolStripButton Button(string text, Action action) { var b = new ToolStripButton(text); b.Click += (_, _) => action(); return b; }
    private static ToolStripButton Button(string text, Func<Task> action) { var b = new ToolStripButton(text); b.Click += async (_, _) => await action(); return b; }
    private static TabPage Page(string title, Control content, params Control[] top) { var page = new TabPage(title); content.Dock = DockStyle.Fill; page.Controls.Add(content); foreach (var c in top.Reverse()) { c.Dock = DockStyle.Top; page.Controls.Add(c); } return page; }
    private static DataGridView Grid() => new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowHeadersVisible = false, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders };
    private static DataGridViewTextBoxColumn TextColumn(string title, string name, int width) => new() { HeaderText = title, Name = name, Width = width, SortMode = DataGridViewColumnSortMode.Automatic };
    private static TextBox Field(Control parent, string label, string value, int width = 300) { parent.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 14, 3, 2) }); var box = new TextBox { Text = value, Width = width }; parent.Controls.Add(box); return box; }

    private readonly record struct PolicySelectionKey(string PlatformId, string? ModelId, int Days,
        long LocalStartTicks, long LocalEndTicks, string TimeZoneId, decimal MaxUsedPercent,
        UnknownUsageBehavior UnknownUsageBehavior, long RefreshIntervalTicks, long? EndSprintDurationTicks,
        decimal? EndSprintMaxUsedPercent);
}
