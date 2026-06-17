using FormsSystemStatsWidget.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FormsSystemStatsWidget.Forms
{
    public partial class WindowWidget
    {
        private int _driveTestFileSizeMb = 4096;
        private int _driveTestBlockSizeKb = 1024;
        private int _driveTestPasses = 2;
        private int _driveTestWorkerThreads = Math.Clamp(Environment.ProcessorCount / 4, 2, Environment.ProcessorCount);
        private bool _driveTestInProgress = false;

        public static bool BlackOutModeEnabled { get; private set; } = false;

        private void PopulateDriveSelections()
        {
            string? previousSelection = (this.toolStripComboBox_drives.SelectedItem as DriveSelection)?.RootPath;

            var driveSelections = DriveInfo
                .GetDrives()
                .Where(d => d.IsReady && d.TotalSize > 0)
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Select(d =>
                {
                    long totalGb = d.TotalSize / (1024L * 1024L * 1024L);
                    return new DriveSelection
                    {
                        RootPath = d.RootDirectory.FullName,
                        DisplayName = $"{d.Name} ({d.DriveType}, {totalGb} GB, {d.DriveFormat})"
                    };
                })
                .Cast<object>()
                .ToArray();

            this.toolStripComboBox_drives.Items.Clear();
            this.toolStripComboBox_drives.Items.AddRange(driveSelections);
            this.UpdateDriveSpeedTestDropDownWidths();

            if (!string.IsNullOrWhiteSpace(previousSelection))
            {
                for (int i = 0; i < this.toolStripComboBox_drives.Items.Count; i++)
                {
                    if (this.toolStripComboBox_drives.Items[i] is DriveSelection selection && string.Equals(selection.RootPath, previousSelection, StringComparison.OrdinalIgnoreCase))
                    {
                        this.toolStripComboBox_drives.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (this.toolStripComboBox_drives.SelectedIndex < 0)
            {
                this.toolStripComboBox_drives.SelectedIndex = this.toolStripComboBox_drives.Items.Count > 0 ? 0 : -1;
            }
        }


        private void ApplyDriveSpeedTestSettingTexts()
        {
            this.toolStripTextBox_testFileSizeMb.Text = this._driveTestFileSizeMb.ToString();
            this.toolStripTextBox_testBlockSizeKb.Text = this._driveTestBlockSizeKb.ToString();
            this.toolStripTextBox_testPasses.Text = this._driveTestPasses.ToString();
            this.toolStripTextBox_testThreads.Text = this._driveTestWorkerThreads.ToString();
        }

        private void UpdateDriveSpeedTestDropDownWidths()
        {
            int maxItemWidth = 0;
            foreach (object? item in this.toolStripComboBox_drives.Items)
            {
                string text = item?.ToString() ?? string.Empty;
                int textWidth = TextRenderer.MeasureText(text, this.toolStripComboBox_drives.Font).Width;
                if (textWidth > maxItemWidth)
                {
                    maxItemWidth = textWidth;
                }
            }

            int comboDropDownWidth = Math.Clamp(maxItemWidth + SystemInformation.VerticalScrollBarWidth + 24, 220, 560);
            this.toolStripComboBox_drives.DropDownWidth = comboDropDownWidth;

            if (this.driveSpeedTestToolStripMenuItem.DropDown is ToolStripDropDownMenu dropDownMenu)
            {
                dropDownMenu.AutoSize = false;
                dropDownMenu.Width = Math.Max(220, comboDropDownWidth + 32);
            }
        }

        private void ApplyDriveTestFileSizeFromText()
        {
            if (int.TryParse(this.toolStripTextBox_testFileSizeMb.Text, out int value))
            {
                this._driveTestFileSizeMb = Math.Clamp(value, 512, 65_536);
            }

            this.toolStripTextBox_testFileSizeMb.Text = this._driveTestFileSizeMb.ToString();
        }

        private void ApplyDriveTestBlockSizeFromText()
        {
            if (int.TryParse(this.toolStripTextBox_testBlockSizeKb.Text, out int value))
            {
                this._driveTestBlockSizeKb = Math.Clamp(value, 4, 4_096);
            }

            this.toolStripTextBox_testBlockSizeKb.Text = this._driveTestBlockSizeKb.ToString();
        }

        private void ApplyDriveTestPassesFromText()
        {
            if (int.TryParse(this.toolStripTextBox_testPasses.Text, out int value))
            {
                this._driveTestPasses = Math.Clamp(value, 1, 10);
            }

            this.toolStripTextBox_testPasses.Text = this._driveTestPasses.ToString();
        }

        private void ApplyDriveTestWorkerThreadsFromText()
        {
            if (int.TryParse(this.toolStripTextBox_testThreads.Text, out int value))
            {
                this._driveTestWorkerThreads = Math.Clamp(value, 1, Environment.ProcessorCount);
            }

            this.toolStripTextBox_testThreads.Text = this._driveTestWorkerThreads.ToString();
        }



        private async Task<(double writeMBps, double readMBps)> RunDriveSpeedPassAsync(string filePath, long fileSizeBytes, int blockSizeBytes, FileOptions options, CancellationToken cancellationToken, Action<long>? progressBytesCallback = null)
        {
            byte[] buffer = new byte[blockSizeBytes];
            Random.Shared.NextBytes(buffer);

            var writeStopwatch = Stopwatch.StartNew();
            await using (var writeStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, blockSizeBytes, options))
            {
                long remainingBytes = fileSizeBytes;
                while (remainingBytes > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int chunk = (int) Math.Min(buffer.Length, remainingBytes);
                    await writeStream.WriteAsync(buffer.AsMemory(0, chunk), cancellationToken);
                    remainingBytes -= chunk;
                    progressBytesCallback?.Invoke(chunk);
                }

                await writeStream.FlushAsync(cancellationToken);
            }
            writeStopwatch.Stop();

            byte[] readBuffer = new byte[blockSizeBytes];
            var readStopwatch = Stopwatch.StartNew();
            await using (var readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, blockSizeBytes, FileOptions.SequentialScan))
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int bytesRead = await readStream.ReadAsync(readBuffer.AsMemory(0, readBuffer.Length), cancellationToken);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    progressBytesCallback?.Invoke(bytesRead);
                }
            }
            readStopwatch.Stop();

            double bytesPerMiB = 1024d * 1024d;
            double writeMBps = fileSizeBytes / bytesPerMiB / Math.Max(writeStopwatch.Elapsed.TotalSeconds, 0.0001d);
            double readMBps = fileSizeBytes / bytesPerMiB / Math.Max(readStopwatch.Elapsed.TotalSeconds, 0.0001d);
            return (writeMBps, readMBps);
        }

        private async Task<(double writeMBps, double readMBps)> RunDriveSmallRandomPassAsync(string filePath, long fileSizeBytes, int blockSizeBytes, FileOptions options, CancellationToken cancellationToken, Action<long>? progressBytesCallback = null)
        {
            byte[] writeBuffer = new byte[blockSizeBytes];
            byte[] readBuffer = new byte[blockSizeBytes];
            Random.Shared.NextBytes(writeBuffer);

            long operationCount = Math.Max(1, fileSizeBytes / blockSizeBytes);
            long maxPosition = Math.Max(0, fileSizeBytes - blockSizeBytes);

            var writeStopwatch = Stopwatch.StartNew();
            await using (FileStream writeStream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None, blockSizeBytes, options))
            {
                writeStream.SetLength(fileSizeBytes);
                for (long index = 0; index < operationCount; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    long randomPosition = maxPosition == 0 ? 0 : Random.Shared.NextInt64(0, maxPosition + 1);
                    long alignedPosition = (randomPosition / blockSizeBytes) * blockSizeBytes;
                    writeStream.Position = alignedPosition;
                    await writeStream.WriteAsync(writeBuffer.AsMemory(0, blockSizeBytes), cancellationToken);
                    progressBytesCallback?.Invoke(blockSizeBytes);
                }

                await writeStream.FlushAsync(cancellationToken);
            }
            writeStopwatch.Stop();

            var readStopwatch = Stopwatch.StartNew();
            await using (FileStream readStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, blockSizeBytes, FileOptions.RandomAccess))
            {
                for (long index = 0; index < operationCount; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    long randomPosition = maxPosition == 0 ? 0 : Random.Shared.NextInt64(0, maxPosition + 1);
                    long alignedPosition = (randomPosition / blockSizeBytes) * blockSizeBytes;
                    readStream.Position = alignedPosition;
                    int bytesRead = await readStream.ReadAsync(readBuffer.AsMemory(0, blockSizeBytes), cancellationToken);
                    progressBytesCallback?.Invoke(bytesRead);
                }
            }
            readStopwatch.Stop();

            long transferredBytes = operationCount * blockSizeBytes;
            double bytesPerMiB = 1024d * 1024d;
            double writeMBps = transferredBytes / bytesPerMiB / Math.Max(writeStopwatch.Elapsed.TotalSeconds, 0.0001d);
            double readMBps = transferredBytes / bytesPerMiB / Math.Max(readStopwatch.Elapsed.TotalSeconds, 0.0001d);
            return (writeMBps, readMBps);
        }

        private async Task<(double writeMBps, double readMBps)> RunDriveParallelPassAsync(string tempDirectoryPath, long totalBytes, int blockSizeBytes, int workerCount, FileOptions options, CancellationToken cancellationToken, Action<long>? progressBytesCallback = null)
        {
            int safeWorkerCount = Math.Max(1, workerCount);
            long bytesPerWorker = Math.Max(64L * 1024L * 1024L, totalBytes / safeWorkerCount);
            long alignedBytesPerWorker = Math.Max(blockSizeBytes, (bytesPerWorker / blockSizeBytes) * blockSizeBytes);
            List<string> filePaths = Enumerable.Range(0, safeWorkerCount)
                .Select(index => Path.Combine(tempDirectoryPath, $".fssw-drive-par-{index}-{Guid.NewGuid():N}.bin"))
                .ToList();

            byte[] workerBuffer = new byte[blockSizeBytes];
            Random.Shared.NextBytes(workerBuffer);

            try
            {
                var writeStopwatch = Stopwatch.StartNew();
                await Task.WhenAll(filePaths.Select(path => DriveBenchmarkIo.WriteWorkerFileAsync(path, alignedBytesPerWorker, workerBuffer, blockSizeBytes, options, cancellationToken, progressBytesCallback)));
                writeStopwatch.Stop();

                byte[] readBuffer = new byte[blockSizeBytes];
                var readStopwatch = Stopwatch.StartNew();
                await Task.WhenAll(filePaths.Select(path => DriveBenchmarkIo.ReadWorkerFileAsync(path, readBuffer, blockSizeBytes, cancellationToken, progressBytesCallback)));
                readStopwatch.Stop();

                long transferredBytes = alignedBytesPerWorker * safeWorkerCount;
                double bytesPerMiB = 1024d * 1024d;
                double writeMBps = transferredBytes / bytesPerMiB / Math.Max(writeStopwatch.Elapsed.TotalSeconds, 0.0001d);
                double readMBps = transferredBytes / bytesPerMiB / Math.Max(readStopwatch.Elapsed.TotalSeconds, 0.0001d);
                return (writeMBps, readMBps);
            }
            finally
            {
                foreach (string path in filePaths)
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        private async Task<string> RunDriveSpeedTestAndBuildReportAsync(string rootPath, CancellationToken cancellationToken)
        {
            long fileSizeBytes = checked((long) this._driveTestFileSizeMb * 1024L * 1024L);
            int blockSizeBytes = checked(this._driveTestBlockSizeKb * 1024);
            int passes = this._driveTestPasses;

            string tempDirectoryPath = await DriveBenchmarkIo.ResolveWritableTempDirectoryAsync(rootPath, cancellationToken);
            string sequentialTempFilePath = Path.Combine(tempDirectoryPath, $".fssw-drive-seq-{Guid.NewGuid():N}.bin");
            string randomTempFilePath = Path.Combine(tempDirectoryPath, $".fssw-drive-rnd-{Guid.NewGuid():N}.bin");

            FileOptions writeOptions = FileOptions.SequentialScan;
            if (this.writeThroughToolStripMenuItem.Checked)
            {
                writeOptions |= FileOptions.WriteThrough;
            }

            var context = new DriveBenchmarkContext(
                rootPath, tempDirectoryPath, sequentialTempFilePath, randomTempFilePath,
                fileSizeBytes, blockSizeBytes, passes, writeOptions);
            var passParameters = this.CalculatePassParameters(fileSizeBytes, blockSizeBytes);
            var results = new DriveSpeedResults(passes);

            Stopwatch totalStopwatch = Stopwatch.StartNew();
            try
            {
                await this.ExecuteDriveBenchmarkPassesAsync(context, passParameters, totalStopwatch, results, cancellationToken);
            }
            finally
            {
                totalStopwatch.Stop();
                TryDeleteTempFile(sequentialTempFilePath);
                TryDeleteTempFile(randomTempFilePath);
            }

            return this.BuildDriveSpeedReport(context, totalStopwatch, results, passParameters);
        }

        private sealed record DriveBenchmarkContext(
            string RootPath,
            string TempDirectoryPath,
            string SequentialTempFilePath,
            string RandomTempFilePath,
            long FileSizeBytes,
            int BlockSizeBytes,
            int Passes,
            FileOptions WriteOptions);

        private sealed record DrivePassParameters(
            long RandomFileSizeBytes,
            int RandomBlockSizeBytes,
            int ParallelWorkerCount,
            long ParallelTransferredBytes);

        private sealed class DriveSpeedResults(int passes)
        {
            public List<double> SequentialWrite { get; } = new(passes);
            public List<double> SequentialRead { get; } = new(passes);
            public List<double> RandomWrite { get; } = new(passes);
            public List<double> RandomRead { get; } = new(passes);
            public List<double> ParallelWrite { get; } = new(passes);
            public List<double> ParallelRead { get; } = new(passes);
            public List<string> PassLines { get; } = new(passes);
        }

        private DrivePassParameters CalculatePassParameters(long fileSizeBytes, int blockSizeBytes)
        {
            long randomFileSizeBytes = Math.Max(512L * 1024L * 1024L, Math.Min(fileSizeBytes, 2L * 1024L * 1024L * 1024L));
            int randomBlockSizeBytes = Math.Max(4 * 1024, Math.Min(64 * 1024, blockSizeBytes / 16));
            int parallelWorkerCount = Math.Clamp(this._driveTestWorkerThreads, 1, Environment.ProcessorCount);
            long parallelTransferredBytes = WidgetStatics.CalculateParallelTransferredBytes(fileSizeBytes, randomBlockSizeBytes, parallelWorkerCount);
            return new DrivePassParameters(randomFileSizeBytes, randomBlockSizeBytes, parallelWorkerCount, parallelTransferredBytes);
        }

        private async Task ExecuteDriveBenchmarkPassesAsync(
            DriveBenchmarkContext ctx,
            DrivePassParameters passParams,
            Stopwatch totalStopwatch,
            DriveSpeedResults results,
            CancellationToken cancellationToken)
        {
            long bytesPerPass = (2L * ctx.FileSizeBytes) + (2L * passParams.RandomFileSizeBytes) + (2L * passParams.ParallelTransferredBytes);
            long totalExpectedBytes = Math.Max(1L, checked(bytesPerPass * ctx.Passes));
            long completedBytes = 0;

            void ReportProgress(long processedBytes)
            {
                long totalProcessed = Interlocked.Add(ref completedBytes, Math.Max(0, processedBytes));
                double progressPercent = Math.Min(100.0, (totalProcessed / (double) totalExpectedBytes) * 100.0);
                TimeSpan elapsed = totalStopwatch.Elapsed;
                string title = WidgetStatics.BuildDriveTestWindowTitle(ctx.RootPath, elapsed, progressPercent);
                this.TrySetWindowTitleSafe(title);
            }

            for (int pass = 1; pass <= ctx.Passes; pass++)
            {
                await this.RunSingleBenchmarkPassAsync(ctx, passParams, pass, results, ReportProgress, cancellationToken);
            }
        }

        private async Task RunSingleBenchmarkPassAsync(
            DriveBenchmarkContext ctx,
            DrivePassParameters passParams,
            int pass,
            DriveSpeedResults results,
            Action<long> reportProgress,
            CancellationToken cancellationToken)
        {
            Stopwatch sequentialDurationStopwatch = Stopwatch.StartNew();
            var (sequentialWriteMBps, sequentialReadMBps) = await this.RunDriveSpeedPassAsync(ctx.SequentialTempFilePath, ctx.FileSizeBytes, ctx.BlockSizeBytes, ctx.WriteOptions, cancellationToken, reportProgress);
            sequentialDurationStopwatch.Stop();
            results.SequentialWrite.Add(sequentialWriteMBps);
            results.SequentialRead.Add(sequentialReadMBps);

            Stopwatch randomDurationStopwatch = Stopwatch.StartNew();
            var (randomWriteMBps, randomReadMBps) = await this.RunDriveSmallRandomPassAsync(ctx.RandomTempFilePath, passParams.RandomFileSizeBytes, passParams.RandomBlockSizeBytes, ctx.WriteOptions, cancellationToken, reportProgress);
            randomDurationStopwatch.Stop();
            results.RandomWrite.Add(randomWriteMBps);
            results.RandomRead.Add(randomReadMBps);

            Stopwatch parallelDurationStopwatch = Stopwatch.StartNew();
            var (parallelWriteMBps, parallelReadMBps) = await this.RunDriveParallelPassAsync(ctx.TempDirectoryPath, ctx.FileSizeBytes, passParams.RandomBlockSizeBytes, passParams.ParallelWorkerCount, ctx.WriteOptions, cancellationToken, reportProgress);
            parallelDurationStopwatch.Stop();
            results.ParallelWrite.Add(parallelWriteMBps);
            results.ParallelRead.Add(parallelReadMBps);

            results.PassLines.Add($"Pass {pass} - Seq ({WidgetStatics.FormatDuration(sequentialDurationStopwatch.Elapsed)}): Write {sequentialWriteMBps:0.00} | Read {sequentialReadMBps:0.00} MiB/s");
            results.PassLines.Add($"Pass {pass} - Rnd {passParams.RandomBlockSizeBytes / 1024} KiB ({WidgetStatics.FormatDuration(randomDurationStopwatch.Elapsed)}): Write {randomWriteMBps:0.00} | Read {randomReadMBps:0.00} MiB/s");
            results.PassLines.Add($"Pass {pass} - Parallel x{passParams.ParallelWorkerCount} ({WidgetStatics.FormatDuration(parallelDurationStopwatch.Elapsed)}): Write {parallelWriteMBps:0.00} | Read {parallelReadMBps:0.00} MiB/s");
        }

        private static void TryDeleteTempFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
            }
        }

        private static (double average, double min, double max) CalculateMetrics(List<double> values)
        {
            if (values.Count == 0)
            {
                return (0, 0, 0);
            }

            return (values.Average(), values.Min(), values.Max());
        }

        private string BuildDriveSpeedReport(
            DriveBenchmarkContext ctx,
            Stopwatch totalStopwatch,
            DriveSpeedResults results,
            DrivePassParameters passParams)
        {
            StringBuilder reportBuilder = new();
            _ = reportBuilder.AppendLine("Drive Speed Test Report");
            _ = reportBuilder.AppendLine(new string('-', 32));
            _ = reportBuilder.AppendLine($"Drive: {ctx.RootPath}");
            _ = reportBuilder.AppendLine($"Temp Folder: {ctx.TempDirectoryPath}");
            _ = reportBuilder.AppendLine($"Target File Size: {this._driveTestFileSizeMb} MiB");
            _ = reportBuilder.AppendLine($"Block Size: {this._driveTestBlockSizeKb} KiB");
            _ = reportBuilder.AppendLine($"Passes: {ctx.Passes}");
            _ = reportBuilder.AppendLine($"Threads: {this._driveTestWorkerThreads}");
            _ = reportBuilder.AppendLine($"Write Through: {(this.writeThroughToolStripMenuItem.Checked ? "On" : "Off")}");
            _ = reportBuilder.AppendLine($"Total Runtime: {totalStopwatch.Elapsed.TotalSeconds:0.00} s");
            _ = reportBuilder.AppendLine();
            _ = reportBuilder.AppendLine("Drive Hardware/Software Info:");
            _ = reportBuilder.AppendLine(new string('-', 32));
            _ = reportBuilder.AppendLine(WidgetStatics.BuildDriveEnvironmentInfoBlock(ctx.RootPath));
            _ = reportBuilder.AppendLine();
            foreach (string passLine in results.PassLines)
            {
                _ = reportBuilder.AppendLine(passLine);
            }
            _ = reportBuilder.AppendLine();
            AppendMetricsSummary(reportBuilder, results);
            return reportBuilder.ToString();
        }

        private static void AppendMetricsSummary(StringBuilder reportBuilder, DriveSpeedResults results)
        {
            var (seqWriteAvg, seqWriteMin, seqWriteMax) = CalculateMetrics(results.SequentialWrite);
            var (seqReadAvg, seqReadMin, seqReadMax) = CalculateMetrics(results.SequentialRead);
            var (rndWriteAvg, rndWriteMin, rndWriteMax) = CalculateMetrics(results.RandomWrite);
            var (rndReadAvg, rndReadMin, rndReadMax) = CalculateMetrics(results.RandomRead);
            var (parWriteAvg, parWriteMin, parWriteMax) = CalculateMetrics(results.ParallelWrite);
            var (parReadAvg, parReadMin, parReadMax) = CalculateMetrics(results.ParallelRead);

            _ = reportBuilder.AppendLine("Sequential:");
            _ = reportBuilder.AppendLine($"  Write Avg/Min/Max: {seqWriteAvg:0.00} / {seqWriteMin:0.00} / {seqWriteMax:0.00} MiB/s");
            _ = reportBuilder.AppendLine($"  Read  Avg/Min/Max: {seqReadAvg:0.00} / {seqReadMin:0.00} / {seqReadMax:0.00} MiB/s");
            _ = reportBuilder.AppendLine("Random small blocks:");
            _ = reportBuilder.AppendLine($"  Write Avg/Min/Max: {rndWriteAvg:0.00} / {rndWriteMin:0.00} / {rndWriteMax:0.00} MiB/s");
            _ = reportBuilder.AppendLine($"  Read  Avg/Min/Max: {rndReadAvg:0.00} / {rndReadMin:0.00} / {rndReadMax:0.00} MiB/s");
            _ = reportBuilder.AppendLine("Parallel small blocks:");
            _ = reportBuilder.AppendLine($"  Write Avg/Min/Max: {parWriteAvg:0.00} / {parWriteMin:0.00} / {parWriteMax:0.00} MiB/s");
            _ = reportBuilder.AppendLine($"  Read  Avg/Min/Max: {parReadAvg:0.00} / {parReadMin:0.00} / {parReadMax:0.00} MiB/s");
        }

        private void ShowDriveSpeedReportDialog(string report)
        {
            TaskDialogButton copyButton = new("Copy Report");
            TaskDialogButton closeButton = TaskDialogButton.Close;
            TaskDialogPage page = new()
            {
                Caption = "Drive Speed Test",
                Heading = "Benchmark finished",
                Text = report,
                Buttons = { copyButton, closeButton },
                DefaultButton = copyButton,
                AllowCancel = true
            };

            TaskDialogButton? result = TaskDialog.ShowDialog(this, page);
            if (result == copyButton)
            {
                try
                {
                    Clipboard.SetText(report);
                }
                catch
                {
                }
            }
        }



        private void driveSpeedTestToolStripMenuItem_DropDownOpening(object? sender, EventArgs e)
        {
            this.PopulateDriveSelections();
            this.ApplyDriveSpeedTestSettingTexts();
            this.UpdateDriveSpeedTestDropDownWidths();
        }

        private void toolStripComboBox_drives_SelectedIndexChanged(object? sender, EventArgs e)
        {
        }

        private async void driveSpeedTestToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (this._driveTestInProgress)
            {
                return;
            }

            if (this.toolStripComboBox_drives.SelectedItem is not DriveSelection drive)
            {
                _ = MessageBox.Show(this, "Please select a drive first in Drive Speed Test > Select Drive.", "Drive Speed Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            this.ApplyDriveTestFileSizeFromText();
            this.ApplyDriveTestBlockSizeFromText();
            this.ApplyDriveTestPassesFromText();
            this.ApplyDriveTestWorkerThreadsFromText();
            this.ApplyDriveFileSizeLimitForSelectedDrive(drive);

            this._driveTestInProgress = true;
            bool timerWasEnabled = this.UpdateTimer.Enabled;
            string previousWindowTitle = this.Text;
            this.driveSpeedTestToolStripMenuItem.Enabled = false;
            this.UseWaitCursor = true;
            this.ContextMenuStrip?.Close();
            if (timerWasEnabled)
            {
                this.UpdateTimer.Stop();
            }

            try
            {
                this.Text = WidgetStatics.BuildDriveTestWindowTitle(drive.RootPath, TimeSpan.Zero, 0);
                string report = await this.RunDriveSpeedTestAndBuildReportAsync(drive.RootPath, CancellationToken.None);
                this.ShowDriveSpeedReportDialog(report);
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(this, $"Drive speed test failed.\n\n{ex.Message}", "Drive Speed Test", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Text = previousWindowTitle;
                this.UseWaitCursor = false;
                this.driveSpeedTestToolStripMenuItem.Enabled = true;
                this._driveTestInProgress = false;
                if (!this._closing && timerWasEnabled)
                {
                    this.UpdateTimer.Start();
                }
            }
        }

        private void toolStripTextBox_testFileSizeMb_Leave(object? sender, EventArgs e)
        {
            this.ApplyDriveTestFileSizeFromText();
        }

        private void toolStripTextBox_testFileSizeMb_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            this.ApplyDriveTestFileSizeFromText();
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_testBlockSizeKb_Leave(object? sender, EventArgs e)
        {
            this.ApplyDriveTestBlockSizeFromText();
        }

        private void toolStripTextBox_testBlockSizeKb_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            this.ApplyDriveTestBlockSizeFromText();
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_testPasses_Leave(object? sender, EventArgs e)
        {
            this.ApplyDriveTestPassesFromText();
        }

        private void toolStripTextBox_testPasses_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            this.ApplyDriveTestPassesFromText();
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_testThreads_Leave(object? sender, EventArgs e)
        {
            this.ApplyDriveTestWorkerThreadsFromText();
        }

        private void toolStripTextBox_testThreads_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            this.ApplyDriveTestWorkerThreadsFromText();
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void ApplyDriveFileSizeLimitForSelectedDrive(DriveSelection drive)
        {
            try
            {
                DriveInfo driveInfo = new(drive.RootPath);
                const long reservedBytes = 512L * 1024L * 1024L;
                long safeBytes = Math.Max(512L * 1024L * 1024L, driveInfo.AvailableFreeSpace - reservedBytes);
                int maxAllowedMb = (int) Math.Clamp(safeBytes / (1024L * 1024L), 512L, 65_536L);
                if (this._driveTestFileSizeMb > maxAllowedMb)
                {
                    this._driveTestFileSizeMb = maxAllowedMb;
                    this.toolStripTextBox_testFileSizeMb.Text = this._driveTestFileSizeMb.ToString();
                }
            }
            catch
            {
            }
        }
    }
}
