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
            label_vram = new Label();
            progressBar_vram = new ProgressBar();
            label_wattage = new Label();
            label_gpuUsage = new Label();
            label_gpuLoad2 = new Label();
            label_gpuWatts2 = new Label();
            label_gpuVram2 = new Label();
            progressBar_vram2 = new ProgressBar();
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
            progressBar_ram.Location = new Point(0, 121);
            progressBar_ram.Maximum = 1000;
            progressBar_ram.Name = "progressBar_ram";
            progressBar_ram.Size = new Size(240, 12);
            progressBar_ram.TabIndex = 1;
            // 
            // label_ram
            // 
            label_ram.AutoSize = true;
            label_ram.Location = new Point(0, 103);
            label_ram.Name = "label_ram";
            label_ram.Size = new Size(44, 15);
            label_ram.TabIndex = 2;
            label_ram.Text = "RAM: -";
            // 
            // contextMenuStrip_widget
            // 
            contextMenuStrip_widget.Items.AddRange(new ToolStripItem[] { updateIntervalToolStripMenuItem, selectGPUToolStripMenuItem, diagramColorToolStripMenuItem, showUsageToolStripMenuItem, alwaysOnTopToolStripMenuItem });
            contextMenuStrip_widget.Name = "contextMenuStrip_widget";
            contextMenuStrip_widget.Size = new Size(181, 136);
            contextMenuStrip_widget.Text = "Settings";
            // 
            // updateIntervalToolStripMenuItem
            // 
            updateIntervalToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_interval });
            updateIntervalToolStripMenuItem.Name = "updateIntervalToolStripMenuItem";
            updateIntervalToolStripMenuItem.Size = new Size(180, 22);
            updateIntervalToolStripMenuItem.Text = "Update Interval ...";
            // 
            // toolStripTextBox_interval
            // 
            toolStripTextBox_interval.Name = "toolStripTextBox_interval";
            toolStripTextBox_interval.Size = new Size(100, 23);
            toolStripTextBox_interval.Text = "420";
            toolStripTextBox_interval.TextChanged += toolStripTextBox_interval_TextChanged;
            // 
            // selectGPUToolStripMenuItem
            // 
            selectGPUToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripComboBox_gpus });
            selectGPUToolStripMenuItem.Name = "selectGPUToolStripMenuItem";
            selectGPUToolStripMenuItem.Size = new Size(180, 22);
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
            diagramColorToolStripMenuItem.Size = new Size(180, 22);
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
            showUsageToolStripMenuItem.Size = new Size(180, 22);
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
            alwaysOnTopToolStripMenuItem.Size = new Size(180, 22);
            alwaysOnTopToolStripMenuItem.Text = "Always on Top";
            alwaysOnTopToolStripMenuItem.CheckedChanged += alwaysOnTopToolStripMenuItem_CheckedChanged;
            // 
            // label_vram
            // 
            label_vram.AutoSize = true;
            label_vram.Location = new Point(0, 160);
            label_vram.Name = "label_vram";
            label_vram.Size = new Size(51, 15);
            label_vram.TabIndex = 4;
            label_vram.Text = "VRAM: -";
            // 
            // progressBar_vram
            // 
            progressBar_vram.Location = new Point(0, 178);
            progressBar_vram.Maximum = 1000;
            progressBar_vram.Name = "progressBar_vram";
            progressBar_vram.Size = new Size(240, 12);
            progressBar_vram.TabIndex = 3;
            // 
            // label_wattage
            // 
            label_wattage.AutoSize = true;
            label_wattage.Location = new Point(0, 145);
            label_wattage.Name = "label_wattage";
            label_wattage.Size = new Size(48, 15);
            label_wattage.TabIndex = 5;
            label_wattage.Text = "Watts: -";
            // 
            // label_gpuUsage
            // 
            label_gpuUsage.AutoSize = true;
            label_gpuUsage.Location = new Point(166, 145);
            label_gpuUsage.Name = "label_gpuUsage";
            label_gpuUsage.Size = new Size(41, 15);
            label_gpuUsage.TabIndex = 6;
            label_gpuUsage.Text = "GPU: -";
            // 
            // label_gpuLoad2
            // 
            label_gpuLoad2.AutoSize = true;
            label_gpuLoad2.Location = new Point(166, 203);
            label_gpuLoad2.Name = "label_gpuLoad2";
            label_gpuLoad2.Size = new Size(41, 15);
            label_gpuLoad2.TabIndex = 10;
            label_gpuLoad2.Text = "GPU: -";
            // 
            // label_gpuWatts2
            // 
            label_gpuWatts2.AutoSize = true;
            label_gpuWatts2.Location = new Point(0, 203);
            label_gpuWatts2.Name = "label_gpuWatts2";
            label_gpuWatts2.Size = new Size(48, 15);
            label_gpuWatts2.TabIndex = 9;
            label_gpuWatts2.Text = "Watts: -";
            // 
            // label_gpuVram2
            // 
            label_gpuVram2.AutoSize = true;
            label_gpuVram2.Location = new Point(0, 218);
            label_gpuVram2.Name = "label_gpuVram2";
            label_gpuVram2.Size = new Size(51, 15);
            label_gpuVram2.TabIndex = 8;
            label_gpuVram2.Text = "VRAM: -";
            // 
            // progressBar_vram2
            // 
            progressBar_vram2.Location = new Point(0, 236);
            progressBar_vram2.Maximum = 1000;
            progressBar_vram2.Name = "progressBar_vram2";
            progressBar_vram2.Size = new Size(240, 12);
            progressBar_vram2.TabIndex = 7;
            // 
            // WindowWidget
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(240, 251);
            ContextMenuStrip = contextMenuStrip_widget;
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
            MaximumSize = new Size(256, 290);
            MinimumSize = new Size(256, 290);
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
    }
}
