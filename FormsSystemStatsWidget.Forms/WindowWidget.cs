using FormsSystemStatsWidget.Core;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Timer = System.Windows.Forms.Timer;

namespace FormsSystemStatsWidget.Forms
{
    public partial class WindowWidget : Form
    {
        private int _updateIntervalMs = 420;
        private Color _diagramColor = Color.White;
        private Color? _percentageColor = Color.BlueViolet;
        private int _driveTestFileSizeMb = 4096;
        private int _driveTestBlockSizeKb = 1024;
        private int _driveTestPasses = 2;
        private int _driveTestWorkerThreads = Math.Clamp(Environment.ProcessorCount / 4, 2, Environment.ProcessorCount);
        private bool _driveTestInProgress = false;

        private Timer UpdateTimer;
        private GpuStats Gpu;
        private GpuStats? Gpu2 = null;
        private volatile bool _closing = false;
        private int _tickInProgress = 0;

        private sealed class DriveSelection
        {
            public required string RootPath { get; init; }
            public required string DisplayName { get; init; }

            public override string ToString() => DisplayName;
        }


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

            this.PopulateDriveSelections();
            this.ApplyDriveSpeedTestSettingTexts();
        }

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

        private static long CalculateParallelTransferredBytes(long totalBytes, int blockSizeBytes, int workerCount)
        {
            int safeWorkerCount = Math.Max(1, workerCount);
            long bytesPerWorker = Math.Max(64L * 1024L * 1024L, totalBytes / safeWorkerCount);
            long alignedBytesPerWorker = Math.Max(blockSizeBytes, (bytesPerWorker / blockSizeBytes) * blockSizeBytes);
            return alignedBytesPerWorker * safeWorkerCount;
        }

        private static string FormatDuration(TimeSpan duration)
        {
            return $"{duration.TotalSeconds:0.00} s";
        }

        private static string BuildDriveTestWindowTitle(string rootPath, TimeSpan elapsed, double progressPercent)
        {
            string driveText = (Path.GetPathRoot(rootPath) ?? rootPath).TrimEnd('\\');
            return $"Testing {driveText}: ... {elapsed:mm\\:ss} ({progressPercent:F2}%)";
        }

        private static string GetSafePropertyValue(ManagementBaseObject source, string propertyName)
        {
            try
            {
                return Convert.ToString(source[propertyName]) ?? "n/a";
            }
            catch
            {
                return "n/a";
            }
        }

        private static string FormatBytesToGiB(long bytes)
        {
            double gib = bytes / 1_073_741_824d;
            return $"{gib:0.00} GiB";
        }

