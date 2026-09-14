using KIScheduler.Core.Domain;

namespace KIScheduler.WinForms;

internal sealed class WorkItemDialog : Form
{
    private readonly IReadOnlyList<PlatformRow> platforms;
    private readonly TextBox title = new();
    private readonly NumericUpDown priority = new() { Minimum = 0, Maximum = 100, Value = 50 };
    private readonly ComboBox platform = Combo();
    private readonly ComboBox profile = Combo();
    private readonly ComboBox model = Combo();
    private readonly ComboBox effort = Combo();
    private readonly ComboBox project = Combo();
    private readonly TextBox prompt = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly CheckBox autoCommit = new() { Text = "Nach Erfolg automatisch committen", AutoSize = true };
    private readonly TextBox commitMessage = new();

    public WorkItemDialog(IReadOnlyList<PlatformRow> platforms, WorkItem? item,
        IReadOnlyDictionary<ProjectId, ProjectDefinition> projects, bool duplicate = false)
    {
        this.platforms = platforms;
        var readOnly = item is not null && !duplicate && !item.CanEdit;
        Text = readOnly ? "Auftrag anzeigen" : duplicate ? "Auftrag duplizieren"
            : item is null ? "Auftrag anlegen" : "Auftrag bearbeiten";
        Width = 740; Height = 420; StartPosition = FormStartPosition.CenterParent;
        var form = CreateLayout();
        AddRow(form, "Titel", title); AddRow(form, "Priorität", priority); AddRow(form, "Plattform", platform);
        AddRow(form, "Profil", profile); AddRow(form, "Modell", model); AddRow(form, "Effort", effort);
        foreach (var value in projects.Values.OrderBy(x => x.Name)) project.Items.Add(new ProjectOption(value));
        project.DropDownWidth = 520;
        project.SelectedIndexChanged += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(prompt.Text) && !PromptBelongsToSelectedProject(prompt.Text))
                prompt.Clear();
        };
        AddRow(form, "Projekt", project);
        var promptPanel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 };
        promptPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        promptPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        promptPanel.Controls.Add(prompt, 0, 0); var choosePrompt = new Button { Text = "Datei wählen …", AutoSize = true };
        choosePrompt.Click += (_, _) =>
        {
            var selectedProject = SelectedProject;
            if (selectedProject is null)
            {
                MessageBox.Show(this, "Bitte zuerst ein Projekt auswählen.");
                return;
            }
            using var dialog = new OpenFileDialog
            {
                Filter = "Markdown-Prompts (*.md)|*.md|Alle Dateien (*.*)|*.*",
                CheckFileExists = true,
                InitialDirectory = selectedProject.RootPath,
                RestoreDirectory = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (!IsPathWithinRoot(dialog.FileName, selectedProject.RootPath))
            {
                MessageBox.Show(this, "Die Prompt-Datei muss innerhalb des gewählten Projektroots liegen.");
                return;
            }
            prompt.Text = dialog.FileName;
        };
        promptPanel.Controls.Add(choosePrompt, 1, 0); AddRow(form, "Prompt-Datei", promptPanel);
        AddRow(form, "", autoCommit); AddRow(form, "Commitnachricht (optional)", commitMessage);
        Controls.Add(form); Controls.Add(Buttons(OnAccept, readOnly));
        foreach (var p in platforms) platform.Items.Add(new PlatformOption(p));
        platform.SelectedIndexChanged += (_, _) => { LoadProfiles(); LoadModels(); };
        profile.SelectedIndexChanged += (_, _) => { };
        model.SelectedIndexChanged += (_, _) => LoadEfforts();
        if (item is null) { if (platform.Items.Count > 0) platform.SelectedIndex = 0; }
        else
        {
            title.Text = item.Title; priority.Value = item.Priority.Value;
            platform.SelectedItem = platform.Items.Cast<PlatformOption>()
                .FirstOrDefault(x => x.Row.Definition.Id.Value.Equals(item.PlatformId.Value,
                    StringComparison.OrdinalIgnoreCase));
            if (platform.SelectedIndex < 0 && platform.Items.Count > 0) platform.SelectedIndex = 0;
            LoadProfiles();
            profile.SelectedItem = profile.Items.Cast<ProfileOption>()
                .FirstOrDefault(x => x.Profile.Id == item.PlatformProfileId);
            LoadModels(); model.SelectedItem = item.ModelId.Value; LoadEfforts(); effort.SelectedItem = item.Effort.Value;
            autoCommit.Checked = item.AutoCommit; commitMessage.Text = item.CommitMessage ?? "";
            var knownProject = item.ProjectId is { } id && projects.TryGetValue(id, out var project) ? project : null;
            if (knownProject is not null)
                this.project.SelectedItem = this.project.Items.Cast<ProjectOption>()
                    .First(x => x.Definition.Id == knownProject.Id);
            prompt.Text = knownProject is null ? item.PromptPath.Value
                : Path.GetFullPath(item.PromptPath.Value, knownProject.RootPath);
        }
        if (item is null && project.Items.Count > 0) project.SelectedIndex = 0;
        form.Enabled = !readOnly;
    }

    public WorkItemEditModel Value => new(title.Text.Trim(), (int)priority.Value, SelectedPlatform?.Definition.Id.Value ?? "",
        model.Text, effort.Text, prompt.Text.Trim(), autoCommit.Checked, commitMessage.Text.Trim(),
        SelectedProject?.Id, SelectedProfile?.Id);

    private ProjectDefinition? SelectedProject => (project.SelectedItem as ProjectOption)?.Definition;
    private PlatformRow? SelectedPlatform => (platform.SelectedItem as PlatformOption)?.Row;
    private PlatformProfile? SelectedProfile => (profile.SelectedItem as ProfileOption)?.Profile;

    private bool PromptBelongsToSelectedProject(string path) =>
        SelectedProject is { } selected && IsPathWithinRoot(path, selected.RootPath);

    private static bool IsPathWithinRoot(string path, string root)
    {
        try
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
            return !Path.IsPathRooted(relative) && relative != ".."
                && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private void LoadModels()
    {
        var selected = model.Text; model.Items.Clear();
        var value = SelectedPlatform?.Definition;
        if (value is null) return; foreach (var m in value.Models) model.Items.Add(m.Id.Value);
        model.SelectedItem = selected; if (model.SelectedIndex < 0 && model.Items.Count > 0) model.SelectedIndex = 0;
    }
    private void LoadEfforts()
    {
        var selected = effort.Text; effort.Items.Clear();
        var value = SelectedPlatform?.Definition
            .Models.FirstOrDefault(x => x.Id.Value.Equals(model.Text, StringComparison.OrdinalIgnoreCase));
        if (value is null) return; foreach (var e in value.SupportedEfforts) effort.Items.Add(e.Value);
        effort.SelectedItem = selected; if (effort.SelectedIndex < 0 && effort.Items.Count > 0) effort.SelectedIndex = 0;
    }
    private void OnAccept()
    {
        if (string.IsNullOrWhiteSpace(title.Text) || SelectedPlatform is null || SelectedProfile is null
            || string.IsNullOrWhiteSpace(model.Text) || string.IsNullOrWhiteSpace(effort.Text)
            || SelectedProject is null || !File.Exists(prompt.Text) || !PromptBelongsToSelectedProject(prompt.Text))
        {
            MessageBox.Show(this,
                "Bitte Titel, feste Ausführungsauswahl, ein Projekt und eine Prompt-Datei innerhalb dieses Projekts angeben.");
            return;
        }
        DialogResult = DialogResult.OK;
    }

    internal static TableLayoutPanel CreateLayout()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 0
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return panel;
    }
    internal static void AddRow(TableLayoutPanel panel, string label, Control control)
    { var row = panel.RowCount++; panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 12, 3) }, 0, row); control.Anchor = AnchorStyles.Left | AnchorStyles.Right; control.Margin = new Padding(3, 5, 3, 3); panel.Controls.Add(control, 1, row); }
    internal static FlowLayoutPanel Buttons(Action accept, bool readOnly = false)
    { var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) }; var ok = new Button { Text = "Speichern", AutoSize = true, Enabled = !readOnly }; var cancel = new Button { Text = readOnly ? "Schließen" : "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true }; ok.Click += (_, _) => accept(); panel.Controls.Add(ok); panel.Controls.Add(cancel); return panel; }
    private static ComboBox Combo() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };

    private void LoadProfiles()
    {
        var selected = (profile.SelectedItem as ProfileOption)?.Profile.Id;
        profile.Items.Clear();
        var available = SelectedPlatform is { } selectedPlatform
            ? selectedPlatform.Profiles.Where(x => x.Profile.Enabled)
                .OrderByDescending(x => x.Profile.IsDefault).ThenBy(x => x.Profile.DisplayName)
            : Enumerable.Empty<ProfileRow>();
        foreach (var value in available)
            profile.Items.Add(new ProfileOption(value.Profile));
        var selectedOption = profile.Items.Cast<ProfileOption>()
            .FirstOrDefault(x => selected is not null && x.Profile.Id == selected.Value)
            ?? profile.Items.Cast<ProfileOption>().FirstOrDefault(x => x.Profile.IsDefault);
        profile.SelectedItem = selectedOption;
    }

    private sealed record PlatformOption(PlatformRow Row)
    {
        public override string ToString() => Row.Definition.Id.Value;
    }

    private sealed record ProfileOption(PlatformProfile Profile)
    {
        public override string ToString() => Profile.IsDefault
            ? $"{Profile.DisplayName} ({Profile.ConfigurationDirectory})"
            : $"{Profile.DisplayName} ({Profile.Name})";
    }

    private sealed record ProjectOption(ProjectDefinition Definition)
    {
        public override string ToString() => Definition.Name;
    }
}

