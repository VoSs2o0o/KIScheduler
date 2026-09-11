using KIScheduler.Core.Domain;
using KIScheduler.Core.Scheduling;

namespace KIScheduler.WinForms;

public sealed class MainForm : Form
{
    private readonly SchedulerUiService ui;
    private readonly ISchedulerEngine scheduler;
    private readonly DataGridView queue = Grid();
    private readonly DataGridView platformGrid = Grid();
    private readonly DataGridView blockGrid = Grid();
    private readonly DataGridView attemptsGrid = Grid();
    private readonly DataGridView eventsGrid = Grid();
    private readonly DataGridView policyGrid = Grid();
    private readonly TextBox filter = new() { PlaceholderText = "Titel, Projekt oder Begründung filtern …", Dock = DockStyle.Fill };
    private readonly ComboBox statusFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly ToolStripStatusLabel workerState = new();
    private readonly ToolStripStatusLabel runningState = new();
    private readonly NotifyIcon tray = new() { Icon = SystemIcons.Application, Text = "KIScheduler", Visible = true };
    private readonly System.Windows.Forms.Timer refreshTimer = new() { Interval = 2500 };
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private DashboardData? data;
    private bool allowClose;

    public MainForm(SchedulerUiService ui, ISchedulerEngine scheduler)
    {
        this.ui = ui;
        this.scheduler = scheduler;
        Text = "KIScheduler";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 650);
        ClientSize = new Size(1320, 780);
        Icon = SystemIcons.Application;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildQueuePage());
        tabs.TabPages.Add(BuildPlatformPage());
        tabs.TabPages.Add(BuildBlocksPage());
        tabs.TabPages.Add(BuildHistoryPage());
        tabs.TabPages.Add(BuildPoliciesPage());
        tabs.TabPages.Add(BuildSettingsPage());
        var status = new StatusStrip();
        status.Items.AddRange([workerState, new ToolStripStatusLabel { Spring = true }, runningState]);
        Controls.Add(tabs);
        Controls.Add(status);

        ConfigureTray();
        Shown += async (_, _) => { await RefreshAsync(true); refreshTimer.Start(); };
        refreshTimer.Tick += async (_, _) => await RefreshAsync();
        queue.SelectionChanged += async (_, _) => await LoadHistoryAsync();
        queue.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditSelectedAsync(); };
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
            TextColumn("Modell", "Model", 135), TextColumn("Effort", "Effort", 70),
            TextColumn("Projekt", "Project", 150), TextColumn("Usage", "Usage", 190),
            TextColumn("Status", "Status", 150), TextColumn("Titel", "Title", 230),
            TextColumn("Begründung", "Reason", 340));
        var tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        tools.Items.Add(Button("Neu", async () => await EditWorkItemAsync(null)));
        tools.Items.Add(Button("Neues Projekt", async () => await CreateProjectAsync()));
        tools.Items.Add(Button("Bearbeiten", async () => await EditSelectedAsync()));
        tools.Items.Add(new ToolStripSeparator());
        tools.Items.Add(Button("Priorität +", async () => await ChangePriorityAsync(5)));
        tools.Items.Add(Button("Priorität −", async () => await ChangePriorityAsync(-5)));
        tools.Items.Add(Button("Pausieren/Fortsetzen", async () => await ToggleItemPauseAsync()));
        tools.Items.Add(Button("Abbrechen", async () => await CancelSelectedAsync()));
        tools.Items.Add(Button("Erneut einreihen", async () => await RequeueSelectedAsync()));
        tools.Items.Add(new ToolStripSeparator());
        tools.Items.Add(Button("Usage aktualisieren", async () => await RefreshAsync(true)));
        tools.Items.Add(Button("Verarbeitung pausieren", ToggleSchedulerPause));
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
        platformGrid.Columns.AddRange(TextColumn("Plattform", "Platform", 110), TextColumn("Zustand", "Health", 130),
            TextColumn("Details", "Details", 260), TextColumn("Verbrauch und Reset", "Usage", 430),
            TextColumn("Wirksame Grenzwerte", "Limits", 400));
        var tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        tools.Items.Add(Button("Jetzt prüfen", async () => await RefreshAsync(true)));
        tools.Items.Add(Button("Plattform bearbeiten", async () => await EditPlatformAsync()));
        return Page("Plattformen & Usage", platformGrid, tools);
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
        attemptsGrid.Columns.AddRange(TextColumn("Nr.", "Number", 50), TextColumn("Start", "Start", 145),
            TextColumn("Ende", "End", 145), TextColumn("Ergebnis", "Result", 170), TextColumn("Exit", "Exit", 55),
            TextColumn("Sitzung", "Session", 240), TextColumn("Diagnose", "Diagnostic", 430));
        eventsGrid.Columns.AddRange(TextColumn("Zeit", "Time", 145), TextColumn("Stufe", "Severity", 90),
            TextColumn("Typ/Stream", "Type", 200), TextColumn("Meldung", "Message", 700));
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 220 };
        split.Panel1.Controls.Add(attemptsGrid); split.Panel2.Controls.Add(eventsGrid);
        var tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        tools.Items.Add(new ToolStripLabel("Live-Ereignisse und Verlauf des markierten Auftrags"));
        tools.Items.Add(Button("Fortsetzungsbefehl kopieren", CopyResumeCommand));
        return Page("Live & Verlauf", split, tools);
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
        var poll = Field(panel, "Scheduler-Polling (hh:mm:ss)", "00:00:10");
        var timeout = Field(panel, "Auftrags-Timeout (hh:mm:ss)", "02:00:00");
        var regex = Field(panel, "Claude Usage-RegEx", @"Current session:\s*(?<used>\d+)%\s*used", 720);
        var sample = Field(panel, "RegEx-Testausgabe", "Current session: 42% used", 720);
        var appServer = Field(panel, "Codex App-Server-Argumente (JSON)", "[\"app-server\",\"--listen\",\"stdio://\"]", 720);
        var save = new Button { Text = "Einstellungen prüfen und speichern", AutoSize = true };
        save.Click += async (_, _) => await UiAction(async () =>
        {
            if (!TimeSpan.TryParse(poll.Text, out var p) || p <= TimeSpan.Zero) throw new InvalidOperationException("Das Polling-Intervall ist ungültig.");
            if (!TimeSpan.TryParse(timeout.Text, out var t) || t <= TimeSpan.Zero) throw new InvalidOperationException("Das Auftrags-Timeout ist ungültig.");
            SchedulerUiService.ValidateRegex(regex.Text);
            if (!System.Text.RegularExpressions.Regex.IsMatch(sample.Text, regex.Text,
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(500)))
                throw new InvalidOperationException("Der reguläre Ausdruck passt nicht auf die Testausgabe.");
            _ = System.Text.Json.JsonSerializer.Deserialize<string[]>(appServer.Text)
                ?? throw new InvalidOperationException("Die App-Server-Argumente sind kein gültiges JSON-Array.");
            await ui.SaveSettingAsync("Scheduler.PollInterval", p.ToString("c"));
            await ui.SaveSettingAsync("Scheduler.ExecutionTimeout", t.ToString("c"));
            await ui.SaveSettingAsync("Claude.Usage.Pattern", regex.Text);
            await ui.SaveSettingAsync("Codex.AppServerArguments", appServer.Text);
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
            ApplyQueueRows(); ApplyPlatformRows(); ApplyBlockRows(); ApplyPolicyRows();
            workerState.Text = scheduler.IsPaused ? "Verarbeitung: pausiert" : "Verarbeitung: aktiv";
            runningState.Text = $"Laufende Aufträge: {scheduler.RunningCount}";
            tray.Text = $"KIScheduler – {scheduler.RunningCount} laufend";
        }
        catch (Exception exception) { workerState.Text = $"Aktualisierung fehlgeschlagen: {exception.Message}"; }
        finally { refreshGate.Release(); }
    }

    private void ApplyQueueRows()
    {
        var selected = SelectedItem()?.Id; queue.Rows.Clear(); if (data is null) return;
        var term = filter.Text.Trim(); var status = statusFilter.SelectedIndex <= 0 ? null : statusFilter.SelectedItem?.ToString();
        foreach (var row in data.Queue.Where(x => (status is null || x.Status == status) &&
            (term.Length == 0 || $"{x.Item.Title} {x.Project} {x.Reason}".Contains(term, StringComparison.CurrentCultureIgnoreCase))))
        {
            var index = queue.Rows.Add(row.Item.Priority.Value, row.Item.PlatformId.Value, row.Item.ModelId.Value,
                row.Item.Effort.Value, row.Project, row.Usage, row.Status, row.Item.Title, row.Reason);
            queue.Rows[index].Tag = row.Item; if (row.Item.Id == selected) queue.Rows[index].Selected = true;
            if (row.Item.Status == WorkItemStatus.WartetAufUsage || row.Status == WorkItemDisplayStatus.ProjektAngehalten.ToString())
                queue.Rows[index].DefaultCellStyle.BackColor = Color.MistyRose;
        }
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
            var index = platformGrid.Rows.Add(row.Definition.Id.Value, row.Health.Status, row.Health.Message ?? "", usage, row.EffectiveLimits);
            platformGrid.Rows[index].Tag = row.Definition;
            if (row.Definition.Id.Value.Equals(selected, StringComparison.OrdinalIgnoreCase))
                platformGrid.CurrentCell = platformGrid.Rows[index].Cells[0];
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
        policyGrid.Rows.Clear(); if (data is null) return;
        foreach (var p in data.Policies)
        { var sprint = p.EndSprintDuration.HasValue ? $"{p.EndSprintDuration:g} → {p.EndSprintMaxUsedPercent}" : "—"; var i = policyGrid.Rows.Add(p.PlatformId.Value, p.ModelId?.Value ?? "alle", string.Join(", ", p.Days.Select(x => x.ToString()[..2])), $"{p.LocalStart:HH:mm}–{p.LocalEnd:HH:mm}", p.MaxUsedPercent, sprint, p.UnknownUsageBehavior, p.RefreshInterval, p.TimeZoneId); policyGrid.Rows[i].Tag = p; }
    }

    private async Task LoadHistoryAsync()
    {
        var item = SelectedItem(); attemptsGrid.Rows.Clear(); eventsGrid.Rows.Clear(); if (item is null) return;
        try { var result = await ui.GetHistoryAsync(item.Id); foreach (var x in result.Attempts) attemptsGrid.Rows.Add(x.SequenceNumber, x.StartedAtUtc.ToLocalTime().ToString("g"), x.CompletedAtUtc.ToLocalTime().ToString("g"), x.Result, x.ExitCode, x.SessionId, x.Diagnostic); foreach (var x in result.Events.Reverse()) eventsGrid.Rows.Add(x.OccurredAtUtc.ToLocalTime().ToString("g"), x.Severity, x.EventType, x.Message); }
        catch (Exception exception) { workerState.Text = exception.Message; }
    }

    private async Task EditWorkItemAsync(WorkItem? item)
    {
        if (data is null) return; using var dialog = new WorkItemDialog(data.Platforms.Select(x => x.Definition).ToList(), item, data.Projects);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await UiAction(async () => { try { await ui.SaveWorkItemAsync(dialog.Value, item); } catch (ProjectRootRequiredException exception) { using var folder = new FolderBrowserDialog { Description = exception.Message, UseDescriptionForTitle = true }; if (folder.ShowDialog(this) != DialogResult.OK) return; if (MessageBox.Show(this, $"'{folder.SelectedPath}' wirklich als Projektroot verwenden?", "Projektroot bestätigen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; await ui.SaveWorkItemAsync(dialog.Value with { ConfirmedProjectRoot = folder.SelectedPath }, item); } await RefreshAsync(); });
    }

    private Task EditSelectedAsync() => SelectedItem() is { } item ? EditWorkItemAsync(item) : Task.CompletedTask;
    private async Task ChangePriorityAsync(int delta)
    { var item = SelectedItem(); if (item is null || data is null) return; var project = item.ProjectId is { } id && data.Projects.TryGetValue(id, out var p) ? p.RootPath : null; var prompt = project is null ? item.PromptPath.Value : Path.GetFullPath(item.PromptPath.Value, project); var model = new WorkItemEditModel(item.Title, Math.Clamp(item.Priority.Value + delta, 0, 100), item.PlatformId.Value, item.ModelId.Value, item.Effort.Value, prompt, item.AutoCommit, item.CommitMessage, project); await UiAction(async () => { await ui.SaveWorkItemAsync(model, item); await RefreshAsync(); }); }
    private async Task ToggleItemPauseAsync() { var item = SelectedItem(); if (item is null) return; await UiAction(async () => { await ui.SetPausedAsync(item, item.Status != WorkItemStatus.Pausiert); await RefreshAsync(); }); }
    private async Task CancelSelectedAsync() { var item = SelectedItem(); if (item is null || MessageBox.Show(this, $"Auftrag '{item.Title}' kontrolliert abbrechen?", "Abbrechen bestätigen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; await UiAction(async () => { if (!await ui.CancelAsync(item)) throw new InvalidOperationException("Der Auftrag kann nicht abgebrochen werden."); await RefreshAsync(); }); }
    private async Task RequeueSelectedAsync() { var item = SelectedItem(); if (item is null) return; await UiAction(async () => { await ui.RequeueAsync(item); await RefreshAsync(); }); }
    private void ToggleSchedulerPause() { if (scheduler.IsPaused) scheduler.Resume(); else scheduler.Pause(); workerState.Text = scheduler.IsPaused ? "Verarbeitung: pausiert" : "Verarbeitung: aktiv"; }

    private async Task ReleaseSelectedHoldAsync()
    { if (blockGrid.CurrentRow?.Tag is not ProjectExecutionHold hold) { MessageBox.Show(this, "Bitte einen Projekt-Hold auswählen."); return; } if (MessageBox.Show(this, "Der Arbeitsbaum kann teilweise verändert sein. Haben Sie ihn geprüft und möchten Sie den Hold bewusst freigeben?", "Manuelle Freigabe", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; await UiAction(async () => { await ui.ReleaseHoldAsync(hold); await RefreshAsync(); }); }
    private async Task EditPlatformAsync() { if (platformGrid.CurrentRow?.Tag is not PlatformDefinition platform) return; using var dialog = new PlatformDialog(platform); if (dialog.ShowDialog(this) != DialogResult.OK) return; await UiAction(async () => { await ui.SavePlatformAsync(dialog.Value); await RefreshAsync(true); }); }
    private async Task CreateProjectAsync() { using var dialog = new NewProjectDialog(); if (dialog.ShowDialog(this) != DialogResult.OK) return; await UiAction(async () => { var result = await ui.CreateProjectAsync(dialog.Value); if (!result.Succeeded) throw new InvalidOperationException(result.Message); MessageBox.Show(this, result.Message, "Projekt erstellt"); await RefreshAsync(); }); }
    private async Task EditPolicyAsync(UsagePolicy? policy) { if (data is null) return; using var dialog = new UsagePolicyDialog(data.Platforms.Select(x => x.Definition).ToList(), policy); if (dialog.ShowDialog(this) != DialogResult.OK) return; var values = data.Policies.ToList(); if (policy is not null) values.Remove(policy); values.Add(dialog.Value); await UiAction(async () => { await ui.SavePoliciesAsync(values); await RefreshAsync(); }); }
    private Task EditSelectedPolicyAsync() => policyGrid.CurrentRow?.Tag is UsagePolicy p ? EditPolicyAsync(p) : Task.CompletedTask;
    private async Task RemoveSelectedPolicyAsync() { if (data is null || policyGrid.CurrentRow?.Tag is not UsagePolicy p) return; await UiAction(async () => { await ui.SavePoliciesAsync(data.Policies.Where(x => !ReferenceEquals(x, p)).ToList()); await RefreshAsync(); }); }

    private void CopyResumeCommand()
    { if (attemptsGrid.SelectedRows.Count == 0) return; var session = attemptsGrid.SelectedRows[0].Cells[5].Value?.ToString(); var item = SelectedItem(); if (string.IsNullOrWhiteSpace(session) || item is null) return; Clipboard.SetText(item.PlatformId.Value.Equals("codex", StringComparison.OrdinalIgnoreCase) ? $"codex exec resume {session} -" : $"claude --resume {session}"); workerState.Text = "Fortsetzungsbefehl wurde kopiert."; }

    private void ConfigureTray()
    {
        var menu = new ContextMenuStrip(); menu.Items.Add("Öffnen", null, (_, _) => RestoreFromTray()); menu.Items.Add("Verarbeitung pausieren/fortsetzen", null, (_, _) => ToggleSchedulerPause()); var running = new ToolStripMenuItem("Laufende Aufträge"); menu.Items.Add(running);
        menu.Opening += (_, _) => { running.DropDownItems.Clear(); var items = data?.Queue.Where(x => x.Item.Status is WorkItemStatus.Reserviert or WorkItemStatus.InBearbeitung).ToList() ?? []; if (items.Count == 0) running.DropDownItems.Add("Keine").Enabled = false; foreach (var x in items) running.DropDownItems.Add($"{x.Item.PlatformId}: {x.Item.Title}").Enabled = false; };
        menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Beenden", null, async (_, _) => await ExitAsync()); tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void RestoreFromTray() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    private void OnFormClosing(object? sender, FormClosingEventArgs e) { if (allowClose) return; e.Cancel = true; Hide(); tray.ShowBalloonTip(1500, "KIScheduler", "Die Verarbeitung läuft im Infobereich weiter.", ToolTipIcon.Info); }
    private async Task ExitAsync() { if (scheduler.RunningCount > 0 && MessageBox.Show(this, $"{scheduler.RunningCount} Auftrag/Aufträge laufen. Beim Beenden werden die Prozessbäume kontrolliert beendet. Fortfahren?", "KIScheduler beenden", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; allowClose = true; refreshTimer.Stop(); Enabled = false; using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35)); try { await scheduler.StopAsync(timeout.Token); } catch (OperationCanceledException) { MessageBox.Show(this, "Die Prozesse konnten nicht rechtzeitig beendet werden.", "Beenden", MessageBoxButtons.OK, MessageBoxIcon.Error); allowClose = false; Enabled = true; refreshTimer.Start(); return; } tray.Visible = false; Close(); }

    private WorkItem? SelectedItem() => queue.CurrentRow?.Tag as WorkItem;
    private string WorkItemName(WorkItemId id) => data?.Queue.FirstOrDefault(x => x.Item.Id == id)?.Item.Title ?? id.ToString();
    private async Task UiAction(Func<Task> action) { try { await action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "KIScheduler", MessageBoxButtons.OK, MessageBoxIcon.Error); } }
    private static ToolStripButton Button(string text, Action action) { var b = new ToolStripButton(text); b.Click += (_, _) => action(); return b; }
    private static ToolStripButton Button(string text, Func<Task> action) { var b = new ToolStripButton(text); b.Click += async (_, _) => await action(); return b; }
    private static TabPage Page(string title, Control content, params Control[] top) { var page = new TabPage(title); content.Dock = DockStyle.Fill; page.Controls.Add(content); foreach (var c in top.Reverse()) { c.Dock = DockStyle.Top; page.Controls.Add(c); } return page; }
    private static DataGridView Grid() => new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowHeadersVisible = false, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders };
    private static DataGridViewTextBoxColumn TextColumn(string title, string name, int width) => new() { HeaderText = title, Name = name, Width = width, SortMode = DataGridViewColumnSortMode.Automatic };
    private static TextBox Field(Control parent, string label, string value, int width = 300) { parent.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 14, 3, 2) }); var box = new TextBox { Text = value, Width = width }; parent.Controls.Add(box); return box; }
}
