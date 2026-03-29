using FormsSystemStatsWidget.Core;
using Timer = System.Windows.Forms.Timer;

namespace FormsSystemStatsWidget.Forms
{
    public partial class WindowWidget : Form
    {
        private int _updateIntervalMs = 420;
        private Color _diagramColor = Color.White;
        private Color? _percentageColor = Color.BlueViolet;

        private Timer UpdateTimer;
        private GpuStats Gpu;
        private GpuStats? Gpu2 = null;


        public WindowWidget()
        {
            this.InitializeComponent();

            this.UpdateTimer = new Timer();
            this.UpdateTimer.Interval = this._updateIntervalMs;
            this.UpdateTimer.Tick += this.Timer_Tick;
            this.UpdateTimer.Start();

            this.toolStripComboBox_gpus.Items.Clear();
            this.toolStripComboBox_gpus.Items.AddRange(GpuStats.GpuNames.ToArray());
            if (this.toolStripComboBox_gpus.Items.Count > 0)
            {
                this.toolStripComboBox_gpus.SelectedIndex = 0;
            }

            this.Gpu = new GpuStats(this.toolStripComboBox_gpus.SelectedIndex);

            // Get GPUs count, if 2nd available, create GpuStats for it
            if (GpuStats.GpuNames.Count > 1)
            {
                this.Gpu2 = new GpuStats(1);
            }

            this.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    this.Capture = false;
                    Message m = Message.Create(this.Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
                    this.WndProc(ref m);
                }
                else if (e.Button == MouseButtons.Right)
                {
                    this.contextMenuStrip_widget.Show(this, e.Location);
                }
            };
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            var threads = CpuStats.GetThreadUsages();
            double ramTotalGb = Math.Round(CpuStats.GetTotalMemoryBytes() / 1_073_741_824.0, 3);
            double ramUsedGb = Math.Round((CpuStats.GetUsedMemoryBytes()) / 1_073_741_824.0, 3);

            double gpuUsage = this.Gpu.CurrentLoad01 * 100;
            double gpuWattage = this.Gpu.CurrentPowerWatts ?? 0;
            double vramTotalGb = Math.Round(this.Gpu.GetTotalVramBytes() / 1_073_741_824.0, 3);
            double vramUsedGb = Math.Round(this.Gpu.GetUsedVramBytes() / 1_073_741_824.0, 3);

            await Task.WhenAll(
                this.UpdateCpuUsageAsync(threads),
                this.UpdateRamUsageAsync(ramTotalGb, ramUsedGb),
                this.UpdateGpuUsageAsync(gpuUsage, gpuWattage),
                this.UpdateVramUsageAsync(vramTotalGb, vramUsedGb)
            );
        }

        private void toolStripTextBox_interval_TextChanged(object sender, EventArgs e)
        {
            string text = this.toolStripTextBox_interval.Text;
            if (int.TryParse(text, out int interval))
            {
                this._updateIntervalMs = interval;
                this.UpdateTimer.Interval = this._updateIntervalMs < 50 ? 50 : this._updateIntervalMs;
            }

            this.toolStripTextBox_interval.Text = this._updateIntervalMs.ToString();
        }


        private async Task UpdateCpuUsageAsync(float[] usages)
        {
            var bmp = await CpuStats.RenderCoresBitmapAsync(usages, this.pictureBox_cpu.Width, this.pictureBox_cpu.Height, this._diagramColor, (this.showUsageToolStripMenuItem.Enabled ? this._percentageColor : null), CancellationToken.None);
            this.Invoke((Action)(() =>
            {
                this.pictureBox_cpu.Image?.Dispose();
                this.pictureBox_cpu.Image = bmp;
            }));
        }

        private async Task UpdateRamUsageAsync(double totalGb, double usedGb)
        {
            double percentUsed = totalGb > 0 ? (usedGb / totalGb) * 100 : 0;

            this.Invoke((Action)(() =>
            {
                this.label_ram.Text = $"RAM: {usedGb} GB / {totalGb} GB ({percentUsed:0.00}%)";
                this.progressBar_ram.Value = Math.Clamp((int)percentUsed * 10, 0, this.progressBar_ram.Maximum);
            }));

            await Task.CompletedTask;
        }

        private async Task UpdateGpuUsageAsync(double usagePercent, double wattage)
        {
            this.Invoke((Action)(() =>
            {
                this.label_gpuUsage.Text = $"GPU: {usagePercent:0.00}%";
                this.label_wattage.Text = $"Watts: {wattage:0.00} W";
            }));

            if (this.Gpu2 != null)
            {
                this.Invoke((Action)(() =>
                {
                    this.label_gpuLoad2.Text = $"GPU2: {this.Gpu2.CurrentLoad01 * 100:0.00}%";
                    this.label_gpuWatts2.Text = $"Watts: {this.Gpu2.CurrentPowerWatts ?? 0:0.00} W";
                }));
            }

            await Task.CompletedTask;
        }