internal sealed class PlatformDialog : Form
{
    private readonly PlatformDefinition original;
    private readonly TextBox executable = new();
    private readonly TextBox models = new() { Multiline = true, Height = 150, ScrollBars = ScrollBars.Vertical };
    private readonly CheckBox enabled = new() { Text = "Plattform aktiviert", AutoSize = true };
    private readonly DataGridView profiles = ProfileGrid();
    public ProfileAction RequestedAction { get; private set; }
    public PlatformProfile? SelectedProfile { get; private set; }

    public PlatformDialog(PlatformDefinition platform, IReadOnlyList<ProfileRow> profileRows)
    {
        original = platform; Text = $"Plattform {platform.Id.Value} und Profile"; Width = 1180; Height = 700;
        StartPosition = FormStartPosition.CenterParent;
        var form = WorkItemDialog.CreateLayout();
        WorkItemDialog.AddRow(form, "Plattform-ID", new TextBox { Text = platform.Id.Value, ReadOnly = true });
        enabled.Checked = platform.Enabled;
        WorkItemDialog.AddRow(form, "", enabled);
        executable.Text = platform.Executable;
        var executablePanel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        executable.Width = 420; executablePanel.Controls.Add(executable);
        var chooseExecutable = new Button { Text = "Datei wählen …", AutoSize = true };
        chooseExecutable.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog
            {
                Title = $"Executable für {platform.Id.Value} wählen",
                Filter = "Ausführbare Dateien (*.exe;*.cmd;*.bat)|*.exe;*.cmd;*.bat|Alle Dateien (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) == DialogResult.OK) executable.Text = dialog.FileName;
        };
        executablePanel.Controls.Add(chooseExecutable);
        WorkItemDialog.AddRow(form, "CLI-Datei", executablePanel);
        models.Text = string.Join(Environment.NewLine, platform.Models.Select(x =>
            $"{x.Id.Value}: {string.Join(", ", x.SupportedEfforts.Select(e => e.Value))}"));
        WorkItemDialog.AddRow(form, "Modelle (Modell: Effort, …)", models);

        profiles.Columns.AddRange(
            TextColumn("Name", "Name", 145), TextColumn("Konfigurationsordner", "Directory", 380),
            TextColumn("Aktiv", "Enabled", 65), TextColumn("Standard", "Default", 75),
            TextColumn("Usage anzeigen", "ShowUsage", 105), TextColumn("Health", "Health", 120),
            TextColumn("Usage / Details", "Usage", 360));
        foreach (var row in profileRows)
        {
            var usage = row.Usage is null ? "unbekannt" : string.Join(" | ", row.Usage.Windows.Select(x =>
                $"{x.Name}: {x.UsedPercent}; Reset: {(x.ResetAtUtc?.ToLocalTime().ToString("g") ?? "unbekannt")}"));
            if (!string.IsNullOrWhiteSpace(row.UsageMessage)) usage += $" — {row.UsageMessage}";
            var index = profiles.Rows.Add(row.Profile.DisplayName, row.Profile.ConfigurationDirectory,
                row.Profile.Enabled ? "Ja" : "Nein",
                row.Profile.IsDefault && row.Profile.Enabled ? "Ja" : row.Profile.IsDefault ? "historisch" : "Nein",
                row.Profile.ShowUsageInStatusBar ? "Ja" : "Nein",
                row.Health.Status, usage);
            profiles.Rows[index].Tag = row.Profile;
            if (!row.Profile.Enabled) profiles.Rows[index].DefaultCellStyle.ForeColor = Color.Gray;
        }
        profiles.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0) Request(ProfileAction.Bearbeiten, profiles.Rows[e.RowIndex].Tag as PlatformProfile);
        };
        var profileTools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        profileTools.Items.Add(ProfileButton("Hinzufügen", ProfileAction.Hinzufuegen));
        profileTools.Items.Add(ProfileButton("Bearbeiten", ProfileAction.Bearbeiten));
        profileTools.Items.Add(ProfileButton("Entfernen/Deaktivieren", ProfileAction.Deaktivieren));
        profileTools.Items.Add(ProfileButton("Anmeldung/Usage prüfen", ProfileAction.Pruefen));
        var profilePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        profilePanel.Controls.Add(profiles); profilePanel.Controls.Add(profileTools);
        var profileRow = form.RowCount;
        WorkItemDialog.AddRow(form, "Profile", profilePanel);
        form.RowStyles[profileRow].SizeType = SizeType.Percent;
        form.RowStyles[profileRow].Height = 100;
        profilePanel.Dock = DockStyle.Fill;
        form.GetControlFromPosition(0, profileRow)!.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        Controls.Add(form); Controls.Add(WorkItemDialog.Buttons(OnAccept));
    }

    public PlatformDefinition Value
    {
        get
        {
            var parsed = models.Lines.Where(x => !string.IsNullOrWhiteSpace(x)).Select(line =>
            {
                var parts = line.Split(':', 2);
                if (parts.Length != 2) throw new FormatException($"Ungültige Modellzeile: {line}");
                return new PlatformModel(new ModelId(parts[0].Trim()),
                    parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(x => new EffortLevel(x)));
            }).ToList();
            return new PlatformDefinition(original.Id, executable.Text, parsed, original.Capacity,
                enabled.Checked, original.ShowUsageInStatusBar);
        }
    }

    private void OnAccept()
    {
        try { _ = Value; DialogResult = DialogResult.OK; }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "Plattform", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void Request(ProfileAction action, PlatformProfile? profile = null)
    {
        RequestedAction = action; SelectedProfile = profile ?? profiles.CurrentRow?.Tag as PlatformProfile;
        DialogResult = DialogResult.Retry;
    }

    private ToolStripButton ProfileButton(string text, ProfileAction action)
    {
        var button = new ToolStripButton(text);
        button.Click += (_, _) => Request(action);
        return button;
    }

    private static DataGridView ProfileGrid() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        RowHeadersVisible = false, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders
    };
    private static DataGridViewTextBoxColumn TextColumn(string title, string name, int width) => new()
        { HeaderText = title, Name = name, Width = width, SortMode = DataGridViewColumnSortMode.Automatic };

    internal enum ProfileAction { None, Hinzufuegen, Bearbeiten, Deaktivieren, Pruefen }
}

