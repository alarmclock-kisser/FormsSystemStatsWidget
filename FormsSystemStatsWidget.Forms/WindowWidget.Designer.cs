namespace FormsSystemStatsWidget.Forms
{
    partial class WindowWidget
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.pictureBox_cpu = new PictureBox();
            this.progressBar_ram = new ProgressBar();
            this.label_ram = new Label();
            this.contextMenuStrip_widget = new ContextMenuStrip(this.components);
            this.updateIntervalToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_interval = new ToolStripTextBox();
            this.selectGPUToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripComboBox_gpus = new ToolStripComboBox();
            this.diagramColorToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_diagramColor = new ToolStripTextBox();
            this.toolStripMenuItem_opacity = new ToolStripMenuItem();
            this.toolStripTextBox_opacity = new ToolStripTextBox();
            this.toolStripMenuItem_clickThrough = new ToolStripMenuItem();
            this.toolStripComboBox_clickOntoHotkey = new ToolStripComboBox();
            this.showUsageToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_percentageColor = new ToolStripTextBox();
            this.alwaysOnTopToolStripMenuItem = new ToolStripMenuItem();
            this.trafficThresholdToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_threshold = new ToolStripTextBox();
            this.toolStripSeparator5 = new ToolStripSeparator();
            this.driveSpeedTestToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripComboBox_drives = new ToolStripComboBox();
            this.testSettingsToolStripMenuItem = new ToolStripMenuItem();
            this.fileSizeMBToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_testFileSizeMb = new ToolStripTextBox();
            this.blockSizeKBToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_testBlockSizeKb = new ToolStripTextBox();
            this.passesToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_testPasses = new ToolStripTextBox();
            this.threadsToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_testThreads = new ToolStripTextBox();
            this.writeThroughToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripSeparator1 = new ToolStripSeparator();
            this.toolStripMenuItem_loadLlamaCppServer = new ToolStripMenuItem();
            this.toolStripMenuItem_modelsDirectory = new ToolStripMenuItem();
            this.toolStripTextBox_modelsDirectory = new ToolStripTextBox();
            this.toolStripComboBox_ggufModels = new ToolStripComboBox();
            this.toolStripMenuItem_loadMmproj = new ToolStripMenuItem();
            this.toolStripMenuItem_contextSize = new ToolStripMenuItem();
            this.toolStripTextBox_contextSize = new ToolStripTextBox();
            this.toolStripMenuItem_batchSize = new ToolStripMenuItem();
            this.toolStripTextBox_batchSize = new ToolStripTextBox();
            this.toolStripMenuItem_splitMode = new ToolStripMenuItem();
            this.toolStripComboBox_splitMode = new ToolStripComboBox();
            this.toolStripMenuItem_tensorSplit = new ToolStripMenuItem();
            this.toolStripTextBox_tensorSplit = new ToolStripTextBox();
            this.toolStripMenuItem_flashAttention = new ToolStripMenuItem();
            this.toolStripMenuItem_gpuLayersCount = new ToolStripMenuItem();
            this.toolStripTextBox_gpuLayersCount = new ToolStripTextBox();
            this.toolStripMenuItem_parallelSlots = new ToolStripMenuItem();
            this.toolStripTextBox_numberParallelSlots = new ToolStripTextBox();
            this.toolStripMenuItem_noWarmup = new ToolStripMenuItem();
            this.toolStripMenuItem_fitMode = new ToolStripMenuItem();
            this.KVoffload_ToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripMenuItem_kvCacheType = new ToolStripMenuItem();
            this.toolStripComboBox_cacheType = new ToolStripComboBox();
            this.toolStripMenuItem_toolCalls = new ToolStripMenuItem();
            this.toolStripSeparator3 = new ToolStripSeparator();
            this.toolStripMenuItem_temperature = new ToolStripMenuItem();
            this.toolStripTextBox_temperature = new ToolStripTextBox();
            this.toolStripMenuItem_repetitionPenalty = new ToolStripMenuItem();
            this.toolStripTextBox_repetationPenalty = new ToolStripTextBox();
            this.toolStripMenuItem_thinkingBudget = new ToolStripMenuItem();
            this.toolStripTextBox_thinkingBudget = new ToolStripTextBox();
            this.toolStripMenuItem_reasoningBudget = new ToolStripMenuItem();
            this.toolStripTextBox_reasoningBudget = new ToolStripTextBox();
            this.toolStripMenuItem_topP = new ToolStripMenuItem();
            this.toolStripTextBox_topP = new ToolStripTextBox();
            this.toolStripMenuItem_minP = new ToolStripMenuItem();
            this.toolStripTextBox_minP = new ToolStripTextBox();
            this.toolStripMenuItem_topK = new ToolStripMenuItem();
            this.toolStripTextBox_topK = new ToolStripTextBox();
            this.toolStripMenuItem_execModelLoadBat = new ToolStripMenuItem();
            this.toolStripComboBox_modelLoadBats = new ToolStripComboBox();
            this.toolStripMenuItem_hideCmd = new ToolStripMenuItem();
            this.rerouteAPILlamacppOllamaToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripMenuItem_openAiApi = new ToolStripMenuItem();
            this.toolStripTextBox_openAiApiUrl = new ToolStripTextBox();
            this.toolStripSeparator4 = new ToolStripSeparator();
            this.llamacppPortToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_llamacppPort = new ToolStripTextBox();
            this.ollamaPortToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_ollamaPort = new ToolStripTextBox();
            this.smartPromptOptimizationsToolStripMenuItem = new ToolStripMenuItem();
            this.enableSmartPromptOptimizationsToolStripMenuItem = new ToolStripMenuItem();
            this.promptSafetyRatioToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_promptSafetyRatio = new ToolStripTextBox();
            this.smartBudgetRatioToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_smartBudgetRatio = new ToolStripTextBox();
            this.largeMessageThresholdCharsToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_largeMessageThresholdChars = new ToolStripTextBox();
            this.skeletonMaxLinesToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_skeletonMaxLines = new ToolStripTextBox();
            this.focusKeywordLimitToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_focusKeywordLimit = new ToolStripTextBox();
            this.tailKeepBonusCharsToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_tailKeepBonusChars = new ToolStripTextBox();
            this.showTokenssToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripSeparator2 = new ToolStripSeparator();
            this.toolStripMenuItem_configureVoiceInputHotkey = new ToolStripMenuItem();
            this.toolStripMenuItem_remapAnyKey = new ToolStripMenuItem();
            this.toolStripSeparator6 = new ToolStripSeparator();
            this.openDebugConsoleToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripMenuItem_visuallyFormatLog = new ToolStripMenuItem();
            this.toolStripMenuItem_includeRawChunksLog = new ToolStripMenuItem();
            this.toolStripMenuItem_logGenerationSpeed = new ToolStripMenuItem();
            this.label_vram = new Label();
            this.progressBar_vram = new ProgressBar();
            this.label_wattage = new Label();
            this.label_gpuUsage = new Label();
            this.label_gpuLoad2 = new Label();
            this.label_gpuWatts2 = new Label();
            this.label_gpuVram2 = new Label();
            this.progressBar_vram2 = new ProgressBar();
            this.label_avgCpuLoadAndTemperature = new Label();
            this.label_topTasksList = new Label();
            this.button_recordUsages = new Button();
            this.label_routingPortsInfo = new Label();
            ((System.ComponentModel.ISupportInitialize) this.pictureBox_cpu).BeginInit();
            this.contextMenuStrip_widget.SuspendLayout();
            this.SuspendLayout();
            // 
            // pictureBox_cpu
            // 
            this.pictureBox_cpu.BackColor = SystemColors.ActiveBorder;
            this.pictureBox_cpu.Dock = DockStyle.Top;
            this.pictureBox_cpu.Location = new Point(0, 0);
            this.pictureBox_cpu.Name = "pictureBox_cpu";
            this.pictureBox_cpu.Size = new Size(240, 100);
            this.pictureBox_cpu.TabIndex = 0;
            this.pictureBox_cpu.TabStop = false;
            // 
            // progressBar_ram
            // 
            this.progressBar_ram.Location = new Point(0, 160);
            this.progressBar_ram.Maximum = 1000;
            this.progressBar_ram.Name = "progressBar_ram";
            this.progressBar_ram.Size = new Size(240, 12);
            this.progressBar_ram.TabIndex = 1;
            // 
            // label_ram
            // 
            this.label_ram.AutoSize = true;
            this.label_ram.Font = new Font("Bahnschrift Condensed", 9.75F);
            this.label_ram.Location = new Point(0, 142);
            this.label_ram.Name = "label_ram";
            this.label_ram.Size = new Size(36, 16);
            this.label_ram.TabIndex = 2;
            this.label_ram.Text = "RAM: -";
            // 
            // contextMenuStrip_widget
            // 
            this.contextMenuStrip_widget.Items.AddRange(new ToolStripItem[] { this.updateIntervalToolStripMenuItem, this.selectGPUToolStripMenuItem, this.diagramColorToolStripMenuItem, this.toolStripMenuItem_opacity, this.showUsageToolStripMenuItem, this.alwaysOnTopToolStripMenuItem, this.trafficThresholdToolStripMenuItem, this.toolStripSeparator5, this.driveSpeedTestToolStripMenuItem, this.toolStripSeparator1, this.toolStripMenuItem_loadLlamaCppServer, this.toolStripMenuItem_execModelLoadBat, this.rerouteAPILlamacppOllamaToolStripMenuItem, this.smartPromptOptimizationsToolStripMenuItem, this.showTokenssToolStripMenuItem, this.toolStripSeparator2, this.toolStripMenuItem_configureVoiceInputHotkey, this.toolStripMenuItem_remapAnyKey, this.toolStripSeparator6, this.openDebugConsoleToolStripMenuItem });
            this.contextMenuStrip_widget.Name = "contextMenuStrip_widget";
            this.contextMenuStrip_widget.Size = new Size(275, 402);
            this.contextMenuStrip_widget.Text = "Settings";
            this.contextMenuStrip_widget.Opening += this.contextMenuStrip_widget_Opening;
            // 
            // updateIntervalToolStripMenuItem
            // 
            this.updateIntervalToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_interval });
            this.updateIntervalToolStripMenuItem.Name = "updateIntervalToolStripMenuItem";
            this.updateIntervalToolStripMenuItem.Size = new Size(274, 22);
            this.updateIntervalToolStripMenuItem.Text = "Update Interval ...";
            // 
            // toolStripTextBox_interval
            // 
            this.toolStripTextBox_interval.Name = "toolStripTextBox_interval";
            this.toolStripTextBox_interval.Size = new Size(100, 23);
            this.toolStripTextBox_interval.Text = "420";
            this.toolStripTextBox_interval.Leave += this.toolStripTextBox_interval_Leave;
            this.toolStripTextBox_interval.KeyDown += this.toolStripTextBox_interval_KeyDown;
            // 
            // selectGPUToolStripMenuItem
            // 
            this.selectGPUToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripComboBox_gpus });
            this.selectGPUToolStripMenuItem.Name = "selectGPUToolStripMenuItem";
            this.selectGPUToolStripMenuItem.Size = new Size(274, 22);
            this.selectGPUToolStripMenuItem.Text = "Select GPU ...";
            // 
            // toolStripComboBox_gpus
            // 
            this.toolStripComboBox_gpus.Name = "toolStripComboBox_gpus";
            this.toolStripComboBox_gpus.Size = new Size(121, 23);
            this.toolStripComboBox_gpus.Text = "0";
            this.toolStripComboBox_gpus.SelectedIndexChanged += this.toolStripComboBox_gpus_SelectedIndexChanged;
            // 
            // diagramColorToolStripMenuItem
            // 
            this.diagramColorToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_diagramColor });
            this.diagramColorToolStripMenuItem.Name = "diagramColorToolStripMenuItem";
            this.diagramColorToolStripMenuItem.Size = new Size(274, 22);
            this.diagramColorToolStripMenuItem.Text = "Diagram Color ...";
            // 
            // toolStripTextBox_diagramColor
            // 
            this.toolStripTextBox_diagramColor.Name = "toolStripTextBox_diagramColor";
            this.toolStripTextBox_diagramColor.Size = new Size(100, 23);
            this.toolStripTextBox_diagramColor.Text = "#ffffff";
            this.toolStripTextBox_diagramColor.DoubleClick += this.toolStripTextBox_diagramColor_DoubleClick;
            this.toolStripTextBox_diagramColor.TextChanged += this.toolStripTextBox_diagramColor_TextChanged;
            // 
            // toolStripMenuItem_opacity
            // 
            this.toolStripMenuItem_opacity.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_opacity, this.toolStripMenuItem_clickThrough, this.toolStripComboBox_clickOntoHotkey });
            this.toolStripMenuItem_opacity.Name = "toolStripMenuItem_opacity";
            this.toolStripMenuItem_opacity.Size = new Size(274, 22);
            this.toolStripMenuItem_opacity.Text = "Window Opacity ...";
            // 
            // toolStripTextBox_opacity
            // 
            this.toolStripTextBox_opacity.Name = "toolStripTextBox_opacity";
            this.toolStripTextBox_opacity.Size = new Size(100, 23);
            this.toolStripTextBox_opacity.Text = "0";
            this.toolStripTextBox_opacity.KeyDown += this.toolStripTextBox_opacity_KeyDown;
            // 
            // toolStripMenuItem_clickThrough
            // 
            this.toolStripMenuItem_clickThrough.Name = "toolStripMenuItem_clickThrough";
            this.toolStripMenuItem_clickThrough.Size = new Size(181, 22);
            this.toolStripMenuItem_clickThrough.Text = "Click Through";
            // 
            // toolStripComboBox_clickOntoHotkey
            // 
            this.toolStripComboBox_clickOntoHotkey.Items.AddRange(new object[] { "Ctrl", "Alt", "Shift" });
            this.toolStripComboBox_clickOntoHotkey.Name = "toolStripComboBox_clickOntoHotkey";
            this.toolStripComboBox_clickOntoHotkey.Size = new Size(121, 23);
            this.toolStripComboBox_clickOntoHotkey.Text = "Ctrl";
            // 
            // showUsageToolStripMenuItem
            // 
            this.showUsageToolStripMenuItem.Checked = true;
            this.showUsageToolStripMenuItem.CheckOnClick = true;
            this.showUsageToolStripMenuItem.CheckState = CheckState.Checked;
            this.showUsageToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_percentageColor });
            this.showUsageToolStripMenuItem.Name = "showUsageToolStripMenuItem";
            this.showUsageToolStripMenuItem.Size = new Size(274, 22);
            this.showUsageToolStripMenuItem.Text = "Show Per Core % ...";
            this.showUsageToolStripMenuItem.CheckedChanged += this.showUsageToolStripMenuItem_CheckedChanged;
            // 
            // toolStripTextBox_percentageColor
            // 
            this.toolStripTextBox_percentageColor.Name = "toolStripTextBox_percentageColor";
            this.toolStripTextBox_percentageColor.Size = new Size(100, 23);
            this.toolStripTextBox_percentageColor.Text = "#8a2be2 ";
            this.toolStripTextBox_percentageColor.DoubleClick += this.toolStripTextBox_percentageColor_DoubleClick;
            this.toolStripTextBox_percentageColor.EnabledChanged += this.toolStripTextBox_percentageColor_EnabledChanged;
            this.toolStripTextBox_percentageColor.TextChanged += this.toolStripTextBox_percentageColor_TextChanged;
            // 
            // alwaysOnTopToolStripMenuItem
            // 
            this.alwaysOnTopToolStripMenuItem.CheckOnClick = true;
            this.alwaysOnTopToolStripMenuItem.Name = "alwaysOnTopToolStripMenuItem";
            this.alwaysOnTopToolStripMenuItem.Size = new Size(274, 22);
            this.alwaysOnTopToolStripMenuItem.Text = "Always on Top";
            this.alwaysOnTopToolStripMenuItem.CheckedChanged += this.alwaysOnTopToolStripMenuItem_CheckedChanged;
            // 
            // trafficThresholdToolStripMenuItem
            // 
            this.trafficThresholdToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_threshold });
            this.trafficThresholdToolStripMenuItem.Name = "trafficThresholdToolStripMenuItem";
            this.trafficThresholdToolStripMenuItem.Size = new Size(274, 22);
            this.trafficThresholdToolStripMenuItem.Text = "Traffic Threshold ...";
            // 
            // toolStripTextBox_threshold
            // 
            this.toolStripTextBox_threshold.Name = "toolStripTextBox_threshold";
            this.toolStripTextBox_threshold.Size = new Size(100, 23);
            this.toolStripTextBox_threshold.Text = "1 MB/s";
            this.toolStripTextBox_threshold.TextChanged += this.toolStripTextBox_threshold_TextChanged;
            // 
            // toolStripSeparator5
            // 
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            this.toolStripSeparator5.Size = new Size(271, 6);
            // 
            // driveSpeedTestToolStripMenuItem
            // 
            this.driveSpeedTestToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripComboBox_drives, this.testSettingsToolStripMenuItem });
            this.driveSpeedTestToolStripMenuItem.Name = "driveSpeedTestToolStripMenuItem";
            this.driveSpeedTestToolStripMenuItem.Size = new Size(274, 22);
            this.driveSpeedTestToolStripMenuItem.Text = "Drive Speed Test ...";
            this.driveSpeedTestToolStripMenuItem.DropDownOpening += this.driveSpeedTestToolStripMenuItem_DropDownOpening;
            this.driveSpeedTestToolStripMenuItem.Click += this.driveSpeedTestToolStripMenuItem_Click;
            // 
            // toolStripComboBox_drives
            // 
            this.toolStripComboBox_drives.DropDownStyle = ComboBoxStyle.DropDownList;
            this.toolStripComboBox_drives.Name = "toolStripComboBox_drives";
            this.toolStripComboBox_drives.Size = new Size(121, 23);
            this.toolStripComboBox_drives.SelectedIndexChanged += this.toolStripComboBox_drives_SelectedIndexChanged;
            // 
            // testSettingsToolStripMenuItem
            // 
            this.testSettingsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.fileSizeMBToolStripMenuItem, this.blockSizeKBToolStripMenuItem, this.passesToolStripMenuItem, this.threadsToolStripMenuItem, this.writeThroughToolStripMenuItem });
            this.testSettingsToolStripMenuItem.Name = "testSettingsToolStripMenuItem";
            this.testSettingsToolStripMenuItem.Size = new Size(181, 22);
            this.testSettingsToolStripMenuItem.Text = "Test Settings ...";
            // 
            // fileSizeMBToolStripMenuItem
            // 
            this.fileSizeMBToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_testFileSizeMb });
            this.fileSizeMBToolStripMenuItem.Name = "fileSizeMBToolStripMenuItem";
            this.fileSizeMBToolStripMenuItem.Size = new Size(163, 22);
            this.fileSizeMBToolStripMenuItem.Text = "File Size (MB) ...";
            // 
            // toolStripTextBox_testFileSizeMb
            // 
            this.toolStripTextBox_testFileSizeMb.Name = "toolStripTextBox_testFileSizeMb";
            this.toolStripTextBox_testFileSizeMb.Size = new Size(100, 23);
            this.toolStripTextBox_testFileSizeMb.Text = "512";
            this.toolStripTextBox_testFileSizeMb.Leave += this.toolStripTextBox_testFileSizeMb_Leave;
            this.toolStripTextBox_testFileSizeMb.KeyDown += this.toolStripTextBox_testFileSizeMb_KeyDown;
            // 
            // blockSizeKBToolStripMenuItem
            // 
            this.blockSizeKBToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_testBlockSizeKb });
            this.blockSizeKBToolStripMenuItem.Name = "blockSizeKBToolStripMenuItem";
            this.blockSizeKBToolStripMenuItem.Size = new Size(163, 22);
            this.blockSizeKBToolStripMenuItem.Text = "Block Size (KB) ...";
            // 
            // toolStripTextBox_testBlockSizeKb
            // 
            this.toolStripTextBox_testBlockSizeKb.Name = "toolStripTextBox_testBlockSizeKb";
            this.toolStripTextBox_testBlockSizeKb.Size = new Size(100, 23);
            this.toolStripTextBox_testBlockSizeKb.Text = "1024";
            this.toolStripTextBox_testBlockSizeKb.Leave += this.toolStripTextBox_testBlockSizeKb_Leave;
            this.toolStripTextBox_testBlockSizeKb.KeyDown += this.toolStripTextBox_testBlockSizeKb_KeyDown;
            // 
            // passesToolStripMenuItem
            // 
            this.passesToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_testPasses });
            this.passesToolStripMenuItem.Name = "passesToolStripMenuItem";
            this.passesToolStripMenuItem.Size = new Size(163, 22);
            this.passesToolStripMenuItem.Text = "Passes ...";
            // 
            // toolStripTextBox_testPasses
            // 
            this.toolStripTextBox_testPasses.Name = "toolStripTextBox_testPasses";
            this.toolStripTextBox_testPasses.Size = new Size(100, 23);
            this.toolStripTextBox_testPasses.Text = "3";
            this.toolStripTextBox_testPasses.Leave += this.toolStripTextBox_testPasses_Leave;
            this.toolStripTextBox_testPasses.KeyDown += this.toolStripTextBox_testPasses_KeyDown;
            // 
            // threadsToolStripMenuItem
            // 
            this.threadsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_testThreads });
            this.threadsToolStripMenuItem.Name = "threadsToolStripMenuItem";
            this.threadsToolStripMenuItem.Size = new Size(163, 22);
            this.threadsToolStripMenuItem.Text = "Threads ...";
            // 
            // toolStripTextBox_testThreads
            // 
            this.toolStripTextBox_testThreads.Name = "toolStripTextBox_testThreads";
            this.toolStripTextBox_testThreads.Size = new Size(100, 23);
            this.toolStripTextBox_testThreads.Text = "4";
            this.toolStripTextBox_testThreads.Leave += this.toolStripTextBox_testThreads_Leave;
            this.toolStripTextBox_testThreads.KeyDown += this.toolStripTextBox_testThreads_KeyDown;
            // 
            // writeThroughToolStripMenuItem
            // 
            this.writeThroughToolStripMenuItem.Checked = true;
            this.writeThroughToolStripMenuItem.CheckOnClick = true;
            this.writeThroughToolStripMenuItem.CheckState = CheckState.Checked;
            this.writeThroughToolStripMenuItem.Name = "writeThroughToolStripMenuItem";
            this.writeThroughToolStripMenuItem.Size = new Size(163, 22);
            this.writeThroughToolStripMenuItem.Text = "Write Through";
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new Size(271, 6);
            // 
            // toolStripMenuItem_loadLlamaCppServer
            // 
            this.toolStripMenuItem_loadLlamaCppServer.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripMenuItem_modelsDirectory, this.toolStripComboBox_ggufModels, this.toolStripMenuItem_loadMmproj, this.toolStripMenuItem_contextSize, this.toolStripMenuItem_batchSize, this.toolStripMenuItem_splitMode, this.toolStripMenuItem_tensorSplit, this.toolStripMenuItem_flashAttention, this.toolStripMenuItem_gpuLayersCount, this.toolStripMenuItem_parallelSlots, this.toolStripMenuItem_noWarmup, this.toolStripMenuItem_fitMode, this.KVoffload_ToolStripMenuItem, this.toolStripMenuItem_kvCacheType, this.toolStripMenuItem_toolCalls, this.toolStripSeparator3, this.toolStripMenuItem_temperature, this.toolStripMenuItem_repetitionPenalty, this.toolStripMenuItem_thinkingBudget, this.toolStripMenuItem_reasoningBudget, this.toolStripMenuItem_topP, this.toolStripMenuItem_minP, this.toolStripMenuItem_topK });
            this.toolStripMenuItem_loadLlamaCppServer.Name = "toolStripMenuItem_loadLlamaCppServer";
            this.toolStripMenuItem_loadLlamaCppServer.Size = new Size(274, 22);
            this.toolStripMenuItem_loadLlamaCppServer.Text = "Load Model (llama-server.exe)";
            this.toolStripMenuItem_loadLlamaCppServer.Click += this.toolStripMenuItem_loadLlamaCppServer_Click;
            // 
            // toolStripMenuItem_modelsDirectory
            // 
            this.toolStripMenuItem_modelsDirectory.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_modelsDirectory });
            this.toolStripMenuItem_modelsDirectory.Name = "toolStripMenuItem_modelsDirectory";
            this.toolStripMenuItem_modelsDirectory.Size = new Size(340, 22);
            this.toolStripMenuItem_modelsDirectory.Text = "Set GGUF Models Directory";
            // 
            // toolStripTextBox_modelsDirectory
            // 
            this.toolStripTextBox_modelsDirectory.Name = "toolStripTextBox_modelsDirectory";
            this.toolStripTextBox_modelsDirectory.Size = new Size(240, 23);
            this.toolStripTextBox_modelsDirectory.Text = "D:\\\\Models\\GGUF\\Others\\";
            this.toolStripTextBox_modelsDirectory.Leave += this.toolStripTextBox_modelsDirectory_Leave;
            // 
            // toolStripComboBox_ggufModels
            // 
            this.toolStripComboBox_ggufModels.Name = "toolStripComboBox_ggufModels";
            this.toolStripComboBox_ggufModels.Size = new Size(280, 23);
            this.toolStripComboBox_ggufModels.Text = "Select a GGUF model";
            this.toolStripComboBox_ggufModels.SelectedIndexChanged += this.toolStripComboBox_ggufModels_SelectedIndexChanged;
            // 
            // toolStripMenuItem_loadMmproj
            // 
            this.toolStripMenuItem_loadMmproj.CheckOnClick = true;
            this.toolStripMenuItem_loadMmproj.Enabled = false;
            this.toolStripMenuItem_loadMmproj.Name = "toolStripMenuItem_loadMmproj";
            this.toolStripMenuItem_loadMmproj.Size = new Size(340, 22);
            this.toolStripMenuItem_loadMmproj.Text = "No MMPROJ available.";
            // 
            // toolStripMenuItem_contextSize
            // 
            this.toolStripMenuItem_contextSize.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_contextSize });
            this.toolStripMenuItem_contextSize.Name = "toolStripMenuItem_contextSize";
            this.toolStripMenuItem_contextSize.Size = new Size(340, 22);
            this.toolStripMenuItem_contextSize.Text = "Context Size";
            // 
            // toolStripTextBox_contextSize
            // 
            this.toolStripTextBox_contextSize.Name = "toolStripTextBox_contextSize";
            this.toolStripTextBox_contextSize.Size = new Size(100, 23);
            this.toolStripTextBox_contextSize.Text = "65536";
            this.toolStripTextBox_contextSize.KeyDown += this.toolStripTextBox_contextSize_KeyDown;
            // 
            // toolStripMenuItem_batchSize
            // 
            this.toolStripMenuItem_batchSize.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_batchSize });
            this.toolStripMenuItem_batchSize.Name = "toolStripMenuItem_batchSize";
            this.toolStripMenuItem_batchSize.Size = new Size(340, 22);
            this.toolStripMenuItem_batchSize.Text = "Batch Size";
            // 
            // toolStripTextBox_batchSize
            // 
            this.toolStripTextBox_batchSize.Name = "toolStripTextBox_batchSize";
            this.toolStripTextBox_batchSize.Size = new Size(100, 23);
            this.toolStripTextBox_batchSize.Text = "4096";
            this.toolStripTextBox_batchSize.KeyDown += this.toolStripTextBox_batchSize_KeyDown;
            // 
            // toolStripMenuItem_splitMode
            // 
            this.toolStripMenuItem_splitMode.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripComboBox_splitMode });
            this.toolStripMenuItem_splitMode.Name = "toolStripMenuItem_splitMode";
            this.toolStripMenuItem_splitMode.Size = new Size(340, 22);
            this.toolStripMenuItem_splitMode.Text = "Split Mode";
            // 
            // toolStripComboBox_splitMode
            // 
            this.toolStripComboBox_splitMode.Items.AddRange(new object[] { "none", "tensor", "layer", "row" });
            this.toolStripComboBox_splitMode.Name = "toolStripComboBox_splitMode";
            this.toolStripComboBox_splitMode.Size = new Size(140, 23);
            this.toolStripComboBox_splitMode.Text = "Select a Split Mode";
            this.toolStripComboBox_splitMode.SelectedChanged += this.toolStripComboBox_splitMode_SelectedChanged;
            // 
            // toolStripMenuItem_tensorSplit
            // 
            this.toolStripMenuItem_tensorSplit.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_tensorSplit });
            this.toolStripMenuItem_tensorSplit.Name = "toolStripMenuItem_tensorSplit";
            this.toolStripMenuItem_tensorSplit.Size = new Size(340, 22);
            this.toolStripMenuItem_tensorSplit.Text = "Tensor Split (multi-GPU splitting)";
            // 
            // toolStripTextBox_tensorSplit
            // 
            this.toolStripTextBox_tensorSplit.Name = "toolStripTextBox_tensorSplit";
            this.toolStripTextBox_tensorSplit.Size = new Size(100, 23);
            this.toolStripTextBox_tensorSplit.KeyDown += this.toolStripTextBox_tensorSplit_KeyDown;
            // 
            // toolStripMenuItem_flashAttention
            // 
            this.toolStripMenuItem_flashAttention.Checked = true;
            this.toolStripMenuItem_flashAttention.CheckOnClick = true;
            this.toolStripMenuItem_flashAttention.CheckState = CheckState.Checked;
            this.toolStripMenuItem_flashAttention.Name = "toolStripMenuItem_flashAttention";
            this.toolStripMenuItem_flashAttention.Size = new Size(340, 22);
            this.toolStripMenuItem_flashAttention.Text = "Flash Attention";
            // 
            // toolStripMenuItem_gpuLayersCount
            // 
            this.toolStripMenuItem_gpuLayersCount.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_gpuLayersCount });
            this.toolStripMenuItem_gpuLayersCount.Name = "toolStripMenuItem_gpuLayersCount";
            this.toolStripMenuItem_gpuLayersCount.Size = new Size(340, 22);
            this.toolStripMenuItem_gpuLayersCount.Text = "GPU Layers Count";
            // 
            // toolStripTextBox_gpuLayersCount
            // 
            this.toolStripTextBox_gpuLayersCount.Name = "toolStripTextBox_gpuLayersCount";
            this.toolStripTextBox_gpuLayersCount.Size = new Size(100, 23);
            this.toolStripTextBox_gpuLayersCount.Text = "999";
            this.toolStripTextBox_gpuLayersCount.KeyDown += this.toolStripTextBox_gpuLayersCount_KeyDown;
            // 
            // toolStripMenuItem_parallelSlots
            // 
            this.toolStripMenuItem_parallelSlots.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_numberParallelSlots });
            this.toolStripMenuItem_parallelSlots.Name = "toolStripMenuItem_parallelSlots";
            this.toolStripMenuItem_parallelSlots.Size = new Size(340, 22);
            this.toolStripMenuItem_parallelSlots.Text = "Number of Parallel Slots";
            // 
            // toolStripTextBox_numberParallelSlots
            // 
            this.toolStripTextBox_numberParallelSlots.Name = "toolStripTextBox_numberParallelSlots";
            this.toolStripTextBox_numberParallelSlots.Size = new Size(100, 23);
            this.toolStripTextBox_numberParallelSlots.Text = "1";
            this.toolStripTextBox_numberParallelSlots.KeyDown += this.toolStripTextBox_numberParallelSlots_KeyDown;
            // 
            // toolStripMenuItem_noWarmup
            // 
            this.toolStripMenuItem_noWarmup.Checked = true;
            this.toolStripMenuItem_noWarmup.CheckOnClick = true;
            this.toolStripMenuItem_noWarmup.CheckState = CheckState.Checked;
            this.toolStripMenuItem_noWarmup.Name = "toolStripMenuItem_noWarmup";
            this.toolStripMenuItem_noWarmup.Size = new Size(340, 22);
            this.toolStripMenuItem_noWarmup.Text = "No Warmup (faster loading)";
            this.toolStripMenuItem_noWarmup.CheckedChanged += this.toolStripMenuItem_noWarmup_CheckedChanged;
            // 
            // toolStripMenuItem_fitMode
            // 
            this.toolStripMenuItem_fitMode.CheckOnClick = true;
            this.toolStripMenuItem_fitMode.Name = "toolStripMenuItem_fitMode";
            this.toolStripMenuItem_fitMode.Size = new Size(340, 22);
            this.toolStripMenuItem_fitMode.Text = "Fit Mode (on / off)";
            this.toolStripMenuItem_fitMode.CheckedChanged += this.toolStripMenuItem_fitMode_CheckedChanged;
            // 
            // KVoffload_ToolStripMenuItem
            // 
            this.KVoffload_ToolStripMenuItem.Checked = true;
            this.KVoffload_ToolStripMenuItem.CheckOnClick = true;
            this.KVoffload_ToolStripMenuItem.CheckState = CheckState.Checked;
            this.KVoffload_ToolStripMenuItem.Name = "KVoffload_ToolStripMenuItem";
            this.KVoffload_ToolStripMenuItem.Size = new Size(340, 22);
            this.KVoffload_ToolStripMenuItem.Text = "KV-offload (context only in VRAM (faster))";
            this.KVoffload_ToolStripMenuItem.CheckedChanged += this.KVoffload_ToolStripMenuItem_CheckedChanged;
            // 
            // toolStripMenuItem_kvCacheType
            // 
            this.toolStripMenuItem_kvCacheType.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripComboBox_cacheType });
            this.toolStripMenuItem_kvCacheType.Name = "toolStripMenuItem_kvCacheType";
            this.toolStripMenuItem_kvCacheType.Size = new Size(340, 22);
            this.toolStripMenuItem_kvCacheType.Text = "K+V Cache Type";
            // 
            // toolStripComboBox_cacheType
            // 
            this.toolStripComboBox_cacheType.Items.AddRange(new object[] { "F32", "BF16", "F16", "Q8_0", "Q6_0", "Q5_1", "Q5_0", "Q4_1", "Q4_0", "Q3_0", "Q2_0" });
            this.toolStripComboBox_cacheType.Name = "toolStripComboBox_cacheType";
            this.toolStripComboBox_cacheType.Size = new Size(121, 23);
            this.toolStripComboBox_cacheType.Text = "F16";
            this.toolStripComboBox_cacheType.SelectedIndexChanged += this.toolStripComboBox_cacheType_SelectedIndexChanged;
            // 
            // toolStripMenuItem_toolCalls
            // 
            this.toolStripMenuItem_toolCalls.CheckOnClick = true;
            this.toolStripMenuItem_toolCalls.Name = "toolStripMenuItem_toolCalls";
            this.toolStripMenuItem_toolCalls.Size = new Size(340, 22);
            this.toolStripMenuItem_toolCalls.Text = "Llama-Server Tool Calls";
            this.toolStripMenuItem_toolCalls.CheckedChanged += this.toolStripMenuItem_toolCalls_CheckedChanged;
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new Size(337, 6);
            // 
            // toolStripMenuItem_temperature
            // 
            this.toolStripMenuItem_temperature.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_temperature });
            this.toolStripMenuItem_temperature.Name = "toolStripMenuItem_temperature";
            this.toolStripMenuItem_temperature.Size = new Size(340, 22);
            this.toolStripMenuItem_temperature.Text = "Temperature";
            // 
            // toolStripTextBox_temperature
            // 
            this.toolStripTextBox_temperature.Name = "toolStripTextBox_temperature";
            this.toolStripTextBox_temperature.Size = new Size(100, 23);
            this.toolStripTextBox_temperature.Text = "1.0";
            this.toolStripTextBox_temperature.KeyDown += this.toolStripTextBox_temperature_KeyDown;
            // 
            // toolStripMenuItem_repetitionPenalty
            // 
            this.toolStripMenuItem_repetitionPenalty.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_repetationPenalty });
            this.toolStripMenuItem_repetitionPenalty.Name = "toolStripMenuItem_repetitionPenalty";
            this.toolStripMenuItem_repetitionPenalty.Size = new Size(340, 22);
            this.toolStripMenuItem_repetitionPenalty.Text = "Repetition Penalty";
            // 
            // toolStripTextBox_repetationPenalty
            // 
            this.toolStripTextBox_repetationPenalty.Name = "toolStripTextBox_repetationPenalty";
            this.toolStripTextBox_repetationPenalty.Size = new Size(100, 23);
            this.toolStripTextBox_repetationPenalty.Text = "1.1";
            this.toolStripTextBox_repetationPenalty.KeyDown += this.toolStripTextBox_repetationPenalty_KeyDown;
            // 
            // toolStripMenuItem_thinkingBudget
            // 
            this.toolStripMenuItem_thinkingBudget.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_thinkingBudget });
            this.toolStripMenuItem_thinkingBudget.Name = "toolStripMenuItem_thinkingBudget";
            this.toolStripMenuItem_thinkingBudget.Size = new Size(340, 22);
            this.toolStripMenuItem_thinkingBudget.Text = "Thinking Budget";
            // 
            // toolStripTextBox_thinkingBudget
            // 
            this.toolStripTextBox_thinkingBudget.Name = "toolStripTextBox_thinkingBudget";
            this.toolStripTextBox_thinkingBudget.Size = new Size(100, 23);
            this.toolStripTextBox_thinkingBudget.Text = "16384";
            this.toolStripTextBox_thinkingBudget.KeyDown += this.toolStripTextBox_thinkingBudget_KeyDown;
            // 
            // toolStripMenuItem_reasoningBudget
            // 
            this.toolStripMenuItem_reasoningBudget.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_reasoningBudget });
            this.toolStripMenuItem_reasoningBudget.Name = "toolStripMenuItem_reasoningBudget";
            this.toolStripMenuItem_reasoningBudget.Size = new Size(340, 22);
            this.toolStripMenuItem_reasoningBudget.Text = "Reasoning Budget";
            // 
            // toolStripTextBox_reasoningBudget
            // 
            this.toolStripTextBox_reasoningBudget.Name = "toolStripTextBox_reasoningBudget";
            this.toolStripTextBox_reasoningBudget.Size = new Size(100, 23);
            this.toolStripTextBox_reasoningBudget.Text = "8192";
            this.toolStripTextBox_reasoningBudget.KeyDown += this.toolStripTextBox_reasoningBudget_KeyDown;
            // 
            // toolStripMenuItem_topP
            // 
            this.toolStripMenuItem_topP.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_topP });
            this.toolStripMenuItem_topP.Name = "toolStripMenuItem_topP";
            this.toolStripMenuItem_topP.Size = new Size(340, 22);
            this.toolStripMenuItem_topP.Text = "Top-P";
            // 
            // toolStripTextBox_topP
            // 
            this.toolStripTextBox_topP.Name = "toolStripTextBox_topP";
            this.toolStripTextBox_topP.Size = new Size(100, 23);
            this.toolStripTextBox_topP.Text = "0.95";
            this.toolStripTextBox_topP.KeyDown += this.toolStripTextBox_topP_KeyDown;
            // 
            // toolStripMenuItem_minP
            // 
            this.toolStripMenuItem_minP.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_minP });
            this.toolStripMenuItem_minP.Name = "toolStripMenuItem_minP";
            this.toolStripMenuItem_minP.Size = new Size(340, 22);
            this.toolStripMenuItem_minP.Text = "Min-P";
            // 
            // toolStripTextBox_minP
            // 
            this.toolStripTextBox_minP.Name = "toolStripTextBox_minP";
            this.toolStripTextBox_minP.Size = new Size(100, 23);
            this.toolStripTextBox_minP.Text = "0.0";
            this.toolStripTextBox_minP.KeyDown += this.toolStripTextBox_minP_KeyDown;
            // 
            // toolStripMenuItem_topK
            // 
            this.toolStripMenuItem_topK.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_topK });
            this.toolStripMenuItem_topK.Name = "toolStripMenuItem_topK";
            this.toolStripMenuItem_topK.Size = new Size(340, 22);
            this.toolStripMenuItem_topK.Text = "Top-K";
            // 
            // toolStripTextBox_topK
            // 
            this.toolStripTextBox_topK.Name = "toolStripTextBox_topK";
            this.toolStripTextBox_topK.Size = new Size(100, 23);
            this.toolStripTextBox_topK.Text = "64";
            this.toolStripTextBox_topK.KeyDown += this.toolStripTextBox_topK_KeyDown;
            // 
            // toolStripMenuItem_execModelLoadBat
            // 
            this.toolStripMenuItem_execModelLoadBat.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripComboBox_modelLoadBats, this.toolStripMenuItem_hideCmd });
            this.toolStripMenuItem_execModelLoadBat.Name = "toolStripMenuItem_execModelLoadBat";
            this.toolStripMenuItem_execModelLoadBat.Size = new Size(274, 22);
            this.toolStripMenuItem_execModelLoadBat.Text = "Execute Model Load .BAT";
            this.toolStripMenuItem_execModelLoadBat.Click += this.toolStripMenuItem_execModelLoadBat_Click;
            // 
            // toolStripComboBox_modelLoadBats
            // 
            this.toolStripComboBox_modelLoadBats.Name = "toolStripComboBox_modelLoadBats";
            this.toolStripComboBox_modelLoadBats.Size = new Size(300, 23);
            this.toolStripComboBox_modelLoadBats.Text = "Select a .BAT file";
            // 
            // toolStripMenuItem_hideCmd
            // 
            this.toolStripMenuItem_hideCmd.Checked = true;
            this.toolStripMenuItem_hideCmd.CheckOnClick = true;
            this.toolStripMenuItem_hideCmd.CheckState = CheckState.Checked;
            this.toolStripMenuItem_hideCmd.Name = "toolStripMenuItem_hideCmd";
            this.toolStripMenuItem_hideCmd.Size = new Size(360, 22);
            this.toolStripMenuItem_hideCmd.Text = "Start without CMD window";
            this.toolStripMenuItem_hideCmd.CheckedChanged += this.toolStripMenuItem_hideCmd_CheckedChanged;
            // 
            // rerouteAPILlamacppOllamaToolStripMenuItem
            // 
            this.rerouteAPILlamacppOllamaToolStripMenuItem.CheckOnClick = true;
            this.rerouteAPILlamacppOllamaToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripMenuItem_openAiApi, this.toolStripSeparator4, this.llamacppPortToolStripMenuItem, this.ollamaPortToolStripMenuItem });
            this.rerouteAPILlamacppOllamaToolStripMenuItem.Name = "rerouteAPILlamacppOllamaToolStripMenuItem";
            this.rerouteAPILlamacppOllamaToolStripMenuItem.Size = new Size(274, 22);
            this.rerouteAPILlamacppOllamaToolStripMenuItem.Text = "Re-route API llama.cpp -> Ollama";
            this.rerouteAPILlamacppOllamaToolStripMenuItem.CheckedChanged += this.rerouteAPILlamacppOllamaToolStripMenuItem_CheckedChanged;
            // 
            // toolStripMenuItem_openAiApi
            // 
            this.toolStripMenuItem_openAiApi.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_openAiApiUrl });
            this.toolStripMenuItem_openAiApi.Name = "toolStripMenuItem_openAiApi";
            this.toolStripMenuItem_openAiApi.Size = new Size(174, 22);
            this.toolStripMenuItem_openAiApi.Text = "Source OpenAI API";
            // 
            // toolStripTextBox_openAiApiUrl
            // 
            this.toolStripTextBox_openAiApiUrl.Name = "toolStripTextBox_openAiApiUrl";
            this.toolStripTextBox_openAiApiUrl.Size = new Size(240, 23);
            this.toolStripTextBox_openAiApiUrl.KeyDown += this.toolStripTextBox_openAiApiUrl_KeyDown;
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new Size(171, 6);
            // 
            // llamacppPortToolStripMenuItem
            // 
            this.llamacppPortToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_llamacppPort });
            this.llamacppPortToolStripMenuItem.Name = "llamacppPortToolStripMenuItem";
            this.llamacppPortToolStripMenuItem.Size = new Size(174, 22);
            this.llamacppPortToolStripMenuItem.Text = "llama.cpp Port";
            // 
            // toolStripTextBox_llamacppPort
            // 
            this.toolStripTextBox_llamacppPort.Name = "toolStripTextBox_llamacppPort";
            this.toolStripTextBox_llamacppPort.Size = new Size(100, 23);
            this.toolStripTextBox_llamacppPort.Text = "8080";
            this.toolStripTextBox_llamacppPort.KeyDown += this.toolStripTextBox_llamacppPort_KeyDown;
            // 
            // ollamaPortToolStripMenuItem
            // 
            this.ollamaPortToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_ollamaPort });
            this.ollamaPortToolStripMenuItem.Name = "ollamaPortToolStripMenuItem";
            this.ollamaPortToolStripMenuItem.Size = new Size(174, 22);
            this.ollamaPortToolStripMenuItem.Text = "Ollama Port";
            // 
            // toolStripTextBox_ollamaPort
            // 
            this.toolStripTextBox_ollamaPort.Name = "toolStripTextBox_ollamaPort";
            this.toolStripTextBox_ollamaPort.Size = new Size(100, 23);
            this.toolStripTextBox_ollamaPort.Text = "11434";
            this.toolStripTextBox_ollamaPort.KeyDown += this.toolStripTextBox_ollamaPort_KeyDown;
            // 
            // smartPromptOptimizationsToolStripMenuItem
            // 
            this.smartPromptOptimizationsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.enableSmartPromptOptimizationsToolStripMenuItem, this.promptSafetyRatioToolStripMenuItem, this.smartBudgetRatioToolStripMenuItem, this.largeMessageThresholdCharsToolStripMenuItem, this.skeletonMaxLinesToolStripMenuItem, this.focusKeywordLimitToolStripMenuItem, this.tailKeepBonusCharsToolStripMenuItem });
            this.smartPromptOptimizationsToolStripMenuItem.Name = "smartPromptOptimizationsToolStripMenuItem";
            this.smartPromptOptimizationsToolStripMenuItem.Size = new Size(274, 22);
            this.smartPromptOptimizationsToolStripMenuItem.Text = "Smart Prompt Optimizations";
            // 
            // enableSmartPromptOptimizationsToolStripMenuItem
            // 
            this.enableSmartPromptOptimizationsToolStripMenuItem.Checked = true;
            this.enableSmartPromptOptimizationsToolStripMenuItem.CheckOnClick = true;
            this.enableSmartPromptOptimizationsToolStripMenuItem.CheckState = CheckState.Checked;
            this.enableSmartPromptOptimizationsToolStripMenuItem.Name = "enableSmartPromptOptimizationsToolStripMenuItem";
            this.enableSmartPromptOptimizationsToolStripMenuItem.Size = new Size(263, 22);
            this.enableSmartPromptOptimizationsToolStripMenuItem.Text = "Enable Smart Prompt Optimizations";
            this.enableSmartPromptOptimizationsToolStripMenuItem.CheckedChanged += this.enableSmartPromptOptimizationsToolStripMenuItem_CheckedChanged;
            // 
            // promptSafetyRatioToolStripMenuItem
            // 
            this.promptSafetyRatioToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_promptSafetyRatio });
            this.promptSafetyRatioToolStripMenuItem.Name = "promptSafetyRatioToolStripMenuItem";
            this.promptSafetyRatioToolStripMenuItem.Size = new Size(263, 22);
            this.promptSafetyRatioToolStripMenuItem.Text = "Prompt Safety Ratio";
            // 
            // toolStripTextBox_promptSafetyRatio
            // 
            this.toolStripTextBox_promptSafetyRatio.Name = "toolStripTextBox_promptSafetyRatio";
            this.toolStripTextBox_promptSafetyRatio.Size = new Size(100, 23);
            this.toolStripTextBox_promptSafetyRatio.Text = "0.90";
            this.toolStripTextBox_promptSafetyRatio.KeyDown += this.toolStripTextBox_promptSafetyRatio_KeyDown;
            // 
            // smartBudgetRatioToolStripMenuItem
            // 
            this.smartBudgetRatioToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_smartBudgetRatio });
            this.smartBudgetRatioToolStripMenuItem.Name = "smartBudgetRatioToolStripMenuItem";
            this.smartBudgetRatioToolStripMenuItem.Size = new Size(263, 22);
            this.smartBudgetRatioToolStripMenuItem.Text = "Smart Budget Ratio";
            // 
            // toolStripTextBox_smartBudgetRatio
            // 
            this.toolStripTextBox_smartBudgetRatio.Name = "toolStripTextBox_smartBudgetRatio";
            this.toolStripTextBox_smartBudgetRatio.Size = new Size(100, 23);
            this.toolStripTextBox_smartBudgetRatio.Text = "0.75";
            this.toolStripTextBox_smartBudgetRatio.KeyDown += this.toolStripTextBox_smartBudgetRatio_KeyDown;
            // 
            // largeMessageThresholdCharsToolStripMenuItem
            // 
            this.largeMessageThresholdCharsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_largeMessageThresholdChars });
            this.largeMessageThresholdCharsToolStripMenuItem.Name = "largeMessageThresholdCharsToolStripMenuItem";
            this.largeMessageThresholdCharsToolStripMenuItem.Size = new Size(263, 22);
            this.largeMessageThresholdCharsToolStripMenuItem.Text = "Large Message Threshold Chars";
            // 
            // toolStripTextBox_largeMessageThresholdChars
            // 
            this.toolStripTextBox_largeMessageThresholdChars.Name = "toolStripTextBox_largeMessageThresholdChars";
            this.toolStripTextBox_largeMessageThresholdChars.Size = new Size(100, 23);
            this.toolStripTextBox_largeMessageThresholdChars.Text = "2400";
            this.toolStripTextBox_largeMessageThresholdChars.KeyDown += this.toolStripTextBox_largeMessageThresholdChars_KeyDown;
            // 
            // skeletonMaxLinesToolStripMenuItem
            // 
            this.skeletonMaxLinesToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_skeletonMaxLines });
            this.skeletonMaxLinesToolStripMenuItem.Name = "skeletonMaxLinesToolStripMenuItem";
            this.skeletonMaxLinesToolStripMenuItem.Size = new Size(263, 22);
            this.skeletonMaxLinesToolStripMenuItem.Text = "Skeleton Max Lines";
            // 
            // toolStripTextBox_skeletonMaxLines
            // 
            this.toolStripTextBox_skeletonMaxLines.Name = "toolStripTextBox_skeletonMaxLines";
            this.toolStripTextBox_skeletonMaxLines.Size = new Size(100, 23);
            this.toolStripTextBox_skeletonMaxLines.Text = "60";
            this.toolStripTextBox_skeletonMaxLines.KeyDown += this.toolStripTextBox_skeletonMaxLines_KeyDown;
            // 
            // focusKeywordLimitToolStripMenuItem
            // 
            this.focusKeywordLimitToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_focusKeywordLimit });
            this.focusKeywordLimitToolStripMenuItem.Name = "focusKeywordLimitToolStripMenuItem";
            this.focusKeywordLimitToolStripMenuItem.Size = new Size(263, 22);
            this.focusKeywordLimitToolStripMenuItem.Text = "Focus Keyword Limit";
            // 
            // toolStripTextBox_focusKeywordLimit
            // 
            this.toolStripTextBox_focusKeywordLimit.Name = "toolStripTextBox_focusKeywordLimit";
            this.toolStripTextBox_focusKeywordLimit.Size = new Size(100, 23);
            this.toolStripTextBox_focusKeywordLimit.Text = "12";
            this.toolStripTextBox_focusKeywordLimit.KeyDown += this.toolStripTextBox_focusKeywordLimit_KeyDown;
            // 
            // tailKeepBonusCharsToolStripMenuItem
            // 
            this.tailKeepBonusCharsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_tailKeepBonusChars });
            this.tailKeepBonusCharsToolStripMenuItem.Name = "tailKeepBonusCharsToolStripMenuItem";
            this.tailKeepBonusCharsToolStripMenuItem.Size = new Size(263, 22);
            this.tailKeepBonusCharsToolStripMenuItem.Text = "Tail Keep Bonus Chars";
            // 
            // toolStripTextBox_tailKeepBonusChars
            // 
            this.toolStripTextBox_tailKeepBonusChars.Name = "toolStripTextBox_tailKeepBonusChars";
            this.toolStripTextBox_tailKeepBonusChars.Size = new Size(100, 23);
            this.toolStripTextBox_tailKeepBonusChars.Text = "500";
            this.toolStripTextBox_tailKeepBonusChars.KeyDown += this.toolStripTextBox_tailKeepBonusChars_KeyDown;
            // 
            // showTokenssToolStripMenuItem
            // 
            this.showTokenssToolStripMenuItem.Checked = true;
            this.showTokenssToolStripMenuItem.CheckOnClick = true;
            this.showTokenssToolStripMenuItem.CheckState = CheckState.Checked;
            this.showTokenssToolStripMenuItem.Name = "showTokenssToolStripMenuItem";
            this.showTokenssToolStripMenuItem.Size = new Size(274, 22);
            this.showTokenssToolStripMenuItem.Text = "Show tokens/s";
            this.showTokenssToolStripMenuItem.CheckedChanged += this.showTokenssToolStripMenuItem_CheckedChanged;
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new Size(271, 6);
            // 
            // toolStripMenuItem_configureVoiceInputHotkey
            // 
            this.toolStripMenuItem_configureVoiceInputHotkey.Name = "toolStripMenuItem_configureVoiceInputHotkey";
            this.toolStripMenuItem_configureVoiceInputHotkey.Size = new Size(274, 22);
            this.toolStripMenuItem_configureVoiceInputHotkey.Text = "Set Voice Input Hotkey ... (Ctrl+RShift)";
            this.toolStripMenuItem_configureVoiceInputHotkey.Click += this.toolStripMenuItem_configureVoiceInputHotkey_Click;
            // 
            // toolStripMenuItem_remapAnyKey
            // 
            this.toolStripMenuItem_remapAnyKey.Name = "toolStripMenuItem_remapAnyKey";
            this.toolStripMenuItem_remapAnyKey.Size = new Size(274, 22);
            this.toolStripMenuItem_remapAnyKey.Text = "Remap any Key ... (0 enabled)";
            this.toolStripMenuItem_remapAnyKey.Click += this.toolStripMenuItem_remapAnyKey_Click;
            // 
            // toolStripSeparator6
            // 
            this.toolStripSeparator6.Name = "toolStripSeparator6";
            this.toolStripSeparator6.Size = new Size(271, 6);
            // 
            // openDebugConsoleToolStripMenuItem
            // 
            this.openDebugConsoleToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripMenuItem_visuallyFormatLog, this.toolStripMenuItem_includeRawChunksLog, this.toolStripMenuItem_logGenerationSpeed });
            this.openDebugConsoleToolStripMenuItem.Name = "openDebugConsoleToolStripMenuItem";
            this.openDebugConsoleToolStripMenuItem.Size = new Size(274, 22);
            this.openDebugConsoleToolStripMenuItem.Text = "Open Debug Console";
            this.openDebugConsoleToolStripMenuItem.Click += this.openDebugConsoleToolStripMenuItem_Click;
            // 
            // toolStripMenuItem_visuallyFormatLog
            // 
            this.toolStripMenuItem_visuallyFormatLog.Checked = true;
            this.toolStripMenuItem_visuallyFormatLog.CheckOnClick = true;
            this.toolStripMenuItem_visuallyFormatLog.CheckState = CheckState.Checked;
            this.toolStripMenuItem_visuallyFormatLog.Name = "toolStripMenuItem_visuallyFormatLog";
            this.toolStripMenuItem_visuallyFormatLog.Size = new Size(278, 22);
            this.toolStripMenuItem_visuallyFormatLog.Text = "Visually Formatted Log";
            this.toolStripMenuItem_visuallyFormatLog.Click += this.toolStripMenuItem_visuallyFormatLog_Click;
            // 
            // toolStripMenuItem_includeRawChunksLog
            // 
            this.toolStripMenuItem_includeRawChunksLog.Checked = true;
            this.toolStripMenuItem_includeRawChunksLog.CheckOnClick = true;
            this.toolStripMenuItem_includeRawChunksLog.CheckState = CheckState.Checked;
            this.toolStripMenuItem_includeRawChunksLog.Name = "toolStripMenuItem_includeRawChunksLog";
            this.toolStripMenuItem_includeRawChunksLog.Size = new Size(278, 22);
            this.toolStripMenuItem_includeRawChunksLog.Text = "Include raw Request/Response Chunks";
            this.toolStripMenuItem_includeRawChunksLog.Click += this.toolStripMenuItem_includeRawChunksLog_Click;
            // 
            // toolStripMenuItem_logGenerationSpeed
            // 
            this.toolStripMenuItem_logGenerationSpeed.CheckOnClick = true;
            this.toolStripMenuItem_logGenerationSpeed.Name = "toolStripMenuItem_logGenerationSpeed";
            this.toolStripMenuItem_logGenerationSpeed.Size = new Size(278, 22);
            this.toolStripMenuItem_logGenerationSpeed.Text = "Log Generation Speed (tok/s)";
            this.toolStripMenuItem_logGenerationSpeed.CheckedChanged += this.toolStripMenuItem_logGenerationSpeed_CheckedChanged;
            // 
            // label_vram
            // 
            this.label_vram.AutoSize = true;
            this.label_vram.Font = new Font("Bahnschrift Condensed", 9.75F);
            this.label_vram.Location = new Point(0, 190);
            this.label_vram.Name = "label_vram";
            this.label_vram.Size = new Size(41, 16);
            this.label_vram.TabIndex = 4;
            this.label_vram.Text = "VRAM: -";
            // 
            // progressBar_vram
            // 
            this.progressBar_vram.Location = new Point(0, 208);
            this.progressBar_vram.Maximum = 1000;
            this.progressBar_vram.Name = "progressBar_vram";
            this.progressBar_vram.Size = new Size(240, 12);
            this.progressBar_vram.TabIndex = 3;
            // 
            // label_wattage
            // 
            this.label_wattage.AutoSize = true;
            this.label_wattage.Font = new Font("Bahnschrift Condensed", 9.75F);
            this.label_wattage.Location = new Point(0, 175);
            this.label_wattage.Name = "label_wattage";
            this.label_wattage.Size = new Size(40, 16);
            this.label_wattage.TabIndex = 5;
            this.label_wattage.Text = "Watts: -";
            // 
            // label_gpuUsage
            // 
            this.label_gpuUsage.AutoSize = true;
            this.label_gpuUsage.Font = new Font("Bahnschrift Condensed", 9.75F);
            this.label_gpuUsage.Location = new Point(166, 175);
            this.label_gpuUsage.Name = "label_gpuUsage";
            this.label_gpuUsage.Size = new Size(34, 16);
            this.label_gpuUsage.TabIndex = 6;
            this.label_gpuUsage.Text = "GPU: -";
            // 
            // label_gpuLoad2
            // 
            this.label_gpuLoad2.AutoSize = true;
            this.label_gpuLoad2.Font = new Font("Bahnschrift Condensed", 9.75F);
            this.label_gpuLoad2.Location = new Point(166, 223);
            this.label_gpuLoad2.Name = "label_gpuLoad2";
            this.label_gpuLoad2.Size = new Size(34, 16);
            this.label_gpuLoad2.TabIndex = 10;
            this.label_gpuLoad2.Text = "GPU: -";
            // 
            // label_gpuWatts2
            // 
            this.label_gpuWatts2.AutoSize = true;
            this.label_gpuWatts2.Font = new Font("Bahnschrift Condensed", 9.75F);
            this.label_gpuWatts2.Location = new Point(0, 223);
            this.label_gpuWatts2.Name = "label_gpuWatts2";
            this.label_gpuWatts2.Size = new Size(40, 16);
            this.label_gpuWatts2.TabIndex = 9;
            this.label_gpuWatts2.Text = "Watts: -";
            // 
            // label_gpuVram2
            // 
            this.label_gpuVram2.AutoSize = true;
            this.label_gpuVram2.Font = new Font("Bahnschrift Condensed", 9.75F);
            this.label_gpuVram2.Location = new Point(0, 238);
            this.label_gpuVram2.Name = "label_gpuVram2";
            this.label_gpuVram2.Size = new Size(41, 16);
            this.label_gpuVram2.TabIndex = 8;
            this.label_gpuVram2.Text = "VRAM: -";
            // 
            // progressBar_vram2
            // 
            this.progressBar_vram2.Location = new Point(0, 256);
            this.progressBar_vram2.Maximum = 1000;
            this.progressBar_vram2.Name = "progressBar_vram2";
            this.progressBar_vram2.Size = new Size(240, 12);
            this.progressBar_vram2.TabIndex = 7;
            // 
            // label_avgCpuLoadAndTemperature
            // 
            this.label_avgCpuLoadAndTemperature.AutoSize = true;
            this.label_avgCpuLoadAndTemperature.Font = new Font("Segoe UI Semilight", 8.25F, FontStyle.Regular, GraphicsUnit.Point,  0);
            this.label_avgCpuLoadAndTemperature.Location = new Point(0, 103);
            this.label_avgCpuLoadAndTemperature.Name = "label_avgCpuLoadAndTemperature";
            this.label_avgCpuLoadAndTemperature.Size = new Size(102, 13);
            this.label_avgCpuLoadAndTemperature.TabIndex = 11;
            this.label_avgCpuLoadAndTemperature.Text = "Avg.: - % (-273,15C°)";
            // 
            // label_topTasksList
            // 
            this.label_topTasksList.AutoSize = true;
            this.label_topTasksList.Font = new Font("Bahnschrift Light Condensed", 8.25F, FontStyle.Regular, GraphicsUnit.Point,  0);
            this.label_topTasksList.Location = new Point(122, 103);
            this.label_topTasksList.Name = "label_topTasksList";
            this.label_topTasksList.Size = new Size(31, 39);
            this.label_topTasksList.TabIndex = 12;
            this.label_topTasksList.Text = "#1 idle\r\n#2 idle\r\n#3 idle";
            // 
            // button_recordUsages
            // 
            this.button_recordUsages.Location = new Point(0, 119);
            this.button_recordUsages.Name = "button_recordUsages";
            this.button_recordUsages.Size = new Size(23, 23);
            this.button_recordUsages.TabIndex = 13;
            this.button_recordUsages.Text = "⏺";
            this.button_recordUsages.UseVisualStyleBackColor = true;
            this.button_recordUsages.Click += this.button_recordUsages_Click;
            // 
            // label_routingPortsInfo
            // 
            this.label_routingPortsInfo.AutoSize = true;
            this.label_routingPortsInfo.Font = new Font("Bahnschrift Light SemiCondensed", 8.25F, FontStyle.Regular, GraphicsUnit.Point,  0);
            this.label_routingPortsInfo.Location = new Point(22, 116);
            this.label_routingPortsInfo.Name = "label_routingPortsInfo";
            this.label_routingPortsInfo.Size = new Size(94, 13);
            this.label_routingPortsInfo.TabIndex = 14;
            this.label_routingPortsInfo.Text = "Port: ----- to -----";
            this.label_routingPortsInfo.Visible = false;
            // 
            // WindowWidget
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(240, 271);
            this.ContextMenuStrip = this.contextMenuStrip_widget;
            this.Controls.Add(this.label_routingPortsInfo);
            this.Controls.Add(this.button_recordUsages);
            this.Controls.Add(this.label_topTasksList);
            this.Controls.Add(this.label_avgCpuLoadAndTemperature);
            this.Controls.Add(this.label_gpuLoad2);
            this.Controls.Add(this.label_gpuWatts2);
            this.Controls.Add(this.label_gpuVram2);
            this.Controls.Add(this.progressBar_vram2);
            this.Controls.Add(this.label_gpuUsage);
            this.Controls.Add(this.label_wattage);
            this.Controls.Add(this.label_vram);
            this.Controls.Add(this.progressBar_vram);
            this.Controls.Add(this.label_ram);
            this.Controls.Add(this.progressBar_ram);
            this.Controls.Add(this.pictureBox_cpu);
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.MaximumSize = new Size(256, 310);
            this.MinimumSize = new Size(256, 310);
            this.Name = "WindowWidget";
            this.Text = "System Statistics";
            this.KeyDown += this.WindowWidget_KeyDown;
            this.KeyUp += this.WindowWidget_KeyUp;
            ((System.ComponentModel.ISupportInitialize) this.pictureBox_cpu).EndInit();
            this.contextMenuStrip_widget.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }



        #endregion

        private PictureBox pictureBox_cpu;
        private ProgressBar progressBar_ram;
        private Label label_ram;
        private ContextMenuStrip contextMenuStrip_widget;
        private ToolStripMenuItem updateIntervalToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_interval;
        private Label label_vram;
        private ProgressBar progressBar_vram;
        private Label label_wattage;
        private Label label_gpuUsage;
        private ToolStripMenuItem selectGPUToolStripMenuItem;
        private ToolStripComboBox toolStripComboBox_gpus;
        private ToolStripMenuItem diagramColorToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_diagramColor;
        private ToolStripMenuItem showUsageToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_percentageColor;
        private ToolStripMenuItem alwaysOnTopToolStripMenuItem;
        private Label label_gpuLoad2;
        private Label label_gpuWatts2;
        private Label label_gpuVram2;
        private ProgressBar progressBar_vram2;
        private ToolStripMenuItem trafficThresholdToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_threshold;
        private ToolStripMenuItem driveSpeedTestToolStripMenuItem;
        private ToolStripComboBox toolStripComboBox_drives;
        private ToolStripMenuItem testSettingsToolStripMenuItem;
        private ToolStripMenuItem fileSizeMBToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_testFileSizeMb;
        private ToolStripMenuItem blockSizeKBToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_testBlockSizeKb;
        private ToolStripMenuItem passesToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_testPasses;
        private ToolStripMenuItem threadsToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_testThreads;
        private ToolStripMenuItem writeThroughToolStripMenuItem;
        private Label label_avgCpuLoadAndTemperature;
        private Label label_topTasksList;
        private Button button_recordUsages;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem rerouteAPILlamacppOllamaToolStripMenuItem;
        private ToolStripMenuItem llamacppPortToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_llamacppPort;
        private ToolStripMenuItem ollamaPortToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_ollamaPort;
        private Label label_routingPortsInfo;
        private ToolStripMenuItem showTokenssToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem openDebugConsoleToolStripMenuItem;
        private ToolStripMenuItem toolStripMenuItem_visuallyFormatLog;
        private ToolStripMenuItem toolStripMenuItem_includeRawChunksLog;
        private ToolStripMenuItem toolStripMenuItem_logGenerationSpeed;
        private ToolStripMenuItem toolStripMenuItem_loadLlamaCppServer;
        private ToolStripMenuItem toolStripMenuItem_modelsDirectory;
        private ToolStripTextBox toolStripTextBox_modelsDirectory;
        private ToolStripComboBox toolStripComboBox_ggufModels;
        private ToolStripMenuItem toolStripMenuItem_loadMmproj;
        private ToolStripMenuItem toolStripMenuItem_contextSize;
        private ToolStripTextBox toolStripTextBox_contextSize;
        private ToolStripMenuItem toolStripMenuItem_batchSize;
        private ToolStripTextBox toolStripTextBox_batchSize;
        private ToolStripMenuItem toolStripMenuItem_splitMode;
        private ToolStripComboBox toolStripComboBox_splitMode;
        private ToolStripMenuItem toolStripMenuItem_tensorSplit;
        private ToolStripTextBox toolStripTextBox_tensorSplit;
        private ToolStripMenuItem toolStripMenuItem_flashAttention;
        private ToolStripMenuItem toolStripMenuItem_gpuLayersCount;
        private ToolStripTextBox toolStripTextBox_gpuLayersCount;
        private ToolStripMenuItem toolStripMenuItem_parallelSlots;
        private ToolStripTextBox toolStripTextBox_numberParallelSlots;
        private ToolStripMenuItem toolStripMenuItem_noWarmup;
        private ToolStripMenuItem toolStripMenuItem_fitMode;
        private ToolStripMenuItem KVoffload_ToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator3;
        private ToolStripMenuItem toolStripMenuItem_temperature;
        private ToolStripTextBox toolStripTextBox_temperature;
        private ToolStripMenuItem toolStripMenuItem_repetitionPenalty;
        private ToolStripTextBox toolStripTextBox_repetationPenalty;
        private ToolStripMenuItem toolStripMenuItem_thinkingBudget;
        private ToolStripTextBox toolStripTextBox_thinkingBudget;
        private ToolStripMenuItem toolStripMenuItem_execModelLoadBat;
        private ToolStripComboBox toolStripComboBox_modelLoadBats;
        private ToolStripMenuItem toolStripMenuItem_openAiApi;
        private ToolStripTextBox toolStripTextBox_openAiApiUrl;
        private ToolStripSeparator toolStripSeparator4;
        private ToolStripMenuItem smartPromptOptimizationsToolStripMenuItem;
        private ToolStripMenuItem enableSmartPromptOptimizationsToolStripMenuItem;
        private ToolStripMenuItem promptSafetyRatioToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_promptSafetyRatio;
        private ToolStripMenuItem smartBudgetRatioToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_smartBudgetRatio;
        private ToolStripMenuItem largeMessageThresholdCharsToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_largeMessageThresholdChars;
        private ToolStripMenuItem skeletonMaxLinesToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_skeletonMaxLines;
        private ToolStripMenuItem focusKeywordLimitToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_focusKeywordLimit;
        private ToolStripMenuItem tailKeepBonusCharsToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_tailKeepBonusChars;
        private ToolStripMenuItem toolStripMenuItem_topP;
        private ToolStripTextBox toolStripTextBox_topP;
        private ToolStripMenuItem toolStripMenuItem_minP;
        private ToolStripTextBox toolStripTextBox_minP;
        private ToolStripMenuItem toolStripMenuItem_topK;
        private ToolStripTextBox toolStripTextBox_topK;
        private ToolStripMenuItem toolStripMenuItem_hideCmd;
        private ToolStripMenuItem toolStripMenuItem_opacity;
        private ToolStripTextBox toolStripTextBox_opacity;
        private ToolStripSeparator toolStripSeparator5;
        private ToolStripMenuItem toolStripMenuItem_clickThrough;
        private ToolStripComboBox toolStripComboBox_clickOntoHotkey;
        private ToolStripMenuItem toolStripMenuItem_reasoningBudget;
        private ToolStripTextBox toolStripTextBox_reasoningBudget;
        private ToolStripMenuItem toolStripMenuItem_configureVoiceInputHotkey;
        private ToolStripSeparator toolStripSeparator6;
        private ToolStripMenuItem toolStripMenuItem_remapAnyKey;
        private ToolStripMenuItem toolStripMenuItem_kvCacheType;
        private ToolStripComboBox toolStripComboBox_cacheType;
        private ToolStripMenuItem toolStripMenuItem_toolCalls;
    }
}
