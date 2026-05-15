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
            selectGPUToolStripMenuItem = new ToolStripMenuItem();
            toolStripComboBox_gpus = new ToolStripComboBox();
            diagramColorToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_diagramColor = new ToolStripTextBox();
            showUsageToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_percentageColor = new ToolStripTextBox();
            alwaysOnTopToolStripMenuItem = new ToolStripMenuItem();
            trafficThresholdToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_threshold = new ToolStripTextBox();
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
            contextMenuStrip_widget.Items.AddRange(new ToolStripItem[] { updateIntervalToolStripMenuItem, selectGPUToolStripMenuItem, diagramColorToolStripMenuItem, showUsageToolStripMenuItem, alwaysOnTopToolStripMenuItem, trafficThresholdToolStripMenuItem, driveSpeedTestToolStripMenuItem });
            contextMenuStrip_widget.Name = "contextMenuStrip_widget";
            contextMenuStrip_widget.Size = new Size(177, 158);
            contextMenuStrip_widget.Text = "Settings";
            // 
            // updateIntervalToolStripMenuItem
            // 
            updateIntervalToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_interval });
            updateIntervalToolStripMenuItem.Name = "updateIntervalToolStripMenuItem";
            updateIntervalToolStripMenuItem.Size = new Size(176, 22);
            updateIntervalToolStripMenuItem.Text = "Update Interval ...";
            // 
            // toolStripTextBox_interval
            // 
            toolStripTextBox_interval.Name = "toolStripTextBox_interval";
            toolStripTextBox_interval.Size = new Size(100, 23);
            toolStripTextBox_interval.Text = "420";
            toolStripTextBox_interval.Leave += toolStripTextBox_interval_Leave;
            toolStripTextBox_interval.KeyDown += toolStripTextBox_interval_KeyDown;
            // 
            // selectGPUToolStripMenuItem
            // 
            selectGPUToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripComboBox_gpus });
            selectGPUToolStripMenuItem.Name = "selectGPUToolStripMenuItem";
            selectGPUToolStripMenuItem.Size = new Size(176, 22);
            selectGPUToolStripMenuItem.Text = "Select GPU ...";
            // 
            // toolStripComboBox_gpus
            // 
            toolStripComboBox_gpus.Name = "toolStripComboBox_gpus";
            toolStripComboBox_gpus.Size = new Size(121, 23);
            toolStripComboBox_gpus.Text = "0";
            toolStripComboBox_gpus.SelectedIndexChanged += toolStripComboBox_gpus_SelectedIndexChanged;
            // 
            // diagramColorToolStripMenuItem
            // 
            diagramColorToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_diagramColor });
            diagramColorToolStripMenuItem.Name = "diagramColorToolStripMenuItem";
            diagramColorToolStripMenuItem.Size = new Size(176, 22);
            diagramColorToolStripMenuItem.Text = "Diagram Color ...";
            // 
            // toolStripTextBox_diagramColor
            // 
            toolStripTextBox_diagramColor.Name = "toolStripTextBox_diagramColor";
            toolStripTextBox_diagramColor.Size = new Size(100, 23);
            toolStripTextBox_diagramColor.Text = "#ffffff";
            toolStripTextBox_diagramColor.DoubleClick += toolStripTextBox_diagramColor_DoubleClick;
            toolStripTextBox_diagramColor.TextChanged += toolStripTextBox_diagramColor_TextChanged;
            // 
            // showUsageToolStripMenuItem
            // 
            showUsageToolStripMenuItem.Checked = true;
            showUsageToolStripMenuItem.CheckOnClick = true;
            showUsageToolStripMenuItem.CheckState = CheckState.Checked;
            showUsageToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_percentageColor });
            showUsageToolStripMenuItem.Name = "showUsageToolStripMenuItem";
            showUsageToolStripMenuItem.Size = new Size(176, 22);
            showUsageToolStripMenuItem.Text = "Show Per Core % ...";
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
            alwaysOnTopToolStripMenuItem.Size = new Size(176, 22);
            alwaysOnTopToolStripMenuItem.Text = "Always on Top";
            alwaysOnTopToolStripMenuItem.CheckedChanged += alwaysOnTopToolStripMenuItem_CheckedChanged;
            // 
            // trafficThresholdToolStripMenuItem
            // 
            trafficThresholdToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_threshold });
            trafficThresholdToolStripMenuItem.Name = "trafficThresholdToolStripMenuItem";
            trafficThresholdToolStripMenuItem.Size = new Size(176, 22);
            trafficThresholdToolStripMenuItem.Text = "Traffic Threshold ...";
            // 
            // toolStripTextBox_threshold
            // 
            toolStripTextBox_threshold.Name = "toolStripTextBox_threshold";
            toolStripTextBox_threshold.Size = new Size(100, 23);
            toolStripTextBox_threshold.Text = "1 MB/s";
            toolStripTextBox_threshold.TextChanged += toolStripTextBox_threshold_TextChanged;
            // 
            // driveSpeedTestToolStripMenuItem
            // 
            driveSpeedTestToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripComboBox_drives, testSettingsToolStripMenuItem });
            driveSpeedTestToolStripMenuItem.Name = "driveSpeedTestToolStripMenuItem";
            driveSpeedTestToolStripMenuItem.Size = new Size(176, 22);
            driveSpeedTestToolStripMenuItem.Text = "Drive Speed Test ...";
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
            testSettingsToolStripMenuItem.Text = "Test-Settings ...";
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
            button_recordUsages.Text = "°";
            button_recordUsages.UseVisualStyleBackColor = true;
            button_recordUsages.Click += button_recordUsages_Click;
            // 
            // WindowWidget
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(240, 271);
            ContextMenuStrip = contextMenuStrip_widget;
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
    }
}