internal sealed class PlatformProfileDialog : Form
{
    private readonly PlatformProfile? original;
    private readonly PlatformId platformId;
    private readonly TextBox name = new();
    private readonly TextBox displayName = new();
    private readonly TextBox directory = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly CheckBox showUsage = new() { Text = "Usage dieses Profils in der Statusleiste anzeigen", AutoSize = true };

    public PlatformProfileDialog(PlatformId platformId, PlatformProfile? profile)
    {
        this.platformId = platformId; original = profile;
        Text = profile is null ? "Profil hinzufügen" : $"Profil {profile.DisplayName} bearbeiten";
        Width = 700; Height = 300; StartPosition = FormStartPosition.CenterParent;
        var form = WorkItemDialog.CreateLayout();
        WorkItemDialog.AddRow(form, "Interner Name", name);
        WorkItemDialog.AddRow(form, "Anzeigename", displayName);
        var directoryPanel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 };
        directoryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        directoryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        directoryPanel.Controls.Add(directory, 0, 0);
        var choose = new Button { Text = "Ordner wählen …", AutoSize = true };
        choose.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Konfigurationsordner des Profils auswählen",
                UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(directory.Text) ? directory.Text : ""
            };
            if (dialog.ShowDialog(this) == DialogResult.OK) directory.Text = dialog.SelectedPath;
        };
        directoryPanel.Controls.Add(choose, 1, 0);
        WorkItemDialog.AddRow(form, "Konfigurationsordner", directoryPanel);
        WorkItemDialog.AddRow(form, "", showUsage);
        Controls.Add(form); Controls.Add(WorkItemDialog.Buttons(OnAccept));
        if (profile is null)
        {
            name.Text = "profil"; displayName.Text = "Neues Profil";
        }
        else
        {
            name.Text = profile.Name; displayName.Text = profile.DisplayName;
            directory.Text = profile.ConfigurationDirectory;
            showUsage.Checked = profile.ShowUsageInStatusBar;
            if (profile.IsDefault)
            {
                name.ReadOnly = true; displayName.ReadOnly = true;
                name.Text = PlatformProfile.DefaultName; displayName.Text = PlatformProfile.DefaultDisplayName;
            }
        }
    }

    public PlatformProfile Value => new(original?.Id ?? PlatformProfileId.New(), platformId,
        name.Text.Trim(), displayName.Text.Trim(), directory.Text.Trim(), original?.Enabled ?? true,
        original?.IsDefault ?? false, showUsage.Checked);

    private void OnAccept()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directory.Text))
                throw new InvalidOperationException("Bitte einen Konfigurationsordner auswählen.");
            _ = Value; DialogResult = DialogResult.OK;
        }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "Profil", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}

