using FormsSystemStatsWidget.Core;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Windows.Forms.Timer;

namespace FormsSystemStatsWidget.Forms
{
    public partial class WindowWidget
    {
        private void TrySetWindowTitleSafe(string title)
        {
            if (this.IsDisposed || this.Disposing)
            {
                return;
            }

            try
            {
                if (!this.IsHandleCreated)
                {
                    return;
                }

                if (this.InvokeRequired)
                {
                    _ = this.BeginInvoke(new Action(() =>
                    {
                        if (!this.IsDisposed && !this.Disposing)
                        {
                            this.Text = title;
                        }
                    }));
                }
                else
                {
                    this.Text = title;
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
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
                    top = WidgetStatics.Ellipsize(TrafficStats.TopTalker, 30);
                }
            }
            this.Text = $"\u2191 {up}  \u2193 {down}  {top}";
        }

        private void HandleLoggerMessageLogged(string text)
        {
            Match match = TokensPerSecondRegex.Match(text);
            if (match.Success && double.TryParse(match.Groups["tps"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedTps))
            {
                this._lastStdOutTokensPerSecond = parsedTps;
                this._lastStdOutTokensPerSecondUtc = DateTime.UtcNow;
                LlamaServerStats.UpdateGenerationSpeed((float) parsedTps);
            }

            if (this._debugConsoleForm == null || this._debugConsoleForm.IsDisposed)
            {
                return;
            }

            this._debugConsoleForm.AppendLogLine(text);
        }

        private void ApplyGpuLayout()
        {
            bool hasSecondGpu = this.Gpu2 != null;

            this.label_gpuLoad2.Visible = hasSecondGpu;
            this.label_gpuWatts2.Visible = hasSecondGpu;
            this.label_gpuVram2.Visible = hasSecondGpu;
            this._progVram2.Visible = hasSecondGpu;

            int clientHeight = hasSecondGpu ? MultiGpuClientHeight : SingleGpuClientHeight;
            this.ClientSize = new Size(this.ClientSize.Width, clientHeight);

            Size windowSize = new(256, clientHeight + 39);
            this.MinimumSize = windowSize;
            this.MaximumSize = windowSize;
        }

        /// <summary>
        /// TIMER TICK : Main Update-Loop for HW Stats and UI.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Timer_Tick(object? sender, EventArgs e)
        {
            if (this._closing)
            {
                return;
            }

            if (Interlocked.Exchange(ref this._tickInProgress, 1) == 1)
            {
                return;
            }

            try
            {
                Stopwatch tickStopwatch = Stopwatch.StartNew();
                var gpuRef = this.Gpu;

                var threadsTask = Task.Run(() => CpuStats.GetThreadUsages());
                var topTasksTask = this.GetTopTasksSnapshotAsync();
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
                        double usage = gpuRef?.CurrentLoad01 * 100 ?? 0;
                        double wattage = gpuRef?.CurrentPowerWatts ?? 0;
                        double vramTotal = Math.Round(gpuRef?.GetTotalVramBytes() / 1_073_741_824.0 ?? 0, 3);
                        double vramUsed = Math.Round(gpuRef?.GetUsedVramBytes() / 1_073_741_824.0 ?? 0, 3);
                        return (usage, wattage, vramTotal, vramUsed);
                    }
                    catch
                    {
                        return (0.0, 0.0, 0.0, 0.0);
                    }
                });
                var trafficTask = Task.Run(() => TrafficStats.Sample(this._updateIntervalMs));

                await Task.WhenAll(threadsTask, topTasksTask, ramTask, gpuTask, trafficTask);

                var threads = threadsTask.Result;
                var topTasks = topTasksTask.Result;
                var (ramTotalGb, ramUsedGb) = ramTask.Result;
                var (gpuUsage, gpuWattage, vramTotalGb, vramUsedGb) = gpuTask.Result;

                this.UpdateAverageCpuLoadAndTemperatureLabel(threads);
                this.UpdateTopTasksLabel(topTasks);
                await this.UpdateLlamaServerGenerationSpeedAsync();

                await Task.WhenAll(
                    this.UpdateCpuUsageAsync(threads),
                    this.UpdateRamUsageAsync(ramTotalGb, ramUsedGb),
                    this.UpdateGpuUsageAsync(gpuUsage, gpuWattage),
                    this.UpdateVramUsageAsync(vramTotalGb, vramUsedGb)
                );

                if (this._closing)
                {
                    return;
                }

                this.UpdateTitleWithTraffic();

