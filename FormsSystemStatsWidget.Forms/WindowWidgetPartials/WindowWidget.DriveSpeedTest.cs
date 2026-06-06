using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FormsSystemStatsWidget.Forms
{
    public partial class WindowWidget
    {
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
                await Task.WhenAll(filePaths.Select(path => WriteWorkerFileAsync(path, alignedBytesPerWorker, workerBuffer, blockSizeBytes, options, cancellationToken, progressBytesCallback)));
                writeStopwatch.Stop();

                byte[] readBuffer = new byte[blockSizeBytes];
                var readStopwatch = Stopwatch.StartNew();
                await Task.WhenAll(filePaths.Select(path => ReadWorkerFileAsync(path, readBuffer, blockSizeBytes, cancellationToken, progressBytesCallback)));
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

        internal static async Task WriteWorkerFileAsync(string path, long fileSizeBytes, byte[] buffer, int blockSizeBytes, FileOptions options, CancellationToken cancellationToken, Action<long>? progressBytesCallback = null)
        {
            await using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None, blockSizeBytes, options);
            long remainingBytes = fileSizeBytes;
            while (remainingBytes > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int chunk = (int) Math.Min(buffer.Length, remainingBytes);
                await stream.WriteAsync(buffer.AsMemory(0, chunk), cancellationToken);
                remainingBytes -= chunk;
                progressBytesCallback?.Invoke(chunk);
            }

            await stream.FlushAsync(cancellationToken);
        }

        internal static async Task ReadWorkerFileAsync(string path, byte[] buffer, int blockSizeBytes, CancellationToken cancellationToken, Action<long>? progressBytesCallback = null)
        {
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, blockSizeBytes, FileOptions.SequentialScan);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                progressBytesCallback?.Invoke(bytesRead);
            }
        }

        private async Task<string> RunDriveSpeedTestAndBuildReportAsync(string rootPath, CancellationToken cancellationToken)
        {
            long fileSizeBytes = checked((long) this._driveTestFileSizeMb * 1024L * 1024L);
            int blockSizeBytes = checked(this._driveTestBlockSizeKb * 1024);
            int passes = this._driveTestPasses;

            string tempDirectoryPath = await ResolveWritableDriveTempDirectoryAsync(rootPath, cancellationToken);
            string sequentialTempFilePath = Path.Combine(tempDirectoryPath, $".fssw-drive-seq-{Guid.NewGuid():N}.bin");
            string randomTempFilePath = Path.Combine(tempDirectoryPath, $".fssw-drive-rnd-{Guid.NewGuid():N}.bin");

            var sequentialWriteResults = new List<double>(passes);
            var sequentialReadResults = new List<double>(passes);
            var randomWriteResults = new List<double>(passes);
            var randomReadResults = new List<double>(passes);
            var parallelWriteResults = new List<double>(passes);
            var parallelReadResults = new List<double>(passes);
            var passLines = new List<string>(passes);

            FileOptions writeOptions = FileOptions.SequentialScan;
            if (this.writeThroughToolStripMenuItem.Checked)
            {
                writeOptions |= FileOptions.WriteThrough;
            }

            Stopwatch totalStopwatch = Stopwatch.StartNew();
            try
            {
                long randomFileSizeBytes = Math.Max(512L * 1024L * 1024L, Math.Min(fileSizeBytes, 2L * 1024L * 1024L * 1024L));
                int randomBlockSizeBytes = Math.Max(4 * 1024, Math.Min(64 * 1024, blockSizeBytes / 16));
                int parallelWorkerCount = Math.Clamp(this._driveTestWorkerThreads, 1, Environment.ProcessorCount);
                long parallelTransferredBytes = WidgetStatics.CalculateParallelTransferredBytes(fileSizeBytes, randomBlockSizeBytes, parallelWorkerCount);

                long bytesPerPass = (2L * fileSizeBytes) + (2L * randomFileSizeBytes) + (2L * parallelTransferredBytes);
                long totalExpectedBytes = Math.Max(1L, checked(bytesPerPass * passes));
                long completedBytes = 0;

                void ReportProgress(long processedBytes)
                {
                    long totalProcessed = Interlocked.Add(ref completedBytes, Math.Max(0, processedBytes));
                    double progressPercent = Math.Min(100.0, (totalProcessed / (double) totalExpectedBytes) * 100.0);
                    TimeSpan elapsed = totalStopwatch.Elapsed;
                    string title = WidgetStatics.BuildDriveTestWindowTitle(rootPath, elapsed, progressPercent);
                    this.TrySetWindowTitleSafe(title);
                }

                for (int pass = 1; pass <= passes; pass++)
                {
                    Stopwatch sequentialDurationStopwatch = Stopwatch.StartNew();
                    var (sequentialWriteMBps, sequentialReadMBps) = await this.RunDriveSpeedPassAsync(sequentialTempFilePath, fileSizeBytes, blockSizeBytes, writeOptions, cancellationToken, ReportProgress);
                    sequentialDurationStopwatch.Stop();
                    sequentialWriteResults.Add(sequentialWriteMBps);
                    sequentialReadResults.Add(sequentialReadMBps);

                    Stopwatch randomDurationStopwatch = Stopwatch.StartNew();
                    var (randomWriteMBps, randomReadMBps) = await this.RunDriveSmallRandomPassAsync(randomTempFilePath, randomFileSizeBytes, randomBlockSizeBytes, writeOptions, cancellationToken, ReportProgress);
                    randomDurationStopwatch.Stop();
                    randomWriteResults.Add(randomWriteMBps);
                    randomReadResults.Add(randomReadMBps);

                    Stopwatch parallelDurationStopwatch = Stopwatch.StartNew();
                    var (parallelWriteMBps, parallelReadMBps) = await this.RunDriveParallelPassAsync(tempDirectoryPath, fileSizeBytes, randomBlockSizeBytes, parallelWorkerCount, writeOptions, cancellationToken, ReportProgress);
                    parallelDurationStopwatch.Stop();
                    parallelWriteResults.Add(parallelWriteMBps);
                    parallelReadResults.Add(parallelReadMBps);

                    passLines.Add($"Pass {pass} - Seq ({WidgetStatics.FormatDuration(sequentialDurationStopwatch.Elapsed)}): Write {sequentialWriteMBps:0.00} | Read {sequentialReadMBps:0.00} MiB/s");
                    passLines.Add($"Pass {pass} - Rnd {randomBlockSizeBytes / 1024} KiB ({WidgetStatics.FormatDuration(randomDurationStopwatch.Elapsed)}): Write {randomWriteMBps:0.00} | Read {randomReadMBps:0.00} MiB/s");
                    passLines.Add($"Pass {pass} - Parallel x{parallelWorkerCount} ({WidgetStatics.FormatDuration(parallelDurationStopwatch.Elapsed)}): Write {parallelWriteMBps:0.00} | Read {parallelReadMBps:0.00} MiB/s");
                }
            }
            finally
            {
                totalStopwatch.Stop();
                try
                {
                    if (File.Exists(sequentialTempFilePath))
                    {
                        File.Delete(sequentialTempFilePath);
                    }
                }
                catch
                {
                }

                try
                {
                    if (File.Exists(randomTempFilePath))
                    {
                        File.Delete(randomTempFilePath);
                    }
                }
                catch
                {
                }
            }

            static (double average, double min, double max) CalculateMetrics(List<double> values)
            {
                if (values.Count == 0)
                {
                    return (0, 0, 0);
                }

                return (values.Average(), values.Min(), values.Max());
            }

            var (seqWriteAvg, seqWriteMin, seqWriteMax) = CalculateMetrics(sequentialWriteResults);
            var (seqReadAvg, seqReadMin, seqReadMax) = CalculateMetrics(sequentialReadResults);
            var (rndWriteAvg, rndWriteMin, rndWriteMax) = CalculateMetrics(randomWriteResults);
            var (rndReadAvg, rndReadMin, rndReadMax) = CalculateMetrics(randomReadResults);
            var (parWriteAvg, parWriteMin, parWriteMax) = CalculateMetrics(parallelWriteResults);
            var (parReadAvg, parReadMin, parReadMax) = CalculateMetrics(parallelReadResults);

            StringBuilder reportBuilder = new();
            _ = reportBuilder.AppendLine("Drive Speed Test Report");
            _ = reportBuilder.AppendLine(new string('-', 32));
            _ = reportBuilder.AppendLine($"Drive: {rootPath}");
            _ = reportBuilder.AppendLine($"Temp Folder: {tempDirectoryPath}");
            _ = reportBuilder.AppendLine($"Target File Size: {this._driveTestFileSizeMb} MiB");
            _ = reportBuilder.AppendLine($"Block Size: {this._driveTestBlockSizeKb} KiB");
            _ = reportBuilder.AppendLine($"Passes: {passes}");
            _ = reportBuilder.AppendLine($"Threads: {this._driveTestWorkerThreads}");
            _ = reportBuilder.AppendLine($"Write Through: {(this.writeThroughToolStripMenuItem.Checked ? "On" : "Off")}");
            _ = reportBuilder.AppendLine($"Total Runtime: {totalStopwatch.Elapsed.TotalSeconds:0.00} s");
            _ = reportBuilder.AppendLine();
            _ = reportBuilder.AppendLine("Drive Hardware/Software Info:");
            _ = reportBuilder.AppendLine(new string('-', 32));
            _ = reportBuilder.AppendLine(WidgetStatics.BuildDriveEnvironmentInfoBlock(rootPath));
            _ = reportBuilder.AppendLine();
            foreach (string passLine in passLines)
            {
                _ = reportBuilder.AppendLine(passLine);
            }
            _ = reportBuilder.AppendLine();
            _ = reportBuilder.AppendLine("Sequential:");
            _ = reportBuilder.AppendLine($"  Write Avg/Min/Max: {seqWriteAvg:0.00} / {seqWriteMin:0.00} / {seqWriteMax:0.00} MiB/s");
            _ = reportBuilder.AppendLine($"  Read  Avg/Min/Max: {seqReadAvg:0.00} / {seqReadMin:0.00} / {seqReadMax:0.00} MiB/s");
            _ = reportBuilder.AppendLine("Random small blocks:");
            _ = reportBuilder.AppendLine($"  Write Avg/Min/Max: {rndWriteAvg:0.00} / {rndWriteMin:0.00} / {rndWriteMax:0.00} MiB/s");
            _ = reportBuilder.AppendLine($"  Read  Avg/Min/Max: {rndReadAvg:0.00} / {rndReadMin:0.00} / {rndReadMax:0.00} MiB/s");
            _ = reportBuilder.AppendLine("Parallel small blocks:");
            _ = reportBuilder.AppendLine($"  Write Avg/Min/Max: {parWriteAvg:0.00} / {parWriteMin:0.00} / {parWriteMax:0.00} MiB/s");
            _ = reportBuilder.AppendLine($"  Read  Avg/Min/Max: {parReadAvg:0.00} / {parReadMin:0.00} / {parReadMax:0.00} MiB/s");
            return reportBuilder.ToString();
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

        internal static async Task<string> ResolveWritableDriveTempDirectoryAsync(string rootPath, CancellationToken cancellationToken)
        {
            string normalizedRootPath = Path.GetPathRoot(rootPath) ?? rootPath;
            string userTempPath = Path.GetTempPath();
            string userTempRoot = Path.GetPathRoot(userTempPath) ?? string.Empty;

            List<string> candidates = [];
            if (string.Equals(userTempRoot, normalizedRootPath, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(Path.Combine(userTempPath, "FormsSystemStatsWidget", "DriveSpeedTest"));
            }

            string userName = Environment.UserName;
            candidates.Add(Path.Combine(normalizedRootPath, "Users", userName, "AppData", "Local", "Temp", "FormsSystemStatsWidget", "DriveSpeedTest"));
            candidates.Add(Path.Combine(normalizedRootPath, "Users", "Public", "Documents", "FormsSystemStatsWidget", "DriveSpeedTest"));
            candidates.Add(Path.Combine(normalizedRootPath, "Temp", "FormsSystemStatsWidget", "DriveSpeedTest"));

            foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    _ = Directory.CreateDirectory(candidate);
                    string probeFilePath = Path.Combine(candidate, $".fssw-probe-{Guid.NewGuid():N}.tmp");
                    await using FileStream probe = new(probeFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.DeleteOnClose);
                    await probe.WriteAsync(new byte[] { 0xAA }, cancellationToken);
                    return candidate;
                }
                catch
                {
                }
            }

            throw new UnauthorizedAccessException($"No writable temp folder was found on drive '{normalizedRootPath}'.");
        }
    }
}
