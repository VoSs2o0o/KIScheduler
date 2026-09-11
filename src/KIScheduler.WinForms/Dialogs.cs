using KIScheduler.Core.Domain;

namespace KIScheduler.WinForms;

internal sealed class WorkItemDialog : Form
{
    private readonly IReadOnlyList<PlatformDefinition> platforms;
    private readonly TextBox title = new();
    private readonly NumericUpDown priority = new() { Minimum = 0, Maximum = 100, Value = 50 };
    private readonly ComboBox platform = Combo();
    private readonly ComboBox model = Combo();
    private readonly ComboBox effort = Combo();
    private readonly TextBox prompt = new() { Width = 460 };
    private readonly CheckBox autoCommit = new() { Text = "Nach Erfolg automatisch committen", AutoSize = true };
    private readonly TextBox commitMessage = new();

    public WorkItemDialog(IReadOnlyList<PlatformDefinition> platforms, WorkItem? item,
        IReadOnlyDictionary<ProjectId, ProjectDefinition> projects)
    {
        this.platforms = platforms;
        Text = item is null ? "Auftrag anlegen" : "Auftrag bearbeiten";
        Width = 700; Height = 365; StartPosition = FormStartPosition.CenterParent;
        var form = CreateLayout();
        AddRow(form, "Titel", title); AddRow(form, "Priorität", priority); AddRow(form, "Plattform", platform);
        AddRow(form, "Modell", model); AddRow(form, "Effort", effort);
        var promptPanel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        promptPanel.Controls.Add(prompt); var choose = new Button { Text = "Datei wählen …", AutoSize = true };
        choose.Click += (_, _) => { using var dialog = new OpenFileDialog { Filter = "Markdown-Prompts (*.md)|*.md|Alle Dateien (*.*)|*.*", CheckFileExists = true }; if (dialog.ShowDialog(this) == DialogResult.OK) prompt.Text = dialog.FileName; };
        promptPanel.Controls.Add(choose); AddRow(form, "Prompt-Datei", promptPanel); AddRow(form, "", autoCommit); AddRow(form, "Commitnachricht (optional)", commitMessage);
        Controls.Add(form); Controls.Add(Buttons(OnAccept));
        foreach (var p in platforms) platform.Items.Add(p.Id.Value);
        platform.SelectedIndexChanged += (_, _) => LoadModels(); model.SelectedIndexChanged += (_, _) => LoadEfforts();
        if (item is null) { if (platform.Items.Count > 0) platform.SelectedIndex = 0; }
        else
        {
            title.Text = item.Title; priority.Value = item.Priority.Value; platform.SelectedItem = item.PlatformId.Value;
            LoadModels(); model.SelectedItem = item.ModelId.Value; LoadEfforts(); effort.SelectedItem = item.Effort.Value;
            autoCommit.Checked = item.AutoCommit; commitMessage.Text = item.CommitMessage ?? "";
            prompt.Text = item.ProjectId is { } id && projects.TryGetValue(id, out var project)
                ? Path.GetFullPath(item.PromptPath.Value, project.RootPath) : item.PromptPath.Value;
            platform.Enabled = model.Enabled = effort.Enabled = !item.HasExecutionStarted;
        }
    }

    public WorkItemEditModel Value => new(title.Text.Trim(), (int)priority.Value, platform.Text,
        model.Text, effort.Text, prompt.Text.Trim(), autoCommit.Checked, commitMessage.Text.Trim());

