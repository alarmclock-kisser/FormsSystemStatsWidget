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
            this.label_vram = new Label();
            this.progressBar_vram = new ProgressBar();
            this.label_wattage = new Label();
            this.label_gpuUsage = new Label();
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
            this.progressBar_ram.Location = new Point(0, 121);
            this.progressBar_ram.Maximum = 1000;
            this.progressBar_ram.Name = "progressBar_ram";
            this.progressBar_ram.Size = new Size(240, 12);
            this.progressBar_ram.TabIndex = 1;
            // 
            // label_ram
            // 
            this.label_ram.AutoSize = true;
            this.label_ram.Location = new Point(0, 103);
            this.label_ram.Name = "label_ram";
            this.label_ram.Size = new Size(44, 15);
            this.label_ram.TabIndex = 2;
            this.label_ram.Text = "RAM: -";
            // 
            // contextMenuStrip_widget
            // 
            this.contextMenuStrip_widget.Items.AddRange(new ToolStripItem[] { this.updateIntervalToolStripMenuItem, this.selectGPUToolStripMenuItem, this.diagramColorToolStripMenuItem, this.showUsageToolStripMenuItem });
            this.contextMenuStrip_widget.Name = "contextMenuStrip_widget";
            this.contextMenuStrip_widget.Size = new Size(181, 114);
            this.contextMenuStrip_widget.Text = "Settings";
            // 
            // updateIntervalToolStripMenuItem
            // 
            this.updateIntervalToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripTextBox_interval });
            this.updateIntervalToolStripMenuItem.Name = "updateIntervalToolStripMenuItem";
            this.updateIntervalToolStripMenuItem.Size = new Size(180, 22);
            this.updateIntervalToolStripMenuItem.Text = "Update Interval ...";
            // 
            // toolStripTextBox_interval
            // 
            this.toolStripTextBox_interval.Name = "toolStripTextBox_interval";
            this.toolStripTextBox_interval.Size = new Size(100, 23);
            this.toolStripTextBox_interval.Text = "250";
            this.toolStripTextBox_interval.TextChanged += this.toolStripTextBox_interval_TextChanged;
            // 
            // selectGPUToolStripMenuItem
            // 
            this.selectGPUToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { this.toolStripComboBox_gpus });
            this.selectGPUToolStripMenuItem.Name = "selectGPUToolStripMenuItem";
            this.selectGPUToolStripMenuItem.Size = new Size(180, 22);
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
            this.diagramColorToolStripMenuItem.Size = new Size(180, 22);
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
            this.showUsageToolStripMenuItem.Size = new Size(180, 22);
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
            // label_vram
            // 
            this.label_vram.AutoSize = true;
            this.label_vram.Location = new Point(0, 160);
            this.label_vram.Name = "label_vram";
            this.label_vram.Size = new Size(51, 15);
            this.label_vram.TabIndex = 4;
            this.label_vram.Text = "VRAM: -";
            // 
            // progressBar_vram
            // 
            this.progressBar_vram.Location = new Point(0, 178);
            this.progressBar_vram.Maximum = 1000;
            this.progressBar_vram.Name = "progressBar_vram";
            this.progressBar_vram.Size = new Size(240, 12);
            this.progressBar_vram.TabIndex = 3;
            // 
            // label_wattage
            // 
            this.label_wattage.AutoSize = true;
            this.label_wattage.Location = new Point(0, 145);
            this.label_wattage.Name = "label_wattage";
            this.label_wattage.Size = new Size(48, 15);
            this.label_wattage.TabIndex = 5;
            this.label_wattage.Text = "Watts: -";
            // 
            // label_gpuUsage
            // 
            this.label_gpuUsage.AutoSize = true;
            this.label_gpuUsage.Location = new Point(166, 145);
            this.label_gpuUsage.Name = "label_gpuUsage";
            this.label_gpuUsage.Size = new Size(41, 15);
            this.label_gpuUsage.TabIndex = 6;
            this.label_gpuUsage.Text = "GPU: -";
            // 
            // WindowWidget
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(240, 193);
            this.ContextMenuStrip = this.contextMenuStrip_widget;
            this.Controls.Add(this.label_gpuUsage);
            this.Controls.Add(this.label_wattage);
            this.Controls.Add(this.label_vram);
            this.Controls.Add(this.progressBar_vram);
            this.Controls.Add(this.label_ram);
            this.Controls.Add(this.progressBar_ram);
            this.Controls.Add(this.pictureBox_cpu);
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.MaximumSize = new Size(256, 232);
            this.MinimumSize = new Size(256, 232);
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
    }
}