internal sealed class ProfileSelectionDialog : Form
{
    private readonly ListBox profiles = new() { Dock = DockStyle.Fill };

    public ProfileSelectionDialog(IReadOnlyList<PlatformProfile> values, string title)
    {
        Text = title; Width = 520; Height = 260; StartPosition = FormStartPosition.CenterParent;
        foreach (var value in values.Where(x => x.Enabled && !x.IsDefault).OrderBy(x => x.DisplayName))
            profiles.Items.Add(new ProfileOption(value));
        Controls.Add(profiles); Controls.Add(WorkItemDialog.Buttons(OnAccept));
    }

    public PlatformProfile? Value => (profiles.SelectedItem as ProfileOption)?.Profile;
    private void OnAccept()
    {
        if (Value is null) { MessageBox.Show(this, "Bitte ein Ersatzprofil auswählen."); return; }
        DialogResult = DialogResult.OK;
    }
    private sealed record ProfileOption(PlatformProfile Profile)
    { public override string ToString() => $"{Profile.DisplayName} ({Profile.ConfigurationDirectory})"; }
}

internal sealed class UsagePolicyDialog : Form
{
    private readonly IReadOnlyList<PlatformDefinition> platforms;
    private readonly ComboBox platform = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox model = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckedListBox days = new() { Height = 85, CheckOnClick = true };
    private readonly DateTimePicker start = TimePicker(); private readonly DateTimePicker end = TimePicker();
    private readonly NumericUpDown limit = PercentBox(75); private readonly CheckBox sprint = new() { Text = "Endspurt verwenden", AutoSize = true };
    private readonly NumericUpDown sprintMinutes = new() { Minimum = 1, Maximum = 1440, Value = 30 };
    private readonly NumericUpDown sprintLimit = PercentBox(100);
    private readonly ComboBox unknown = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown polling = new() { Minimum = 1, Maximum = 1440, Value = 1 };
    private readonly ComboBox zone = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };

    public UsagePolicyDialog(IReadOnlyList<PlatformDefinition> platforms, UsagePolicy? value)
    {
        this.platforms = platforms; Text = value is null ? "Usage-Regel hinzufügen" : "Usage-Regel bearbeiten"; Width = 650; Height = 620; StartPosition = FormStartPosition.CenterParent;
        var form = WorkItemDialog.CreateLayout(); foreach (var p in platforms) platform.Items.Add(p.Id.Value); platform.SelectedIndexChanged += (_, _) => LoadModels();
        model.Items.Add("alle"); foreach (var d in Enum.GetValues<DayOfWeek>()) days.Items.Add(d);
        foreach (var b in Enum.GetValues<UnknownUsageBehavior>()) unknown.Items.Add(b); foreach (var z in TimeZoneInfo.GetSystemTimeZones()) zone.Items.Add(z.Id);
        WorkItemDialog.AddRow(form, "Plattform", platform); WorkItemDialog.AddRow(form, "Modell", model); WorkItemDialog.AddRow(form, "Wochentage", days);
        WorkItemDialog.AddRow(form, "Start (inklusive)", start); WorkItemDialog.AddRow(form, "Ende (exklusive)", end); WorkItemDialog.AddRow(form, "Max. Verbrauch %", limit);
        WorkItemDialog.AddRow(form, "", sprint); WorkItemDialog.AddRow(form, "Endspurt-Dauer (Min.)", sprintMinutes); WorkItemDialog.AddRow(form, "Endspurt-Grenze %", sprintLimit);
        WorkItemDialog.AddRow(form, "Unbekannte Usage", unknown); WorkItemDialog.AddRow(form, "Polling (Min.)", polling); WorkItemDialog.AddRow(form, "Zeitzone", zone);
        Controls.Add(form); Controls.Add(WorkItemDialog.Buttons(OnAccept));
        if (value is null)
        { if (platform.Items.Count > 0) platform.SelectedIndex = 0; model.SelectedIndex = 0; for (var i = 0; i < days.Items.Count; i++) days.SetItemChecked(i, true); start.Value = DateTime.Today; end.Value = DateTime.Today.AddHours(23).AddMinutes(59); unknown.SelectedItem = UnknownUsageBehavior.Blockieren; zone.SelectedItem = TimeZoneInfo.Local.Id; }
        else
        { platform.SelectedItem = value.PlatformId.Value; LoadModels(); model.SelectedItem = value.ModelId?.Value ?? "alle"; for (var i = 0; i < days.Items.Count; i++) days.SetItemChecked(i, value.Days.Contains((DayOfWeek)days.Items[i])); start.Value = DateTime.Today + value.LocalStart.ToTimeSpan(); end.Value = DateTime.Today + value.LocalEnd.ToTimeSpan(); limit.Value = value.MaxUsedPercent.Value; sprint.Checked = value.EndSprintDuration.HasValue; if (value.EndSprintDuration.HasValue) sprintMinutes.Value = (decimal)value.EndSprintDuration.Value.TotalMinutes; if (value.EndSprintMaxUsedPercent.HasValue) sprintLimit.Value = value.EndSprintMaxUsedPercent.Value.Value; unknown.SelectedItem = value.UnknownUsageBehavior; polling.Value = (decimal)value.RefreshInterval.TotalMinutes; zone.SelectedItem = value.TimeZoneId; }
    }
    public UsagePolicy Value => new(new PlatformId(platform.Text), days.CheckedItems.Cast<DayOfWeek>(), TimeOnly.FromDateTime(start.Value), TimeOnly.FromDateTime(end.Value), zone.Text, new UsagePercent(limit.Value), (UnknownUsageBehavior)unknown.SelectedItem!, TimeSpan.FromMinutes((double)polling.Value), model.Text == "alle" ? null : new ModelId(model.Text), sprint.Checked ? TimeSpan.FromMinutes((double)sprintMinutes.Value) : null, sprint.Checked ? new UsagePercent(sprintLimit.Value) : null);
    private void LoadModels() { var old = model.Text; model.Items.Clear(); model.Items.Add("alle"); var p = platforms.FirstOrDefault(x => x.Id.Value == platform.Text); if (p is not null) foreach (var m in p.Models) model.Items.Add(m.Id.Value); model.SelectedItem = model.Items.Contains(old) ? old : "alle"; }
    private void OnAccept() { try { _ = Value; DialogResult = DialogResult.OK; } catch (Exception ex) { MessageBox.Show(this, ex.Message); } }
    private static DateTimePicker TimePicker() => new() { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true };
    private static NumericUpDown PercentBox(decimal value) => new() { Minimum = 0, Maximum = 100, DecimalPlaces = 1, Value = value };
}