    private void LoadModels()
    {
        var selected = model.Text; model.Items.Clear();
        var value = platforms.FirstOrDefault(x => x.Id.Value.Equals(platform.Text, StringComparison.OrdinalIgnoreCase));
        if (value is null) return; foreach (var m in value.Models) model.Items.Add(m.Id.Value);
        model.SelectedItem = selected; if (model.SelectedIndex < 0 && model.Items.Count > 0) model.SelectedIndex = 0;
    }
    private void LoadEfforts()
    {
        var selected = effort.Text; effort.Items.Clear();
        var value = platforms.FirstOrDefault(x => x.Id.Value.Equals(platform.Text, StringComparison.OrdinalIgnoreCase))?
            .Models.FirstOrDefault(x => x.Id.Value.Equals(model.Text, StringComparison.OrdinalIgnoreCase));
        if (value is null) return; foreach (var e in value.SupportedEfforts) effort.Items.Add(e.Value);
        effort.SelectedItem = selected; if (effort.SelectedIndex < 0 && effort.Items.Count > 0) effort.SelectedIndex = 0;
    }
    private void OnAccept()
    {
        if (string.IsNullOrWhiteSpace(title.Text) || string.IsNullOrWhiteSpace(platform.Text)
            || string.IsNullOrWhiteSpace(model.Text) || string.IsNullOrWhiteSpace(effort.Text)
            || !File.Exists(prompt.Text)) { MessageBox.Show(this, "Bitte Titel, feste Ausführungsauswahl und eine vorhandene Prompt-Datei angeben."); return; }
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
    internal static FlowLayoutPanel Buttons(Action accept)
    { var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) }; var ok = new Button { Text = "Speichern", AutoSize = true }; var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true }; ok.Click += (_, _) => accept(); panel.Controls.Add(ok); panel.Controls.Add(cancel); return panel; }
    private static ComboBox Combo() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
}

internal sealed class PlatformDialog : Form
{
    private readonly PlatformDefinition original;
    private readonly TextBox executable = new();
    private readonly TextBox models = new() { Multiline = true, Height = 150, ScrollBars = ScrollBars.Vertical };
    public PlatformDialog(PlatformDefinition platform)
    {
        original = platform; Text = $"Plattform {platform.Id.Value} bearbeiten"; Width = 620; Height = 350; StartPosition = FormStartPosition.CenterParent;
        var form = WorkItemDialog.CreateLayout(); WorkItemDialog.AddRow(form, "Plattform-ID", new TextBox { Text = platform.Id.Value, ReadOnly = true });
        executable.Text = platform.Executable;
        var executablePanel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        executable.Width = 300;
        executablePanel.Controls.Add(executable);
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
        models.Text = string.Join(Environment.NewLine, platform.Models.Select(x => $"{x.Id.Value}: {string.Join(", ", x.SupportedEfforts.Select(e => e.Value))}"));
        WorkItemDialog.AddRow(form, "Modelle (Modell: Effort, …)", models); Controls.Add(form); Controls.Add(WorkItemDialog.Buttons(OnAccept));
    }
    public PlatformDefinition Value
    {
        get
        {
            var parsed = models.Lines.Where(x => !string.IsNullOrWhiteSpace(x)).Select(line =>
            { var parts = line.Split(':', 2); if (parts.Length != 2) throw new FormatException($"Ungültige Modellzeile: {line}"); return new PlatformModel(new ModelId(parts[0].Trim()), parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => new EffortLevel(x))); }).ToList();
            return new PlatformDefinition(original.Id, executable.Text, parsed, original.Capacity);
        }
    }
    private void OnAccept() { try { _ = Value; DialogResult = DialogResult.OK; } catch (Exception ex) { MessageBox.Show(this, ex.Message); } }
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

internal sealed class NewProjectDialog : Form
{
    private readonly TextBox name = new(); private readonly TextBox root = new(); private readonly TextBox template = new() { Text = "classlib" }; private readonly TextBox branch = new() { Text = "master" };
    public NewProjectDialog()
    {
        Text = "Neues Projekt bestätigt anlegen"; Width = 650; Height = 330; StartPosition = FormStartPosition.CenterParent;
        var form = WorkItemDialog.CreateLayout(); WorkItemDialog.AddRow(form, "Projektname", name); WorkItemDialog.AddRow(form, "Neues Projektroot", root); WorkItemDialog.AddRow(form, "dotnet-new-Vorlage", template); WorkItemDialog.AddRow(form, "Zielbranch", branch);
        Controls.Add(form); Controls.Add(WorkItemDialog.Buttons(OnAccept));
    }
    public ProjectDefinition Value => new(ProjectId.New(), name.Text, root.Text, branch.Text, defaultTemplate: template.Text);
    private void OnAccept() { try { if (Directory.Exists(root.Text)) throw new InvalidOperationException("Das Zielverzeichnis muss neu sein; sein übergeordnetes Verzeichnis muss existieren."); _ = Value; if (MessageBox.Show(this, $"Projekt jetzt mit 'dotnet new {template.Text}' unter '{root.Text}' erzeugen?", "Neuanlage bestätigen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) DialogResult = DialogResult.OK; } catch (Exception ex) { MessageBox.Show(this, ex.Message); } }
}
