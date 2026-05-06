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
            this.showUsageToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_percentageColor = new ToolStripTextBox();
            this.alwaysOnTopToolStripMenuItem = new ToolStripMenuItem();
            this.trafficThresholdToolStripMenuItem = new ToolStripMenuItem();
            this.toolStripTextBox_threshold = new ToolStripTextBox();
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
            this.contextMenuStrip_widget.Items.AddRange(new ToolStripItem[] { this.updateIntervalToolStripMenuItem, this.selectGPUToolStripMenuItem, this.diagramColorToolStripMenuItem, this.showUsageToolStripMenuItem, this.alwaysOnTopToolStripMenuItem, this.trafficThresholdToolStripMenuItem, this.driveSpeedTestToolStripMenuItem });
            this.contextMenuStrip_widget.Name = "contextMenuStrip_widget";
            this.contextMenuStrip_widget.Size = new Size(177, 158);
            this.contextMenuStrip_widget.Text = "Settings";
            // 
            // updateIntervalToolStripMenuItem
            // 
            this.updateIntervalToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_interval });
            this.updateIntervalToolStripMenuItem.Name = "updateIntervalToolStripMenuItem";
            this.updateIntervalToolStripMenuItem.Size = new Size(176, 22);
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
            this.selectGPUToolStripMenuItem.Size = new Size(176, 22);
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
            this.diagramColorToolStripMenuItem.Size = new Size(176, 22);
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
            // showUsageToolStripMenuItem
            // 
            this.showUsageToolStripMenuItem.Checked = true;
            this.showUsageToolStripMenuItem.CheckOnClick = true;
            this.showUsageToolStripMenuItem.CheckState = CheckState.Checked;
            this.showUsageToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_percentageColor });
            this.showUsageToolStripMenuItem.Name = "showUsageToolStripMenuItem";
            this.showUsageToolStripMenuItem.Size = new Size(176, 22);
            this.showUsageToolStripMenuItem.Text = "Show Per Core % ...";
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
            this.alwaysOnTopToolStripMenuItem.Size = new Size(176, 22);
            this.alwaysOnTopToolStripMenuItem.Text = "Always on Top";
            this.alwaysOnTopToolStripMenuItem.CheckedChanged += this.alwaysOnTopToolStripMenuItem_CheckedChanged;
            // 
            // trafficThresholdToolStripMenuItem
            // 
            this.trafficThresholdToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_threshold });
            this.trafficThresholdToolStripMenuItem.Name = "trafficThresholdToolStripMenuItem";
            this.trafficThresholdToolStripMenuItem.Size = new Size(176, 22);
            this.trafficThresholdToolStripMenuItem.Text = "Traffic Threshold ...";
            // 
            // toolStripTextBox_threshold
            // 
            this.toolStripTextBox_threshold.Name = "toolStripTextBox_threshold";
            this.toolStripTextBox_threshold.Size = new Size(100, 23);
            this.toolStripTextBox_threshold.Text = "1 MB/s";
            this.toolStripTextBox_threshold.TextChanged += this.toolStripTextBox_threshold_TextChanged;
            // 
            // driveSpeedTestToolStripMenuItem
            // 
            this.driveSpeedTestToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripComboBox_drives, this.testSettingsToolStripMenuItem });
            this.driveSpeedTestToolStripMenuItem.Name = "driveSpeedTestToolStripMenuItem";
            this.driveSpeedTestToolStripMenuItem.Size = new Size(176, 22);
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
            this.testSettingsToolStripMenuItem.Text = "Test-Settings ...";
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
            // WindowWidget
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(240, 271);
            this.ContextMenuStrip = this.contextMenuStrip_widget;
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
    }
}