internal sealed class ProjectDialog : Form
{
    private readonly ProjectDefinition? original;
    private readonly TextBox name = new();
    private readonly TextBox root = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly TextBox branch = new() { Text = "master" };

    public ProjectDialog(ProjectDefinition? project)
    {
        original = project;
        Text = project is null ? "Projekt anlegen" : "Projekt bearbeiten";
        Width = 720; Height = 280; StartPosition = FormStartPosition.CenterParent;
        var form = WorkItemDialog.CreateLayout();
        WorkItemDialog.AddRow(form, "Projektname", name);
        var rootPanel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 };
        rootPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rootPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rootPanel.Controls.Add(root, 0, 0);
        var chooseRoot = new Button { Text = "Ordner wählen …", AutoSize = true };
        chooseRoot.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Vorhandenes Projektroot auswählen",
                UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(root.Text) ? root.Text : ""
            };
            if (dialog.ShowDialog(this) == DialogResult.OK) root.Text = dialog.SelectedPath;
        };
        rootPanel.Controls.Add(chooseRoot, 1, 0);
        WorkItemDialog.AddRow(form, "Projektroot", rootPanel);
        WorkItemDialog.AddRow(form, "Zielbranch", branch);
        Controls.Add(form); Controls.Add(WorkItemDialog.Buttons(OnAccept));
        if (project is not null)
        {
            name.Text = project.Name;
            root.Text = project.RootPath;
            branch.Text = project.TargetBranch;
        }
    }

    public ProjectDefinition Value => new(original?.Id ?? ProjectId.New(), name.Text, root.Text, branch.Text,
        original?.ValidationCommands, original?.DefaultTemplate ?? "classlib");

    private void OnAccept()
    {
        try
        {
            if (!Directory.Exists(root.Text))
                throw new InvalidOperationException("Bitte ein vorhandenes Projektroot auswählen.");
            _ = Value;
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }
}