        private static string BuildDriveEnvironmentInfoBlock(string rootPath)
        {
            StringBuilder infoBuilder = new StringBuilder();

            infoBuilder.AppendLine("Environment:");
            infoBuilder.AppendLine($"  OS: {RuntimeInformation.OSDescription}");
            infoBuilder.AppendLine($"  Runtime: {RuntimeInformation.FrameworkDescription}");
            infoBuilder.AppendLine($"  Process: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}, OS64: {(Environment.Is64BitOperatingSystem ? "Yes" : "No")}");
            infoBuilder.AppendLine($"  Machine: {Environment.MachineName}");
            infoBuilder.AppendLine($"  Logical CPU Cores: {Environment.ProcessorCount}");

            try
            {
                DriveInfo driveInfo = new DriveInfo(rootPath);
                infoBuilder.AppendLine("Logical Drive:");
                infoBuilder.AppendLine($"  Name: {driveInfo.Name}");
                infoBuilder.AppendLine($"  Type: {driveInfo.DriveType}");
                infoBuilder.AppendLine($"  FileSystem: {driveInfo.DriveFormat}");
                infoBuilder.AppendLine($"  Volume Label: {(string.IsNullOrWhiteSpace(driveInfo.VolumeLabel) ? "n/a" : driveInfo.VolumeLabel)}");
                infoBuilder.AppendLine($"  Total: {FormatBytesToGiB(driveInfo.TotalSize)}");
                infoBuilder.AppendLine($"  Free: {FormatBytesToGiB(driveInfo.TotalFreeSpace)}");
                infoBuilder.AppendLine($"  Free (User): {FormatBytesToGiB(driveInfo.AvailableFreeSpace)}");
            }
            catch
            {
                infoBuilder.AppendLine("Logical Drive: n/a");
            }

            string driveId = (Path.GetPathRoot(rootPath) ?? rootPath).TrimEnd('\\').ToUpperInvariant();
            try
            {
                using ManagementObjectSearcher logicalDiskSearcher = new ManagementObjectSearcher($"SELECT * FROM Win32_LogicalDisk WHERE DeviceID='{driveId}'");
                using ManagementObjectCollection logicalDisks = logicalDiskSearcher.Get();

                infoBuilder.AppendLine("WMI Logical Disk:");
                foreach (ManagementObject logicalDisk in logicalDisks.Cast<ManagementObject>())
                {
                    infoBuilder.AppendLine($"  DeviceID: {GetSafePropertyValue(logicalDisk, "DeviceID")}");
                    infoBuilder.AppendLine($"  VolumeName: {GetSafePropertyValue(logicalDisk, "VolumeName")}");
                    infoBuilder.AppendLine($"  VolumeSerialNumber: {GetSafePropertyValue(logicalDisk, "VolumeSerialNumber")}");
                    infoBuilder.AppendLine($"  ProviderName: {GetSafePropertyValue(logicalDisk, "ProviderName")}");
                    infoBuilder.AppendLine($"  Compressed: {GetSafePropertyValue(logicalDisk, "Compressed")}");
                    infoBuilder.AppendLine($"  SupportsDiskQuotas: {GetSafePropertyValue(logicalDisk, "SupportsDiskQuotas")}");
                    infoBuilder.AppendLine($"  SupportsFileBasedCompression: {GetSafePropertyValue(logicalDisk, "SupportsFileBasedCompression")}");
                }

                HashSet<string> diskDeviceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                List<ManagementObject> diskDrives = new List<ManagementObject>();

                foreach (ManagementObject logicalDisk in logicalDisks.Cast<ManagementObject>())
                {
                    string logicalDiskPath = logicalDisk.Path.RelativePath;
                    using ManagementObjectSearcher partitionSearcher = new ManagementObjectSearcher($"ASSOCIATORS OF {{{logicalDiskPath}}} WHERE AssocClass = Win32_LogicalDiskToPartition");
                    using ManagementObjectCollection partitions = partitionSearcher.Get();
                    foreach (ManagementObject partition in partitions.Cast<ManagementObject>())
                    {
                        string partitionPath = partition.Path.RelativePath;
                        using ManagementObjectSearcher diskSearcher = new ManagementObjectSearcher($"ASSOCIATORS OF {{{partitionPath}}} WHERE AssocClass = Win32_DiskDriveToDiskPartition");
                        using ManagementObjectCollection disks = diskSearcher.Get();
                        foreach (ManagementObject disk in disks.Cast<ManagementObject>())
                        {
                            string deviceId = GetSafePropertyValue(disk, "DeviceID");
                            if (diskDeviceIds.Add(deviceId))
                            {
                                diskDrives.Add(disk);
                            }
                        }
                    }
                }

                if (diskDrives.Count > 0)
                {
                    bool raidCandidate = diskDrives.Count > 1;
                    infoBuilder.AppendLine("Physical Disk Mapping:");
                    infoBuilder.AppendLine($"  Mapped Physical Devices: {diskDrives.Count}");

                    for (int index = 0; index < diskDrives.Count; index++)
                    {
                        ManagementObject disk = diskDrives[index];
                        string model = GetSafePropertyValue(disk, "Model");
                        string caption = GetSafePropertyValue(disk, "Caption");
                        string manufacturer = GetSafePropertyValue(disk, "Manufacturer");
                        string interfaceType = GetSafePropertyValue(disk, "InterfaceType");
                        string mediaType = GetSafePropertyValue(disk, "MediaType");
                        string serialNumber = GetSafePropertyValue(disk, "SerialNumber");
                        string firmware = GetSafePropertyValue(disk, "FirmwareRevision");
                        string pnpDeviceId = GetSafePropertyValue(disk, "PNPDeviceID");
                        string sizeRaw = GetSafePropertyValue(disk, "Size");

                        if (model.Contains("RAID", StringComparison.OrdinalIgnoreCase) || caption.Contains("RAID", StringComparison.OrdinalIgnoreCase) || pnpDeviceId.Contains("RAID", StringComparison.OrdinalIgnoreCase))
                        {
                            raidCandidate = true;
                        }

                        string sizeText = sizeRaw;
                        if (long.TryParse(sizeRaw, out long diskBytes))
                        {
                            sizeText = $"{sizeRaw} ({FormatBytesToGiB(diskBytes)})";
                        }

                        infoBuilder.AppendLine($"  Disk #{index + 1}:");
                        infoBuilder.AppendLine($"    Model: {model}");
                        infoBuilder.AppendLine($"    Caption: {caption}");
                        infoBuilder.AppendLine($"    Manufacturer: {manufacturer}");
                        infoBuilder.AppendLine($"    InterfaceType: {interfaceType}");
                        infoBuilder.AppendLine($"    MediaType: {mediaType}");
                        infoBuilder.AppendLine($"    Size: {sizeText}");
                        infoBuilder.AppendLine($"    FirmwareRevision: {firmware}");
                        infoBuilder.AppendLine($"    SerialNumber: {serialNumber}");
                        infoBuilder.AppendLine($"    PNPDeviceID: {pnpDeviceId}");
                    }

                    infoBuilder.AppendLine($"  RAID / Multi-Disk Hint: {(raidCandidate ? "Likely" : "Not detected")}");
                }
                else
                {
                    infoBuilder.AppendLine("Physical Disk Mapping: n/a");
                }
            }
            catch (Exception ex)
            {
                infoBuilder.AppendLine($"WMI Storage Details: unavailable ({ex.GetType().Name})");
            }

            return infoBuilder.ToString().TrimEnd();
        }

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
                    this.BeginInvoke(new Action(() =>
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

        private static async Task<string> ResolveWritableDriveTempDirectoryAsync(string rootPath, CancellationToken cancellationToken)
        {
            string normalizedRootPath = Path.GetPathRoot(rootPath) ?? rootPath;
            string userTempPath = Path.GetTempPath();
            string userTempRoot = Path.GetPathRoot(userTempPath) ?? string.Empty;

            List<string> candidates = new List<string>();
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
                    Directory.CreateDirectory(candidate);
                    string probeFilePath = Path.Combine(candidate, $".fssw-probe-{Guid.NewGuid():N}.tmp");
                    await using FileStream probe = new FileStream(probeFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.DeleteOnClose);
                    await probe.WriteAsync(new byte[] { 0xAA }, cancellationToken);
                    return candidate;
                }
                catch
                {
                }
            }

            throw new UnauthorizedAccessException($"No writable temp folder was found on drive '{normalizedRootPath}'.");
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
                    int chunk = (int)Math.Min(buffer.Length, remainingBytes);
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
            await using (FileStream writeStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, blockSizeBytes, options))
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
            await using (FileStream readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, blockSizeBytes, FileOptions.RandomAccess))
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

        private static async Task WriteWorkerFileAsync(string path, long fileSizeBytes, byte[] buffer, int blockSizeBytes, FileOptions options, CancellationToken cancellationToken, Action<long>? progressBytesCallback = null)
        {
            await using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, blockSizeBytes, options);
            long remainingBytes = fileSizeBytes;
            while (remainingBytes > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int chunk = (int)Math.Min(buffer.Length, remainingBytes);
                await stream.WriteAsync(buffer.AsMemory(0, chunk), cancellationToken);
                remainingBytes -= chunk;
                progressBytesCallback?.Invoke(chunk);
            }

            await stream.FlushAsync(cancellationToken);
        }

        private static async Task ReadWorkerFileAsync(string path, byte[] buffer, int blockSizeBytes, CancellationToken cancellationToken, Action<long>? progressBytesCallback = null)
        {
            await using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, blockSizeBytes, FileOptions.SequentialScan);
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
            long fileSizeBytes = checked((long)this._driveTestFileSizeMb * 1024L * 1024L);
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
                long parallelTransferredBytes = CalculateParallelTransferredBytes(fileSizeBytes, randomBlockSizeBytes, parallelWorkerCount);

                long bytesPerPass = (2L * fileSizeBytes) + (2L * randomFileSizeBytes) + (2L * parallelTransferredBytes);
                long totalExpectedBytes = Math.Max(1L, checked(bytesPerPass * passes));
                long completedBytes = 0;

                void ReportProgress(long processedBytes)
                {
                    long totalProcessed = Interlocked.Add(ref completedBytes, Math.Max(0, processedBytes));
                    double progressPercent = Math.Min(100.0, (totalProcessed / (double)totalExpectedBytes) * 100.0);
                    TimeSpan elapsed = totalStopwatch.Elapsed;
                    string title = BuildDriveTestWindowTitle(rootPath, elapsed, progressPercent);
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

                    passLines.Add($"Pass {pass} - Seq ({FormatDuration(sequentialDurationStopwatch.Elapsed)}): Write {sequentialWriteMBps:0.00} | Read {sequentialReadMBps:0.00} MiB/s");
                    passLines.Add($"Pass {pass} - Rnd {randomBlockSizeBytes / 1024} KiB ({FormatDuration(randomDurationStopwatch.Elapsed)}): Write {randomWriteMBps:0.00} | Read {randomReadMBps:0.00} MiB/s");
                    passLines.Add($"Pass {pass} - Parallel x{parallelWorkerCount} ({FormatDuration(parallelDurationStopwatch.Elapsed)}): Write {parallelWriteMBps:0.00} | Read {parallelReadMBps:0.00} MiB/s");
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

            StringBuilder reportBuilder = new StringBuilder();
            reportBuilder.AppendLine("Drive Speed Test Report");
            reportBuilder.AppendLine(new string('-', 32));
            reportBuilder.AppendLine($"Drive: {rootPath}");
            reportBuilder.AppendLine($"Temp Folder: {tempDirectoryPath}");
            reportBuilder.AppendLine($"Target File Size: {this._driveTestFileSizeMb} MiB");
            reportBuilder.AppendLine($"Block Size: {this._driveTestBlockSizeKb} KiB");
            reportBuilder.AppendLine($"Passes: {passes}");
            reportBuilder.AppendLine($"Threads: {this._driveTestWorkerThreads}");
            reportBuilder.AppendLine($"Write Through: {(this.writeThroughToolStripMenuItem.Checked ? "On" : "Off")}");
            reportBuilder.AppendLine($"Total Runtime: {totalStopwatch.Elapsed.TotalSeconds:0.00} s");
            reportBuilder.AppendLine();
            reportBuilder.AppendLine("Drive Hardware/Software Info:");
            reportBuilder.AppendLine(new string('-', 32));
            reportBuilder.AppendLine(BuildDriveEnvironmentInfoBlock(rootPath));
            reportBuilder.AppendLine();
            foreach (string passLine in passLines)
            {
                reportBuilder.AppendLine(passLine);
            }
            reportBuilder.AppendLine();
            reportBuilder.AppendLine("Sequential:");
            reportBuilder.AppendLine($"  Write Avg/Min/Max: {seqWriteAvg:0.00} / {seqWriteMin:0.00} / {seqWriteMax:0.00} MiB/s");
            reportBuilder.AppendLine($"  Read  Avg/Min/Max: {seqReadAvg:0.00} / {seqReadMin:0.00} / {seqReadMax:0.00} MiB/s");
            reportBuilder.AppendLine("Random small blocks:");
            reportBuilder.AppendLine($"  Write Avg/Min/Max: {rndWriteAvg:0.00} / {rndWriteMin:0.00} / {rndWriteMax:0.00} MiB/s");
            reportBuilder.AppendLine($"  Read  Avg/Min/Max: {rndReadAvg:0.00} / {rndReadMin:0.00} / {rndReadMax:0.00} MiB/s");
            reportBuilder.AppendLine("Parallel small blocks:");
            reportBuilder.AppendLine($"  Write Avg/Min/Max: {parWriteAvg:0.00} / {parWriteMin:0.00} / {parWriteMax:0.00} MiB/s");
            reportBuilder.AppendLine($"  Read  Avg/Min/Max: {parReadAvg:0.00} / {parReadMin:0.00} / {parReadMax:0.00} MiB/s");
            return reportBuilder.ToString();
        }

        private void ShowDriveSpeedReportDialog(string report)
        {
            TaskDialogButton copyButton = new TaskDialogButton("Copy Report");
            TaskDialogButton closeButton = TaskDialogButton.Close;
            TaskDialogPage page = new TaskDialogPage
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

                this.UpdateAverageCpuLoadAndTemperatureLabel(threads);

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

        private void UpdateAverageCpuLoadAndTemperatureLabel(float[] usages)
        {
            if (_closing)
            {
                return;
            }

            double averageLoadPercent = usages.Length > 0 ? usages.Average() * 100d : 0d;
            double? averageTemperatureCelsius = this.TryGetAverageCpuTemperatureCelsius();

            if (averageTemperatureCelsius.HasValue)
            {
                this.label_avgCpuLoadAndTemperature.Text = $"{averageLoadPercent:0.00}% | {averageTemperatureCelsius.Value:0.0} °C";
            }
            else
            {
                this.label_avgCpuLoadAndTemperature.Text = $"{averageLoadPercent:0.00}%";
            }
        }

        private double? TryGetAverageCpuTemperatureCelsius()
        {
            return CpuStats.GetAverageCpuTemperatureCelsius();
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
                MessageBox.Show(this, "Please select a drive first in Drive Speed Test > Select Drive.", "Drive Speed Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                this.Text = BuildDriveTestWindowTitle(drive.RootPath, TimeSpan.Zero, 0);
                string report = await this.RunDriveSpeedTestAndBuildReportAsync(drive.RootPath, CancellationToken.None);
                this.ShowDriveSpeedReportDialog(report);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Drive speed test failed.\n\n{ex.Message}", "Drive Speed Test", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Text = previousWindowTitle;
                this.UseWaitCursor = false;
                this.driveSpeedTestToolStripMenuItem.Enabled = true;
                this._driveTestInProgress = false;
                if (!_closing && timerWasEnabled)
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
                DriveInfo driveInfo = new DriveInfo(drive.RootPath);
                const long reservedBytes = 512L * 1024L * 1024L;
                long safeBytes = Math.Max(512L * 1024L * 1024L, driveInfo.AvailableFreeSpace - reservedBytes);
                int maxAllowedMb = (int)Math.Clamp(safeBytes / (1024L * 1024L), 512L, 65_536L);
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