        private async Task UpdateVramUsageAsync(double totalGb, double usedGb)
        {
            double percentUsed = totalGb > 0 ? (usedGb / totalGb) * 100 : 0;
            this.Invoke((Action)(() =>
            {
                this.label_vram.Text = $"VRAM: {usedGb} GB / {totalGb} GB ({percentUsed:0.00}%)";
                this.progressBar_vram.Value = Math.Clamp((int)percentUsed * 10, 0, this.progressBar_vram.Maximum);
            }));

            if (this.Gpu2 != null)
            {
                this.Invoke((Action)(() =>
                {
                    this.label_gpuVram2.Text = $"VRAM: {Math.Round(this.Gpu2.GetUsedVramBytes() / 1_073_741_824.0, 3)} GB / {Math.Round(this.Gpu2.GetTotalVramBytes() / 1_073_741_824.0, 3)} GB ({(this.Gpu2.GetTotalVramBytes() > 0 ? (this.Gpu2.GetUsedVramBytes() / this.Gpu2.GetTotalVramBytes()) * 100 : 0):0.00}%)";
                    this.progressBar_vram2.Value = Math.Clamp((int)((this.Gpu2.GetTotalVramBytes() > 0 ? (this.Gpu2.GetUsedVramBytes() / this.Gpu2.GetTotalVramBytes()) * 100 : 0) * 10), 0, this.progressBar_vram2.Maximum);
                }));
            }

            await Task.CompletedTask;
        }

        private void toolStripComboBox_gpus_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.Gpu?.Dispose();
            this.Gpu = new GpuStats(this.toolStripComboBox_gpus.SelectedIndex);
        }

        private void toolStripTextBox_diagramColor_TextChanged(object sender, EventArgs e)
        {
            string hex = this.toolStripTextBox_diagramColor.Text.Replace("#", "");
            if (hex.Length == 6 && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int rgb))
            {
                // rgb is RRGGBB; ensure the color is created opaque by adding alpha 0xFF
                this._diagramColor = Color.FromArgb(unchecked((int)0xFF000000 | rgb));
            }
        }

        private void toolStripTextBox_percentageColor_TextChanged(object sender, EventArgs e)
        {
            string hex = this.toolStripTextBox_percentageColor.Text.Replace("#", "");
            if (hex.Length == 6 && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int rgb))
            {
                // rgb is RRGGBB; construct an opaque Color (add alpha byte)
                this._percentageColor = Color.FromArgb(unchecked((int)0xFF000000 | rgb));
            }
            else
            {
                this._percentageColor = null;
            }

        }

        private void toolStripTextBox_percentageColor_EnabledChanged(object sender, EventArgs e)
        {
            // Do not clear the stored color when disabling; keep the selected color so it can be re-enabled.
            if (this.toolStripTextBox_percentageColor.Enabled)
            {
                this.toolStripTextBox_percentageColor.Text = this._percentageColor.HasValue ? $"#{this._percentageColor.Value.ToArgb() & 0xFFFFFF:X6}" : "";
            }
        }

        private void toolStripTextBox_diagramColor_DoubleClick(object sender, EventArgs e)
        {
            // Color picker dialog
            using (ColorDialog colorDialog = new ColorDialog())
            {
                colorDialog.AllowFullOpen = true;
                colorDialog.AnyColor = true;
                colorDialog.SolidColorOnly = false;
                colorDialog.Color = this._diagramColor;
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    this._diagramColor = colorDialog.Color;
                    this.toolStripTextBox_diagramColor.Text = $"#{colorDialog.Color.ToArgb() & 0xFFFFFF:X6}";
                }
            }
        }

        private void toolStripTextBox_percentageColor_DoubleClick(object sender, EventArgs e)
        {
            // Color picker dialog
            using (ColorDialog colorDialog = new ColorDialog())
            {
                colorDialog.AllowFullOpen = true;
                colorDialog.AnyColor = true;
                colorDialog.SolidColorOnly = false;
                colorDialog.Color = this._percentageColor ?? Color.White;
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    this._percentageColor = colorDialog.Color;
                    this.toolStripTextBox_percentageColor.Text = $"#{colorDialog.Color.ToArgb() & 0xFFFFFF:X6}";
                    this.toolStripTextBox_percentageColor.Enabled = true;
                }
            }
        }

        private void alwaysOnTopToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            this.TopMost = this.alwaysOnTopToolStripMenuItem.Checked;
        }
    }
}
