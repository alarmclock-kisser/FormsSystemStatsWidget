using FormsSystemStatsWidget.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FormsSystemStatsWidget.Forms
{
    public partial class WindowWidget
    {
        private void button_recordUsages_Click(object sender, EventArgs e)
        {
            if (this._recordingCancellationTokenSource != null)
            {
                this._recordingCancellationTokenSource?.Cancel();
                return;
            }

            string metricsDirectoryPath = GetMetricsDirectoryPath();
            EnsureMetricsDirectory(metricsDirectoryPath);
            PruneMetricsDirectory(metricsDirectoryPath, null);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string filePath = Path.Combine(metricsDirectoryPath, $"SystemStats_{timestamp}.csv");

            this._recordingCancellationTokenSource = new CancellationTokenSource();
            this.button_recordUsages.ForeColor = Color.Red;
            this._recordingTask = this.RecordUsagesToCsvAsync(filePath, this._recordingCancellationTokenSource.Token);
        }

        private sealed class RecordingAccumulator
        {
            public double CpuLoadSum;
            public double CpuPowerSum;
            public int CpuPowerSamples;
            public double CpuEnergyWh;
            public double GpuLoadSum;
            public double GpuPowerSum;
            public int GpuPowerSamples;
            public double GpuEnergyWh;
            public int SampleCount;

            public void Accumulate(RecordingSample sample)
            {
                this.CpuLoadSum += sample.CpuLoadPercent;
                if (sample.CpuPackagePowerWatts.HasValue)
                {
                    this.CpuPowerSum += sample.CpuPackagePowerWatts.Value;
                    this.CpuPowerSamples++;
                    this.CpuEnergyWh += sample.CpuPackagePowerWatts.Value * sample.ElapsedSecondsSincePrevious / 3600d;
                }

                this.GpuLoadSum += sample.GpuAverageLoadPercent;
                if (sample.GpuAveragePowerWatts.HasValue)
                {
                    this.GpuPowerSum += sample.GpuAveragePowerWatts.Value;
                    this.GpuPowerSamples++;
                    this.GpuEnergyWh += sample.GpuAveragePowerWatts.Value * sample.ElapsedSecondsSincePrevious / 3600d;
                }
            }

            public RecordingSummary ToSummary(DateTimeOffset startedAt, DateTimeOffset endedAt)
            {
                return CreateRecordingSummary(
                    startedAt,
                    endedAt,
                    this.CpuLoadSum,
                    this.CpuPowerSum,
                    this.CpuPowerSamples,
                    this.CpuEnergyWh,
                    this.GpuLoadSum,
                    this.GpuPowerSum,
                    this.GpuPowerSamples,
                    this.GpuEnergyWh,
                    this.SampleCount);
            }
        }

        private async Task RecordUsagesToCsvAsync(string filePath, CancellationToken cancellationToken)
        {
            Exception? failure = null;
            RecordingSummary? finalSummary = null;

            bool hasSecondGpu = this.Gpu2 != null;
            DateTimeOffset recordingStartedAt = DateTimeOffset.Now;
            DateTimeOffset previousSampleTimestamp = recordingStartedAt;
            List<string> csvLines = [];
            long currentFileBytes = 0;
            var accumulator = new RecordingAccumulator();

            try
            {
                string header = BuildRecordingHeader(hasSecondGpu);
                csvLines.Add(header);
                currentFileBytes += GetRecordingLineByteCount(header);
                await File.WriteAllTextAsync(filePath, header + Environment.NewLine, RecordingCsvEncoding, cancellationToken).ConfigureAwait(false);

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    RecordingSample sample = await this.BuildRecordingSampleAsync(hasSecondGpu, previousSampleTimestamp, cancellationToken).ConfigureAwait(false);
                    csvLines.Add(sample.CsvLine);
                    currentFileBytes += GetRecordingLineByteCount(sample.CsvLine);
                    accumulator.Accumulate(sample);

                    await File.AppendAllTextAsync(filePath, sample.CsvLine + Environment.NewLine, RecordingCsvEncoding, cancellationToken).ConfigureAwait(false);

                    currentFileBytes = await this.MaintainRecordingFileAsync(filePath, csvLines, accumulator, currentFileBytes, cancellationToken).ConfigureAwait(false);

                    previousSampleTimestamp = sample.Timestamp;

                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                finalSummary = accumulator.ToSummary(recordingStartedAt, DateTimeOffset.Now);

                try
                {
                    await AppendRecordingSummaryAsync(filePath, csvLines, finalSummary.Value, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                this.CompleteRecording(filePath, failure, finalSummary);
            }
        }

        private async Task<long> MaintainRecordingFileAsync(
            string filePath,
            List<string> csvLines,
            RecordingAccumulator accumulator,
            long currentFileBytes,
            CancellationToken cancellationToken)
        {
            accumulator.SampleCount++;
            if (accumulator.SampleCount % MetricsQuotaCheckSampleCount == 0)
            {
                PruneMetricsDirectory(Path.GetDirectoryName(filePath) ?? GetMetricsDirectoryPath(), filePath);
            }

            if (currentFileBytes <= MetricsCurrentFileSoftQuotaBytes)
            {
                return currentFileBytes;
            }

            long trimmedFileBytes = currentFileBytes;
            TrimRecordingLines(csvLines, ref trimmedFileBytes);
            await RewriteRecordingFileAsync(filePath, csvLines, cancellationToken).ConfigureAwait(false);
            return trimmedFileBytes;
        }

        private static string GetMetricsDirectoryPath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "FSSWidget");
        }

        private static void EnsureMetricsDirectory(string directoryPath)
        {
            _ = Directory.CreateDirectory(directoryPath);
        }

        private static long GetRecordingLineByteCount(string line)
        {
            return RecordingCsvEncoding.GetByteCount(line) + RecordingCsvEncoding.GetByteCount(Environment.NewLine);
        }

        private static void TrimRecordingLines(List<string> csvLines, ref long currentFileBytes)
        {
            while (csvLines.Count > 1 && currentFileBytes > MetricsCurrentFileSoftQuotaBytes)
            {
                string removed = csvLines[1];
                currentFileBytes -= GetRecordingLineByteCount(removed);
                csvLines.RemoveAt(1);
            }
        }

        private static async Task RewriteRecordingFileAsync(string filePath, List<string> csvLines, CancellationToken cancellationToken)
        {
            string tempFilePath = filePath + ".tmp";
            await File.WriteAllLinesAsync(tempFilePath, csvLines, RecordingCsvEncoding, cancellationToken);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            File.Move(tempFilePath, filePath);
        }

        private static void PruneMetricsDirectory(string directoryPath, string? preserveFilePath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    return;
                }

                List<string> files = Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => !string.Equals(path, preserveFilePath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                long totalBytes = files.Sum(path => new FileInfo(path).Length);
                if (totalBytes <= MetricsDirectoryQuotaBytes)
                {
                    return;
                }

                foreach (string filePath in files
                    .OrderBy(path => File.GetLastWriteTimeUtc(path))
                    .ThenBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    if (totalBytes <= MetricsDirectoryQuotaBytes)
                    {
                        break;
                    }

                    try
                    {
                        long fileSize = new FileInfo(filePath).Length;
                        File.Delete(filePath);
                        totalBytes -= fileSize;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private static RecordingSummary CreateRecordingSummary(
            DateTimeOffset startedAt,
            DateTimeOffset endedAt,
            double cpuLoadSum,
            double cpuPowerSum,
            int cpuPowerSamples,
            double cpuEnergyWh,
            double gpuLoadSum,
            double gpuPowerSum,
            int gpuPowerSamples,
            double gpuEnergyWh,
            int sampleCount)
        {
            TimeSpan duration = endedAt - startedAt;
            double safeSampleCount = Math.Max(1, sampleCount);
            double cpuAverageLoadPercent = cpuLoadSum / safeSampleCount;
            double? cpuAveragePowerWatts = cpuPowerSamples > 0 ? cpuPowerSum / cpuPowerSamples : null;
            double gpuAverageLoadPercent = gpuLoadSum / safeSampleCount;
            double? gpuAveragePowerWatts = gpuPowerSamples > 0 ? gpuPowerSum / gpuPowerSamples : null;

            double? totalAveragePowerWatts = cpuAveragePowerWatts.HasValue || gpuAveragePowerWatts.HasValue
                ? (cpuAveragePowerWatts ?? 0d) + (gpuAveragePowerWatts ?? 0d)
                : null;

            double totalEnergyWh = cpuEnergyWh + gpuEnergyWh;
            return new RecordingSummary(duration, cpuAverageLoadPercent, cpuAveragePowerWatts, cpuEnergyWh, gpuAverageLoadPercent, gpuAveragePowerWatts, gpuEnergyWh, totalAveragePowerWatts, totalEnergyWh);
        }

        private static async Task AppendRecordingSummaryAsync(string filePath, List<string> csvLines, RecordingSummary summary, CancellationToken cancellationToken)
        {
            List<string> summaryLines = BuildRecordingSummaryLines(summary);
            csvLines.AddRange(summaryLines);
            await File.AppendAllLinesAsync(filePath, summaryLines, RecordingCsvEncoding, cancellationToken);
        }

        private static List<string> BuildRecordingSummaryLines(RecordingSummary summary)
        {
            List<string> lines =
            [
                "==========",
                "Metric;Value",
                $"Time;{summary.Duration:hh\\:mm\\:ss}",
                $"CPU Watts (avg);{CsvFormatting.FormatNullableNumber(summary.CpuAveragePowerWatts)}",
                $"CPU Watts (Wh);{summary.CpuEnergyWh:0.000}",
                $"GPU(s) Watts (avg);{CsvFormatting.FormatNullableNumber(summary.GpuAveragePowerWatts)}",
                $"GPU(s) Watts (Wh);{summary.GpuEnergyWh:0.000}",
                $"~W/h (CPU+GPU avg);{CsvFormatting.FormatNullableNumber(summary.TotalAveragePowerWatts)}",
                $"~Load (CPU);{summary.CpuAverageLoadPercent:0.00}%",
                $"~Load (GPU(s));{summary.GpuAverageLoadPercent:0.00}%",
                $"Total Energy (Wh);{summary.TotalEnergyWh:0.000}"
            ];

            return lines;
        }



        private static string BuildRecordingHeader(bool hasSecondGpu)
        {
            List<string> columns =
            [
                "Timestamp",
                "CPU Usage (%)",
                "CPU Temperature (°C)",
                "CPU Package Power (W)",
                "GPU 1 Usage (%)",
                "GPU 1 Power (W)",
                "GPU 1 VRAM Used (GB)",
                "GPU 1 VRAM Total (GB)",
                "GPU Total Power (W)",
                "CPU+GPU Total Power (W)",
                "RAM Used (GB)",
                "RAM Total (GB)",
                "RAM Usage (%)",
                "Network Up (B/s)",
                "Network Down (B/s)",
                "Top CPU Task 1",
                "Top CPU Task 1 (%)",
                "Top CPU Task 2",
                "Top CPU Task 2 (%)",
                "Top CPU Task 3",
                "Top CPU Task 3 (%)"
            ];

            if (hasSecondGpu)
            {
                columns.InsertRange(8, new[]
                {
                    "GPU 2 Usage (%)",
                    "GPU 2 Power (W)",
                    "GPU 2 VRAM Used (GB)",
                    "GPU 2 VRAM Total (GB)"
                });
            }

            return string.Join(";", columns.Select(CsvFormatting.EscapeValue));
        }

        private readonly record struct GpuSampleData(
            double UsagePercent,
            double PowerWatts,
            double VramUsedGb,
            double VramTotalGb);

        private async Task<RecordingSample> BuildRecordingSampleAsync(bool hasSecondGpu, DateTimeOffset previousSampleTimestamp, CancellationToken cancellationToken)
        {
            DateTimeOffset timestamp = DateTimeOffset.Now;

            Task<float[]> cpuTask = Task.Run(() => CpuStats.GetThreadUsages(), cancellationToken);
            Task<CpuStats.CpuTelemetrySnapshot> cpuTelemetryTask = Task.Run(() => CpuStats.GetCpuTelemetrySnapshot(), cancellationToken);
            Task<(double ramTotalGb, double ramUsedGb)> ramTask = Task.Run(() =>
            {
                double ramTotalGb = Math.Round(CpuStats.GetTotalMemoryBytes() / 1_073_741_824.0, 3);
                double ramUsedGb = Math.Round(CpuStats.GetUsedMemoryBytes() / 1_073_741_824.0, 3);
                return (ramTotalGb, ramUsedGb);
            }, cancellationToken);
            Task<IReadOnlyList<(string processName, double cpuPercent)>> topTasksTask = Task.Run(() => CpuStats.GetTopCpuProcesses(), cancellationToken);

            await Task.WhenAll(cpuTask, cpuTelemetryTask, ramTask, topTasksTask).ConfigureAwait(false);

            float[] cpuThreadUsages = cpuTask.Result;
            double cpuUsagePercent = cpuThreadUsages.Length > 0 ? cpuThreadUsages.Average() * 100d : 0d;
            CpuStats.CpuTelemetrySnapshot cpuTelemetry = cpuTelemetryTask.Result;

            (double ramTotalGb, double ramUsedGb) = ramTask.Result;
            double ramUsagePercent = ramTotalGb > 0d ? (ramUsedGb / ramTotalGb) * 100d : 0d;

            GpuSampleData gpu1 = SampleGpuData(this.Gpu);
            GpuSampleData gpu2 = hasSecondGpu ? SampleGpuData(this.Gpu2) : default;

            double gpuTotalPowerWatts = gpu1.PowerWatts + gpu2.PowerWatts;
            double totalTrackedPowerWatts = (cpuTelemetry.PackagePowerWatts ?? 0d) + gpuTotalPowerWatts;
            double gpuAverageLoadPercent = hasSecondGpu ? (gpu1.UsagePercent + gpu2.UsagePercent) / 2d : gpu1.UsagePercent;

            IReadOnlyList<(string processName, double cpuPercent)> topTasks = topTasksTask.Result;
            List<string> values = BuildSampleCsvValues(timestamp, cpuUsagePercent, cpuTelemetry, gpu1, gpu2, gpuTotalPowerWatts, totalTrackedPowerWatts, ramUsedGb, ramTotalGb, ramUsagePercent, topTasks, hasSecondGpu);

            double elapsedSeconds = Math.Max(0.05d, (timestamp - previousSampleTimestamp).TotalSeconds);

            return new RecordingSample(
                string.Join(";", values.Select(CsvFormatting.EscapeValue)),
                cpuUsagePercent,
                cpuTelemetry.PackagePowerWatts,
                gpuAverageLoadPercent,
                gpuTotalPowerWatts > 0d ? gpuTotalPowerWatts : null,
                timestamp,
                elapsedSeconds);
        }

        private static GpuSampleData SampleGpuData(GpuStats? gpu)
        {
            if (gpu == null)
            {
                return default;
            }

            try
            {
                return new GpuSampleData(
                    gpu.CurrentLoad01 * 100d,
                    gpu.CurrentPowerWatts ?? 0d,
                    Math.Round(gpu.GetUsedVramBytes() / 1_073_741_824.0, 3),
                    Math.Round(gpu.GetTotalVramBytes() / 1_073_741_824.0, 3));
            }
            catch
            {
                return default;
            }
        }

        private static List<string> BuildSampleCsvValues(
            DateTimeOffset timestamp,
            double cpuUsagePercent,
            CpuStats.CpuTelemetrySnapshot cpuTelemetry,
            GpuSampleData gpu1,
            GpuSampleData gpu2,
            double gpuTotalPowerWatts,
            double totalTrackedPowerWatts,
            double ramUsedGb,
            double ramTotalGb,
            double ramUsagePercent,
            IReadOnlyList<(string processName, double cpuPercent)> topTasks,
            bool hasSecondGpu)
        {
            List<string> values =
            [
                timestamp.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
                CsvFormatting.FormatNumber(cpuUsagePercent),
                CsvFormatting.FormatNullableNumber(cpuTelemetry.AverageTemperatureCelsius),
                CsvFormatting.FormatNullableNumber(cpuTelemetry.PackagePowerWatts),
                CsvFormatting.FormatNumber(gpu1.UsagePercent),
                CsvFormatting.FormatNumber(gpu1.PowerWatts),
                CsvFormatting.FormatNumber(gpu1.VramUsedGb, "0.000"),
                CsvFormatting.FormatNumber(gpu1.VramTotalGb, "0.000"),
                CsvFormatting.FormatNumber(gpuTotalPowerWatts),
                CsvFormatting.FormatNumber(totalTrackedPowerWatts),
                CsvFormatting.FormatNumber(ramUsedGb, "0.000"),
                CsvFormatting.FormatNumber(ramTotalGb, "0.000"),
                CsvFormatting.FormatNumber(ramUsagePercent),
                TrafficStats.UpBytesPerSecond.ToString("0", CultureInfo.InvariantCulture),
                TrafficStats.DownBytesPerSecond.ToString("0", CultureInfo.InvariantCulture),
                GetTopTaskName(topTasks, 0),
                GetTopTaskPercent(topTasks, 0),
                GetTopTaskName(topTasks, 1),
                GetTopTaskPercent(topTasks, 1),
                GetTopTaskName(topTasks, 2),
                GetTopTaskPercent(topTasks, 2)
            ];

            if (hasSecondGpu)
            {
                values.InsertRange(8, new[]
                {
                    CsvFormatting.FormatNumber(gpu2.UsagePercent),
                    CsvFormatting.FormatNumber(gpu2.PowerWatts),
                    CsvFormatting.FormatNumber(gpu2.VramUsedGb, "0.000"),
                    CsvFormatting.FormatNumber(gpu2.VramTotalGb, "0.000")
                });
            }

            return values;
        }

        private void CompleteRecording(string filePath, Exception? failure, RecordingSummary? finalSummary)
        {
            if (this.IsDisposed || this.Disposing)
            {
                this._recordingCancellationTokenSource?.Dispose();
                this._recordingCancellationTokenSource = null;
                this._recordingTask = null;
                return;
            }

            if (this.InvokeRequired)
            {
                _ = this.BeginInvoke(new Action(() => this.CompleteRecording(filePath, failure, finalSummary)));
                return;
            }

            this._recordingCancellationTokenSource?.Dispose();
            this._recordingCancellationTokenSource = null;
            this._recordingTask = null;
            this.button_recordUsages.ForeColor = BlackOutModeEnabled ? Color.White : SystemColors.ControlText;

            if (failure != null)
            {
                _ = MessageBox.Show(this, $"Beim Aufzeichnen der Systemdaten ist ein Fehler aufgetreten.\n\n{failure.Message}", "Aufzeichnung fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string summaryText = this.BuildRecordingCompletionText(filePath, finalSummary);

            if (!this._closing)
            {
                _ = MessageBox.Show(this, summaryText, "Aufzeichnung beendet", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private string BuildRecordingCompletionText(string filePath, RecordingSummary? finalSummary)
        {
            if (!finalSummary.HasValue)
            {
                return $"Die Aufzeichnung wurde beendet.\n\nCSV-Datei:\n{filePath}";
            }

            RecordingSummary summary = finalSummary.Value;
            string cpuWatts = CsvFormatting.FormatNullableNumber(summary.CpuAveragePowerWatts);
            string gpuWatts = CsvFormatting.FormatNullableNumber(summary.GpuAveragePowerWatts);
            string wattsPerHour = CsvFormatting.FormatNullableNumber(summary.TotalAveragePowerWatts);

            return $"Die Aufzeichnung wurde beendet.\n\nCSV-Datei:\n{filePath}\n\nZusammenfassung:\n• Time: {summary.Duration:hh\\:mm\\:ss}\n• CPU Watts: {cpuWatts}\n• GPU(s) Watts: {gpuWatts}\n• ~W/h: {wattsPerHour}\n• ~Load (CPU): {summary.CpuAverageLoadPercent:0.00}%\n• ~Load (GPU(s)): {summary.GpuAverageLoadPercent:0.00}%";
        }
    }
}