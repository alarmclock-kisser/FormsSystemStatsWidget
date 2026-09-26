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
            components = new System.ComponentModel.Container();
            pictureBox_cpu = new PictureBox();
            progressBar_ram = new ProgressBar();
            label_ram = new Label();
            contextMenuStrip_widget = new ContextMenuStrip(components);
            updateIntervalToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_interval = new ToolStripTextBox();
            diagramColorToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_diagramColor = new ToolStripTextBox();
            toolStripMenuItem_blackOutMode = new ToolStripMenuItem();
            toolStripMenuItem_opacity = new ToolStripMenuItem();
            toolStripTextBox_opacity = new ToolStripTextBox();
            toolStripMenuItem_clickThrough = new ToolStripMenuItem();
            toolStripComboBox_clickOntoHotkey = new ToolStripComboBox();
            showUsageToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_percentageColor = new ToolStripTextBox();
            alwaysOnTopToolStripMenuItem = new ToolStripMenuItem();
            trafficThresholdToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_threshold = new ToolStripTextBox();
            toolStripSeparator5 = new ToolStripSeparator();
            driveSpeedTestToolStripMenuItem = new ToolStripMenuItem();
            toolStripComboBox_drives = new ToolStripComboBox();
            testSettingsToolStripMenuItem = new ToolStripMenuItem();
            fileSizeMBToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_testFileSizeMb = new ToolStripTextBox();
            blockSizeKBToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_testBlockSizeKb = new ToolStripTextBox();
            passesToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_testPasses = new ToolStripTextBox();
            threadsToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_testThreads = new ToolStripTextBox();
            writeThroughToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            toolStripMenuItem_loadLlamaCppServer = new ToolStripMenuItem();
            toolStripMenuItem_modelsDirectory = new ToolStripMenuItem();
            toolStripTextBox_modelsDirectory = new ToolStripTextBox();
            toolStripComboBox_ggufModels = new ToolStripComboBox();
            toolStripMenuItem_loadMmproj = new ToolStripMenuItem();
            toolStripMenuItem_contextSize = new ToolStripMenuItem();
            toolStripTextBox_contextSize = new ToolStripTextBox();
            toolStripMenuItem_batchSize = new ToolStripMenuItem();
            toolStripTextBox_batchSize = new ToolStripTextBox();
            toolStripMenuItem_splitMode = new ToolStripMenuItem();
            toolStripComboBox_splitMode = new ToolStripComboBox();
            toolStripMenuItem_tensorSplit = new ToolStripMenuItem();
            toolStripTextBox_tensorSplit = new ToolStripTextBox();
            toolStripMenuItem_flashAttention = new ToolStripMenuItem();
            toolStripMenuItem_gpuLayersCount = new ToolStripMenuItem();
            toolStripTextBox_gpuLayersCount = new ToolStripTextBox();
            toolStripMenuItem_parallelSlots = new ToolStripMenuItem();
            toolStripTextBox_numberParallelSlots = new ToolStripTextBox();
            toolStripMenuItem_noWarmup = new ToolStripMenuItem();
            toolStripMenuItem_fitMode = new ToolStripMenuItem();
            KVoffload_ToolStripMenuItem = new ToolStripMenuItem();
            toolStripMenuItem_kvCacheType = new ToolStripMenuItem();
            toolStripComboBox_cacheType = new ToolStripComboBox();
            toolStripMenuItem_toolCalls = new ToolStripMenuItem();
            toolStripMenuItem_additionalArgs = new ToolStripMenuItem();
            toolStripTextBox_additionalArgs = new ToolStripTextBox();
            toolStripSeparator3 = new ToolStripSeparator();
            toolStripMenuItem_temperature = new ToolStripMenuItem();
            toolStripTextBox_temperature = new ToolStripTextBox();
            toolStripMenuItem_repetitionPenalty = new ToolStripMenuItem();
            toolStripTextBox_repetationPenalty = new ToolStripTextBox();
            ToolStripMenuItem_presencePenalty = new ToolStripMenuItem();
            toolStripTextBox_presencePenalty = new ToolStripTextBox();
            toolStripMenuItem_reasoningEffort = new ToolStripMenuItem();
            toolStripComboBox_reasoningEffort = new ToolStripComboBox();
            toolStripMenuItem_thinking = new ToolStripMenuItem();
            toolStripMenuItem_reasoningBudget = new ToolStripMenuItem();
            toolStripTextBox_reasoningBudget = new ToolStripTextBox();
            toolStripMenuItem_topP = new ToolStripMenuItem();
            toolStripTextBox_topP = new ToolStripTextBox();
            toolStripMenuItem_minP = new ToolStripMenuItem();
            toolStripTextBox_minP = new ToolStripTextBox();
            toolStripMenuItem_topK = new ToolStripMenuItem();
            toolStripTextBox_topK = new ToolStripTextBox();
            toolStripMenuItem_execModelLoadBat = new ToolStripMenuItem();
            toolStripComboBox_modelLoadBats = new ToolStripComboBox();
            toolStripMenuItem_hideCmd = new ToolStripMenuItem();
            toolStripMenuItem_loadOnnxGenaiServer = new ToolStripMenuItem();
            toolStripMenuItem_onnxModelRootDir = new ToolStripMenuItem();
            toolStripTextBox_onnxModelRootDir = new ToolStripTextBox();
            toolStripComboBox_onnxModels = new ToolStripComboBox();
            toolStripMenuItem_onnxContextLength = new ToolStripMenuItem();
            toolStripTextBox_onnxContextLength = new ToolStripTextBox();
            toolStripMenuItem_onnxMaxTokens = new ToolStripMenuItem();
            toolStripTextBox_onnxMaxTokens = new ToolStripTextBox();
            toolStripMenuItem_onnxTemperature = new ToolStripMenuItem();
            toolStripTextBox_onnxTemperature = new ToolStripTextBox();
            toolStripMenuItem_onnxTopP = new ToolStripMenuItem();
            toolStripTextBox_onnxTopP = new ToolStripTextBox();
            toolStripMenuItem_onnxTopK = new ToolStripMenuItem();
            toolStripTextBox_onnxTopK = new ToolStripTextBox();
            toolStripMenuItem_onnxRepeatPenalty = new ToolStripMenuItem();
            toolStripTextBox_onnxRepeatPenalty = new ToolStripTextBox();
            toolStripComboBox_onnxExecutionProvider = new ToolStripComboBox();
            toolStripMenuItem_onnxHideCmd = new ToolStripMenuItem();
            rerouteAPILlamacppOllamaToolStripMenuItem = new ToolStripMenuItem();
            toolStripMenuItem_openAiApi = new ToolStripMenuItem();
            toolStripTextBox_openAiApiUrl = new ToolStripTextBox();
            toolStripSeparator4 = new ToolStripSeparator();
            llamacppPortToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_llamacppPort = new ToolStripTextBox();
            ollamaPortToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_ollamaPort = new ToolStripTextBox();
            printGenerationStatsToolStripMenuItem = new ToolStripMenuItem();
            showTokenssToolStripMenuItem = new ToolStripMenuItem();
            extendCopilotSystemPromptToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_additionalCopilotSystemPrompt = new ToolStripTextBox();
            toolStripMenuItem_appendParams = new ToolStripMenuItem();
            smartPromptOptimizationsToolStripMenuItem = new ToolStripMenuItem();
            promptSafetyRatioToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_promptSafetyRatio = new ToolStripTextBox();
            smartBudgetRatioToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_smartBudgetRatio = new ToolStripTextBox();
            largeMessageThresholdCharsToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_largeMessageThresholdChars = new ToolStripTextBox();
            skeletonMaxLinesToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_skeletonMaxLines = new ToolStripTextBox();
            focusKeywordLimitToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_focusKeywordLimit = new ToolStripTextBox();
            tailKeepBonusCharsToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_tailKeepBonusChars = new ToolStripTextBox();
            injectToolCallingRulesToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_injectToolCallingRules = new ToolStripTextBox();
            toolStripSeparator2 = new ToolStripSeparator();
            toolStripMenuItem_remapAnyKey = new ToolStripMenuItem();
            toolStripSeparator6 = new ToolStripSeparator();
            openDebugConsoleToolStripMenuItem = new ToolStripMenuItem();
            toolStripMenuItem_visuallyFormatLog = new ToolStripMenuItem();
            toolStripMenuItem_includeRawChunksLog = new ToolStripMenuItem();
            toolStripMenuItem_logGenerationSpeed = new ToolStripMenuItem();
            label_vram = new Label();
            progressBar_vram = new ProgressBar();
            label_wattage = new Label();
            label_gpuUsage = new Label();
            label_gpuLoad2 = new Label();
            label_gpuWatts2 = new Label();
            label_gpuVram2 = new Label();
            progressBar_vram2 = new ProgressBar();
            label_avgCpuLoadAndTemperature = new Label();
            label_topTasksList = new Label();
            button_recordUsages = new Button();
            label_routingPortsInfo = new Label();
            ((System.ComponentModel.ISupportInitialize)pictureBox_cpu).BeginInit();
            contextMenuStrip_widget.SuspendLayout();
            SuspendLayout();
            // 
            // pictureBox_cpu
            // 
            pictureBox_cpu.BackColor = SystemColors.ActiveBorder;
            pictureBox_cpu.Dock = DockStyle.Top;
            pictureBox_cpu.Location = new Point(0, 0);
            pictureBox_cpu.Name = "pictureBox_cpu";
            pictureBox_cpu.Size = new Size(240, 100);
            pictureBox_cpu.TabIndex = 0;
            pictureBox_cpu.TabStop = false;
            // 
            // progressBar_ram
            // 
            progressBar_ram.Location = new Point(0, 160);
            progressBar_ram.Maximum = 1000;
            progressBar_ram.Name = "progressBar_ram";
            progressBar_ram.Size = new Size(240, 12);
            progressBar_ram.TabIndex = 1;
            // 
            // label_ram
            // 
            label_ram.AutoSize = true;
            label_ram.Font = new Font("Bahnschrift Condensed", 9.75F);
            label_ram.Location = new Point(0, 142);
            label_ram.Name = "label_ram";
            label_ram.Size = new Size(36, 16);
            label_ram.TabIndex = 2;
            label_ram.Text = "RAM: -";
            // 
            // contextMenuStrip_widget
            // 
            contextMenuStrip_widget.Items.AddRange(new ToolStripItem[] { updateIntervalToolStripMenuItem, diagramColorToolStripMenuItem, toolStripMenuItem_opacity, showUsageToolStripMenuItem, alwaysOnTopToolStripMenuItem, trafficThresholdToolStripMenuItem, toolStripSeparator5, driveSpeedTestToolStripMenuItem, toolStripSeparator1, toolStripMenuItem_loadLlamaCppServer, toolStripMenuItem_execModelLoadBat, toolStripMenuItem_loadOnnxGenaiServer, rerouteAPILlamacppOllamaToolStripMenuItem, smartPromptOptimizationsToolStripMenuItem, toolStripSeparator2, toolStripMenuItem_remapAnyKey, toolStripSeparator6, openDebugConsoleToolStripMenuItem });
            contextMenuStrip_widget.Name = "contextMenuStrip_widget";
            contextMenuStrip_widget.Size = new Size(291, 358);
            contextMenuStrip_widget.Text = "Settings";
            contextMenuStrip_widget.Opening += contextMenuStrip_widget_Opening;
            // 
            // updateIntervalToolStripMenuItem
            // 
            updateIntervalToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_interval });
            updateIntervalToolStripMenuItem.Name = "updateIntervalToolStripMenuItem";
            updateIntervalToolStripMenuItem.Size = new Size(290, 22);
            updateIntervalToolStripMenuItem.Text = "🕒 Update Interval";
            // 
            // toolStripTextBox_interval
            // 
            toolStripTextBox_interval.Name = "toolStripTextBox_interval";
            toolStripTextBox_interval.Size = new Size(100, 23);
            toolStripTextBox_interval.Text = "420";
            toolStripTextBox_interval.Leave += toolStripTextBox_interval_Leave;
            toolStripTextBox_interval.KeyDown += toolStripTextBox_interval_KeyDown;
            // 
            // diagramColorToolStripMenuItem
            // 
            diagramColorToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_diagramColor, toolStripMenuItem_blackOutMode });
            diagramColorToolStripMenuItem.Name = "diagramColorToolStripMenuItem";
            diagramColorToolStripMenuItem.Size = new Size(290, 22);
            diagramColorToolStripMenuItem.Text = "🖌 Diagram Color";
            // 
            // toolStripTextBox_diagramColor
            // 
            toolStripTextBox_diagramColor.Name = "toolStripTextBox_diagramColor";
            toolStripTextBox_diagramColor.Size = new Size(100, 23);
            toolStripTextBox_diagramColor.Text = "#ffffff";
            toolStripTextBox_diagramColor.DoubleClick += toolStripTextBox_diagramColor_DoubleClick;
            toolStripTextBox_diagramColor.TextChanged += toolStripTextBox_diagramColor_TextChanged;
            // 
            // toolStripMenuItem_blackOutMode
            // 
            toolStripMenuItem_blackOutMode.CheckOnClick = true;
            toolStripMenuItem_blackOutMode.Name = "toolStripMenuItem_blackOutMode";
            toolStripMenuItem_blackOutMode.Size = new Size(186, 22);
            toolStripMenuItem_blackOutMode.Text = "total black-out mode";
            toolStripMenuItem_blackOutMode.CheckedChanged += toolStripMenuItem_blackOutMode_CheckedChanged;
            // 
            // toolStripMenuItem_opacity
            // 
            toolStripMenuItem_opacity.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_opacity, toolStripMenuItem_clickThrough, toolStripComboBox_clickOntoHotkey });
            toolStripMenuItem_opacity.Name = "toolStripMenuItem_opacity";
            toolStripMenuItem_opacity.Size = new Size(290, 22);
            toolStripMenuItem_opacity.Text = "👁 Window Opacity";
            // 
            // toolStripTextBox_opacity
            // 
            toolStripTextBox_opacity.Name = "toolStripTextBox_opacity";
            toolStripTextBox_opacity.Size = new Size(100, 23);
            toolStripTextBox_opacity.Text = "0";
            toolStripTextBox_opacity.KeyDown += toolStripTextBox_opacity_KeyDown;
            // 
            // toolStripMenuItem_clickThrough
            // 
            toolStripMenuItem_clickThrough.Name = "toolStripMenuItem_clickThrough";
            toolStripMenuItem_clickThrough.Size = new Size(181, 22);
            toolStripMenuItem_clickThrough.Text = "Click Through";
            // 
            // toolStripComboBox_clickOntoHotkey
            // 
            toolStripComboBox_clickOntoHotkey.Items.AddRange(new object[] { "Ctrl", "Alt", "Shift" });
            toolStripComboBox_clickOntoHotkey.Name = "toolStripComboBox_clickOntoHotkey";
            toolStripComboBox_clickOntoHotkey.Size = new Size(121, 23);
            toolStripComboBox_clickOntoHotkey.Text = "Ctrl";
            // 
            // showUsageToolStripMenuItem
            // 
            showUsageToolStripMenuItem.Checked = true;
            showUsageToolStripMenuItem.CheckOnClick = true;
            showUsageToolStripMenuItem.CheckState = CheckState.Checked;
            showUsageToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_percentageColor });
            showUsageToolStripMenuItem.Name = "showUsageToolStripMenuItem";
            showUsageToolStripMenuItem.Size = new Size(290, 22);
            showUsageToolStripMenuItem.Text = "📊 Show per Core %";
            showUsageToolStripMenuItem.CheckedChanged += showUsageToolStripMenuItem_CheckedChanged;
            // 
            // toolStripTextBox_percentageColor
            // 
            toolStripTextBox_percentageColor.Name = "toolStripTextBox_percentageColor";
            toolStripTextBox_percentageColor.Size = new Size(100, 23);
            toolStripTextBox_percentageColor.Text = "#8a2be2 ";
            toolStripTextBox_percentageColor.DoubleClick += toolStripTextBox_percentageColor_DoubleClick;
            toolStripTextBox_percentageColor.EnabledChanged += toolStripTextBox_percentageColor_EnabledChanged;
            toolStripTextBox_percentageColor.TextChanged += toolStripTextBox_percentageColor_TextChanged;
            // 
            // alwaysOnTopToolStripMenuItem
            // 
            alwaysOnTopToolStripMenuItem.CheckOnClick = true;
            alwaysOnTopToolStripMenuItem.Name = "alwaysOnTopToolStripMenuItem";
            alwaysOnTopToolStripMenuItem.Size = new Size(290, 22);
            alwaysOnTopToolStripMenuItem.Text = "📌 Always on Top";
            alwaysOnTopToolStripMenuItem.CheckedChanged += alwaysOnTopToolStripMenuItem_CheckedChanged;
            // 
            // trafficThresholdToolStripMenuItem
            // 
            trafficThresholdToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_threshold });
            trafficThresholdToolStripMenuItem.Name = "trafficThresholdToolStripMenuItem";
            trafficThresholdToolStripMenuItem.Size = new Size(290, 22);
            trafficThresholdToolStripMenuItem.Text = "⇅ Traffic Threshold ...";
            // 
            // toolStripTextBox_threshold
            // 
            toolStripTextBox_threshold.Name = "toolStripTextBox_threshold";
            toolStripTextBox_threshold.Size = new Size(100, 23);
            toolStripTextBox_threshold.Text = "1 MB/s";
            toolStripTextBox_threshold.ToolTipText = "Threshold to show a Task that is using the internet.";
            toolStripTextBox_threshold.TextChanged += toolStripTextBox_threshold_TextChanged;
            // 
            // toolStripSeparator5
            // 
            toolStripSeparator5.Name = "toolStripSeparator5";
            toolStripSeparator5.Size = new Size(287, 6);
            // 
            // driveSpeedTestToolStripMenuItem
            // 
            driveSpeedTestToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripComboBox_drives, testSettingsToolStripMenuItem });
            driveSpeedTestToolStripMenuItem.Name = "driveSpeedTestToolStripMenuItem";
            driveSpeedTestToolStripMenuItem.Size = new Size(290, 22);
            driveSpeedTestToolStripMenuItem.Text = "⏱ Drive Speed Test ...";
            driveSpeedTestToolStripMenuItem.DropDownOpening += driveSpeedTestToolStripMenuItem_DropDownOpening;
            driveSpeedTestToolStripMenuItem.Click += driveSpeedTestToolStripMenuItem_Click;
            // 
            // toolStripComboBox_drives
            // 
            toolStripComboBox_drives.DropDownStyle = ComboBoxStyle.DropDownList;
            toolStripComboBox_drives.Name = "toolStripComboBox_drives";
            toolStripComboBox_drives.Size = new Size(121, 23);
            toolStripComboBox_drives.SelectedIndexChanged += toolStripComboBox_drives_SelectedIndexChanged;
            // 
            // testSettingsToolStripMenuItem
            // 
            testSettingsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { fileSizeMBToolStripMenuItem, blockSizeKBToolStripMenuItem, passesToolStripMenuItem, threadsToolStripMenuItem, writeThroughToolStripMenuItem });
            testSettingsToolStripMenuItem.Name = "testSettingsToolStripMenuItem";
            testSettingsToolStripMenuItem.Size = new Size(181, 22);
            testSettingsToolStripMenuItem.Text = "Test Settings ...";
            // 
            // fileSizeMBToolStripMenuItem
            // 
            fileSizeMBToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_testFileSizeMb });
            fileSizeMBToolStripMenuItem.Name = "fileSizeMBToolStripMenuItem";
            fileSizeMBToolStripMenuItem.Size = new Size(163, 22);
            fileSizeMBToolStripMenuItem.Text = "File Size (MB) ...";
            // 
            // toolStripTextBox_testFileSizeMb
            // 
            toolStripTextBox_testFileSizeMb.Name = "toolStripTextBox_testFileSizeMb";
            toolStripTextBox_testFileSizeMb.Size = new Size(100, 23);
            toolStripTextBox_testFileSizeMb.Text = "512";
            toolStripTextBox_testFileSizeMb.Leave += toolStripTextBox_testFileSizeMb_Leave;
            toolStripTextBox_testFileSizeMb.KeyDown += toolStripTextBox_testFileSizeMb_KeyDown;
            // 
            // blockSizeKBToolStripMenuItem
            // 
            blockSizeKBToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_testBlockSizeKb });
            blockSizeKBToolStripMenuItem.Name = "blockSizeKBToolStripMenuItem";
            blockSizeKBToolStripMenuItem.Size = new Size(163, 22);
            blockSizeKBToolStripMenuItem.Text = "Block Size (KB) ...";
            // 
            // toolStripTextBox_testBlockSizeKb
            // 
            toolStripTextBox_testBlockSizeKb.Name = "toolStripTextBox_testBlockSizeKb";
            toolStripTextBox_testBlockSizeKb.Size = new Size(100, 23);
            toolStripTextBox_testBlockSizeKb.Text = "1024";
            toolStripTextBox_testBlockSizeKb.Leave += toolStripTextBox_testBlockSizeKb_Leave;
            toolStripTextBox_testBlockSizeKb.KeyDown += toolStripTextBox_testBlockSizeKb_KeyDown;
            // 
            // passesToolStripMenuItem
            // 
            passesToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_testPasses });
            passesToolStripMenuItem.Name = "passesToolStripMenuItem";
            passesToolStripMenuItem.Size = new Size(163, 22);
            passesToolStripMenuItem.Text = "Passes ...";
            // 
            // toolStripTextBox_testPasses
            // 
            toolStripTextBox_testPasses.Name = "toolStripTextBox_testPasses";
            toolStripTextBox_testPasses.Size = new Size(100, 23);
            toolStripTextBox_testPasses.Text = "3";
            toolStripTextBox_testPasses.Leave += toolStripTextBox_testPasses_Leave;
            toolStripTextBox_testPasses.KeyDown += toolStripTextBox_testPasses_KeyDown;
            // 
            // threadsToolStripMenuItem
            // 
            threadsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_testThreads });
            threadsToolStripMenuItem.Name = "threadsToolStripMenuItem";
            threadsToolStripMenuItem.Size = new Size(163, 22);
            threadsToolStripMenuItem.Text = "Threads ...";
            // 
            // toolStripTextBox_testThreads
            // 
            toolStripTextBox_testThreads.Name = "toolStripTextBox_testThreads";
            toolStripTextBox_testThreads.Size = new Size(100, 23);
            toolStripTextBox_testThreads.Text = "4";
            toolStripTextBox_testThreads.Leave += toolStripTextBox_testThreads_Leave;
            toolStripTextBox_testThreads.KeyDown += toolStripTextBox_testThreads_KeyDown;
            // 
            // writeThroughToolStripMenuItem
            // 
            writeThroughToolStripMenuItem.Checked = true;
            writeThroughToolStripMenuItem.CheckOnClick = true;
            writeThroughToolStripMenuItem.CheckState = CheckState.Checked;
            writeThroughToolStripMenuItem.Name = "writeThroughToolStripMenuItem";
            writeThroughToolStripMenuItem.Size = new Size(163, 22);
            writeThroughToolStripMenuItem.Text = "Write Through";
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(287, 6);
            // 
            // toolStripMenuItem_loadLlamaCppServer
            // 
            toolStripMenuItem_loadLlamaCppServer.DropDownItems.AddRange(new ToolStripItem[] { toolStripMenuItem_modelsDirectory, toolStripComboBox_ggufModels, toolStripMenuItem_loadMmproj, toolStripMenuItem_contextSize, toolStripMenuItem_batchSize, toolStripMenuItem_splitMode, toolStripMenuItem_tensorSplit, toolStripMenuItem_flashAttention, toolStripMenuItem_gpuLayersCount, toolStripMenuItem_parallelSlots, toolStripMenuItem_noWarmup, toolStripMenuItem_fitMode, KVoffload_ToolStripMenuItem, toolStripMenuItem_kvCacheType, toolStripMenuItem_toolCalls, toolStripMenuItem_additionalArgs, toolStripSeparator3, toolStripMenuItem_temperature, toolStripMenuItem_repetitionPenalty, ToolStripMenuItem_presencePenalty, toolStripMenuItem_reasoningEffort, toolStripMenuItem_thinking, toolStripMenuItem_reasoningBudget, toolStripMenuItem_topP, toolStripMenuItem_minP, toolStripMenuItem_topK });
            toolStripMenuItem_loadLlamaCppServer.Name = "toolStripMenuItem_loadLlamaCppServer";
            toolStripMenuItem_loadLlamaCppServer.Size = new Size(290, 22);
            toolStripMenuItem_loadLlamaCppServer.Text = "⚙ Load Model (llama-server.exe)";
            toolStripMenuItem_loadLlamaCppServer.Click += toolStripMenuItem_loadLlamaCppServer_Click;
            // 
            // toolStripMenuItem_modelsDirectory
            // 
            toolStripMenuItem_modelsDirectory.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_modelsDirectory });
            toolStripMenuItem_modelsDirectory.Name = "toolStripMenuItem_modelsDirectory";
            toolStripMenuItem_modelsDirectory.Size = new Size(340, 22);
            toolStripMenuItem_modelsDirectory.Text = "Set GGUF Models Directory";
            // 
            // toolStripTextBox_modelsDirectory
            // 
            toolStripTextBox_modelsDirectory.Name = "toolStripTextBox_modelsDirectory";
            toolStripTextBox_modelsDirectory.Size = new Size(240, 23);
            toolStripTextBox_modelsDirectory.Text = "D:\\\\Models\\GGUF\\Others\\";
            toolStripTextBox_modelsDirectory.KeyDown += toolStripTextBox_modelsDirectory_KeyDown;
            // 
            // toolStripComboBox_ggufModels
            // 
            toolStripComboBox_ggufModels.Name = "toolStripComboBox_ggufModels";
            toolStripComboBox_ggufModels.Size = new Size(280, 23);
            toolStripComboBox_ggufModels.Text = "Select a GGUF model";
            toolStripComboBox_ggufModels.SelectedIndexChanged += toolStripComboBox_ggufModels_SelectedIndexChanged;
            // 
            // toolStripMenuItem_loadMmproj
            // 
            toolStripMenuItem_loadMmproj.CheckOnClick = true;
            toolStripMenuItem_loadMmproj.Enabled = false;
            toolStripMenuItem_loadMmproj.Name = "toolStripMenuItem_loadMmproj";
            toolStripMenuItem_loadMmproj.Size = new Size(340, 22);
            toolStripMenuItem_loadMmproj.Text = "No MMPROJ available.";
            // 
            // toolStripMenuItem_contextSize
            // 
            toolStripMenuItem_contextSize.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_contextSize });
            toolStripMenuItem_contextSize.Name = "toolStripMenuItem_contextSize";
            toolStripMenuItem_contextSize.Size = new Size(340, 22);
            toolStripMenuItem_contextSize.Text = "Context Size";
            // 
            // toolStripTextBox_contextSize
            // 
            toolStripTextBox_contextSize.Name = "toolStripTextBox_contextSize";
            toolStripTextBox_contextSize.Size = new Size(100, 23);
            toolStripTextBox_contextSize.Text = "65536";
            toolStripTextBox_contextSize.KeyDown += toolStripTextBox_contextSize_KeyDown;
            // 
            // toolStripMenuItem_batchSize
            // 
            toolStripMenuItem_batchSize.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_batchSize });
            toolStripMenuItem_batchSize.Name = "toolStripMenuItem_batchSize";
            toolStripMenuItem_batchSize.Size = new Size(340, 22);
            toolStripMenuItem_batchSize.Text = "Batch Size";
            // 
            // toolStripTextBox_batchSize
            // 
            toolStripTextBox_batchSize.Name = "toolStripTextBox_batchSize";
            toolStripTextBox_batchSize.Size = new Size(100, 23);
            toolStripTextBox_batchSize.Text = "4096";
            toolStripTextBox_batchSize.KeyDown += toolStripTextBox_batchSize_KeyDown;
            // 
            // toolStripMenuItem_splitMode
            // 
            toolStripMenuItem_splitMode.DropDownItems.AddRange(new ToolStripItem[] { toolStripComboBox_splitMode });
            toolStripMenuItem_splitMode.Name = "toolStripMenuItem_splitMode";
            toolStripMenuItem_splitMode.Size = new Size(340, 22);
            toolStripMenuItem_splitMode.Text = "Split Mode";
            // 
            // toolStripComboBox_splitMode
            // 
            toolStripComboBox_splitMode.Items.AddRange(new object[] { "none", "tensor", "layer", "row" });
            toolStripComboBox_splitMode.Name = "toolStripComboBox_splitMode";
            toolStripComboBox_splitMode.Size = new Size(140, 23);
            toolStripComboBox_splitMode.Text = "Select a Split Mode";
            toolStripComboBox_splitMode.SelectedChanged += toolStripComboBox_splitMode_SelectedChanged;
            // 
            // toolStripMenuItem_tensorSplit
            // 
            toolStripMenuItem_tensorSplit.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_tensorSplit });
            toolStripMenuItem_tensorSplit.Name = "toolStripMenuItem_tensorSplit";
            toolStripMenuItem_tensorSplit.Size = new Size(340, 22);
            toolStripMenuItem_tensorSplit.Text = "Tensor Split (multi-GPU splitting)";
            // 
            // toolStripTextBox_tensorSplit
            // 
            toolStripTextBox_tensorSplit.Name = "toolStripTextBox_tensorSplit";
            toolStripTextBox_tensorSplit.Size = new Size(100, 23);
            toolStripTextBox_tensorSplit.KeyDown += toolStripTextBox_tensorSplit_KeyDown;
            // 
            // toolStripMenuItem_flashAttention
            // 
            toolStripMenuItem_flashAttention.Checked = true;
            toolStripMenuItem_flashAttention.CheckOnClick = true;
            toolStripMenuItem_flashAttention.CheckState = CheckState.Checked;
            toolStripMenuItem_flashAttention.Name = "toolStripMenuItem_flashAttention";
            toolStripMenuItem_flashAttention.Size = new Size(340, 22);
            toolStripMenuItem_flashAttention.Text = "Flash Attention";
            // 
            // toolStripMenuItem_gpuLayersCount
            // 
            toolStripMenuItem_gpuLayersCount.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_gpuLayersCount });
            toolStripMenuItem_gpuLayersCount.Name = "toolStripMenuItem_gpuLayersCount";
            toolStripMenuItem_gpuLayersCount.Size = new Size(340, 22);
            toolStripMenuItem_gpuLayersCount.Text = "GPU Layers Count";
            // 
            // toolStripTextBox_gpuLayersCount
            // 
            toolStripTextBox_gpuLayersCount.Name = "toolStripTextBox_gpuLayersCount";
            toolStripTextBox_gpuLayersCount.Size = new Size(100, 23);
            toolStripTextBox_gpuLayersCount.Text = "999";
            toolStripTextBox_gpuLayersCount.KeyDown += toolStripTextBox_gpuLayersCount_KeyDown;
            // 
            // toolStripMenuItem_parallelSlots
            // 
            toolStripMenuItem_parallelSlots.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_numberParallelSlots });
            toolStripMenuItem_parallelSlots.Name = "toolStripMenuItem_parallelSlots";
            toolStripMenuItem_parallelSlots.Size = new Size(340, 22);
            toolStripMenuItem_parallelSlots.Text = "Number of Parallel Slots";
            // 
            // toolStripTextBox_numberParallelSlots
            // 
            toolStripTextBox_numberParallelSlots.Name = "toolStripTextBox_numberParallelSlots";
            toolStripTextBox_numberParallelSlots.Size = new Size(100, 23);
            toolStripTextBox_numberParallelSlots.Text = "1";
            toolStripTextBox_numberParallelSlots.KeyDown += toolStripTextBox_numberParallelSlots_KeyDown;
            // 
            // toolStripMenuItem_noWarmup
            // 
            toolStripMenuItem_noWarmup.Checked = true;
            toolStripMenuItem_noWarmup.CheckOnClick = true;
            toolStripMenuItem_noWarmup.CheckState = CheckState.Checked;
            toolStripMenuItem_noWarmup.Name = "toolStripMenuItem_noWarmup";
            toolStripMenuItem_noWarmup.Size = new Size(340, 22);
            toolStripMenuItem_noWarmup.Text = "No Warmup (faster loading)";
            toolStripMenuItem_noWarmup.CheckedChanged += toolStripMenuItem_noWarmup_CheckedChanged;
            // 
            // toolStripMenuItem_fitMode
            // 
            toolStripMenuItem_fitMode.CheckOnClick = true;
            toolStripMenuItem_fitMode.Name = "toolStripMenuItem_fitMode";
            toolStripMenuItem_fitMode.Size = new Size(340, 22);
            toolStripMenuItem_fitMode.Text = "Fit Mode (on / off)";
            toolStripMenuItem_fitMode.CheckedChanged += toolStripMenuItem_fitMode_CheckedChanged;
            // 
            // KVoffload_ToolStripMenuItem
            // 
            KVoffload_ToolStripMenuItem.Checked = true;
            KVoffload_ToolStripMenuItem.CheckOnClick = true;
            KVoffload_ToolStripMenuItem.CheckState = CheckState.Checked;
            KVoffload_ToolStripMenuItem.Name = "KVoffload_ToolStripMenuItem";
            KVoffload_ToolStripMenuItem.Size = new Size(340, 22);
            KVoffload_ToolStripMenuItem.Text = "KV-offload (context only in VRAM (faster))";
            KVoffload_ToolStripMenuItem.CheckedChanged += KVoffload_ToolStripMenuItem_CheckedChanged;
            // 
            // toolStripMenuItem_kvCacheType
            // 
            toolStripMenuItem_kvCacheType.DropDownItems.AddRange(new ToolStripItem[] { toolStripComboBox_cacheType });
            toolStripMenuItem_kvCacheType.Name = "toolStripMenuItem_kvCacheType";
            toolStripMenuItem_kvCacheType.Size = new Size(340, 22);
            toolStripMenuItem_kvCacheType.Text = "K+V Cache Type";
            // 
            // toolStripComboBox_cacheType
            // 
            toolStripComboBox_cacheType.Items.AddRange(new object[] { "f32", "bf16", "f16", "q8_0", "q5_1", "q5_0", "q4_1", "q4_0", "iq4_nl" });
            toolStripComboBox_cacheType.Name = "toolStripComboBox_cacheType";
            toolStripComboBox_cacheType.Size = new Size(80, 23);
            toolStripComboBox_cacheType.Text = "f16";
            toolStripComboBox_cacheType.SelectedIndexChanged += toolStripComboBox_cacheType_SelectedIndexChanged;
            // 
            // toolStripMenuItem_toolCalls
            // 
            toolStripMenuItem_toolCalls.CheckOnClick = true;
            toolStripMenuItem_toolCalls.Name = "toolStripMenuItem_toolCalls";
            toolStripMenuItem_toolCalls.Size = new Size(340, 22);
            toolStripMenuItem_toolCalls.Text = "Llama-Server Tool Calls";
            toolStripMenuItem_toolCalls.CheckedChanged += toolStripMenuItem_toolCalls_CheckedChanged;
            // 
            // toolStripMenuItem_additionalArgs
            // 
            toolStripMenuItem_additionalArgs.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_additionalArgs });
            toolStripMenuItem_additionalArgs.Name = "toolStripMenuItem_additionalArgs";
            toolStripMenuItem_additionalArgs.Size = new Size(340, 22);
            toolStripMenuItem_additionalArgs.Text = "Additional Load Args (0)";
            // 
            // toolStripTextBox_additionalArgs
            // 
            toolStripTextBox_additionalArgs.Name = "toolStripTextBox_additionalArgs";
            toolStripTextBox_additionalArgs.Size = new Size(340, 23);
            toolStripTextBox_additionalArgs.Text = "--mlock ";
            toolStripTextBox_additionalArgs.KeyDown += toolStripTextBox_additionalArgs_KeyDown;
            // 
            // toolStripSeparator3
            // 
            toolStripSeparator3.Name = "toolStripSeparator3";
            toolStripSeparator3.Size = new Size(337, 6);
            // 
            // toolStripMenuItem_temperature
            // 
            toolStripMenuItem_temperature.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_temperature });
            toolStripMenuItem_temperature.Name = "toolStripMenuItem_temperature";
            toolStripMenuItem_temperature.Size = new Size(340, 22);
            toolStripMenuItem_temperature.Text = "Temperature";
            // 
            // toolStripTextBox_temperature
            // 
            toolStripTextBox_temperature.Name = "toolStripTextBox_temperature";
            toolStripTextBox_temperature.Size = new Size(100, 23);
            toolStripTextBox_temperature.Text = "1.0";
            toolStripTextBox_temperature.KeyDown += toolStripTextBox_temperature_KeyDown;
            // 
            // toolStripMenuItem_repetitionPenalty
            // 
            toolStripMenuItem_repetitionPenalty.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_repetationPenalty });
            toolStripMenuItem_repetitionPenalty.Name = "toolStripMenuItem_repetitionPenalty";
            toolStripMenuItem_repetitionPenalty.Size = new Size(340, 22);
            toolStripMenuItem_repetitionPenalty.Text = "Repetition Penalty";
            // 
            // toolStripTextBox_repetationPenalty
            // 
            toolStripTextBox_repetationPenalty.Name = "toolStripTextBox_repetationPenalty";
            toolStripTextBox_repetationPenalty.Size = new Size(100, 23);
            toolStripTextBox_repetationPenalty.Text = "1.0";
            toolStripTextBox_repetationPenalty.KeyDown += toolStripTextBox_repetationPenalty_KeyDown;
            // 
            // ToolStripMenuItem_presencePenalty
            // 
            ToolStripMenuItem_presencePenalty.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_presencePenalty });
            ToolStripMenuItem_presencePenalty.Name = "ToolStripMenuItem_presencePenalty";
            ToolStripMenuItem_presencePenalty.Size = new Size(340, 22);
            ToolStripMenuItem_presencePenalty.Text = "Presence Penalty";
            // 
            // toolStripTextBox_presencePenalty
            // 
            toolStripTextBox_presencePenalty.Name = "toolStripTextBox_presencePenalty";
            toolStripTextBox_presencePenalty.Size = new Size(100, 23);
            toolStripTextBox_presencePenalty.Text = "1.0";
            toolStripTextBox_presencePenalty.KeyDown += toolStripTextBox_presencePenalty_KeyDown;
            // 
            // toolStripMenuItem_reasoningEffort
            // 
            toolStripMenuItem_reasoningEffort.DropDownItems.AddRange(new ToolStripItem[] { toolStripComboBox_reasoningEffort });
            toolStripMenuItem_reasoningEffort.Name = "toolStripMenuItem_reasoningEffort";
            toolStripMenuItem_reasoningEffort.Size = new Size(340, 22);
            toolStripMenuItem_reasoningEffort.Text = "Reasoning Effort";
            // 
            // toolStripComboBox_reasoningEffort
            // 
            toolStripComboBox_reasoningEffort.Items.AddRange(new object[] { "xhigh", "medium", "low" });
            toolStripComboBox_reasoningEffort.Name = "toolStripComboBox_reasoningEffort";
            toolStripComboBox_reasoningEffort.Size = new Size(100, 23);
            toolStripComboBox_reasoningEffort.Text = "xhigh";
            toolStripComboBox_reasoningEffort.SelectedIndexChanged += toolStripComboBox_reasoningEffort_SelectedIndexChanged;
            // 
            // toolStripMenuItem_thinking
            // 
            toolStripMenuItem_thinking.Checked = true;
            toolStripMenuItem_thinking.CheckOnClick = true;
            toolStripMenuItem_thinking.CheckState = CheckState.Checked;
            toolStripMenuItem_thinking.Name = "toolStripMenuItem_thinking";
            toolStripMenuItem_thinking.Size = new Size(340, 22);
            toolStripMenuItem_thinking.Text = "Thinking enabled";
            toolStripMenuItem_thinking.CheckedChanged += toolStripMenuItem_thinking_CheckedChanged;
            // 
            // toolStripMenuItem_reasoningBudget
            // 
            toolStripMenuItem_reasoningBudget.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_reasoningBudget });
            toolStripMenuItem_reasoningBudget.Name = "toolStripMenuItem_reasoningBudget";
            toolStripMenuItem_reasoningBudget.Size = new Size(340, 22);
            toolStripMenuItem_reasoningBudget.Text = "Reasoning Budget";
            // 
            // toolStripTextBox_reasoningBudget
            // 
            toolStripTextBox_reasoningBudget.Name = "toolStripTextBox_reasoningBudget";
            toolStripTextBox_reasoningBudget.Size = new Size(100, 23);
            toolStripTextBox_reasoningBudget.Text = "4096";
            toolStripTextBox_reasoningBudget.KeyDown += toolStripTextBox_reasoningBudget_KeyDown;
            // 
            // toolStripMenuItem_topP
            // 
            toolStripMenuItem_topP.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_topP });
            toolStripMenuItem_topP.Name = "toolStripMenuItem_topP";
            toolStripMenuItem_topP.Size = new Size(340, 22);
            toolStripMenuItem_topP.Text = "Top-P";
            // 
            // toolStripTextBox_topP
            // 
            toolStripTextBox_topP.Name = "toolStripTextBox_topP";
            toolStripTextBox_topP.Size = new Size(100, 23);
            toolStripTextBox_topP.Text = "0.95";
            toolStripTextBox_topP.KeyDown += toolStripTextBox_topP_KeyDown;
            // 
            // toolStripMenuItem_minP
            // 
            toolStripMenuItem_minP.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_minP });
            toolStripMenuItem_minP.Name = "toolStripMenuItem_minP";
            toolStripMenuItem_minP.Size = new Size(340, 22);
            toolStripMenuItem_minP.Text = "Min-P";
            // 
            // toolStripTextBox_minP
            // 
            toolStripTextBox_minP.Name = "toolStripTextBox_minP";
            toolStripTextBox_minP.Size = new Size(100, 23);
            toolStripTextBox_minP.Text = "0.0";
            toolStripTextBox_minP.KeyDown += toolStripTextBox_minP_KeyDown;
            // 
            // toolStripMenuItem_topK
            // 
            toolStripMenuItem_topK.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_topK });
            toolStripMenuItem_topK.Name = "toolStripMenuItem_topK";
            toolStripMenuItem_topK.Size = new Size(340, 22);
            toolStripMenuItem_topK.Text = "Top-K";
            // 
            // toolStripTextBox_topK
            // 
            toolStripTextBox_topK.Name = "toolStripTextBox_topK";
            toolStripTextBox_topK.Size = new Size(100, 23);
            toolStripTextBox_topK.Text = "64";
            toolStripTextBox_topK.KeyDown += toolStripTextBox_topK_KeyDown;
            // 
            // toolStripMenuItem_execModelLoadBat
            // 
            toolStripMenuItem_execModelLoadBat.DropDownItems.AddRange(new ToolStripItem[] { toolStripComboBox_modelLoadBats, toolStripMenuItem_hideCmd });
            toolStripMenuItem_execModelLoadBat.Name = "toolStripMenuItem_execModelLoadBat";
            toolStripMenuItem_execModelLoadBat.Size = new Size(290, 22);
            toolStripMenuItem_execModelLoadBat.Text = "📜 Execute Model Load .BAT";
            toolStripMenuItem_execModelLoadBat.Click += toolStripMenuItem_execModelLoadBat_Click;
            // 
            // toolStripComboBox_modelLoadBats
            // 
            toolStripComboBox_modelLoadBats.Name = "toolStripComboBox_modelLoadBats";
            toolStripComboBox_modelLoadBats.Size = new Size(360, 23);
            toolStripComboBox_modelLoadBats.Text = "Select a .BAT file";
            // 
            // toolStripMenuItem_hideCmd
            // 
            toolStripMenuItem_hideCmd.Checked = true;
            toolStripMenuItem_hideCmd.CheckOnClick = true;
            toolStripMenuItem_hideCmd.CheckState = CheckState.Checked;
            toolStripMenuItem_hideCmd.Name = "toolStripMenuItem_hideCmd";
            toolStripMenuItem_hideCmd.Size = new Size(420, 22);
            toolStripMenuItem_hideCmd.Text = "Start without CMD window";
            toolStripMenuItem_hideCmd.CheckedChanged += toolStripMenuItem_hideCmd_CheckedChanged;
            // 
            // toolStripMenuItem_loadOnnxGenaiServer
            // 
            toolStripMenuItem_loadOnnxGenaiServer.DropDownItems.AddRange(new ToolStripItem[] { toolStripMenuItem_onnxModelRootDir, toolStripComboBox_onnxModels, toolStripMenuItem_onnxContextLength, toolStripMenuItem_onnxMaxTokens, toolStripMenuItem_onnxTemperature, toolStripMenuItem_onnxTopP, toolStripMenuItem_onnxTopK, toolStripMenuItem_onnxRepeatPenalty, toolStripComboBox_onnxExecutionProvider, toolStripMenuItem_onnxHideCmd });
            toolStripMenuItem_loadOnnxGenaiServer.Enabled = false;
            toolStripMenuItem_loadOnnxGenaiServer.Name = "toolStripMenuItem_loadOnnxGenaiServer";
            toolStripMenuItem_loadOnnxGenaiServer.Size = new Size(290, 22);
            toolStripMenuItem_loadOnnxGenaiServer.Text = "🔮 Load ONNX-Genai Server (checking...)";
            toolStripMenuItem_loadOnnxGenaiServer.Click += toolStripMenuItem_loadOnnxGenaiServer_Click;
            // 
            // toolStripMenuItem_onnxModelRootDir
            // 
            toolStripMenuItem_onnxModelRootDir.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_onnxModelRootDir });
            toolStripMenuItem_onnxModelRootDir.Name = "toolStripMenuItem_onnxModelRootDir";
            toolStripMenuItem_onnxModelRootDir.Size = new Size(340, 22);
            toolStripMenuItem_onnxModelRootDir.Text = "Model Root Directory";
            // 
            // toolStripTextBox_onnxModelRootDir
            // 
            toolStripTextBox_onnxModelRootDir.Name = "toolStripTextBox_onnxModelRootDir";
            toolStripTextBox_onnxModelRootDir.Size = new Size(280, 23);
            toolStripTextBox_onnxModelRootDir.Text = "D:\\Models\\ONNX";
            toolStripTextBox_onnxModelRootDir.KeyDown += toolStripTextBox_onnxModelRootDir_KeyDown;
            // 
            // toolStripComboBox_onnxModels
            // 
            toolStripComboBox_onnxModels.Name = "toolStripComboBox_onnxModels";
            toolStripComboBox_onnxModels.Size = new Size(280, 23);
            toolStripComboBox_onnxModels.Text = "Select an ONNX model";
            toolStripComboBox_onnxModels.SelectedIndexChanged += toolStripComboBox_onnxModels_SelectedIndexChanged;
            // 
            // toolStripMenuItem_onnxContextLength
            // 
            toolStripMenuItem_onnxContextLength.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_onnxContextLength });
            toolStripMenuItem_onnxContextLength.Name = "toolStripMenuItem_onnxContextLength";
            toolStripMenuItem_onnxContextLength.Size = new Size(340, 22);
            toolStripMenuItem_onnxContextLength.Text = "Context Length";
            // 
            // toolStripTextBox_onnxContextLength
            // 
            toolStripTextBox_onnxContextLength.Name = "toolStripTextBox_onnxContextLength";
            toolStripTextBox_onnxContextLength.Size = new Size(100, 23);
            toolStripTextBox_onnxContextLength.Text = "4096";
            toolStripTextBox_onnxContextLength.KeyDown += toolStripTextBox_onnxContextLength_KeyDown;
            // 
            // toolStripMenuItem_onnxMaxTokens
            // 
            toolStripMenuItem_onnxMaxTokens.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_onnxMaxTokens });
            toolStripMenuItem_onnxMaxTokens.Name = "toolStripMenuItem_onnxMaxTokens";
            toolStripMenuItem_onnxMaxTokens.Size = new Size(340, 22);
            toolStripMenuItem_onnxMaxTokens.Text = "Max Tokens";
            // 
            // toolStripTextBox_onnxMaxTokens
            // 
            toolStripTextBox_onnxMaxTokens.Name = "toolStripTextBox_onnxMaxTokens";
            toolStripTextBox_onnxMaxTokens.Size = new Size(100, 23);
            toolStripTextBox_onnxMaxTokens.Text = "1024";
            toolStripTextBox_onnxMaxTokens.KeyDown += toolStripTextBox_onnxMaxTokens_KeyDown;
            // 
            // toolStripMenuItem_onnxTemperature
            // 
            toolStripMenuItem_onnxTemperature.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_onnxTemperature });
            toolStripMenuItem_onnxTemperature.Name = "toolStripMenuItem_onnxTemperature";
            toolStripMenuItem_onnxTemperature.Size = new Size(340, 22);
            toolStripMenuItem_onnxTemperature.Text = "Temperature";
            // 
            // toolStripTextBox_onnxTemperature
            // 
            toolStripTextBox_onnxTemperature.Name = "toolStripTextBox_onnxTemperature";
            toolStripTextBox_onnxTemperature.Size = new Size(100, 23);
            toolStripTextBox_onnxTemperature.Text = "0.8";
            toolStripTextBox_onnxTemperature.KeyDown += toolStripTextBox_onnxTemperature_KeyDown;
            // 
            // toolStripMenuItem_onnxTopP
            // 
            toolStripMenuItem_onnxTopP.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_onnxTopP });
            toolStripMenuItem_onnxTopP.Name = "toolStripMenuItem_onnxTopP";
            toolStripMenuItem_onnxTopP.Size = new Size(340, 22);
            toolStripMenuItem_onnxTopP.Text = "Top P";
            // 
            // toolStripTextBox_onnxTopP
            // 
            toolStripTextBox_onnxTopP.Name = "toolStripTextBox_onnxTopP";
            toolStripTextBox_onnxTopP.Size = new Size(100, 23);
            toolStripTextBox_onnxTopP.Text = "0.9";
            toolStripTextBox_onnxTopP.KeyDown += toolStripTextBox_onnxTopP_KeyDown;
            // 
            // toolStripMenuItem_onnxTopK
            // 
            toolStripMenuItem_onnxTopK.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_onnxTopK });
            toolStripMenuItem_onnxTopK.Name = "toolStripMenuItem_onnxTopK";
            toolStripMenuItem_onnxTopK.Size = new Size(340, 22);
            toolStripMenuItem_onnxTopK.Text = "Top K";
            // 
            // toolStripTextBox_onnxTopK
            // 
            toolStripTextBox_onnxTopK.Name = "toolStripTextBox_onnxTopK";
            toolStripTextBox_onnxTopK.Size = new Size(100, 23);
            toolStripTextBox_onnxTopK.Text = "40";
            toolStripTextBox_onnxTopK.KeyDown += toolStripTextBox_onnxTopK_KeyDown;
            // 
            // toolStripMenuItem_onnxRepeatPenalty
            // 
            toolStripMenuItem_onnxRepeatPenalty.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_onnxRepeatPenalty });
            toolStripMenuItem_onnxRepeatPenalty.Name = "toolStripMenuItem_onnxRepeatPenalty";
            toolStripMenuItem_onnxRepeatPenalty.Size = new Size(340, 22);
            toolStripMenuItem_onnxRepeatPenalty.Text = "Repeat Penalty";
            // 
            // toolStripTextBox_onnxRepeatPenalty
            // 
            toolStripTextBox_onnxRepeatPenalty.Name = "toolStripTextBox_onnxRepeatPenalty";
            toolStripTextBox_onnxRepeatPenalty.Size = new Size(100, 23);
            toolStripTextBox_onnxRepeatPenalty.Text = "1.1";
            toolStripTextBox_onnxRepeatPenalty.KeyDown += toolStripTextBox_onnxRepeatPenalty_KeyDown;
            // 
            // toolStripComboBox_onnxExecutionProvider
            // 
            toolStripComboBox_onnxExecutionProvider.Items.AddRange(new object[] { "CUDA", "Dml", "CPU" });
            toolStripComboBox_onnxExecutionProvider.Name = "toolStripComboBox_onnxExecutionProvider";
            toolStripComboBox_onnxExecutionProvider.Size = new Size(120, 23);
            toolStripComboBox_onnxExecutionProvider.Text = "-Provider-";
            toolStripComboBox_onnxExecutionProvider.SelectedIndexChanged += toolStripComboBox_onnxExecutionProvider_SelectedIndexChanged;
            // 
            // toolStripMenuItem_onnxHideCmd
            // 
            toolStripMenuItem_onnxHideCmd.CheckOnClick = true;
            toolStripMenuItem_onnxHideCmd.Name = "toolStripMenuItem_onnxHideCmd";
            toolStripMenuItem_onnxHideCmd.Size = new Size(340, 22);
            toolStripMenuItem_onnxHideCmd.Text = "Hide Console";
            toolStripMenuItem_onnxHideCmd.CheckedChanged += toolStripMenuItem_onnxHideCmd_CheckedChanged;
            // 
            // rerouteAPILlamacppOllamaToolStripMenuItem
            // 
            rerouteAPILlamacppOllamaToolStripMenuItem.CheckOnClick = true;
            rerouteAPILlamacppOllamaToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripMenuItem_openAiApi, toolStripSeparator4, llamacppPortToolStripMenuItem, ollamaPortToolStripMenuItem, printGenerationStatsToolStripMenuItem, showTokenssToolStripMenuItem, extendCopilotSystemPromptToolStripMenuItem });
            rerouteAPILlamacppOllamaToolStripMenuItem.Name = "rerouteAPILlamacppOllamaToolStripMenuItem";
            rerouteAPILlamacppOllamaToolStripMenuItem.Size = new Size(290, 22);
            rerouteAPILlamacppOllamaToolStripMenuItem.Text = "🔗 Re-route API llama.cpp -> Ollama";
            rerouteAPILlamacppOllamaToolStripMenuItem.CheckedChanged += rerouteAPILlamacppOllamaToolStripMenuItem_CheckedChanged;
            // 
            // toolStripMenuItem_openAiApi
            // 
            toolStripMenuItem_openAiApi.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_openAiApiUrl });
            toolStripMenuItem_openAiApi.Name = "toolStripMenuItem_openAiApi";
            toolStripMenuItem_openAiApi.Size = new Size(232, 22);
            toolStripMenuItem_openAiApi.Text = "Source OpenAI API";
            // 
            // toolStripTextBox_openAiApiUrl
            // 
            toolStripTextBox_openAiApiUrl.Name = "toolStripTextBox_openAiApiUrl";
            toolStripTextBox_openAiApiUrl.Size = new Size(240, 23);
            toolStripTextBox_openAiApiUrl.KeyDown += toolStripTextBox_openAiApiUrl_KeyDown;
            // 
            // toolStripSeparator4
            // 
            toolStripSeparator4.Name = "toolStripSeparator4";
            toolStripSeparator4.Size = new Size(229, 6);
            // 
            // llamacppPortToolStripMenuItem
            // 
            llamacppPortToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_llamacppPort });
            llamacppPortToolStripMenuItem.Name = "llamacppPortToolStripMenuItem";
            llamacppPortToolStripMenuItem.Size = new Size(232, 22);
            llamacppPortToolStripMenuItem.Text = "llama.cpp Port";
            // 
            // toolStripTextBox_llamacppPort
            // 
            toolStripTextBox_llamacppPort.Name = "toolStripTextBox_llamacppPort";
            toolStripTextBox_llamacppPort.Size = new Size(100, 23);
            toolStripTextBox_llamacppPort.Text = "8080";
            toolStripTextBox_llamacppPort.KeyDown += toolStripTextBox_llamacppPort_KeyDown;
            // 
            // ollamaPortToolStripMenuItem
            // 
            ollamaPortToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_ollamaPort });
            ollamaPortToolStripMenuItem.Name = "ollamaPortToolStripMenuItem";
            ollamaPortToolStripMenuItem.Size = new Size(232, 22);
            ollamaPortToolStripMenuItem.Text = "Ollama Port";
            // 
            // toolStripTextBox_ollamaPort
            // 
            toolStripTextBox_ollamaPort.Name = "toolStripTextBox_ollamaPort";
            toolStripTextBox_ollamaPort.Size = new Size(100, 23);
            toolStripTextBox_ollamaPort.Text = "11434";
            toolStripTextBox_ollamaPort.KeyDown += toolStripTextBox_ollamaPort_KeyDown;
            // 
            // printGenerationStatsToolStripMenuItem
            // 
            printGenerationStatsToolStripMenuItem.Checked = true;
            printGenerationStatsToolStripMenuItem.CheckOnClick = true;
            printGenerationStatsToolStripMenuItem.CheckState = CheckState.Checked;
            printGenerationStatsToolStripMenuItem.Name = "printGenerationStatsToolStripMenuItem";
            printGenerationStatsToolStripMenuItem.Size = new Size(232, 22);
            printGenerationStatsToolStripMenuItem.Text = "Print Generation Stats";
            printGenerationStatsToolStripMenuItem.Click += printGenerationStatsToolStripMenuItem_Click;
            // 
            // showTokenssToolStripMenuItem
            // 
            showTokenssToolStripMenuItem.Checked = true;
            showTokenssToolStripMenuItem.CheckOnClick = true;
            showTokenssToolStripMenuItem.CheckState = CheckState.Checked;
            showTokenssToolStripMenuItem.Name = "showTokenssToolStripMenuItem";
            showTokenssToolStripMenuItem.Size = new Size(232, 22);
            showTokenssToolStripMenuItem.Text = "Show tokens/s";
            // 
            // extendCopilotSystemPromptToolStripMenuItem
            // 
            extendCopilotSystemPromptToolStripMenuItem.Checked = true;
            extendCopilotSystemPromptToolStripMenuItem.CheckOnClick = true;
            extendCopilotSystemPromptToolStripMenuItem.CheckState = CheckState.Checked;
            extendCopilotSystemPromptToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_additionalCopilotSystemPrompt, toolStripMenuItem_appendParams });
            extendCopilotSystemPromptToolStripMenuItem.Name = "extendCopilotSystemPromptToolStripMenuItem";
            extendCopilotSystemPromptToolStripMenuItem.Size = new Size(232, 22);
            extendCopilotSystemPromptToolStripMenuItem.Text = "Extend Copilot SystemPrompt";
            extendCopilotSystemPromptToolStripMenuItem.CheckedChanged += extendCopilotSystemPromptToolStripMenuItem_CheckedChanged;
            // 
            // toolStripTextBox_additionalCopilotSystemPrompt
            // 
            toolStripTextBox_additionalCopilotSystemPrompt.Name = "toolStripTextBox_additionalCopilotSystemPrompt";
            toolStripTextBox_additionalCopilotSystemPrompt.Size = new Size(100, 23);
            toolStripTextBox_additionalCopilotSystemPrompt.KeyDown += toolStripTextBox_additionalCopilotSystemPrompt_KeyDown;
            // 
            // toolStripMenuItem_appendParams
            // 
            toolStripMenuItem_appendParams.Checked = true;
            toolStripMenuItem_appendParams.CheckOnClick = true;
            toolStripMenuItem_appendParams.CheckState = CheckState.Checked;
            toolStripMenuItem_appendParams.Name = "toolStripMenuItem_appendParams";
            toolStripMenuItem_appendParams.Size = new Size(216, 22);
            toolStripMenuItem_appendParams.Text = "Tell LLM the args + params";
            toolStripMenuItem_appendParams.CheckedChanged += toolStripMenuItem_appendParams_CheckedChanged;
            // 
            // smartPromptOptimizationsToolStripMenuItem
            // 
            smartPromptOptimizationsToolStripMenuItem.Checked = true;
            smartPromptOptimizationsToolStripMenuItem.CheckOnClick = true;
            smartPromptOptimizationsToolStripMenuItem.CheckState = CheckState.Checked;
            smartPromptOptimizationsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { promptSafetyRatioToolStripMenuItem, smartBudgetRatioToolStripMenuItem, largeMessageThresholdCharsToolStripMenuItem, skeletonMaxLinesToolStripMenuItem, focusKeywordLimitToolStripMenuItem, tailKeepBonusCharsToolStripMenuItem, injectToolCallingRulesToolStripMenuItem });
            smartPromptOptimizationsToolStripMenuItem.Name = "smartPromptOptimizationsToolStripMenuItem";
            smartPromptOptimizationsToolStripMenuItem.Size = new Size(290, 22);
            smartPromptOptimizationsToolStripMenuItem.Text = "💡 Smart Prompt Optimizations";
            smartPromptOptimizationsToolStripMenuItem.CheckedChanged += smartPromptOptimizationsToolStripMenuItem_CheckedChanged;
            // 
            // promptSafetyRatioToolStripMenuItem
            // 
            promptSafetyRatioToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_promptSafetyRatio });
            promptSafetyRatioToolStripMenuItem.Name = "promptSafetyRatioToolStripMenuItem";
            promptSafetyRatioToolStripMenuItem.Size = new Size(241, 22);
            promptSafetyRatioToolStripMenuItem.Text = "Prompt Safety Ratio";
            // 
            // toolStripTextBox_promptSafetyRatio
            // 
            toolStripTextBox_promptSafetyRatio.Name = "toolStripTextBox_promptSafetyRatio";
            toolStripTextBox_promptSafetyRatio.Size = new Size(100, 23);
            toolStripTextBox_promptSafetyRatio.Text = "0.90";
            toolStripTextBox_promptSafetyRatio.KeyDown += toolStripTextBox_promptSafetyRatio_KeyDown;
            // 
            // smartBudgetRatioToolStripMenuItem
            // 
            smartBudgetRatioToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_smartBudgetRatio });
            smartBudgetRatioToolStripMenuItem.Name = "smartBudgetRatioToolStripMenuItem";
            smartBudgetRatioToolStripMenuItem.Size = new Size(241, 22);
            smartBudgetRatioToolStripMenuItem.Text = "Smart Budget Ratio";
            // 
            // toolStripTextBox_smartBudgetRatio
            // 
            toolStripTextBox_smartBudgetRatio.Name = "toolStripTextBox_smartBudgetRatio";
            toolStripTextBox_smartBudgetRatio.Size = new Size(100, 23);
            toolStripTextBox_smartBudgetRatio.Text = "0.75";
            toolStripTextBox_smartBudgetRatio.KeyDown += toolStripTextBox_smartBudgetRatio_KeyDown;
            // 
            // largeMessageThresholdCharsToolStripMenuItem
            // 
            largeMessageThresholdCharsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_largeMessageThresholdChars });
            largeMessageThresholdCharsToolStripMenuItem.Name = "largeMessageThresholdCharsToolStripMenuItem";
            largeMessageThresholdCharsToolStripMenuItem.Size = new Size(241, 22);
            largeMessageThresholdCharsToolStripMenuItem.Text = "Large Message Threshold Chars";
            // 
            // toolStripTextBox_largeMessageThresholdChars
            // 
            toolStripTextBox_largeMessageThresholdChars.Name = "toolStripTextBox_largeMessageThresholdChars";
            toolStripTextBox_largeMessageThresholdChars.Size = new Size(100, 23);
            toolStripTextBox_largeMessageThresholdChars.Text = "2400";
            toolStripTextBox_largeMessageThresholdChars.KeyDown += toolStripTextBox_largeMessageThresholdChars_KeyDown;
            // 
            // skeletonMaxLinesToolStripMenuItem
            // 
            skeletonMaxLinesToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_skeletonMaxLines });
            skeletonMaxLinesToolStripMenuItem.Name = "skeletonMaxLinesToolStripMenuItem";
            skeletonMaxLinesToolStripMenuItem.Size = new Size(241, 22);
            skeletonMaxLinesToolStripMenuItem.Text = "Skeleton Max Lines";
            // 
            // toolStripTextBox_skeletonMaxLines
            // 
            toolStripTextBox_skeletonMaxLines.Name = "toolStripTextBox_skeletonMaxLines";
            toolStripTextBox_skeletonMaxLines.Size = new Size(100, 23);
            toolStripTextBox_skeletonMaxLines.Text = "60";
            toolStripTextBox_skeletonMaxLines.KeyDown += toolStripTextBox_skeletonMaxLines_KeyDown;
            // 
            // focusKeywordLimitToolStripMenuItem
            // 
            focusKeywordLimitToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_focusKeywordLimit });
            focusKeywordLimitToolStripMenuItem.Name = "focusKeywordLimitToolStripMenuItem";
            focusKeywordLimitToolStripMenuItem.Size = new Size(241, 22);
            focusKeywordLimitToolStripMenuItem.Text = "Focus Keyword Limit";
            // 
            // toolStripTextBox_focusKeywordLimit
            // 
            toolStripTextBox_focusKeywordLimit.Name = "toolStripTextBox_focusKeywordLimit";
            toolStripTextBox_focusKeywordLimit.Size = new Size(100, 23);
            toolStripTextBox_focusKeywordLimit.Text = "12";
            toolStripTextBox_focusKeywordLimit.KeyDown += toolStripTextBox_focusKeywordLimit_KeyDown;
            // 
            // tailKeepBonusCharsToolStripMenuItem
            // 
            tailKeepBonusCharsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_tailKeepBonusChars });
            tailKeepBonusCharsToolStripMenuItem.Name = "tailKeepBonusCharsToolStripMenuItem";
            tailKeepBonusCharsToolStripMenuItem.Size = new Size(241, 22);
            tailKeepBonusCharsToolStripMenuItem.Text = "Tail Keep Bonus Chars";
            // 
            // toolStripTextBox_tailKeepBonusChars
            // 
            toolStripTextBox_tailKeepBonusChars.Name = "toolStripTextBox_tailKeepBonusChars";
            toolStripTextBox_tailKeepBonusChars.Size = new Size(100, 23);
            toolStripTextBox_tailKeepBonusChars.Text = "500";
            toolStripTextBox_tailKeepBonusChars.KeyDown += toolStripTextBox_tailKeepBonusChars_KeyDown;
            // 
            // injectToolCallingRulesToolStripMenuItem
            // 
            injectToolCallingRulesToolStripMenuItem.Checked = true;
            injectToolCallingRulesToolStripMenuItem.CheckOnClick = true;
            injectToolCallingRulesToolStripMenuItem.CheckState = CheckState.Checked;
            injectToolCallingRulesToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_injectToolCallingRules });
            injectToolCallingRulesToolStripMenuItem.Name = "injectToolCallingRulesToolStripMenuItem";
            injectToolCallingRulesToolStripMenuItem.Size = new Size(241, 22);
            injectToolCallingRulesToolStripMenuItem.Text = "Inject Tool Calling Rules";
            injectToolCallingRulesToolStripMenuItem.CheckedChanged += injectToolCallingRulesToolStripMenuItem_CheckedChanged;
            // 
            // toolStripTextBox_injectToolCallingRules
            // 
            toolStripTextBox_injectToolCallingRules.Name = "toolStripTextBox_injectToolCallingRules";
            toolStripTextBox_injectToolCallingRules.Size = new Size(100, 23);
            toolStripTextBox_injectToolCallingRules.KeyDown += toolStripTextBox_injectToolCallingRules_KeyDown;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new Size(287, 6);
            // 
            // toolStripMenuItem_remapAnyKey
            // 
            toolStripMenuItem_remapAnyKey.Enabled = false;
            toolStripMenuItem_remapAnyKey.Name = "toolStripMenuItem_remapAnyKey";
            toolStripMenuItem_remapAnyKey.Size = new Size(290, 22);
            toolStripMenuItem_remapAnyKey.Text = "Remap any Key ... (0 enabled)";
            toolStripMenuItem_remapAnyKey.Click += toolStripMenuItem_remapAnyKey_Click;
            // 
            // toolStripSeparator6
            // 
            toolStripSeparator6.Name = "toolStripSeparator6";
            toolStripSeparator6.Size = new Size(287, 6);
            // 
            // openDebugConsoleToolStripMenuItem
            // 
            openDebugConsoleToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripMenuItem_visuallyFormatLog, toolStripMenuItem_includeRawChunksLog, toolStripMenuItem_logGenerationSpeed });
            openDebugConsoleToolStripMenuItem.Name = "openDebugConsoleToolStripMenuItem";
            openDebugConsoleToolStripMenuItem.Size = new Size(290, 22);
            openDebugConsoleToolStripMenuItem.Text = "💻 Open Debug Console";
            openDebugConsoleToolStripMenuItem.Click += openDebugConsoleToolStripMenuItem_Click;
            // 
            // toolStripMenuItem_visuallyFormatLog
            // 
            toolStripMenuItem_visuallyFormatLog.Checked = true;
            toolStripMenuItem_visuallyFormatLog.CheckOnClick = true;
            toolStripMenuItem_visuallyFormatLog.CheckState = CheckState.Checked;
            toolStripMenuItem_visuallyFormatLog.Name = "toolStripMenuItem_visuallyFormatLog";
            toolStripMenuItem_visuallyFormatLog.Size = new Size(278, 22);
            toolStripMenuItem_visuallyFormatLog.Text = "Visually Formatted Log";
            toolStripMenuItem_visuallyFormatLog.Click += toolStripMenuItem_visuallyFormatLog_Click;
            // 
            // toolStripMenuItem_includeRawChunksLog
            // 
            toolStripMenuItem_includeRawChunksLog.Checked = true;
            toolStripMenuItem_includeRawChunksLog.CheckOnClick = true;
            toolStripMenuItem_includeRawChunksLog.CheckState = CheckState.Checked;
            toolStripMenuItem_includeRawChunksLog.Name = "toolStripMenuItem_includeRawChunksLog";
            toolStripMenuItem_includeRawChunksLog.Size = new Size(278, 22);
            toolStripMenuItem_includeRawChunksLog.Text = "Include raw Request/Response Chunks";
            toolStripMenuItem_includeRawChunksLog.Click += toolStripMenuItem_includeRawChunksLog_Click;
            // 
            // toolStripMenuItem_logGenerationSpeed
            // 
            toolStripMenuItem_logGenerationSpeed.CheckOnClick = true;
            toolStripMenuItem_logGenerationSpeed.Name = "toolStripMenuItem_logGenerationSpeed";
            toolStripMenuItem_logGenerationSpeed.Size = new Size(278, 22);
            toolStripMenuItem_logGenerationSpeed.Text = "Log Generation Speed (tok/s)";
            toolStripMenuItem_logGenerationSpeed.CheckedChanged += toolStripMenuItem_logGenerationSpeed_CheckedChanged;
            // 
            // label_vram
            // 
            label_vram.AutoSize = true;
            label_vram.Font = new Font("Bahnschrift Condensed", 9.75F);
            label_vram.Location = new Point(0, 190);
            label_vram.Name = "label_vram";
            label_vram.Size = new Size(41, 16);
            label_vram.TabIndex = 4;
            label_vram.Text = "VRAM: -";
            // 
            // progressBar_vram
            // 
            progressBar_vram.Location = new Point(0, 208);
            progressBar_vram.Maximum = 1000;
            progressBar_vram.Name = "progressBar_vram";
            progressBar_vram.Size = new Size(240, 12);
            progressBar_vram.TabIndex = 3;
            // 
            // label_wattage
            // 
            label_wattage.AutoSize = true;
            label_wattage.Font = new Font("Bahnschrift Condensed", 9.75F);
            label_wattage.Location = new Point(0, 175);
            label_wattage.Name = "label_wattage";
            label_wattage.Size = new Size(40, 16);
            label_wattage.TabIndex = 5;
            label_wattage.Text = "Watts: -";
            // 
            // label_gpuUsage
            // 
            label_gpuUsage.AutoSize = true;
            label_gpuUsage.Font = new Font("Bahnschrift Condensed", 9.75F);
            label_gpuUsage.Location = new Point(166, 175);
            label_gpuUsage.Name = "label_gpuUsage";
            label_gpuUsage.Size = new Size(34, 16);
            label_gpuUsage.TabIndex = 6;
            label_gpuUsage.Text = "GPU: -";
            // 
            // label_gpuLoad2
            // 
            label_gpuLoad2.AutoSize = true;
            label_gpuLoad2.Font = new Font("Bahnschrift Condensed", 9.75F);
            label_gpuLoad2.Location = new Point(166, 223);
            label_gpuLoad2.Name = "label_gpuLoad2";
            label_gpuLoad2.Size = new Size(34, 16);
            label_gpuLoad2.TabIndex = 10;
            label_gpuLoad2.Text = "GPU: -";
            // 
            // label_gpuWatts2
            // 
            label_gpuWatts2.AutoSize = true;
            label_gpuWatts2.Font = new Font("Bahnschrift Condensed", 9.75F);
            label_gpuWatts2.Location = new Point(0, 223);
            label_gpuWatts2.Name = "label_gpuWatts2";
            label_gpuWatts2.Size = new Size(40, 16);
            label_gpuWatts2.TabIndex = 9;
            label_gpuWatts2.Text = "Watts: -";
            // 
            // label_gpuVram2
            // 
            label_gpuVram2.AutoSize = true;
            label_gpuVram2.Font = new Font("Bahnschrift Condensed", 9.75F);
            label_gpuVram2.Location = new Point(0, 238);
            label_gpuVram2.Name = "label_gpuVram2";
            label_gpuVram2.Size = new Size(41, 16);
            label_gpuVram2.TabIndex = 8;
            label_gpuVram2.Text = "VRAM: -";
            // 
            // progressBar_vram2
            // 
            progressBar_vram2.Location = new Point(0, 256);
            progressBar_vram2.Maximum = 1000;
            progressBar_vram2.Name = "progressBar_vram2";
            progressBar_vram2.Size = new Size(240, 12);
            progressBar_vram2.TabIndex = 7;
            // 
            // label_avgCpuLoadAndTemperature
            // 
            label_avgCpuLoadAndTemperature.AutoSize = true;
            label_avgCpuLoadAndTemperature.Font = new Font("Segoe UI Semilight", 8.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label_avgCpuLoadAndTemperature.Location = new Point(0, 103);
            label_avgCpuLoadAndTemperature.Name = "label_avgCpuLoadAndTemperature";
            label_avgCpuLoadAndTemperature.Size = new Size(102, 13);
            label_avgCpuLoadAndTemperature.TabIndex = 11;
            label_avgCpuLoadAndTemperature.Text = "Avg.: - % (-273,15C°)";
            // 
            // label_topTasksList
            // 
            label_topTasksList.AutoSize = true;
            label_topTasksList.Font = new Font("Bahnschrift Light Condensed", 8.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label_topTasksList.Location = new Point(122, 103);
            label_topTasksList.Name = "label_topTasksList";
            label_topTasksList.Size = new Size(31, 39);
            label_topTasksList.TabIndex = 12;
            label_topTasksList.Text = "#1 idle\r\n#2 idle\r\n#3 idle";
            // 
            // button_recordUsages
            // 
            button_recordUsages.Location = new Point(0, 119);
            button_recordUsages.Name = "button_recordUsages";
            button_recordUsages.Size = new Size(23, 23);
            button_recordUsages.TabIndex = 13;
            button_recordUsages.Text = "⏺";
            button_recordUsages.UseVisualStyleBackColor = true;
            button_recordUsages.Click += button_recordUsages_Click;
            // 
            // label_routingPortsInfo
            // 
            label_routingPortsInfo.AutoSize = true;
            label_routingPortsInfo.Font = new Font("Bahnschrift Light SemiCondensed", 8.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label_routingPortsInfo.Location = new Point(22, 116);
            label_routingPortsInfo.Name = "label_routingPortsInfo";
            label_routingPortsInfo.Size = new Size(94, 13);
            label_routingPortsInfo.TabIndex = 14;
            label_routingPortsInfo.Text = "Port: ----- to -----";
            label_routingPortsInfo.Visible = false;
            // 
            // WindowWidget
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(240, 271);
            ContextMenuStrip = contextMenuStrip_widget;
            Controls.Add(label_routingPortsInfo);
            Controls.Add(button_recordUsages);
            Controls.Add(label_topTasksList);
            Controls.Add(label_avgCpuLoadAndTemperature);
            Controls.Add(label_gpuLoad2);
            Controls.Add(label_gpuWatts2);
            Controls.Add(label_gpuVram2);
            Controls.Add(progressBar_vram2);
            Controls.Add(label_gpuUsage);
            Controls.Add(label_wattage);
            Controls.Add(label_vram);
            Controls.Add(progressBar_vram);
            Controls.Add(label_ram);
            Controls.Add(progressBar_ram);
            Controls.Add(pictureBox_cpu);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            MaximumSize = new Size(256, 310);
            MinimumSize = new Size(256, 310);
            Name = "WindowWidget";
            Text = "System Statistics";
            ((System.ComponentModel.ISupportInitialize)pictureBox_cpu).EndInit();
            contextMenuStrip_widget.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
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
        private ToolStripMenuItem toolStripMenuItem_thinking;
        private ToolStripMenuItem toolStripMenuItem_execModelLoadBat;
        private ToolStripComboBox toolStripComboBox_modelLoadBats;
        private ToolStripMenuItem toolStripMenuItem_loadOnnxGenaiServer;
        private ToolStripMenuItem toolStripMenuItem_onnxModelRootDir;
        private ToolStripMenuItem toolStripMenuItem_onnxContextLength;
        private ToolStripMenuItem toolStripMenuItem_onnxMaxTokens;
        private ToolStripMenuItem toolStripMenuItem_onnxTemperature;
        private ToolStripMenuItem toolStripMenuItem_onnxTopP;
        private ToolStripMenuItem toolStripMenuItem_onnxTopK;
        private ToolStripMenuItem toolStripMenuItem_onnxRepeatPenalty;
        private ToolStripTextBox toolStripTextBox_onnxModelRootDir;
        private ToolStripComboBox toolStripComboBox_onnxModels;
        private ToolStripTextBox toolStripTextBox_onnxContextLength;
        private ToolStripTextBox toolStripTextBox_onnxMaxTokens;
        private ToolStripTextBox toolStripTextBox_onnxTemperature;
        private ToolStripTextBox toolStripTextBox_onnxTopP;
        private ToolStripTextBox toolStripTextBox_onnxTopK;
        private ToolStripTextBox toolStripTextBox_onnxRepeatPenalty;
        private ToolStripComboBox toolStripComboBox_onnxExecutionProvider;
        private ToolStripMenuItem toolStripMenuItem_onnxHideCmd;
        private ToolStripMenuItem toolStripMenuItem_openAiApi;
        private ToolStripTextBox toolStripTextBox_openAiApiUrl;
        private ToolStripSeparator toolStripSeparator4;
        private ToolStripMenuItem smartPromptOptimizationsToolStripMenuItem;
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
        private ToolStripMenuItem ToolStripMenuItem_presencePenalty;
        private ToolStripTextBox toolStripTextBox_presencePenalty;
        private ToolStripMenuItem toolStripMenuItem_reasoningEffort;
        private ToolStripComboBox toolStripComboBox_reasoningEffort;
        private ToolStripSeparator toolStripSeparator6;
        private ToolStripMenuItem toolStripMenuItem_remapAnyKey;
        private ToolStripMenuItem toolStripMenuItem_kvCacheType;
        private ToolStripComboBox toolStripComboBox_cacheType;
        private ToolStripMenuItem toolStripMenuItem_toolCalls;
        private ToolStripMenuItem toolStripMenuItem_additionalArgs;
        private ToolStripTextBox toolStripTextBox_additionalArgs;
        private ToolStripMenuItem toolStripMenuItem_blackOutMode;
        private ToolStripMenuItem printGenerationStatsToolStripMenuItem;
        private ToolStripMenuItem injectToolCallingRulesToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_injectToolCallingRules;
        private ToolStripMenuItem showTokenssToolStripMenuItem;
        private ToolStripMenuItem extendCopilotSystemPromptToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_additionalCopilotSystemPrompt;
        private ToolStripMenuItem toolStripMenuItem_appendParams;
    }
}