                this.LogTickDurationIfSlow(tickStopwatch.Elapsed);
            }
            finally
            {
                _ = Interlocked.Exchange(ref this._tickInProgress, 0);
            }
        }

        private async Task UpdateLlamaServerGenerationSpeedAsync()
        {
            if (!this.rerouteAPILlamacppOllamaToolStripMenuItem.Checked || !this.showTokenssToolStripMenuItem.Checked)
            {
                return;
            }

            int llamaPort = int.TryParse(this.toolStripTextBox_llamacppPort.Text.Trim(), out int parsedLlamaPort) ? parsedLlamaPort : 8080;
            string ollamaPortStr = this.toolStripTextBox_ollamaPort.Text.Trim();

            // FIX: Niemals den Text aus dem Label parsen, da Asynchronität zu kaputten Strings wie "Port: ----" führen kann.
            // Immer sauber frisch aus den Config-Textboxen aufbauen!
            string baseText = $"Port {llamaPort} to {ollamaPortStr}";

            double genSpeed;
            if ((DateTime.UtcNow - this._lastStdOutTokensPerSecondUtc) <= TimeSpan.FromSeconds(2))
            {
                genSpeed = this._lastStdOutTokensPerSecond;
            }
            else
            {
                genSpeed = await LlamaServerStats.GetCurrentLlamaServerGenerationStatsAsync(llamaPort) ?? 0f;
            }

            string speedString = genSpeed >= 0.01f ? $"{genSpeed:0.000} tokens/s" : "Idle (0.000 tokens/s)";
            string nextText = $"{baseText}{Environment.NewLine}{speedString}";

            if (this.InvokeRequired)
            {
                _ = this.BeginInvoke(new Action(() =>
                {
                    if (!this.IsDisposed && !this.Disposing)
                    {
                        this.label_routingPortsInfo.Text = nextText;

                        if (genSpeed > 0f && this.toolStripMenuItem_logGenerationSpeed.Checked)
                        {
                            Logger.Log($" --- Llama server generation speed: {genSpeed:0.000} tokens/s");
                        }
                    }
                }));
                return;
            }

            this.label_routingPortsInfo.Text = nextText;
        }

        private Task<IReadOnlyList<(string processName, double cpuPercent)>> GetTopTasksSnapshotAsync()
        {
            DateTime nowUtc = DateTime.UtcNow;
            return (nowUtc - this._lastTopTasksSampleUtc) < TopTasksSamplingInterval
                ? Task.FromResult(this._cachedTopTasks)
                : Task.Run(() =>
            {
                IReadOnlyList<(string processName, double cpuPercent)> sampledTopTasks = CpuStats.GetTopCpuProcesses();
                this._cachedTopTasks = sampledTopTasks;
                this._lastTopTasksSampleUtc = DateTime.UtcNow;
                return sampledTopTasks;
            });
        }

        private void LogTickDurationIfSlow(TimeSpan tickDuration)
        {
            if (!Debugger.IsAttached)
            {
                return;
            }

            if (tickDuration.TotalMilliseconds < this._updateIntervalMs * 1.2)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            if ((nowUtc - this._lastTickDiagnosticsUtc) < TimeSpan.FromSeconds(3))
            {
                return;
            }

            this._lastTickDiagnosticsUtc = nowUtc;
            Debug.WriteLine($"[WidgetTick] Slow tick: {tickDuration.TotalMilliseconds:0} ms (target {this._updateIntervalMs} ms)");
        }

        private async Task UpdateCpuUsageAsync(float[] usages)
        {
            Color? percentageColor = this.showUsageToolStripMenuItem.Checked ? this._percentageColor : null;
            var bmp = await CpuStats.RenderCoresBitmapAsync(usages, this.pictureBox_cpu.Width, this.pictureBox_cpu.Height, this._diagramColor, percentageColor, CancellationToken.None);
            if (this._closing) { bmp?.Dispose(); return; }
            this.pictureBox_cpu.Image?.Dispose();
            this.pictureBox_cpu.Image = bmp;
        }

        private void UpdateTopTasksLabel(IReadOnlyList<(string processName, double cpuPercent)> topTasks)
        {
            if (this._closing)
            {
                return;
            }

            if (topTasks.Count == 0)
            {
                this.label_topTasksList.Text = "   -% idle\r\n   -% idle\r\n   -% idle";
                return;
            }

            List<string> lines = new(3);
            for (int index = 0; index < 3; index++)
            {
                if (index < topTasks.Count)
                {
                    (string processName, double cpuPercent) = topTasks[index];
                    string name = WidgetStatics.Ellipsize(processName, 24);
                    string percentText = $"{Math.Round(cpuPercent),3:0}%";
                    lines.Add($"{percentText} {name}");
                }
                else
                {
                    lines.Add("  0% -");
                }
            }

            this.label_topTasksList.Text = string.Join(Environment.NewLine, lines);
        }

        private Task UpdateRamUsageAsync(double totalGb, double usedGb)
        {
            if (this._closing)
            {
                return Task.CompletedTask;
            }

            double percentUsed = totalGb > 0 ? (usedGb / totalGb) * 100 : 0;

            this.label_ram.Text = $"RAM: {usedGb} GB / {totalGb} GB ({percentUsed:0.00}%)";
            this._progRam.Value = Math.Clamp((int) percentUsed, 0, this._progRam.Maximum);


            return Task.CompletedTask;
        }

        private Task UpdateGpuUsageAsync(double usagePercent, double wattage)
        {
            if (this._closing)
            {
                return Task.CompletedTask;
            }

            this.label_gpuUsage.Text = $"GPU: {usagePercent:0.00}%";
            this.label_wattage.Text = $"Watts: {wattage:0.00} W";
            this.label_gpuUsage.ForeColor = usagePercent >= 80 ? Color.Red : BlackOutModeEnabled ? Color.White : Color.Black;

            if (this.Gpu2 != null)
            {
                this.label_gpuLoad2.Text = $"GPU2: {this.Gpu2.CurrentLoad01 * 100:0.00}%";
                this.label_gpuWatts2.Text = $"Watts: {this.Gpu2.CurrentPowerWatts ?? 0:0.00} W";
                this.label_gpuLoad2.ForeColor = (this.Gpu2.CurrentLoad01 * 100) >= 80 ? Color.Red : BlackOutModeEnabled ? Color.White : Color.Black;
            }

            return Task.CompletedTask;
        }

        private Task UpdateVramUsageAsync(double totalGb, double usedGb)
        {
            if (this._closing)
            {
                return Task.CompletedTask;
            }

            double percentUsed = totalGb > 0 ? (usedGb / totalGb) * 100 : 0;
            this.label_vram.Text = $"VRAM: {usedGb} GB / {totalGb} GB ({percentUsed:0.00}%)";
            this._progVram.Value = Math.Clamp((int) percentUsed, 0, this._progVram.Maximum);

            if (this.Gpu2 != null)
            {
                long gpu2TotalBytes = this.Gpu2.GetTotalVramBytes();
                long gpu2UsedBytes = this.Gpu2.GetUsedVramBytes();
                double gpu2TotalGb = Math.Round(gpu2TotalBytes / 1_073_741_824.0, 3);
                double gpu2UsedGb = Math.Round(gpu2UsedBytes / 1_073_741_824.0, 3);
                double gpu2PercentUsed = gpu2TotalBytes > 0 ? (Math.Max(0.0, gpu2UsedBytes) / gpu2TotalBytes) * 100.0 : 0.0;

                this.label_gpuVram2.Text = $"VRAM: {gpu2UsedGb} GB / {gpu2TotalGb} GB ({gpu2PercentUsed:0.00}%)";
                this._progVram2.Value = Math.Clamp((int) gpu2PercentUsed, 0, this._progVram2.Maximum);
            }

            return Task.CompletedTask;
        }

        private void UpdateAverageCpuLoadAndTemperatureLabel(float[] usages)
        {
            if (this._closing)
            {
                return;
            }

            double averageLoadPercent = usages.Length > 0 ? usages.Average() * 100d : 0d;

            // No thermal or wattage getting here, since it always fails and causes frequent Exceptions, just set avg. load to label
            this.label_avgCpuLoadAndTemperature.Text = averageLoadPercent.ToString("0.000") + "%";
            return;


            // NON-EFFECTIVE CODE (CUT-OFF, NEVER REACHED (for a reason (!)))
            //CpuStats.CpuTelemetrySnapshot telemetry = CpuStats.GetCpuTelemetrySnapshot();

            //List<string> parts = new()
            //{
            //    $"{averageLoadPercent:0.00}%"
            //};

            //if (telemetry.AverageTemperatureCelsius.HasValue)
            //{
            //    parts.Add($"{telemetry.AverageTemperatureCelsius.Value:0.0} °C");
            //}

            //if (telemetry.PackagePowerWatts.HasValue)
            //{
            //    parts.Add($"{telemetry.PackagePowerWatts.Value:0.0} W");
            //}

            //this.label_avgCpuLoadAndTemperature.Text = string.Join(" | ", parts);
        }

        // ToolStripMenu Event Handlers
        private void toolStripComboBox_gpus_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.Gpu?.Dispose();
            this.Gpu = new GpuStats(this.toolStripComboBox_gpus.SelectedIndex);
        }

        // Get top tasks with loads %
        internal static string GetTopTaskName(IReadOnlyList<(string processName, double cpuPercent)> topTasks, int index)
        {
            return index < topTasks.Count ? topTasks[index].processName : string.Empty;
        }

        internal static string GetTopTaskPercent(IReadOnlyList<(string processName, double cpuPercent)> topTasks, int index)
        {
            return index < topTasks.Count
                ? CsvFormatting.FormatNumber(topTasks[index].cpuPercent)
                : string.Empty;
        }
    }
}
