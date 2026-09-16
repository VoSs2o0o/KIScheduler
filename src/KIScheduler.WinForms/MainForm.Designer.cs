#nullable enable
#pragma warning disable CS8600 // Visual Studio serializes resource casts without nullability annotations.

namespace KIScheduler.WinForms;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = new System.ComponentModel.Container();
    private TabControl tabs = null!;
    private StatusStrip status = null!;
    private ToolStripStatusLabel workerState = null!;
    private ToolStripStatusLabel usageState = null!;
    private ToolStripStatusLabel runningState = null!;
    private TabPage queuePage = null!;
    private TabPage projectsPage = null!;
    private TabPage platformPage = null!;
    private TabPage blocksPage = null!;
    private TabPage historyPage = null!;
    private TabPage policiesPage = null!;
    private TabPage settingsPage = null!;
    private DataGridView queue = null!;
    private DataGridView projectGrid = null!;
    private DataGridView platformGrid = null!;
    private DataGridView blockGrid = null!;
    private DataGridView attemptsGrid = null!;
    private DataGridView eventsGrid = null!;
    private DataGridView policyGrid = null!;
    private DataGridViewTextBoxColumn queuePriorityColumn = null!;
    private DataGridViewTextBoxColumn queuePlatformColumn = null!;
    private DataGridViewTextBoxColumn queueProfileColumn = null!;
    private DataGridViewTextBoxColumn queueModelColumn = null!;
    private DataGridViewTextBoxColumn queueEffortColumn = null!;
    private DataGridViewTextBoxColumn queueProjectColumn = null!;
    private DataGridViewTextBoxColumn queueScheduledStartColumn = null!;
    private DataGridViewTextBoxColumn queueUsageColumn = null!;
    private DataGridViewTextBoxColumn queueStatusColumn = null!;
    private DataGridViewTextBoxColumn queueTitleColumn = null!;
    private DataGridViewTextBoxColumn queueReasonColumn = null!;
    private DataGridViewTextBoxColumn projectGridNameColumn = null!;
    private DataGridViewTextBoxColumn projectGridRootColumn = null!;
    private DataGridViewTextBoxColumn projectGridBranchColumn = null!;
    private DataGridViewTextBoxColumn platformGridPlatformColumn = null!;
    private DataGridViewTextBoxColumn platformGridEnabledColumn = null!;
    private DataGridViewTextBoxColumn platformGridDefaultProfileColumn = null!;
    private DataGridViewTextBoxColumn platformGridProfilesColumn = null!;
    private DataGridViewTextBoxColumn platformGridHealthColumn = null!;
    private DataGridViewTextBoxColumn platformGridDetailsColumn = null!;
    private DataGridViewTextBoxColumn platformGridUsageColumn = null!;
    private DataGridViewTextBoxColumn platformGridLimitsColumn = null!;
    private DataGridViewTextBoxColumn blockGridTypeColumn = null!;
    private DataGridViewTextBoxColumn blockGridTargetColumn = null!;
    private DataGridViewTextBoxColumn blockGridTriggerColumn = null!;
    private DataGridViewTextBoxColumn blockGridSinceColumn = null!;
    private DataGridViewTextBoxColumn blockGridRuleColumn = null!;
    private DataGridViewTextBoxColumn blockGridReasonColumn = null!;
    private DataGridViewTextBoxColumn attemptsGridNumberColumn = null!;
    private DataGridViewTextBoxColumn attemptsGridProfileColumn = null!;
    private DataGridViewTextBoxColumn attemptsGridStartColumn = null!;
    private DataGridViewTextBoxColumn attemptsGridEndColumn = null!;
    private DataGridViewTextBoxColumn attemptsGridResultColumn = null!;
    private DataGridViewTextBoxColumn attemptsGridExitColumn = null!;
    private DataGridViewTextBoxColumn attemptsGridSessionColumn = null!;
    private DataGridViewTextBoxColumn attemptsGridDiagnosticColumn = null!;
    private DataGridViewTextBoxColumn eventsGridTimeColumn = null!;
    private DataGridViewTextBoxColumn eventsGridProfileColumn = null!;
    private DataGridViewTextBoxColumn eventsGridSeverityColumn = null!;
    private DataGridViewTextBoxColumn eventsGridTypeColumn = null!;
    private DataGridViewTextBoxColumn eventsGridMessageColumn = null!;
    private DataGridViewTextBoxColumn policyGridPlatformColumn = null!;
    private DataGridViewTextBoxColumn policyGridModelColumn = null!;
    private DataGridViewTextBoxColumn policyGridDaysColumn = null!;
    private DataGridViewTextBoxColumn policyGridRangeColumn = null!;
    private DataGridViewTextBoxColumn policyGridLimitColumn = null!;
    private DataGridViewTextBoxColumn policyGridSprintColumn = null!;
    private DataGridViewTextBoxColumn policyGridUnknownColumn = null!;
    private DataGridViewTextBoxColumn policyGridPollingColumn = null!;
    private DataGridViewTextBoxColumn policyGridZoneColumn = null!;
    private ToolStrip queueTools = null!;
    private ToolStrip projectTools = null!;
    private ToolStrip platformTools = null!;
    private ToolStrip blockTools = null!;
    private ToolStrip historyTools = null!;
    private ToolStrip policyTools = null!;
    private ToolStripButton newItemButton = null!;
    private ToolStripButton newProjectButton = null!;
    private ToolStripButton editItemButton = null!;
    private ToolStripButton duplicateItemButton = null!;
    private ToolStripButton historyButton = null!;
    private ToolStripSeparator queueSeparator1 = null!;
    private ToolStripButton priorityIncreaseButton = null!;
    private ToolStripButton priorityDecreaseButton = null!;
    private ToolStripButton itemPauseButton = null!;
    private ToolStripButton cancelButton = null!;
    private ToolStripButton humanReviewButton = null!;
    private ToolStripButton requeueButton = null!;
    private ToolStripSeparator queueSeparator2 = null!;
    private ToolStripButton usageRefreshButton = null!;
    private ToolStripButton schedulerPauseButton = null!;
    private ToolStripButton createProjectButton = null!;
    private ToolStripButton editProjectButton = null!;
    private ToolStripButton deleteProjectButton = null!;
    private ToolStripButton checkPlatformsButton = null!;
    private ToolStripButton managePlatformsButton = null!;
    private ToolStripButton releaseHoldButton = null!;
    private ToolStripButton copyResumeButton = null!;
    private ToolStripButton addPolicyButton = null!;
    private ToolStripButton editPolicyButton = null!;
    private ToolStripButton removePolicyButton = null!;
    private TableLayoutPanel queueFilters = null!;
    private TextBox filter = null!;
    private ComboBox statusFilter = null!;
    private SplitContainer historySplit = null!;
    private TextBox reviewDetails = null!;
    private TableLayoutPanel settingsLayout = null!;
    private Panel settingsScroll = null!;
    private TableLayoutPanel settingsContent = null!;
    private Label runtimeHeading = null!;
    private Label claudeHeading = null!;
    private Label testHeading = null!;
    private ComboBox minimizeTarget = null!;
    private ComboBox regexChoice = null!;
    private TextBox sample = null!;
    private Button testButton = null!;
    private Label testResult = null!;
    private Button saveSettingsButton = null!;
    private Button cancelSettingsButton = null!;
    private LinkLabel projectLink = null!;
    private Label versionLabel = null!;
    private FlowLayoutPanel navigationLinks = null!;
    private Button runtimeNavigationButton = null!;
    private Button claudeNavigationButton = null!;
    private Button testNavigationButton = null!;
    private TextBox poll = null!;
    private TextBox timeout = null!;
    private TextBox usageHistoryInterval = null!;
    private TextBox maximumAttempts = null!;
    private TextBox retryBackoff = null!;
    private TextBox maximumRetryBackoff = null!;
    private TextBox regex = null!;
    private TextBox weeklyRegex = null!;
    private TextBox costRegex = null!;
    private TextBox freeRegex = null!;
    private TextBox appServer = null!;
    private TableLayoutPanel settingsNavigation = null!;
    private FlowLayoutPanel settingsActions = null!;
    private Panel settingsBody = null!;
    private TableLayoutPanel settingsFooter = null!;
    private Label runtimeHint = null!;
    private TableLayoutPanel pollField = null!;
    private Label pollLabel = null!;
    private TableLayoutPanel timeoutField = null!;
    private Label timeoutLabel = null!;
    private TableLayoutPanel usageHistoryIntervalField = null!;
    private Label usageHistoryIntervalLabel = null!;
    private TableLayoutPanel maximumAttemptsField = null!;
    private Label maximumAttemptsLabel = null!;
    private TableLayoutPanel retryBackoffField = null!;
    private Label retryBackoffLabel = null!;
    private TableLayoutPanel maximumRetryBackoffField = null!;
    private Label maximumRetryBackoffLabel = null!;
    private TableLayoutPanel regexField = null!;
    private Label regexLabel = null!;
    private TableLayoutPanel weeklyRegexField = null!;
    private Label weeklyRegexLabel = null!;
    private TableLayoutPanel costRegexField = null!;
    private Label costRegexLabel = null!;
    private TableLayoutPanel freeRegexField = null!;
    private Label freeRegexLabel = null!;
    private TableLayoutPanel appServerField = null!;
    private Label appServerLabel = null!;
    private TableLayoutPanel minimizeField = null!;
    private Label freeHint = null!;
    private GroupBox testArea = null!;
    private TableLayoutPanel testLayout = null!;
    private ToolStripStatusLabel statusSpring = null!;
    private Label minimizeLabel = null!;
    private Label regexChoiceLabel = null!;
    private Label sampleLabel = null!;
    private Label navigationHeading = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

   private void InitializeComponent() {
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
      tabs = new TabControl();
      queuePage = new TabPage();
      queue = new DataGridView();
      queuePriorityColumn = new DataGridViewTextBoxColumn();
      queuePlatformColumn = new DataGridViewTextBoxColumn();
      queueProfileColumn = new DataGridViewTextBoxColumn();
      queueModelColumn = new DataGridViewTextBoxColumn();
      queueEffortColumn = new DataGridViewTextBoxColumn();
      queueProjectColumn = new DataGridViewTextBoxColumn();
      queueScheduledStartColumn = new DataGridViewTextBoxColumn();
      queueUsageColumn = new DataGridViewTextBoxColumn();
      queueStatusColumn = new DataGridViewTextBoxColumn();
      queueTitleColumn = new DataGridViewTextBoxColumn();
      queueReasonColumn = new DataGridViewTextBoxColumn();
      queueFilters = new TableLayoutPanel();
      filter = new TextBox();
      statusFilter = new ComboBox();
      queueTools = new ToolStrip();
      newItemButton = new ToolStripButton();
      newProjectButton = new ToolStripButton();
      editItemButton = new ToolStripButton();
      duplicateItemButton = new ToolStripButton();
      historyButton = new ToolStripButton();
      queueSeparator1 = new ToolStripSeparator();
      priorityIncreaseButton = new ToolStripButton();
      priorityDecreaseButton = new ToolStripButton();
      itemPauseButton = new ToolStripButton();
      cancelButton = new ToolStripButton();
      humanReviewButton = new ToolStripButton();
      requeueButton = new ToolStripButton();
      queueSeparator2 = new ToolStripSeparator();
      usageRefreshButton = new ToolStripButton();
      schedulerPauseButton = new ToolStripButton();
      projectsPage = new TabPage();
      projectGrid = new DataGridView();
      projectGridNameColumn = new DataGridViewTextBoxColumn();
      projectGridRootColumn = new DataGridViewTextBoxColumn();
      projectGridBranchColumn = new DataGridViewTextBoxColumn();
      projectTools = new ToolStrip();
      createProjectButton = new ToolStripButton();
      editProjectButton = new ToolStripButton();
      deleteProjectButton = new ToolStripButton();
      platformPage = new TabPage();
      platformGrid = new DataGridView();
      platformGridPlatformColumn = new DataGridViewTextBoxColumn();
      platformGridEnabledColumn = new DataGridViewTextBoxColumn();
      platformGridDefaultProfileColumn = new DataGridViewTextBoxColumn();
      platformGridProfilesColumn = new DataGridViewTextBoxColumn();
      platformGridHealthColumn = new DataGridViewTextBoxColumn();
      platformGridDetailsColumn = new DataGridViewTextBoxColumn();
      platformGridUsageColumn = new DataGridViewTextBoxColumn();
      platformGridLimitsColumn = new DataGridViewTextBoxColumn();
      platformTools = new ToolStrip();
      checkPlatformsButton = new ToolStripButton();
      managePlatformsButton = new ToolStripButton();
      blocksPage = new TabPage();
      blockGrid = new DataGridView();
      blockGridTypeColumn = new DataGridViewTextBoxColumn();
      blockGridTargetColumn = new DataGridViewTextBoxColumn();
      blockGridTriggerColumn = new DataGridViewTextBoxColumn();
      blockGridSinceColumn = new DataGridViewTextBoxColumn();
      blockGridRuleColumn = new DataGridViewTextBoxColumn();
      blockGridReasonColumn = new DataGridViewTextBoxColumn();
      blockTools = new ToolStrip();
      releaseHoldButton = new ToolStripButton();
      historyPage = new TabPage();
      historySplit = new SplitContainer();
      attemptsGrid = new DataGridView();
      attemptsGridNumberColumn = new DataGridViewTextBoxColumn();
      attemptsGridProfileColumn = new DataGridViewTextBoxColumn();
      attemptsGridStartColumn = new DataGridViewTextBoxColumn();
      attemptsGridEndColumn = new DataGridViewTextBoxColumn();
      attemptsGridResultColumn = new DataGridViewTextBoxColumn();
      attemptsGridExitColumn = new DataGridViewTextBoxColumn();
      attemptsGridSessionColumn = new DataGridViewTextBoxColumn();
      attemptsGridDiagnosticColumn = new DataGridViewTextBoxColumn();
      eventsGrid = new DataGridView();
      eventsGridTimeColumn = new DataGridViewTextBoxColumn();
      eventsGridProfileColumn = new DataGridViewTextBoxColumn();
      eventsGridSeverityColumn = new DataGridViewTextBoxColumn();
      eventsGridTypeColumn = new DataGridViewTextBoxColumn();
      eventsGridMessageColumn = new DataGridViewTextBoxColumn();
      reviewDetails = new TextBox();
      historyTools = new ToolStrip();
      copyResumeButton = new ToolStripButton();
      policiesPage = new TabPage();
      policyGrid = new DataGridView();
      policyGridPlatformColumn = new DataGridViewTextBoxColumn();
      policyGridModelColumn = new DataGridViewTextBoxColumn();
      policyGridDaysColumn = new DataGridViewTextBoxColumn();
      policyGridRangeColumn = new DataGridViewTextBoxColumn();
      policyGridLimitColumn = new DataGridViewTextBoxColumn();
      policyGridSprintColumn = new DataGridViewTextBoxColumn();
      policyGridUnknownColumn = new DataGridViewTextBoxColumn();
      policyGridPollingColumn = new DataGridViewTextBoxColumn();
      policyGridZoneColumn = new DataGridViewTextBoxColumn();
      policyTools = new ToolStrip();
      addPolicyButton = new ToolStripButton();
      editPolicyButton = new ToolStripButton();
      removePolicyButton = new ToolStripButton();
      settingsPage = new TabPage();
      settingsLayout = new TableLayoutPanel();
      settingsNavigation = new TableLayoutPanel();
      navigationLinks = new FlowLayoutPanel();
      navigationHeading = new Label();
      runtimeNavigationButton = new Button();
      claudeNavigationButton = new Button();
      testNavigationButton = new Button();
      settingsActions = new FlowLayoutPanel();
      saveSettingsButton = new Button();
      cancelSettingsButton = new Button();
      settingsBody = new Panel();
      settingsScroll = new Panel();
      settingsContent = new TableLayoutPanel();
      runtimeHeading = new Label();
      runtimeHint = new Label();
      pollField = new TableLayoutPanel();
      pollLabel = new Label();
      poll = new TextBox();
      timeoutField = new TableLayoutPanel();
      timeoutLabel = new Label();
      timeout = new TextBox();
      usageHistoryIntervalField = new TableLayoutPanel();
      usageHistoryIntervalLabel = new Label();
      usageHistoryInterval = new TextBox();
      maximumAttemptsField = new TableLayoutPanel();
      maximumAttemptsLabel = new Label();
      maximumAttempts = new TextBox();
      retryBackoffField = new TableLayoutPanel();
      retryBackoffLabel = new Label();
      retryBackoff = new TextBox();
      maximumRetryBackoffField = new TableLayoutPanel();
      maximumRetryBackoffLabel = new Label();
      maximumRetryBackoff = new TextBox();
      regexField = new TableLayoutPanel();
      regexLabel = new Label();
      regex = new TextBox();
      weeklyRegexField = new TableLayoutPanel();
      weeklyRegexLabel = new Label();
      weeklyRegex = new TextBox();
      costRegexField = new TableLayoutPanel();
      costRegexLabel = new Label();
      costRegex = new TextBox();
      freeRegexField = new TableLayoutPanel();
      freeRegexLabel = new Label();
      freeRegex = new TextBox();
      appServerField = new TableLayoutPanel();
      appServerLabel = new Label();
      appServer = new TextBox();
      minimizeField = new TableLayoutPanel();
      minimizeLabel = new Label();
      minimizeTarget = new ComboBox();
      claudeHeading = new Label();
      freeHint = new Label();
      testHeading = new Label();
      testArea = new GroupBox();
      testLayout = new TableLayoutPanel();
      regexChoiceLabel = new Label();
      regexChoice = new ComboBox();
      sampleLabel = new Label();
      sample = new TextBox();
      testButton = new Button();
      testResult = new Label();
      settingsFooter = new TableLayoutPanel();
      projectLink = new LinkLabel();
      versionLabel = new Label();
      status = new StatusStrip();
      workerState = new ToolStripStatusLabel();
      statusSpring = new ToolStripStatusLabel();
      usageState = new ToolStripStatusLabel();
      runningState = new ToolStripStatusLabel();
      tabs.SuspendLayout();
      queuePage.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)queue).BeginInit();
      queueFilters.SuspendLayout();
      queueTools.SuspendLayout();
      projectsPage.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)projectGrid).BeginInit();
      projectTools.SuspendLayout();
      platformPage.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)platformGrid).BeginInit();
      platformTools.SuspendLayout();
      blocksPage.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)blockGrid).BeginInit();
      blockTools.SuspendLayout();
      historyPage.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)historySplit).BeginInit();
      historySplit.Panel1.SuspendLayout();
      historySplit.Panel2.SuspendLayout();
      historySplit.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)attemptsGrid).BeginInit();
      ((System.ComponentModel.ISupportInitialize)eventsGrid).BeginInit();
      historyTools.SuspendLayout();
      policiesPage.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)policyGrid).BeginInit();
      policyTools.SuspendLayout();
      settingsPage.SuspendLayout();
      settingsLayout.SuspendLayout();
      settingsNavigation.SuspendLayout();
      navigationLinks.SuspendLayout();
      settingsActions.SuspendLayout();
      settingsBody.SuspendLayout();
      settingsScroll.SuspendLayout();
      settingsContent.SuspendLayout();
      pollField.SuspendLayout();
      timeoutField.SuspendLayout();
      usageHistoryIntervalField.SuspendLayout();
      maximumAttemptsField.SuspendLayout();
      retryBackoffField.SuspendLayout();
      maximumRetryBackoffField.SuspendLayout();
      regexField.SuspendLayout();
      weeklyRegexField.SuspendLayout();
      costRegexField.SuspendLayout();
      freeRegexField.SuspendLayout();
      appServerField.SuspendLayout();
      minimizeField.SuspendLayout();
      testArea.SuspendLayout();
      testLayout.SuspendLayout();
      settingsFooter.SuspendLayout();
      status.SuspendLayout();
      SuspendLayout();
      // 
      // tabs
      // 
      tabs.Controls.Add(queuePage);
      tabs.Controls.Add(projectsPage);
      tabs.Controls.Add(platformPage);
      tabs.Controls.Add(blocksPage);
      tabs.Controls.Add(historyPage);
      tabs.Controls.Add(policiesPage);
      tabs.Controls.Add(settingsPage);
      tabs.Dock = DockStyle.Fill;
      tabs.Location = new Point(0, 0);
      tabs.Name = "tabs";
      tabs.SelectedIndex = 0;
      tabs.Size = new Size(1320, 758);
      tabs.TabIndex = 0;
      // 
      // queuePage
      // 
      queuePage.Controls.Add(queue);
      queuePage.Controls.Add(queueFilters);
      queuePage.Controls.Add(queueTools);
      queuePage.Location = new Point(4, 24);
      queuePage.Name = "queuePage";
      queuePage.Padding = new Padding(3);
      queuePage.Size = new Size(1312, 730);
      queuePage.TabIndex = 0;
      queuePage.Text = "Warteschlange";
      // 
      // queue
      // 
      queue.AllowUserToAddRows = false;
      queue.AllowUserToDeleteRows = false;
      queue.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
      queue.Columns.AddRange(new DataGridViewColumn[] { queuePriorityColumn, queuePlatformColumn, queueProfileColumn, queueModelColumn, queueEffortColumn, queueProjectColumn, queueScheduledStartColumn, queueUsageColumn, queueStatusColumn, queueTitleColumn, queueReasonColumn });
      queue.Dock = DockStyle.Fill;
      queue.Location = new Point(3, 145);
      queue.MultiSelect = false;
      queue.Name = "queue";
      queue.ReadOnly = true;
      queue.RowHeadersVisible = false;
      queue.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
      queue.Size = new Size(1306, 582);
      queue.TabIndex = 0;
      // 
      // queuePriorityColumn
      // 
      queuePriorityColumn.HeaderText = "Priorität";
      queuePriorityColumn.Name = "queuePriorityColumn";
      queuePriorityColumn.ReadOnly = true;
      queuePriorityColumn.Width = 75;
      // 
      // queuePlatformColumn
      // 
      queuePlatformColumn.HeaderText = "Plattform";
      queuePlatformColumn.Name = "queuePlatformColumn";
      queuePlatformColumn.ReadOnly = true;
      queuePlatformColumn.Width = 85;
      // 
      // queueProfileColumn
      // 
      queueProfileColumn.HeaderText = "Profil";
      queueProfileColumn.Name = "queueProfileColumn";
      queueProfileColumn.ReadOnly = true;
      queueProfileColumn.Width = 150;
      // 
      // queueModelColumn
      // 
      queueModelColumn.HeaderText = "Modell";
      queueModelColumn.Name = "queueModelColumn";
      queueModelColumn.ReadOnly = true;
      queueModelColumn.Width = 135;
      // 
      // queueEffortColumn
      // 
      queueEffortColumn.HeaderText = "Effort";
      queueEffortColumn.Name = "queueEffortColumn";
      queueEffortColumn.ReadOnly = true;
      queueEffortColumn.Width = 70;
      // 
      // queueProjectColumn
      // 
      queueProjectColumn.HeaderText = "Projekt";
      queueProjectColumn.Name = "queueProjectColumn";
      queueProjectColumn.ReadOnly = true;
      queueProjectColumn.Width = 150;
      // 
      // queueScheduledStartColumn
      // 
      queueScheduledStartColumn.HeaderText = "Geplanter Start";
      queueScheduledStartColumn.Name = "queueScheduledStartColumn";
      queueScheduledStartColumn.ReadOnly = true;
      queueScheduledStartColumn.Width = 165;
      // 
      // queueUsageColumn
      // 
      queueUsageColumn.HeaderText = "Usage";
      queueUsageColumn.Name = "queueUsageColumn";
      queueUsageColumn.ReadOnly = true;
      queueUsageColumn.Width = 190;
      // 
      // queueStatusColumn
      // 
      queueStatusColumn.HeaderText = "Status";
      queueStatusColumn.Name = "queueStatusColumn";
      queueStatusColumn.ReadOnly = true;
      queueStatusColumn.Width = 150;
      // 
      // queueTitleColumn
      // 
      queueTitleColumn.HeaderText = "Titel";
      queueTitleColumn.Name = "queueTitleColumn";
      queueTitleColumn.ReadOnly = true;
      queueTitleColumn.Width = 230;
      // 
      // queueReasonColumn
      // 
      queueReasonColumn.HeaderText = "Begründung";
      queueReasonColumn.Name = "queueReasonColumn";
      queueReasonColumn.ReadOnly = true;
      queueReasonColumn.Width = 340;
      // 
      // queueFilters
      // 
      queueFilters.ColumnCount = 2;
      queueFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
      queueFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
      queueFilters.Controls.Add(filter, 0, 0);
      queueFilters.Controls.Add(statusFilter, 1, 0);
      queueFilters.Dock = DockStyle.Top;
      queueFilters.Location = new Point(3, 109);
      queueFilters.Name = "queueFilters";
      queueFilters.Padding = new Padding(4);
      queueFilters.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
      queueFilters.Size = new Size(1306, 36);
      queueFilters.TabIndex = 1;
      // 
      // filter
      // 
      filter.Dock = DockStyle.Fill;
      filter.Location = new Point(7, 7);
      filter.Name = "filter";
      filter.PlaceholderText = "Titel, Projekt oder Begründung filtern …";
      filter.Size = new Size(1092, 23);
      filter.TabIndex = 0;
      // 
      // statusFilter
      // 
      statusFilter.DropDownStyle = ComboBoxStyle.DropDownList;
      statusFilter.Location = new Point(1105, 7);
      statusFilter.Name = "statusFilter";
      statusFilter.Size = new Size(190, 23);
      statusFilter.TabIndex = 1;
      // 
      // queueTools
      // 
      queueTools.AutoSize = false;
      queueTools.BackColor = Color.White;
      queueTools.GripStyle = ToolStripGripStyle.Hidden;
      queueTools.ImageScalingSize = new Size(64, 64);
      queueTools.Items.AddRange(new ToolStripItem[] { newItemButton, newProjectButton, editItemButton, duplicateItemButton, historyButton, queueSeparator1, priorityIncreaseButton, priorityDecreaseButton, itemPauseButton, cancelButton, humanReviewButton, requeueButton, queueSeparator2, usageRefreshButton, schedulerPauseButton });
      queueTools.Location = new Point(3, 3);
      queueTools.Name = "queueTools";
      queueTools.Padding = new Padding(5, 3, 5, 3);
      queueTools.Size = new Size(1306, 106);
      queueTools.TabIndex = 2;
      // 
      // newItemButton
      // 
      newItemButton.AutoSize = false;
      newItemButton.Name = "newItemButton";
      newItemButton.Size = new Size(94, 98);
      newItemButton.Text = "Neu";
      newItemButton.TextImageRelation = TextImageRelation.ImageAboveText;
      // 
      // newProjectButton
      // 
      newProjectButton.AutoSize = false;
      newProjectButton.Name = "newProjectButton";
      newProjectButton.Size = new Size(94, 98);
      newProjectButton.Text = "Projekt";
      newProjectButton.TextImageRelation = TextImageRelation.ImageAboveText;
      newProjectButton.ToolTipText = "Neues Projekt";
      // 
      // editItemButton
      // 
      editItemButton.AutoSize = false;
      editItemButton.Name = "editItemButton";
      editItemButton.Size = new Size(94, 98);
      editItemButton.Text = "Bearbeiten";
      editItemButton.TextImageRelation = TextImageRelation.ImageAboveText;
      // 
      // duplicateItemButton
      // 
      duplicateItemButton.AutoSize = false;
      duplicateItemButton.Name = "duplicateItemButton";
      duplicateItemButton.Size = new Size(94, 98);
      duplicateItemButton.Text = "Duplizieren";
      duplicateItemButton.TextImageRelation = TextImageRelation.ImageAboveText;
      // 
      // historyButton
      // 
      historyButton.AutoSize = false;
      historyButton.Enabled = false;
      historyButton.Name = "historyButton";
      historyButton.Size = new Size(94, 98);
      historyButton.Text = "Verlauf";
      historyButton.TextImageRelation = TextImageRelation.ImageAboveText;
      // 
      // queueSeparator1
      // 
      queueSeparator1.Name = "queueSeparator1";
      queueSeparator1.Size = new Size(6, 100);
      // 
      // priorityIncreaseButton
      // 
      priorityIncreaseButton.AutoSize = false;
      priorityIncreaseButton.Enabled = false;
      priorityIncreaseButton.Name = "priorityIncreaseButton";
      priorityIncreaseButton.Size = new Size(94, 98);
      priorityIncreaseButton.Text = "Prio +";
      priorityIncreaseButton.TextImageRelation = TextImageRelation.ImageAboveText;
      priorityIncreaseButton.ToolTipText = "Priorität +";
      // 
      // priorityDecreaseButton
      // 
      priorityDecreaseButton.AutoSize = false;
      priorityDecreaseButton.Enabled = false;
      priorityDecreaseButton.Name = "priorityDecreaseButton";
      priorityDecreaseButton.Size = new Size(94, 98);
      priorityDecreaseButton.Text = "Prio −";
      priorityDecreaseButton.TextImageRelation = TextImageRelation.ImageAboveText;
      priorityDecreaseButton.ToolTipText = "Priorität −";
      // 
      // itemPauseButton
      // 
      itemPauseButton.AutoSize = false;
      itemPauseButton.Enabled = false;
      itemPauseButton.Name = "itemPauseButton";
      itemPauseButton.Size = new Size(94, 98);
      itemPauseButton.Text = "Pause/Weiter";
      itemPauseButton.TextImageRelation = TextImageRelation.ImageAboveText;
      itemPauseButton.ToolTipText = "Pausieren/Fortsetzen";
      // 
      // cancelButton
      // 
      cancelButton.AutoSize = false;
      cancelButton.Enabled = false;
      cancelButton.Name = "cancelButton";
      cancelButton.Size = new Size(94, 98);
      cancelButton.Text = "Abbrechen";
      cancelButton.TextImageRelation = TextImageRelation.ImageAboveText;
      // 
      // humanReviewButton
      // 
      humanReviewButton.AutoSize = false;
      humanReviewButton.Enabled = false;
      humanReviewButton.Name = "humanReviewButton";
      humanReviewButton.Size = new Size(94, 98);
      humanReviewButton.Text = "Prüfen";
      humanReviewButton.TextImageRelation = TextImageRelation.ImageAboveText;
      humanReviewButton.ToolTipText = "Extern prüfen";
      // 
      // requeueButton
      // 
      requeueButton.AutoSize = false;
      requeueButton.Enabled = false;
      requeueButton.Name = "requeueButton";
      requeueButton.Size = new Size(94, 98);
      requeueButton.Text = "Einreihen";
      requeueButton.TextImageRelation = TextImageRelation.ImageAboveText;
      requeueButton.ToolTipText = "Erneut einreihen";
      // 
      // queueSeparator2
      // 
      queueSeparator2.Name = "queueSeparator2";
      queueSeparator2.Size = new Size(6, 100);
      // 
      // usageRefreshButton
      // 
      usageRefreshButton.AutoSize = false;
      usageRefreshButton.Name = "usageRefreshButton";
      usageRefreshButton.Size = new Size(94, 98);
      usageRefreshButton.Text = "Usage";
      usageRefreshButton.TextImageRelation = TextImageRelation.ImageAboveText;
      usageRefreshButton.ToolTipText = "Usage aktualisieren";
      // 
      // schedulerPauseButton
      // 
      schedulerPauseButton.AutoSize = false;
      schedulerPauseButton.Name = "schedulerPauseButton";
      schedulerPauseButton.Size = new Size(94, 98);
      schedulerPauseButton.Text = "Pausieren";
      schedulerPauseButton.TextImageRelation = TextImageRelation.ImageAboveText;
      // 
      // projectsPage
      // 
      projectsPage.Controls.Add(projectGrid);
      projectsPage.Controls.Add(projectTools);
      projectsPage.Location = new Point(4, 24);
      projectsPage.Name = "projectsPage";
      projectsPage.Padding = new Padding(3);
      projectsPage.Size = new Size(1312, 730);
      projectsPage.TabIndex = 1;
      projectsPage.Text = "Projekte";
      // 
      // projectGrid
      // 
      projectGrid.AllowUserToAddRows = false;
      projectGrid.AllowUserToDeleteRows = false;
      projectGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
      projectGrid.Columns.AddRange(new DataGridViewColumn[] { projectGridNameColumn, projectGridRootColumn, projectGridBranchColumn });
      projectGrid.Dock = DockStyle.Fill;
      projectGrid.Location = new Point(3, 109);
      projectGrid.MultiSelect = false;
      projectGrid.Name = "projectGrid";
      projectGrid.ReadOnly = true;
      projectGrid.RowHeadersVisible = false;
      projectGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
      projectGrid.Size = new Size(1306, 618);
      projectGrid.TabIndex = 0;
      // 
      // projectGridNameColumn
      // 
      projectGridNameColumn.HeaderText = "Name";
      projectGridNameColumn.Name = "projectGridNameColumn";
      projectGridNameColumn.ReadOnly = true;
      projectGridNameColumn.Width = 240;
      // 
      // projectGridRootColumn
      // 
      projectGridRootColumn.HeaderText = "Projektroot";
      projectGridRootColumn.Name = "projectGridRootColumn";
      projectGridRootColumn.ReadOnly = true;
      projectGridRootColumn.Width = 650;
      // 
      // projectGridBranchColumn
      // 
      projectGridBranchColumn.HeaderText = "Zielbranch";
      projectGridBranchColumn.Name = "projectGridBranchColumn";
      projectGridBranchColumn.ReadOnly = true;
      projectGridBranchColumn.Width = 180;
      // 
      // projectTools
      // 
      projectTools.AutoSize = false;
      projectTools.BackColor = Color.White;
      projectTools.GripStyle = ToolStripGripStyle.Hidden;
      projectTools.ImageScalingSize = new Size(64, 64);
      projectTools.Items.AddRange(new ToolStripItem[] { createProjectButton, editProjectButton, deleteProjectButton });
      projectTools.Location = new Point(3, 3);
      projectTools.Name = "projectTools";
      projectTools.Padding = new Padding(5, 3, 5, 3);
      projectTools.Size = new Size(1306, 106);
      projectTools.TabIndex = 1;
      // 
      // createProjectButton
      // 
      createProjectButton.AutoSize = false;
      createProjectButton.Name = "createProjectButton";
      createProjectButton.Size = new Size(94, 98);
      createProjectButton.Text = "Anlegen";
      createProjectButton.TextImageRelation = TextImageRelation.ImageAboveText;
      createProjectButton.ToolTipText = "Projekt anlegen";
      // 
      // editProjectButton
      // 
      editProjectButton.AutoSize = false;
      editProjectButton.Name = "editProjectButton";
      editProjectButton.Size = new Size(94, 98);
      editProjectButton.Text = "Bearbeiten";
      editProjectButton.TextImageRelation = TextImageRelation.ImageAboveText;
      editProjectButton.ToolTipText = "Projekt bearbeiten";
      // 
      // deleteProjectButton
      // 
      deleteProjectButton.AutoSize = false;
      deleteProjectButton.Name = "deleteProjectButton";
      deleteProjectButton.Size = new Size(94, 98);
      deleteProjectButton.Text = "Löschen";
      deleteProjectButton.TextImageRelation = TextImageRelation.ImageAboveText;
      deleteProjectButton.ToolTipText = "Projekt löschen";
      // 
      // platformPage
      // 
      platformPage.Controls.Add(platformGrid);
      platformPage.Controls.Add(platformTools);
      platformPage.Location = new Point(4, 24);
      platformPage.Name = "platformPage";
      platformPage.Padding = new Padding(3);
      platformPage.Size = new Size(1312, 730);
      platformPage.TabIndex = 2;
      platformPage.Text = "Plattformen & Usage";
      // 
      // platformGrid
      // 
      platformGrid.AllowUserToAddRows = false;
      platformGrid.AllowUserToDeleteRows = false;
      platformGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
      platformGrid.Columns.AddRange(new DataGridViewColumn[] { platformGridPlatformColumn, platformGridEnabledColumn, platformGridDefaultProfileColumn, platformGridProfilesColumn, platformGridHealthColumn, platformGridDetailsColumn, platformGridUsageColumn, platformGridLimitsColumn });
      platformGrid.Dock = DockStyle.Fill;
      platformGrid.Location = new Point(3, 109);
      platformGrid.MultiSelect = false;
      platformGrid.Name = "platformGrid";
      platformGrid.ReadOnly = true;
      platformGrid.RowHeadersVisible = false;
      platformGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
      platformGrid.Size = new Size(1306, 618);
      platformGrid.TabIndex = 0;
      // 
      // platformGridPlatformColumn
      // 
      platformGridPlatformColumn.HeaderText = "Plattform";
      platformGridPlatformColumn.Name = "platformGridPlatformColumn";
      platformGridPlatformColumn.ReadOnly = true;
      platformGridPlatformColumn.Width = 110;
      // 
      // platformGridEnabledColumn
      // 
      platformGridEnabledColumn.HeaderText = "Aktiv";
      platformGridEnabledColumn.Name = "platformGridEnabledColumn";
      platformGridEnabledColumn.ReadOnly = true;
      platformGridEnabledColumn.Width = 65;
      // 
      // platformGridDefaultProfileColumn
      // 
      platformGridDefaultProfileColumn.HeaderText = "Standardprofil";
      platformGridDefaultProfileColumn.Name = "platformGridDefaultProfileColumn";
      platformGridDefaultProfileColumn.ReadOnly = true;
      platformGridDefaultProfileColumn.Width = 150;
      // 
      // platformGridProfilesColumn
      // 
      platformGridProfilesColumn.HeaderText = "Profile";
      platformGridProfilesColumn.Name = "platformGridProfilesColumn";
      platformGridProfilesColumn.ReadOnly = true;
      platformGridProfilesColumn.Width = 75;
      // 
      // platformGridHealthColumn
      // 
      platformGridHealthColumn.HeaderText = "Zustand";
      platformGridHealthColumn.Name = "platformGridHealthColumn";
      platformGridHealthColumn.ReadOnly = true;
      platformGridHealthColumn.Width = 130;
      // 
      // platformGridDetailsColumn
      // 
      platformGridDetailsColumn.HeaderText = "Details";
      platformGridDetailsColumn.Name = "platformGridDetailsColumn";
      platformGridDetailsColumn.ReadOnly = true;
      platformGridDetailsColumn.Width = 260;
      // 
      // platformGridUsageColumn
      // 
      platformGridUsageColumn.HeaderText = "Verbrauch und Reset";
      platformGridUsageColumn.Name = "platformGridUsageColumn";
      platformGridUsageColumn.ReadOnly = true;
      platformGridUsageColumn.Width = 430;
      // 
      // platformGridLimitsColumn
      // 
      platformGridLimitsColumn.HeaderText = "Wirksame Grenzwerte";
      platformGridLimitsColumn.Name = "platformGridLimitsColumn";
      platformGridLimitsColumn.ReadOnly = true;
      platformGridLimitsColumn.Width = 400;
      // 
      // platformTools
      // 
      platformTools.AutoSize = false;
      platformTools.BackColor = Color.White;
      platformTools.GripStyle = ToolStripGripStyle.Hidden;
      platformTools.ImageScalingSize = new Size(64, 64);
      platformTools.Items.AddRange(new ToolStripItem[] { checkPlatformsButton, managePlatformsButton });
      platformTools.Location = new Point(3, 3);
      platformTools.Name = "platformTools";
      platformTools.Padding = new Padding(5, 3, 5, 3);
      platformTools.Size = new Size(1306, 106);
      platformTools.TabIndex = 1;
      // 
      // checkPlatformsButton
      // 
      checkPlatformsButton.AutoSize = false;
      checkPlatformsButton.Name = "checkPlatformsButton";
      checkPlatformsButton.Size = new Size(94, 98);
      checkPlatformsButton.Text = "Jetzt prüfen";
      checkPlatformsButton.TextImageRelation = TextImageRelation.ImageAboveText;
      // 
      // managePlatformsButton
      // 
      managePlatformsButton.AutoSize = false;
      managePlatformsButton.Name = "managePlatformsButton";
      managePlatformsButton.Size = new Size(94, 98);
      managePlatformsButton.Text = "Verwalten";
      managePlatformsButton.TextImageRelation = TextImageRelation.ImageAboveText;
      managePlatformsButton.ToolTipText = "Plattform und Profile verwalten";
      // 
      // blocksPage
      // 
      blocksPage.Controls.Add(blockGrid);
      blocksPage.Controls.Add(blockTools);
      blocksPage.Location = new Point(4, 24);
      blocksPage.Name = "blocksPage";
      blocksPage.Padding = new Padding(3);
      blocksPage.Size = new Size(1312, 730);
      blocksPage.TabIndex = 3;
      blocksPage.Text = "Sperren";
      // 
      // blockGrid
      // 
      blockGrid.AllowUserToAddRows = false;
      blockGrid.AllowUserToDeleteRows = false;
      blockGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
      blockGrid.Columns.AddRange(new DataGridViewColumn[] { blockGridTypeColumn, blockGridTargetColumn, blockGridTriggerColumn, blockGridSinceColumn, blockGridRuleColumn, blockGridReasonColumn });
      blockGrid.Dock = DockStyle.Fill;
      blockGrid.Location = new Point(3, 109);
      blockGrid.MultiSelect = false;
      blockGrid.Name = "blockGrid";
      blockGrid.ReadOnly = true;
      blockGrid.RowHeadersVisible = false;
      blockGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
      blockGrid.Size = new Size(1306, 618);
      blockGrid.TabIndex = 0;
      // 
      // blockGridTypeColumn
      // 
      blockGridTypeColumn.HeaderText = "Typ";
      blockGridTypeColumn.Name = "blockGridTypeColumn";
      blockGridTypeColumn.ReadOnly = true;
      blockGridTypeColumn.Width = 130;
      // 
      // blockGridTargetColumn
      // 
      blockGridTargetColumn.HeaderText = "Plattform/Projekt";
      blockGridTargetColumn.Name = "blockGridTargetColumn";
      blockGridTargetColumn.ReadOnly = true;
      blockGridTargetColumn.Width = 180;
      // 
      // blockGridTriggerColumn
      // 
      blockGridTriggerColumn.HeaderText = "Auslösender Auftrag";
      blockGridTriggerColumn.Name = "blockGridTriggerColumn";
      blockGridTriggerColumn.ReadOnly = true;
      blockGridTriggerColumn.Width = 230;
      // 
      // blockGridSinceColumn
      // 
      blockGridSinceColumn.HeaderText = "Seit";
      blockGridSinceColumn.Name = "blockGridSinceColumn";
      blockGridSinceColumn.ReadOnly = true;
      blockGridSinceColumn.Width = 145;
      // 
      // blockGridRuleColumn
      // 
      blockGridRuleColumn.HeaderText = "Freigaberegel";
      blockGridRuleColumn.Name = "blockGridRuleColumn";
      blockGridRuleColumn.ReadOnly = true;
      blockGridRuleColumn.Width = 260;
      // 
      // blockGridReasonColumn
      // 
      blockGridReasonColumn.HeaderText = "Ursache";
      blockGridReasonColumn.Name = "blockGridReasonColumn";
      blockGridReasonColumn.ReadOnly = true;
      blockGridReasonColumn.Width = 450;
      // 
      // blockTools
      // 
      blockTools.AutoSize = false;
      blockTools.BackColor = Color.White;
      blockTools.GripStyle = ToolStripGripStyle.Hidden;
      blockTools.ImageScalingSize = new Size(64, 64);
      blockTools.Items.AddRange(new ToolStripItem[] { releaseHoldButton });
      blockTools.Location = new Point(3, 3);
      blockTools.Name = "blockTools";
      blockTools.Padding = new Padding(5, 3, 5, 3);
      blockTools.Size = new Size(1306, 106);
      blockTools.TabIndex = 1;
      // 
      // releaseHoldButton
      // 
      releaseHoldButton.AutoSize = false;
      releaseHoldButton.Name = "releaseHoldButton";
      releaseHoldButton.Size = new Size(94, 98);
      releaseHoldButton.Text = "Freigeben";
      releaseHoldButton.TextImageRelation = TextImageRelation.ImageAboveText;
      releaseHoldButton.ToolTipText = "Projekt-Hold bewusst freigeben";
      // 
      // historyPage
      // 
      historyPage.Controls.Add(historySplit);
      historyPage.Controls.Add(reviewDetails);
      historyPage.Controls.Add(historyTools);
      historyPage.Location = new Point(4, 24);
      historyPage.Name = "historyPage";
      historyPage.Padding = new Padding(3);
      historyPage.Size = new Size(1312, 730);
      historyPage.TabIndex = 4;
      historyPage.Text = "Historie";
      // 
      // historySplit
      // 
      historySplit.Dock = DockStyle.Fill;
      historySplit.Location = new Point(3, 227);
      historySplit.Name = "historySplit";
      historySplit.Orientation = Orientation.Horizontal;
      // 
      // historySplit.Panel1
      // 
      historySplit.Panel1.Controls.Add(attemptsGrid);
      // 
      // historySplit.Panel2
      // 
      historySplit.Panel2.Controls.Add(eventsGrid);
      historySplit.Size = new Size(1306, 500);
      historySplit.SplitterDistance = 231;
      historySplit.TabIndex = 0;
      // 
      // attemptsGrid
      // 
      attemptsGrid.AllowUserToAddRows = false;
      attemptsGrid.AllowUserToDeleteRows = false;
      attemptsGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
      attemptsGrid.Columns.AddRange(new DataGridViewColumn[] { attemptsGridNumberColumn, attemptsGridProfileColumn, attemptsGridStartColumn, attemptsGridEndColumn, attemptsGridResultColumn, attemptsGridExitColumn, attemptsGridSessionColumn, attemptsGridDiagnosticColumn });
      attemptsGrid.Dock = DockStyle.Fill;
      attemptsGrid.Location = new Point(0, 0);
      attemptsGrid.MultiSelect = false;
      attemptsGrid.Name = "attemptsGrid";
      attemptsGrid.ReadOnly = true;
      attemptsGrid.RowHeadersVisible = false;
      attemptsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
      attemptsGrid.Size = new Size(1306, 231);
      attemptsGrid.TabIndex = 0;
      // 
      // attemptsGridNumberColumn
      // 
      attemptsGridNumberColumn.HeaderText = "Nr.";
      attemptsGridNumberColumn.Name = "attemptsGridNumberColumn";
      attemptsGridNumberColumn.ReadOnly = true;
      attemptsGridNumberColumn.Width = 50;
      // 
      // attemptsGridProfileColumn
      // 
      attemptsGridProfileColumn.HeaderText = "Profil";
      attemptsGridProfileColumn.Name = "attemptsGridProfileColumn";
      attemptsGridProfileColumn.ReadOnly = true;
      attemptsGridProfileColumn.Width = 145;
      // 
      // attemptsGridStartColumn
      // 
      attemptsGridStartColumn.HeaderText = "Start";
      attemptsGridStartColumn.Name = "attemptsGridStartColumn";
      attemptsGridStartColumn.ReadOnly = true;
      attemptsGridStartColumn.Width = 145;
      // 
      // attemptsGridEndColumn
      // 
      attemptsGridEndColumn.HeaderText = "Ende";
      attemptsGridEndColumn.Name = "attemptsGridEndColumn";
      attemptsGridEndColumn.ReadOnly = true;
      attemptsGridEndColumn.Width = 145;
      // 
      // attemptsGridResultColumn
      // 
      attemptsGridResultColumn.HeaderText = "Ergebnis";
      attemptsGridResultColumn.Name = "attemptsGridResultColumn";
      attemptsGridResultColumn.ReadOnly = true;
      attemptsGridResultColumn.Width = 170;
      // 
      // attemptsGridExitColumn
      // 
      attemptsGridExitColumn.HeaderText = "Exit";
      attemptsGridExitColumn.Name = "attemptsGridExitColumn";
      attemptsGridExitColumn.ReadOnly = true;
      attemptsGridExitColumn.Width = 55;
      // 
      // attemptsGridSessionColumn
      // 
      attemptsGridSessionColumn.HeaderText = "Sitzung";
      attemptsGridSessionColumn.Name = "attemptsGridSessionColumn";
      attemptsGridSessionColumn.ReadOnly = true;
      attemptsGridSessionColumn.Width = 240;
      // 
      // attemptsGridDiagnosticColumn
      // 
      attemptsGridDiagnosticColumn.HeaderText = "Diagnose";
      attemptsGridDiagnosticColumn.Name = "attemptsGridDiagnosticColumn";
      attemptsGridDiagnosticColumn.ReadOnly = true;
      attemptsGridDiagnosticColumn.Width = 430;
      // 
      // eventsGrid
      // 
      eventsGrid.AllowUserToAddRows = false;
      eventsGrid.AllowUserToDeleteRows = false;
      eventsGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
      eventsGrid.Columns.AddRange(new DataGridViewColumn[] { eventsGridTimeColumn, eventsGridProfileColumn, eventsGridSeverityColumn, eventsGridTypeColumn, eventsGridMessageColumn });
      eventsGrid.Dock = DockStyle.Fill;
      eventsGrid.Location = new Point(0, 0);
      eventsGrid.MultiSelect = false;
      eventsGrid.Name = "eventsGrid";
      eventsGrid.ReadOnly = true;
      eventsGrid.RowHeadersVisible = false;
      eventsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
      eventsGrid.Size = new Size(1306, 265);
      eventsGrid.TabIndex = 0;
      // 
      // eventsGridTimeColumn
      // 
      eventsGridTimeColumn.HeaderText = "Zeit";
      eventsGridTimeColumn.Name = "eventsGridTimeColumn";
      eventsGridTimeColumn.ReadOnly = true;
      eventsGridTimeColumn.Width = 145;
      // 
      // eventsGridProfileColumn
      // 
      eventsGridProfileColumn.HeaderText = "Profil";
      eventsGridProfileColumn.Name = "eventsGridProfileColumn";
      eventsGridProfileColumn.ReadOnly = true;
      eventsGridProfileColumn.Width = 145;
      // 
      // eventsGridSeverityColumn
      // 
      eventsGridSeverityColumn.HeaderText = "Stufe";
      eventsGridSeverityColumn.Name = "eventsGridSeverityColumn";
      eventsGridSeverityColumn.ReadOnly = true;
      eventsGridSeverityColumn.Width = 90;
      // 
      // eventsGridTypeColumn
      // 
      eventsGridTypeColumn.HeaderText = "Typ/Stream";
      eventsGridTypeColumn.Name = "eventsGridTypeColumn";
      eventsGridTypeColumn.ReadOnly = true;
      eventsGridTypeColumn.Width = 200;
      // 
      // eventsGridMessageColumn
      // 
      eventsGridMessageColumn.HeaderText = "Meldung";
      eventsGridMessageColumn.Name = "eventsGridMessageColumn";
      eventsGridMessageColumn.ReadOnly = true;
      eventsGridMessageColumn.Width = 700;
      // 
      // reviewDetails
      // 
      reviewDetails.BackColor = SystemColors.Info;
      reviewDetails.Dock = DockStyle.Top;
      reviewDetails.Location = new Point(3, 109);
      reviewDetails.Multiline = true;
      reviewDetails.Name = "reviewDetails";
      reviewDetails.ReadOnly = true;
      reviewDetails.ScrollBars = ScrollBars.Vertical;
      reviewDetails.Size = new Size(1306, 118);
      reviewDetails.TabIndex = 1;
      // 
      // historyTools
      // 
      historyTools.AutoSize = false;
      historyTools.BackColor = Color.White;
      historyTools.GripStyle = ToolStripGripStyle.Hidden;
      historyTools.ImageScalingSize = new Size(64, 64);
      historyTools.Items.AddRange(new ToolStripItem[] { copyResumeButton });
      historyTools.Location = new Point(3, 3);
      historyTools.Name = "historyTools";
      historyTools.Padding = new Padding(5, 3, 5, 3);
      historyTools.Size = new Size(1306, 106);
      historyTools.TabIndex = 2;
      // 
      // copyResumeButton
      // 
      copyResumeButton.AutoSize = false;
      copyResumeButton.Name = "copyResumeButton";
      copyResumeButton.Size = new Size(94, 98);
      copyResumeButton.Text = "Kopieren";
      copyResumeButton.TextImageRelation = TextImageRelation.ImageAboveText;
      copyResumeButton.ToolTipText = "Fortsetzungsbefehl kopieren";
      // 
      // policiesPage
      // 
      policiesPage.Controls.Add(policyGrid);
      policiesPage.Controls.Add(policyTools);
      policiesPage.Location = new Point(4, 24);
      policiesPage.Name = "policiesPage";
      policiesPage.Padding = new Padding(3);
      policiesPage.Size = new Size(1312, 730);
      policiesPage.TabIndex = 5;
      policiesPage.Text = "Usage-Regeln";
      // 
      // policyGrid
      // 
      policyGrid.AllowUserToAddRows = false;
      policyGrid.AllowUserToDeleteRows = false;
      policyGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
      policyGrid.Columns.AddRange(new DataGridViewColumn[] { policyGridPlatformColumn, policyGridModelColumn, policyGridDaysColumn, policyGridRangeColumn, policyGridLimitColumn, policyGridSprintColumn, policyGridUnknownColumn, policyGridPollingColumn, policyGridZoneColumn });
      policyGrid.Dock = DockStyle.Fill;
      policyGrid.Location = new Point(3, 109);
      policyGrid.MultiSelect = false;
      policyGrid.Name = "policyGrid";
      policyGrid.ReadOnly = true;
      policyGrid.RowHeadersVisible = false;
      policyGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
      policyGrid.Size = new Size(1306, 618);
      policyGrid.TabIndex = 0;
      // 
      // policyGridPlatformColumn
      // 
      policyGridPlatformColumn.HeaderText = "Plattform";
      policyGridPlatformColumn.Name = "policyGridPlatformColumn";
      policyGridPlatformColumn.ReadOnly = true;
      // 
      // policyGridModelColumn
      // 
      policyGridModelColumn.HeaderText = "Modell";
      policyGridModelColumn.Name = "policyGridModelColumn";
      policyGridModelColumn.ReadOnly = true;
      policyGridModelColumn.Width = 130;
      // 
      // policyGridDaysColumn
      // 
      policyGridDaysColumn.HeaderText = "Tage";
      policyGridDaysColumn.Name = "policyGridDaysColumn";
      policyGridDaysColumn.ReadOnly = true;
      policyGridDaysColumn.Width = 210;
      // 
      // policyGridRangeColumn
      // 
      policyGridRangeColumn.HeaderText = "Zeitraum";
      policyGridRangeColumn.Name = "policyGridRangeColumn";
      policyGridRangeColumn.ReadOnly = true;
      policyGridRangeColumn.Width = 130;
      // 
      // policyGridLimitColumn
      // 
      policyGridLimitColumn.HeaderText = "Grenze";
      policyGridLimitColumn.Name = "policyGridLimitColumn";
      policyGridLimitColumn.ReadOnly = true;
      policyGridLimitColumn.Width = 80;
      // 
      // policyGridSprintColumn
      // 
      policyGridSprintColumn.HeaderText = "Endspurt";
      policyGridSprintColumn.Name = "policyGridSprintColumn";
      policyGridSprintColumn.ReadOnly = true;
      policyGridSprintColumn.Width = 170;
      // 
      // policyGridUnknownColumn
      // 
      policyGridUnknownColumn.HeaderText = "Unbekannt";
      policyGridUnknownColumn.Name = "policyGridUnknownColumn";
      policyGridUnknownColumn.ReadOnly = true;
      // 
      // policyGridPollingColumn
      // 
      policyGridPollingColumn.HeaderText = "Polling";
      policyGridPollingColumn.Name = "policyGridPollingColumn";
      policyGridPollingColumn.ReadOnly = true;
      // 
      // policyGridZoneColumn
      // 
      policyGridZoneColumn.HeaderText = "Zeitzone";
      policyGridZoneColumn.Name = "policyGridZoneColumn";
      policyGridZoneColumn.ReadOnly = true;
      policyGridZoneColumn.Width = 220;
      // 
      // policyTools
      // 
      policyTools.AutoSize = false;
      policyTools.BackColor = Color.White;
      policyTools.GripStyle = ToolStripGripStyle.Hidden;
      policyTools.ImageScalingSize = new Size(64, 64);
      policyTools.Items.AddRange(new ToolStripItem[] { addPolicyButton, editPolicyButton, removePolicyButton });
      policyTools.Location = new Point(3, 3);
      policyTools.Name = "policyTools";
      policyTools.Padding = new Padding(5, 3, 5, 3);
      policyTools.Size = new Size(1306, 106);
      policyTools.TabIndex = 1;
      // 
      // addPolicyButton
      // 
      addPolicyButton.AutoSize = false;
      addPolicyButton.Name = "addPolicyButton";
      addPolicyButton.Size = new Size(94, 98);
      addPolicyButton.Text = "Hinzufügen";
      addPolicyButton.TextImageRelation = TextImageRelation.ImageAboveText;
      addPolicyButton.ToolTipText = "Regel hinzufügen";
      // 
      // editPolicyButton
      // 
      editPolicyButton.AutoSize = false;
      editPolicyButton.Name = "editPolicyButton";
      editPolicyButton.Size = new Size(94, 98);
      editPolicyButton.Text = "Bearbeiten";
      editPolicyButton.TextImageRelation = TextImageRelation.ImageAboveText;
      editPolicyButton.ToolTipText = "Regel bearbeiten";
      // 
      // removePolicyButton
      // 
      removePolicyButton.AutoSize = false;
      removePolicyButton.Name = "removePolicyButton";
      removePolicyButton.Size = new Size(94, 98);
      removePolicyButton.Text = "Entfernen";
      removePolicyButton.TextImageRelation = TextImageRelation.ImageAboveText;
      removePolicyButton.ToolTipText = "Regel entfernen";
      // 
      // settingsPage
      // 
      settingsPage.Controls.Add(settingsLayout);
      settingsPage.Location = new Point(4, 24);
      settingsPage.Name = "settingsPage";
      settingsPage.Padding = new Padding(3);
      settingsPage.Size = new Size(1312, 730);
      settingsPage.TabIndex = 6;
      settingsPage.Text = "Einstellungen";
      // 
      // settingsLayout
      // 
      settingsLayout.ColumnCount = 2;
      settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
      settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
      settingsLayout.Controls.Add(settingsNavigation, 0, 0);
      settingsLayout.Controls.Add(settingsBody, 1, 0);
      settingsLayout.Dock = DockStyle.Fill;
      settingsLayout.Location = new Point(3, 3);
      settingsLayout.Name = "settingsLayout";
      settingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
      settingsLayout.Size = new Size(1306, 724);
      settingsLayout.TabIndex = 0;
      // 
      // settingsNavigation
      // 
      settingsNavigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
      settingsNavigation.Controls.Add(navigationLinks, 0, 0);
      settingsNavigation.Controls.Add(settingsActions, 0, 1);
      settingsNavigation.Dock = DockStyle.Fill;
      settingsNavigation.Location = new Point(3, 3);
      settingsNavigation.Name = "settingsNavigation";
      settingsNavigation.RowCount = 2;
      settingsNavigation.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
      settingsNavigation.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));
      settingsNavigation.Size = new Size(214, 718);
      settingsNavigation.TabIndex = 0;
      // 
      // navigationLinks
      // 
      navigationLinks.AutoScroll = true;
      navigationLinks.BackColor = SystemColors.Control;
      navigationLinks.Controls.Add(navigationHeading);
      navigationLinks.Controls.Add(runtimeNavigationButton);
      navigationLinks.Controls.Add(claudeNavigationButton);
      navigationLinks.Controls.Add(testNavigationButton);
      navigationLinks.Dock = DockStyle.Fill;
      navigationLinks.FlowDirection = FlowDirection.TopDown;
      navigationLinks.Location = new Point(3, 3);
      navigationLinks.Name = "navigationLinks";
      navigationLinks.Padding = new Padding(10, 14, 10, 10);
      navigationLinks.Size = new Size(208, 600);
      navigationLinks.TabIndex = 0;
      navigationLinks.WrapContents = false;
      // 
      // navigationHeading
      // 
      navigationHeading.AutoSize = true;
      navigationHeading.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
      navigationHeading.Location = new Point(13, 14);
      navigationHeading.Name = "navigationHeading";
      navigationHeading.Size = new Size(57, 15);
      navigationHeading.TabIndex = 0;
      navigationHeading.Text = "Bereiche";
      // 
      // runtimeNavigationButton
      // 
      runtimeNavigationButton.Location = new Point(13, 32);
      runtimeNavigationButton.Name = "runtimeNavigationButton";
      runtimeNavigationButton.Size = new Size(190, 56);
      runtimeNavigationButton.TabIndex = 1;
      runtimeNavigationButton.Text = "Laufzeit & Provider";
      runtimeNavigationButton.TextAlign = ContentAlignment.MiddleLeft;
      // 
      // claudeNavigationButton
      // 
      claudeNavigationButton.Location = new Point(13, 94);
      claudeNavigationButton.Name = "claudeNavigationButton";
      claudeNavigationButton.Size = new Size(190, 56);
      claudeNavigationButton.TabIndex = 2;
      claudeNavigationButton.Text = "Claude-Ausgabe";
      claudeNavigationButton.TextAlign = ContentAlignment.MiddleLeft;
      // 
      // testNavigationButton
      // 
      testNavigationButton.Location = new Point(13, 156);
      testNavigationButton.Name = "testNavigationButton";
      testNavigationButton.Size = new Size(190, 56);
      testNavigationButton.TabIndex = 3;
      testNavigationButton.Text = "RegEx-Test";
      testNavigationButton.TextAlign = ContentAlignment.MiddleLeft;
      // 
      // settingsActions
      // 
      settingsActions.BorderStyle = BorderStyle.FixedSingle;
      settingsActions.Controls.Add(saveSettingsButton);
      settingsActions.Controls.Add(cancelSettingsButton);
      settingsActions.Dock = DockStyle.Fill;
      settingsActions.FlowDirection = FlowDirection.TopDown;
      settingsActions.Location = new Point(3, 609);
      settingsActions.Name = "settingsActions";
      settingsActions.Padding = new Padding(10, 8, 10, 4);
      settingsActions.Size = new Size(208, 106);
      settingsActions.TabIndex = 1;
      settingsActions.WrapContents = false;
      // 
      // saveSettingsButton
      // 
      saveSettingsButton.Location = new Point(13, 11);
      saveSettingsButton.Name = "saveSettingsButton";
      saveSettingsButton.Size = new Size(190, 42);
      saveSettingsButton.TabIndex = 0;
      saveSettingsButton.Text = "Speichern";
      // 
      // cancelSettingsButton
      // 
      cancelSettingsButton.Location = new Point(13, 59);
      cancelSettingsButton.Name = "cancelSettingsButton";
      cancelSettingsButton.Size = new Size(190, 42);
      cancelSettingsButton.TabIndex = 1;
      cancelSettingsButton.Text = "Abbrechen";
      // 
      // settingsBody
      // 
      settingsBody.Controls.Add(settingsScroll);
      settingsBody.Controls.Add(settingsFooter);
      settingsBody.Dock = DockStyle.Fill;
      settingsBody.Location = new Point(223, 3);
      settingsBody.Name = "settingsBody";
      settingsBody.Size = new Size(1080, 718);
      settingsBody.TabIndex = 1;
      // 
      // settingsScroll
      // 
      settingsScroll.AutoScroll = true;
      settingsScroll.Controls.Add(settingsContent);
      settingsScroll.Dock = DockStyle.Fill;
      settingsScroll.Location = new Point(0, 0);
      settingsScroll.Name = "settingsScroll";
      settingsScroll.Size = new Size(1080, 684);
      settingsScroll.TabIndex = 0;
      // 
      // settingsContent
      // 
      settingsContent.AutoSize = true;
      settingsContent.AutoSizeMode = AutoSizeMode.GrowAndShrink;
      settingsContent.ColumnCount = 3;
      settingsContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
      settingsContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
      settingsContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
      settingsContent.Controls.Add(runtimeHeading, 0, 0);
      settingsContent.Controls.Add(runtimeHint, 0, 1);
      settingsContent.Controls.Add(pollField, 0, 2);
      settingsContent.Controls.Add(timeoutField, 1, 2);
      settingsContent.Controls.Add(usageHistoryIntervalField, 2, 2);
      settingsContent.Controls.Add(maximumAttemptsField, 0, 3);
      settingsContent.Controls.Add(retryBackoffField, 1, 3);
      settingsContent.Controls.Add(maximumRetryBackoffField, 2, 3);
      settingsContent.Controls.Add(regexField, 0, 6);
      settingsContent.Controls.Add(weeklyRegexField, 0, 7);
      settingsContent.Controls.Add(costRegexField, 0, 8);
      settingsContent.Controls.Add(freeRegexField, 0, 9);
      settingsContent.Controls.Add(appServerField, 0, 13);
      settingsContent.Controls.Add(minimizeField, 0, 4);
      settingsContent.Controls.Add(claudeHeading, 0, 5);
      settingsContent.Controls.Add(freeHint, 0, 10);
      settingsContent.Controls.Add(testHeading, 0, 11);
      settingsContent.Controls.Add(testArea, 0, 12);
      settingsContent.Dock = DockStyle.Top;
      settingsContent.Location = new Point(0, 0);
      settingsContent.Name = "settingsContent";
      settingsContent.Padding = new Padding(16, 12, 16, 12);
      settingsContent.RowCount = 14;
      settingsContent.RowStyles.Add(new RowStyle());
      settingsContent.RowStyles.Add(new RowStyle());
      settingsContent.RowStyles.Add(new RowStyle());
      settingsContent.RowStyles.Add(new RowStyle());
      settingsContent.RowStyles.Add(new RowStyle());
      settingsContent.RowStyles.Add(new RowStyle());
      settingsContent.RowStyles.Add(new RowStyle());
      settingsContent.RowStyles.Add(new RowStyle());
      settingsContent.RowStyles.Add(new RowStyle());
      settingsContent.RowStyles.Add(new RowStyle());
      settingsContent.RowStyles.Add(new RowStyle());
      settingsContent.RowStyles.Add(new RowStyle());
      settingsContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 250F));
      settingsContent.RowStyles.Add(new RowStyle());
      settingsContent.Size = new Size(1063, 963);
      settingsContent.TabIndex = 0;
      // 
      // runtimeHeading
      // 
      runtimeHeading.AutoSize = true;
      settingsContent.SetColumnSpan(runtimeHeading, 3);
      runtimeHeading.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
      runtimeHeading.Location = new Point(19, 12);
      runtimeHeading.Name = "runtimeHeading";
      runtimeHeading.Size = new Size(209, 15);
      runtimeHeading.TabIndex = 0;
      runtimeHeading.Text = "Laufzeit- und Provider-Einstellungen";
      // 
      // runtimeHint
      // 
      runtimeHint.AutoSize = true;
      settingsContent.SetColumnSpan(runtimeHint, 3);
      runtimeHint.Location = new Point(19, 27);
      runtimeHint.Name = "runtimeHint";
      runtimeHint.Size = new Size(640, 15);
      runtimeHint.TabIndex = 1;
      runtimeHint.Text = "Änderungen werden validiert und gespeichert. Die Laufzeit- und Provider-Einstellungen gelten nach dem nächsten Start.";
      // 
      // pollField
      // 
      pollField.AutoSize = true;
      pollField.ColumnCount = 1;
      pollField.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
      pollField.Controls.Add(pollLabel, 0, 0);
      pollField.Controls.Add(poll, 0, 1);
      pollField.Dock = DockStyle.Fill;
      pollField.Location = new Point(22, 46);
      pollField.Margin = new Padding(6, 4, 6, 8);
      pollField.Name = "pollField";
      pollField.RowCount = 2;
      pollField.RowStyles.Add(new RowStyle());
      pollField.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
      pollField.Size = new Size(331, 47);
      pollField.TabIndex = 2;
      // 
      // pollLabel
      // 
      pollLabel.AutoSize = true;
      pollLabel.Location = new Point(0, 0);
      pollLabel.Margin = new Padding(0, 0, 0, 4);
      pollLabel.Name = "pollLabel";
      pollLabel.Size = new Size(164, 15);
      pollLabel.TabIndex = 0;
      pollLabel.Text = "Scheduler-Polling (hh:mm:ss)";
      // 
      // poll
      // 
      poll.Dock = DockStyle.Fill;
      poll.Location = new Point(3, 22);
      poll.Name = "poll";
      poll.Size = new Size(325, 23);
      poll.TabIndex = 1;
      // 
      // timeoutField
      // 
      timeoutField.AutoSize = true;
      timeoutField.ColumnCount = 1;
      timeoutField.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
      timeoutField.Controls.Add(timeoutLabel, 0, 0);
      timeoutField.Controls.Add(timeout, 0, 1);
      timeoutField.Dock = DockStyle.Fill;
      timeoutField.Location = new Point(365, 46);
      timeoutField.Margin = new Padding(6, 4, 6, 8);
      timeoutField.Name = "timeoutField";
      timeoutField.RowCount = 2;
      timeoutField.RowStyles.Add(new RowStyle());
      timeoutField.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
      timeoutField.Size = new Size(331, 47);
      timeoutField.TabIndex = 3;
      // 
      // timeoutLabel
      // 
      timeoutLabel.AutoSize = true;
      timeoutLabel.Location = new Point(0, 0);
      timeoutLabel.Margin = new Padding(0, 0, 0, 4);
      timeoutLabel.Name = "timeoutLabel";
      timeoutLabel.Size = new Size(164, 15);
      timeoutLabel.TabIndex = 0;
      timeoutLabel.Text = "Auftrags-Timeout (hh:mm:ss)";
      // 
      // timeout
      // 
      timeout.Dock = DockStyle.Fill;
      timeout.Location = new Point(3, 22);
      timeout.Name = "timeout";
      timeout.Size = new Size(325, 23);
      timeout.TabIndex = 1;
      // 
      // usageHistoryIntervalField
      // 
      usageHistoryIntervalField.AutoSize = true;
      usageHistoryIntervalField.ColumnCount = 1;
      usageHistoryIntervalField.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
      usageHistoryIntervalField.Controls.Add(usageHistoryIntervalLabel, 0, 0);
      usageHistoryIntervalField.Controls.Add(usageHistoryInterval, 0, 1);
      usageHistoryIntervalField.Dock = DockStyle.Fill;
      usageHistoryIntervalField.Location = new Point(708, 46);
      usageHistoryIntervalField.Margin = new Padding(6, 4, 6, 8);
      usageHistoryIntervalField.Name = "usageHistoryIntervalField";
      usageHistoryIntervalField.RowCount = 2;
      usageHistoryIntervalField.RowStyles.Add(new RowStyle());
      usageHistoryIntervalField.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
      usageHistoryIntervalField.Size = new Size(333, 47);
      usageHistoryIntervalField.TabIndex = 4;
      // 
      // usageHistoryIntervalLabel
      // 
      usageHistoryIntervalLabel.AutoSize = true;
      usageHistoryIntervalLabel.Location = new Point(0, 0);
      usageHistoryIntervalLabel.Margin = new Padding(0, 0, 0, 4);
      usageHistoryIntervalLabel.Name = "usageHistoryIntervalLabel";
      usageHistoryIntervalLabel.Size = new Size(216, 15);
      usageHistoryIntervalLabel.TabIndex = 0;
      usageHistoryIntervalLabel.Text = "Usage-Ereignisse in Historie (hh:mm:ss)";
      // 
      // usageHistoryInterval
      // 
      usageHistoryInterval.Dock = DockStyle.Fill;
      usageHistoryInterval.Location = new Point(3, 22);
      usageHistoryInterval.Name = "usageHistoryInterval";
      usageHistoryInterval.Size = new Size(327, 23);
      usageHistoryInterval.TabIndex = 1;
      // 
      // maximumAttemptsField
      // 
      maximumAttemptsField.AutoSize = true;
      maximumAttemptsField.ColumnCount = 1;
      maximumAttemptsField.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
      maximumAttemptsField.Controls.Add(maximumAttemptsLabel, 0, 0);
      maximumAttemptsField.Controls.Add(maximumAttempts, 0, 1);
      maximumAttemptsField.Dock = DockStyle.Fill;
      maximumAttemptsField.Location = new Point(22, 105);
      maximumAttemptsField.Margin = new Padding(6, 4, 6, 8);
      maximumAttemptsField.Name = "maximumAttemptsField";
      maximumAttemptsField.RowCount = 2;
      maximumAttemptsField.RowStyles.Add(new RowStyle());
      maximumAttemptsField.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
      maximumAttemptsField.Size = new Size(331, 47);
      maximumAttemptsField.TabIndex = 5;
      // 
      // maximumAttemptsLabel
      // 
      maximumAttemptsLabel.AutoSize = true;
      maximumAttemptsLabel.Location = new Point(0, 0);
      maximumAttemptsLabel.Margin = new Padding(0, 0, 0, 4);
      maximumAttemptsLabel.Name = "maximumAttemptsLabel";
      maximumAttemptsLabel.Size = new Size(109, 15);
      maximumAttemptsLabel.TabIndex = 0;
      maximumAttemptsLabel.Text = "Maximale Versuche";
      // 
      // maximumAttempts
      // 
      maximumAttempts.Dock = DockStyle.Fill;
      maximumAttempts.Location = new Point(3, 22);
      maximumAttempts.Name = "maximumAttempts";
      maximumAttempts.Size = new Size(325, 23);
      maximumAttempts.TabIndex = 1;
      // 
      // retryBackoffField
      // 
      retryBackoffField.AutoSize = true;
      retryBackoffField.ColumnCount = 1;
      retryBackoffField.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
      retryBackoffField.Controls.Add(retryBackoffLabel, 0, 0);
      retryBackoffField.Controls.Add(retryBackoff, 0, 1);
      retryBackoffField.Dock = DockStyle.Fill;
      retryBackoffField.Location = new Point(365, 105);
      retryBackoffField.Margin = new Padding(6, 4, 6, 8);
      retryBackoffField.Name = "retryBackoffField";
      retryBackoffField.RowCount = 2;
      retryBackoffField.RowStyles.Add(new RowStyle());
      retryBackoffField.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
      retryBackoffField.Size = new Size(331, 47);
      retryBackoffField.TabIndex = 6;
      // 
      // retryBackoffLabel
      // 
      retryBackoffLabel.AutoSize = true;
      retryBackoffLabel.Location = new Point(0, 0);
      retryBackoffLabel.Margin = new Padding(0, 0, 0, 4);
      retryBackoffLabel.Name = "retryBackoffLabel";
      retryBackoffLabel.Size = new Size(142, 15);
      retryBackoffLabel.TabIndex = 0;
      retryBackoffLabel.Text = "Retry-Backoff (hh:mm:ss)";
      // 
      // retryBackoff
      // 
      retryBackoff.Dock = DockStyle.Fill;
      retryBackoff.Location = new Point(3, 22);
      retryBackoff.Name = "retryBackoff";
      retryBackoff.Size = new Size(325, 23);
      retryBackoff.TabIndex = 1;
      // 
      // maximumRetryBackoffField
      // 
      maximumRetryBackoffField.AutoSize = true;
      maximumRetryBackoffField.ColumnCount = 1;
      maximumRetryBackoffField.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
      maximumRetryBackoffField.Controls.Add(maximumRetryBackoffLabel, 0, 0);
      maximumRetryBackoffField.Controls.Add(maximumRetryBackoff, 0, 1);
      maximumRetryBackoffField.Dock = DockStyle.Fill;
      maximumRetryBackoffField.Location = new Point(708, 105);
      maximumRetryBackoffField.Margin = new Padding(6, 4, 6, 8);
      maximumRetryBackoffField.Name = "maximumRetryBackoffField";
      maximumRetryBackoffField.RowCount = 2;
      maximumRetryBackoffField.RowStyles.Add(new RowStyle());
      maximumRetryBackoffField.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
      maximumRetryBackoffField.Size = new Size(333, 47);
      maximumRetryBackoffField.TabIndex = 7;
      // 
      // maximumRetryBackoffLabel
      // 
      maximumRetryBackoffLabel.AutoSize = true;
      maximumRetryBackoffLabel.Location = new Point(0, 0);
      maximumRetryBackoffLabel.Margin = new Padding(0, 0, 0, 4);
      maximumRetryBackoffLabel.Name = "maximumRetryBackoffLabel";
      maximumRetryBackoffLabel.Size = new Size(201, 15);
      maximumRetryBackoffLabel.TabIndex = 0;
      maximumRetryBackoffLabel.Text = "Maximaler Retry-Backoff (hh:mm:ss)";
      // 
      // maximumRetryBackoff
      // 
      maximumRetryBackoff.Dock = DockStyle.Fill;
      maximumRetryBackoff.Location = new Point(3, 22);
      maximumRetryBackoff.Name = "maximumRetryBackoff";
      maximumRetryBackoff.Size = new Size(327, 23);
      maximumRetryBackoff.TabIndex = 1;
      // 
      // regexField
      // 
      regexField.AutoSize = true;
      regexField.ColumnCount = 1;
      settingsContent.SetColumnSpan(regexField, 3);
      regexField.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
      regexField.Controls.Add(regexLabel, 0, 0);
      regexField.Controls.Add(regex, 0, 1);
      regexField.Dock = DockStyle.Fill;
      regexField.Location = new Point(22, 236);
      regexField.Margin = new Padding(6, 4, 6, 8);
      regexField.Name = "regexField";
      regexField.RowCount = 2;
      regexField.RowStyles.Add(new RowStyle());
      regexField.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
      regexField.Size = new Size(1019, 83);
      regexField.TabIndex = 8;
      // 
      // regexLabel
      // 
      regexLabel.AutoSize = true;
      regexLabel.Location = new Point(0, 0);
      regexLabel.Margin = new Padding(0, 0, 0, 4);
      regexLabel.Name = "regexLabel";
      regexLabel.Size = new Size(211, 15);
      regexLabel.TabIndex = 0;
      regexLabel.Text = "Claude Session-RegEx (5h-Kontingent)";
      // 
      // regex
      // 
      regex.Dock = DockStyle.Fill;
      regex.Location = new Point(3, 22);
      regex.Multiline = true;
      regex.Name = "regex";
      regex.ScrollBars = ScrollBars.Both;
      regex.Size = new Size(1013, 58);
      regex.TabIndex = 1;
      regex.WordWrap = false;
      // 
      // weeklyRegexField
      // 
      weeklyRegexField.AutoSize = true;
      weeklyRegexField.ColumnCount = 1;
      settingsContent.SetColumnSpan(weeklyRegexField, 3);
      weeklyRegexField.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
      weeklyRegexField.Controls.Add(weeklyRegexLabel, 0, 0);
      weeklyRegexField.Controls.Add(weeklyRegex, 0, 1);
      weeklyRegexField.Dock = DockStyle.Fill;
      weeklyRegexField.Location = new Point(22, 331);
      weeklyRegexField.Margin = new Padding(6, 4, 6, 8);
      weeklyRegexField.Name = "weeklyRegexField";
      weeklyRegexField.RowCount = 2;
      weeklyRegexField.RowStyles.Add(new RowStyle());
      weeklyRegexField.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
      weeklyRegexField.Size = new Size(1019, 83);
      weeklyRegexField.TabIndex = 9;
      // 
      // weeklyRegexLabel
      // 
      weeklyRegexLabel.AutoSize = true;
      weeklyRegexLabel.Location = new Point(0, 0);
      weeklyRegexLabel.Margin = new Padding(0, 0, 0, 4);
      weeklyRegexLabel.Name = "weeklyRegexLabel";
      weeklyRegexLabel.Size = new Size(128, 15);
      weeklyRegexLabel.TabIndex = 0;
      weeklyRegexLabel.Text = "Claude Wochen-RegEx";
      // 
      // weeklyRegex
      // 
      weeklyRegex.Dock = DockStyle.Fill;
      weeklyRegex.Location = new Point(3, 22);
      weeklyRegex.Multiline = true;
      weeklyRegex.Name = "weeklyRegex";
      weeklyRegex.ScrollBars = ScrollBars.Both;
      weeklyRegex.Size = new Size(1013, 58);
      weeklyRegex.TabIndex = 1;
      weeklyRegex.WordWrap = false;
      // 
      // costRegexField
      // 
      costRegexField.AutoSize = true;
      costRegexField.ColumnCount = 1;
      settingsContent.SetColumnSpan(costRegexField, 3);
      costRegexField.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
      costRegexField.Controls.Add(costRegexLabel, 0, 0);
      costRegexField.Controls.Add(costRegex, 0, 1);
      costRegexField.Dock = DockStyle.Fill;
      costRegexField.Location = new Point(22, 426);
      costRegexField.Margin = new Padding(6, 4, 6, 8);
      costRegexField.Name = "costRegexField";
      costRegexField.RowCount = 2;
      costRegexField.RowStyles.Add(new RowStyle());
      costRegexField.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
      costRegexField.Size = new Size(1019, 83);
      costRegexField.TabIndex = 10;
      // 
      // costRegexLabel
      // 
      costRegexLabel.AutoSize = true;
      costRegexLabel.Location = new Point(0, 0);
      costRegexLabel.Margin = new Padding(0, 0, 0, 4);
      costRegexLabel.Name = "costRegexLabel";
      costRegexLabel.Size = new Size(201, 15);
      costRegexLabel.TabIndex = 0;
      costRegexLabel.Text = "Claude Total-Cost-RegEx (ohne Abo)";
      // 
      // costRegex
      // 
      costRegex.Dock = DockStyle.Fill;
      costRegex.Location = new Point(3, 22);
      costRegex.Multiline = true;
      costRegex.Name = "costRegex";
      costRegex.ScrollBars = ScrollBars.Both;
      costRegex.Size = new Size(1013, 58);
      costRegex.TabIndex = 1;
      costRegex.WordWrap = false;
      // 
      // freeRegexField
      // 
      freeRegexField.AutoSize = true;
      freeRegexField.ColumnCount = 1;
      settingsContent.SetColumnSpan(freeRegexField, 3);
      freeRegexField.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
      freeRegexField.Controls.Add(freeRegexLabel, 0, 0);
      freeRegexField.Controls.Add(freeRegex, 0, 1);
      freeRegexField.Dock = DockStyle.Fill;
      freeRegexField.Location = new Point(22, 521);
      freeRegexField.Margin = new Padding(6, 4, 6, 8);
      freeRegexField.Name = "freeRegexField";
      freeRegexField.RowCount = 2;
      freeRegexField.RowStyles.Add(new RowStyle());
      freeRegexField.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
      freeRegexField.Size = new Size(1019, 83);
      freeRegexField.TabIndex = 11;
      // 
      // freeRegexLabel
      // 
      freeRegexLabel.AutoSize = true;
      freeRegexLabel.Location = new Point(0, 0);
      freeRegexLabel.Margin = new Padding(0, 0, 0, 4);
      freeRegexLabel.Name = "freeRegexLabel";
      freeRegexLabel.Size = new Size(268, 15);
      freeRegexLabel.TabIndex = 0;
      freeRegexLabel.Text = "Claude Kontotyp-RegEx (Free-Account erkennen)";
      // 
      // freeRegex
      // 
      freeRegex.Dock = DockStyle.Fill;
      freeRegex.Location = new Point(3, 22);
      freeRegex.Multiline = true;
      freeRegex.Name = "freeRegex";
      freeRegex.ScrollBars = ScrollBars.Both;
      freeRegex.Size = new Size(1013, 58);
      freeRegex.TabIndex = 1;
      freeRegex.WordWrap = false;
      // 
      // appServerField
      // 
      appServerField.AutoSize = true;
      appServerField.ColumnCount = 1;
      settingsContent.SetColumnSpan(appServerField, 3);
      appServerField.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
      appServerField.Controls.Add(appServerLabel, 0, 0);
      appServerField.Controls.Add(appServer, 0, 1);
      appServerField.Dock = DockStyle.Fill;
      appServerField.Location = new Point(22, 896);
      appServerField.Margin = new Padding(6, 4, 6, 8);
      appServerField.Name = "appServerField";
      appServerField.RowCount = 2;
      appServerField.RowStyles.Add(new RowStyle());
      appServerField.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
      appServerField.Size = new Size(1019, 47);
      appServerField.TabIndex = 12;
      // 
      // appServerLabel
      // 
      appServerLabel.AutoSize = true;
      appServerLabel.Location = new Point(0, 0);
      appServerLabel.Margin = new Padding(0, 0, 0, 4);
      appServerLabel.Name = "appServerLabel";
      appServerLabel.Size = new Size(207, 15);
      appServerLabel.TabIndex = 0;
      appServerLabel.Text = "Codex App-Server-Argumente (JSON)";
      // 
      // appServer
      // 
      appServer.Dock = DockStyle.Fill;
      appServer.Location = new Point(3, 22);
      appServer.Name = "appServer";
      appServer.Size = new Size(1013, 23);
      appServer.TabIndex = 1;
      // 
      // minimizeField
      // 
      minimizeField.AutoSize = true;
      minimizeField.ColumnCount = 1;
      minimizeField.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
      minimizeField.Controls.Add(minimizeLabel, 0, 0);
      minimizeField.Controls.Add(minimizeTarget, 0, 1);
      minimizeField.Dock = DockStyle.Fill;
      minimizeField.Location = new Point(22, 164);
      minimizeField.Margin = new Padding(6, 4, 6, 8);
      minimizeField.Name = "minimizeField";
      minimizeField.RowCount = 2;
      minimizeField.RowStyles.Add(new RowStyle());
      minimizeField.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
      minimizeField.Size = new Size(331, 45);
      minimizeField.TabIndex = 13;
      // 
      // minimizeLabel
      // 
      minimizeLabel.AutoSize = true;
      minimizeLabel.Location = new Point(3, 0);
      minimizeLabel.Name = "minimizeLabel";
      minimizeLabel.Size = new Size(98, 15);
      minimizeLabel.TabIndex = 0;
      minimizeLabel.Text = "Beim Minimieren";
      // 
      // minimizeTarget
      // 
      minimizeTarget.Dock = DockStyle.Fill;
      minimizeTarget.DropDownStyle = ComboBoxStyle.DropDownList;
      minimizeTarget.Items.AddRange(new object[] { "Taskleiste", "Infobereich (Tray)" });
      minimizeTarget.Location = new Point(3, 18);
      minimizeTarget.Name = "minimizeTarget";
      minimizeTarget.Size = new Size(325, 23);
      minimizeTarget.TabIndex = 1;
      // 
      // claudeHeading
      // 
      claudeHeading.AutoSize = true;
      settingsContent.SetColumnSpan(claudeHeading, 3);
      claudeHeading.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
      claudeHeading.Location = new Point(19, 217);
      claudeHeading.Name = "claudeHeading";
      claudeHeading.Size = new Size(153, 15);
      claudeHeading.TabIndex = 14;
      claudeHeading.Text = "Claude-Ausgabe erkennen";
      // 
      // freeHint
      // 
      freeHint.AutoSize = true;
      settingsContent.SetColumnSpan(freeHint, 3);
      freeHint.Location = new Point(19, 612);
      freeHint.Name = "freeHint";
      freeHint.Size = new Size(849, 15);
      freeHint.TabIndex = 15;
      freeHint.Text = "Der Kontotyp-RegEx erkennt Hinweise wie 'free account', 'free plan' oder 'free tier'. Für erkannte Konten ohne Abo wird das Kontingent als verbraucht angezeigt.";
      // 
      // testHeading
      // 
      testHeading.AutoSize = true;
      settingsContent.SetColumnSpan(testHeading, 3);
      testHeading.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
      testHeading.Location = new Point(19, 627);
      testHeading.Name = "testHeading";
      testHeading.Size = new Size(70, 15);
      testHeading.TabIndex = 16;
      testHeading.Text = "RegEx-Test";
      // 
      // testArea
      // 
      settingsContent.SetColumnSpan(testArea, 3);
      testArea.Controls.Add(testLayout);
      testArea.Dock = DockStyle.Fill;
      testArea.Location = new Point(22, 644);
      testArea.Margin = new Padding(6, 2, 6, 12);
      testArea.Name = "testArea";
      testArea.Size = new Size(1019, 236);
      testArea.TabIndex = 17;
      testArea.TabStop = false;
      // 
      // testLayout
      // 
      testLayout.ColumnCount = 1;
      testLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
      testLayout.Controls.Add(regexChoiceLabel, 0, 0);
      testLayout.Controls.Add(regexChoice, 0, 1);
      testLayout.Controls.Add(sampleLabel, 0, 2);
      testLayout.Controls.Add(sample, 0, 3);
      testLayout.Controls.Add(testButton, 0, 4);
      testLayout.Controls.Add(testResult, 0, 5);
      testLayout.Dock = DockStyle.Fill;
      testLayout.Location = new Point(3, 19);
      testLayout.Name = "testLayout";
      testLayout.Padding = new Padding(12, 8, 12, 8);
      testLayout.RowCount = 6;
      testLayout.RowStyles.Add(new RowStyle());
      testLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
      testLayout.RowStyles.Add(new RowStyle());
      testLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
      testLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
      testLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
      testLayout.Size = new Size(1013, 214);
      testLayout.TabIndex = 0;
      // 
      // regexChoiceLabel
      // 
      regexChoiceLabel.AutoSize = true;
      regexChoiceLabel.Location = new Point(15, 8);
      regexChoiceLabel.Name = "regexChoiceLabel";
      regexChoiceLabel.Size = new Size(98, 15);
      regexChoiceLabel.TabIndex = 0;
      regexChoiceLabel.Text = "RegEx auswählen";
      // 
      // regexChoice
      // 
      regexChoice.Dock = DockStyle.Fill;
      regexChoice.DropDownStyle = ComboBoxStyle.DropDownList;
      regexChoice.Items.AddRange(new object[] { "Session (5h-Kontingent)", "Woche", "Total Cost (ohne Abo)", "Kontotyp (Free-Account)" });
      regexChoice.Location = new Point(15, 26);
      regexChoice.Name = "regexChoice";
      regexChoice.Size = new Size(983, 23);
      regexChoice.TabIndex = 1;
      // 
      // sampleLabel
      // 
      sampleLabel.AutoSize = true;
      sampleLabel.Location = new Point(15, 53);
      sampleLabel.Name = "sampleLabel";
      sampleLabel.Size = new Size(320, 15);
      sampleLabel.TabIndex = 2;
      sampleLabel.Text = "Claude-Beispielausgabe (anpassbar, wird nicht gespeichert)";
      // 
      // sample
      // 
      sample.Dock = DockStyle.Fill;
      sample.Location = new Point(15, 71);
      sample.Multiline = true;
      sample.Name = "sample";
      sample.ScrollBars = ScrollBars.Vertical;
      sample.Size = new Size(983, 66);
      sample.TabIndex = 3;
      sample.Text = "Current session: 42% used";
      // 
      // testButton
      // 
      testButton.Anchor = AnchorStyles.Left;
      testButton.AutoSize = true;
      testButton.Location = new Point(15, 146);
      testButton.Name = "testButton";
      testButton.Size = new Size(72, 25);
      testButton.TabIndex = 4;
      testButton.Text = "Test RegEx";
      // 
      // testResult
      // 
      testResult.Dock = DockStyle.Fill;
      testResult.Location = new Point(15, 178);
      testResult.Name = "testResult";
      testResult.Size = new Size(983, 28);
      testResult.TabIndex = 5;
      testResult.Text = "Noch nicht getestet.";
      testResult.TextAlign = ContentAlignment.MiddleLeft;
      // 
      // settingsFooter
      // 
      settingsFooter.ColumnCount = 2;
      settingsFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
      settingsFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
      settingsFooter.Controls.Add(projectLink, 0, 0);
      settingsFooter.Controls.Add(versionLabel, 1, 0);
      settingsFooter.Dock = DockStyle.Bottom;
      settingsFooter.Location = new Point(0, 684);
      settingsFooter.Name = "settingsFooter";
      settingsFooter.Padding = new Padding(22, 0, 22, 0);
      settingsFooter.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
      settingsFooter.Size = new Size(1080, 34);
      settingsFooter.TabIndex = 1;
      // 
      // projectLink
      // 
      projectLink.Dock = DockStyle.Fill;
      projectLink.Location = new Point(25, 0);
      projectLink.Name = "projectLink";
      projectLink.Size = new Size(910, 34);
      projectLink.TabIndex = 0;
      projectLink.TabStop = true;
      projectLink.Text = "https://github.com/VoSs2o0o/KIScheduler";
      projectLink.TextAlign = ContentAlignment.MiddleLeft;
      // 
      // versionLabel
      // 
      versionLabel.Dock = DockStyle.Fill;
      versionLabel.Location = new Point(941, 0);
      versionLabel.Name = "versionLabel";
      versionLabel.Size = new Size(114, 34);
      versionLabel.TabIndex = 1;
      versionLabel.TextAlign = ContentAlignment.MiddleRight;
      // 
      // status
      // 
      status.Items.AddRange(new ToolStripItem[] { workerState, statusSpring, usageState, runningState });
      status.Location = new Point(0, 758);
      status.Name = "status";
      status.Size = new Size(1320, 22);
      status.TabIndex = 1;
      // 
      // workerState
      // 
      workerState.BorderSides = ToolStripStatusLabelBorderSides.Left | ToolStripStatusLabelBorderSides.Top | ToolStripStatusLabelBorderSides.Right | ToolStripStatusLabelBorderSides.Bottom;
      workerState.Name = "workerState";
      workerState.Padding = new Padding(6, 2, 6, 2);
      workerState.Size = new Size(16, 17);
      // 
      // statusSpring
      // 
      statusSpring.Name = "statusSpring";
      statusSpring.Size = new Size(1265, 17);
      statusSpring.Spring = true;
      // 
      // usageState
      // 
      usageState.BackColor = Color.AliceBlue;
      usageState.BorderSides = ToolStripStatusLabelBorderSides.Left | ToolStripStatusLabelBorderSides.Top | ToolStripStatusLabelBorderSides.Right | ToolStripStatusLabelBorderSides.Bottom;
      usageState.Margin = new Padding(8, 0, 0, 0);
      usageState.Name = "usageState";
      usageState.Padding = new Padding(6, 2, 6, 2);
      usageState.Size = new Size(16, 22);
      usageState.Visible = false;
      // 
      // runningState
      // 
      runningState.BackColor = Color.LemonChiffon;
      runningState.BorderSides = ToolStripStatusLabelBorderSides.Left | ToolStripStatusLabelBorderSides.Top | ToolStripStatusLabelBorderSides.Right | ToolStripStatusLabelBorderSides.Bottom;
      runningState.Margin = new Padding(8, 0, 0, 0);
      runningState.Name = "runningState";
      runningState.Padding = new Padding(6, 2, 6, 2);
      runningState.Size = new Size(16, 22);
      // 
      // MainForm
      // 
      ClientSize = new Size(1320, 780);
      Controls.Add(tabs);
      Controls.Add(status);
      Icon = (Icon)resources.GetObject("$this.Icon");
      MinimumSize = new Size(1000, 650);
      Name = "MainForm";
      StartPosition = FormStartPosition.CenterScreen;
      Text = "KIScheduler";
      tabs.ResumeLayout(false);
      queuePage.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)queue).EndInit();
      queueFilters.ResumeLayout(false);
      queueFilters.PerformLayout();
      queueTools.ResumeLayout(false);
      queueTools.PerformLayout();
      projectsPage.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)projectGrid).EndInit();
      projectTools.ResumeLayout(false);
      projectTools.PerformLayout();
      platformPage.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)platformGrid).EndInit();
      platformTools.ResumeLayout(false);
      platformTools.PerformLayout();
      blocksPage.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)blockGrid).EndInit();
      blockTools.ResumeLayout(false);
      blockTools.PerformLayout();
      historyPage.ResumeLayout(false);
      historyPage.PerformLayout();
      historySplit.Panel1.ResumeLayout(false);
      historySplit.Panel2.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)historySplit).EndInit();
      historySplit.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)attemptsGrid).EndInit();
      ((System.ComponentModel.ISupportInitialize)eventsGrid).EndInit();
      historyTools.ResumeLayout(false);
      historyTools.PerformLayout();
      policiesPage.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)policyGrid).EndInit();
      policyTools.ResumeLayout(false);
      policyTools.PerformLayout();
      settingsPage.ResumeLayout(false);
      settingsLayout.ResumeLayout(false);
      settingsNavigation.ResumeLayout(false);
      navigationLinks.ResumeLayout(false);
      navigationLinks.PerformLayout();
      settingsActions.ResumeLayout(false);
      settingsBody.ResumeLayout(false);
      settingsScroll.ResumeLayout(false);
      settingsScroll.PerformLayout();
      settingsContent.ResumeLayout(false);
      settingsContent.PerformLayout();
      pollField.ResumeLayout(false);
      pollField.PerformLayout();
      timeoutField.ResumeLayout(false);
      timeoutField.PerformLayout();
      usageHistoryIntervalField.ResumeLayout(false);
      usageHistoryIntervalField.PerformLayout();
      maximumAttemptsField.ResumeLayout(false);
      maximumAttemptsField.PerformLayout();
      retryBackoffField.ResumeLayout(false);
      retryBackoffField.PerformLayout();
      maximumRetryBackoffField.ResumeLayout(false);
      maximumRetryBackoffField.PerformLayout();
      regexField.ResumeLayout(false);
      regexField.PerformLayout();
      weeklyRegexField.ResumeLayout(false);
      weeklyRegexField.PerformLayout();
      costRegexField.ResumeLayout(false);
      costRegexField.PerformLayout();
      freeRegexField.ResumeLayout(false);
      freeRegexField.PerformLayout();
      appServerField.ResumeLayout(false);
      appServerField.PerformLayout();
      minimizeField.ResumeLayout(false);
      minimizeField.PerformLayout();
      testArea.ResumeLayout(false);
      testLayout.ResumeLayout(false);
      testLayout.PerformLayout();
      settingsFooter.ResumeLayout(false);
      status.ResumeLayout(false);
      status.PerformLayout();
      ResumeLayout(false);
      PerformLayout();
   }
}
