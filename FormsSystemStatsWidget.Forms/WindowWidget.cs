using FormsSystemStatsWidget.Core;
using System.Text.RegularExpressions;
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
        private volatile bool _closing = false;
        private int _tickInProgress = 0;


        public WindowWidget()
        {
            this.InitializeComponent();
            this.DoubleBuffered = true;

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

            try { TrafficStats.Init(); }
            catch { }
        }

        private void UpdateTitleWithTraffic()
        {
            string up = TrafficStats.FormatBytesPerSecond(TrafficStats.UpBytesPerSecond);
            string down = TrafficStats.FormatBytesPerSecond(TrafficStats.DownBytesPerSecond);
            string top = string.Empty;
            // Only show top talker when both conditions hold:
            // 1) total network traffic is at or above the configured threshold
            // 2) the top process IO rate is at or above the configured threshold
            double netTotal = TrafficStats.UpBytesPerSecond + TrafficStats.DownBytesPerSecond;
            if (netTotal >= TrafficStats.ThresholdBytesPerSecond && !string.IsNullOrEmpty(TrafficStats.TopTalker) && TrafficStats.ActiveProcesses.Count > 0)
            {
                var topEntry = TrafficStats.ActiveProcesses[0];
                if (topEntry.Name == TrafficStats.TopTalker && topEntry.IoBytesPerSec >= TrafficStats.ThresholdBytesPerSecond)
                {
                    top = Ellipsize(TrafficStats.TopTalker, 30);
                }
            }
            this.Text = $"\u2191 {up}  \u2193 {down}  {top}";
        }

        private static string Ellipsize(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || maxLen <= 0)
                return string.Empty;
            if (text.Length <= maxLen)
                return text;
            if (maxLen <= 3)
                return text.Substring(0, maxLen);
            return text.Substring(0, maxLen - 3) + "...";
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            if (_closing) return;
            if (Interlocked.Exchange(ref _tickInProgress, 1) == 1) return;

            this.UpdateTimer.Stop();

            try
            {
                var gpuRef = this.Gpu;

                var threadsTask = Task.Run(() => CpuStats.GetThreadUsages());
                var ramTask = Task.Run(() =>
                {
                    double total = Math.Round(CpuStats.GetTotalMemoryBytes() / 1_073_741_824.0, 3);
                    double used = Math.Round(CpuStats.GetUsedMemoryBytes() / 1_073_741_824.0, 3);
                    return (total, used);
                });
                var gpuTask = Task.Run(() =>
                {
                    try
                    {
                        double usage = gpuRef.CurrentLoad01 * 100;
                        double wattage = gpuRef.CurrentPowerWatts ?? 0;
                        double vramTotal = Math.Round(gpuRef.GetTotalVramBytes() / 1_073_741_824.0, 3);
                        double vramUsed = Math.Round(gpuRef.GetUsedVramBytes() / 1_073_741_824.0, 3);
                        return (usage, wattage, vramTotal, vramUsed);
                    }
                    catch
                    {
                        return (0.0, 0.0, 0.0, 0.0);
                    }
                });
                var trafficTask = Task.Run(() => TrafficStats.Sample(this.UpdateTimer.Interval));

                await Task.WhenAll(threadsTask, ramTask, gpuTask, trafficTask);

                var threads = threadsTask.Result;
                var (ramTotalGb, ramUsedGb) = ramTask.Result;
                var (gpuUsage, gpuWattage, vramTotalGb, vramUsedGb) = gpuTask.Result;

                await Task.WhenAll(
                    this.UpdateCpuUsageAsync(threads),
                    this.UpdateRamUsageAsync(ramTotalGb, ramUsedGb),
                    this.UpdateGpuUsageAsync(gpuUsage, gpuWattage),
                    this.UpdateVramUsageAsync(vramTotalGb, vramUsedGb)
                );

                if (_closing) return;
                UpdateTitleWithTraffic();
            }
            finally
            {
                Interlocked.Exchange(ref _tickInProgress, 0);
                if (!_closing)
                {
                    this.UpdateTimer.Start();
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _closing = true;
            this.UpdateTimer.Stop();

            try { this.Gpu?.Dispose(); } catch { }
            try { this.Gpu2?.Dispose(); } catch { }

            base.OnFormClosing(e);
        }

        private void toolStripTextBox_interval_Leave(object? sender, EventArgs e)
        {
            this.ApplyIntervalFromText();
        }

        private void toolStripTextBox_interval_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            this.ApplyIntervalFromText();
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void ApplyIntervalFromText()
        {
            string text = this.toolStripTextBox_interval.Text;
            if (int.TryParse(text, out int interval))
            {
                this._updateIntervalMs = interval;
            }

            this.UpdateTimer.Interval = this._updateIntervalMs < 50 ? 50 : this._updateIntervalMs;
            this.toolStripTextBox_interval.Text = this._updateIntervalMs.ToString();
        }


        private async Task UpdateCpuUsageAsync(float[] usages)
        {
            var bmp = await CpuStats.RenderCoresBitmapAsync(usages, this.pictureBox_cpu.Width, this.pictureBox_cpu.Height, this._diagramColor, (this.showUsageToolStripMenuItem.Enabled ? this._percentageColor : null), CancellationToken.None);
            if (_closing) { bmp?.Dispose(); return; }
            this.pictureBox_cpu.Image?.Dispose();
            this.pictureBox_cpu.Image = bmp;
        }

        private Task UpdateRamUsageAsync(double totalGb, double usedGb)
        {
            if (_closing) return Task.CompletedTask;
            double percentUsed = totalGb > 0 ? (usedGb / totalGb) * 100 : 0;

            this.label_ram.Text = $"RAM: {usedGb} GB / {totalGb} GB ({percentUsed:0.00}%)";
            this.progressBar_ram.Value = Math.Clamp((int)percentUsed * 10, 0, this.progressBar_ram.Maximum);

            return Task.CompletedTask;
        }

        private Task UpdateGpuUsageAsync(double usagePercent, double wattage)
        {
            if (_closing) return Task.CompletedTask;

            this.label_gpuUsage.Text = $"GPU: {usagePercent:0.00}%";
            this.label_wattage.Text = $"Watts: {wattage:0.00} W";

            if (this.Gpu2 != null)
            {
                this.label_gpuLoad2.Text = $"GPU2: {this.Gpu2.CurrentLoad01 * 100:0.00}%";
                this.label_gpuWatts2.Text = $"Watts: {this.Gpu2.CurrentPowerWatts ?? 0:0.00} W";
            }

            return Task.CompletedTask;
        }

        private Task UpdateVramUsageAsync(double totalGb, double usedGb)
        {
            if (_closing) return Task.CompletedTask;
            double percentUsed = totalGb > 0 ? (usedGb / totalGb) * 100 : 0;
            this.label_vram.Text = $"VRAM: {usedGb} GB / {totalGb} GB ({percentUsed:0.00}%)";
            this.progressBar_vram.Value = Math.Clamp((int)percentUsed * 10, 0, this.progressBar_vram.Maximum);

            if (this.Gpu2 != null)
            {
                long gpu2TotalBytes = this.Gpu2.GetTotalVramBytes();
                long gpu2UsedBytes = this.Gpu2.GetUsedVramBytes();
                double gpu2TotalGb = Math.Round(gpu2TotalBytes / 1_073_741_824.0, 3);
                double gpu2UsedGb = Math.Round(gpu2UsedBytes / 1_073_741_824.0, 3);
                double gpu2PercentUsed = gpu2TotalBytes > 0 ? (Math.Max(0.0, gpu2UsedBytes) / gpu2TotalBytes) * 100.0 : 0.0;

                this.label_gpuVram2.Text = $"VRAM: {gpu2UsedGb} GB / {gpu2TotalGb} GB ({gpu2PercentUsed:0.00}%)";
                this.progressBar_vram2.Value = Math.Clamp((int)(gpu2PercentUsed * 10), 0, this.progressBar_vram2.Maximum);
            }

            return Task.CompletedTask;
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

        private void toolStripTextBox_threshold_TextChanged(object sender, EventArgs e)
        {
            string text = this.toolStripTextBox_threshold.Text;

            // Try parsing as traffic speed with optional suffixes (e.g. "10 MB/s", "500 KB/s"), remove all spaces before parsing
            // Supported suffixes: B/s, kB/s, KB/s, mB/s, MB/s, gB/s, GB/s (kilo, kibi, mega, mebi, giga, giby -Bytes per second) (case-sensitive (!), s/S doesn't matter mean always seconds)
            text = text.Replace(" ", "");

            Regex regex = new(@"^([0-9]+(?:[.,][0-9]+)?)(?i:([kmg]?b)/(s|m|h|d))$");
            if (regex.IsMatch(text))
            {
                // Consecutive numbers substring with optional decimal point (invariant culture ('.' & ','))
                double? value = text.StartsWith("0x") && int.TryParse(text.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out int hexVal)
                    ? hexVal
                    : double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double numVal)
                        ? numVal
                        : (double?)null;
                if (value == null)
                {
                    return;
                }

                TrafficStats.ThresholdBytesPerSecond = 0; // determine by suffix and value, differentiate between kB/KB, mB/MB, gB/GB (kilo, kibi, mega, mebi, giga, giby -Bytes) (case-sensitive (!))
                string suffix = regex.Match(text).Groups[2].Value;
                long multiplier = suffix switch
                {
                    "B" => 1,
                    "kB" or "kib" => 1_000,
                    "KB"=> 1_024,
                    "mB" or "mib" => 1_000_000,
                    "MB" => 1_048_576,
                    "gB" or "gib" => 1_000_000_000,
                    "GB" => 1_073_741_824,
                    _ => 0
                };

                // Consider time unit suffix (s, m, h, d) for seconds, minutes, hours, days, apply multiplier accordingly
                string timeSuffix = regex.Match(text).Groups[3].Value.ToLower();
                multiplier *= timeSuffix switch
                {
                    "s" => 1,
                    "m" => 60,
                    "h" => 3600,
                    "d" => 86400,
                    _ => 1
                };

                TrafficStats.ThresholdBytesPerSecond = value.Value * multiplier;
            }
        }
    }
}
