using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace FormsSystemStatsWidget.Forms
{
    internal static class WidgetStatics
    {
        internal static long CalculateParallelTransferredBytes(long totalBytes, int blockSizeBytes, int workerCount)
        {
            int safeWorkerCount = Math.Max(1, workerCount);
            long bytesPerWorker = Math.Max(64L * 1024L * 1024L, totalBytes / safeWorkerCount);
            long alignedBytesPerWorker = Math.Max(blockSizeBytes, (bytesPerWorker / blockSizeBytes) * blockSizeBytes);
            return alignedBytesPerWorker * safeWorkerCount;
        }

        internal static string FormatDuration(TimeSpan duration)
        {
            return $"{duration.TotalSeconds:0.00} s";
        }

        internal static string BuildDriveTestWindowTitle(string rootPath, TimeSpan elapsed, double progressPercent)
        {
            string driveText = (Path.GetPathRoot(rootPath) ?? rootPath).TrimEnd('\\');
            return $"Testing {driveText}: ... {elapsed:mm\\:ss} ({progressPercent:F2}%)";
        }

        internal static string GetSafePropertyValue(ManagementBaseObject source, string propertyName)
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

        internal static string FormatBytesToGiB(long bytes)
        {
            double gib = bytes / 1_073_741_824d;
            return $"{gib:0.00} GiB";
        }

        internal static string BuildDriveEnvironmentInfoBlock(string rootPath)
        {
            StringBuilder infoBuilder = new();

            _ = infoBuilder.AppendLine("Environment:");
            _ = infoBuilder.AppendLine($"  OS: {RuntimeInformation.OSDescription}");
            _ = infoBuilder.AppendLine($"  Runtime: {RuntimeInformation.FrameworkDescription}");
            _ = infoBuilder.AppendLine($"  Process: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}, OS64: {(Environment.Is64BitOperatingSystem ? "Yes" : "No")}");
            _ = infoBuilder.AppendLine($"  Machine: {Environment.MachineName}");
            _ = infoBuilder.AppendLine($"  Logical CPU Cores: {Environment.ProcessorCount}");

            try
            {
                DriveInfo driveInfo = new(rootPath);
                _ = infoBuilder.AppendLine("Logical Drive:");
                _ = infoBuilder.AppendLine($"  Name: {driveInfo.Name}");
                _ = infoBuilder.AppendLine($"  Type: {driveInfo.DriveType}");
                _ = infoBuilder.AppendLine($"  FileSystem: {driveInfo.DriveFormat}");
                _ = infoBuilder.AppendLine($"  Volume Label: {(string.IsNullOrWhiteSpace(driveInfo.VolumeLabel) ? "n/a" : driveInfo.VolumeLabel)}");
                _ = infoBuilder.AppendLine($"  Total: {FormatBytesToGiB(driveInfo.TotalSize)}");
                _ = infoBuilder.AppendLine($"  Free: {FormatBytesToGiB(driveInfo.TotalFreeSpace)}");
                _ = infoBuilder.AppendLine($"  Free (User): {FormatBytesToGiB(driveInfo.AvailableFreeSpace)}");
            }
            catch
            {
                _ = infoBuilder.AppendLine("Logical Drive: n/a");
            }

            string driveId = (Path.GetPathRoot(rootPath) ?? rootPath).TrimEnd('\\').ToUpperInvariant();
            try
            {
                using ManagementObjectSearcher logicalDiskSearcher = new($"SELECT * FROM Win32_LogicalDisk WHERE DeviceID='{driveId}'");
                using ManagementObjectCollection logicalDisks = logicalDiskSearcher.Get();

                _ = infoBuilder.AppendLine("WMI Logical Disk:");
                foreach (ManagementObject logicalDisk in logicalDisks.Cast<ManagementObject>())
                {
                    _ = infoBuilder.AppendLine($"  DeviceID: {GetSafePropertyValue(logicalDisk, "DeviceID")}");
                    _ = infoBuilder.AppendLine($"  VolumeName: {GetSafePropertyValue(logicalDisk, "VolumeName")}");
                    _ = infoBuilder.AppendLine($"  VolumeSerialNumber: {GetSafePropertyValue(logicalDisk, "VolumeSerialNumber")}");
                    _ = infoBuilder.AppendLine($"  ProviderName: {GetSafePropertyValue(logicalDisk, "ProviderName")}");
                    _ = infoBuilder.AppendLine($"  Compressed: {GetSafePropertyValue(logicalDisk, "Compressed")}");
                    _ = infoBuilder.AppendLine($"  SupportsDiskQuotas: {GetSafePropertyValue(logicalDisk, "SupportsDiskQuotas")}");
                    _ = infoBuilder.AppendLine($"  SupportsFileBasedCompression: {GetSafePropertyValue(logicalDisk, "SupportsFileBasedCompression")}");
                }

                HashSet<string> diskDeviceIds = new(StringComparer.OrdinalIgnoreCase);
                List<ManagementObject> diskDrives = [];

                foreach (ManagementObject logicalDisk in logicalDisks.Cast<ManagementObject>())
                {
                    string logicalDiskPath = logicalDisk.Path.RelativePath;
                    using ManagementObjectSearcher partitionSearcher = new($"ASSOCIATORS OF {{{logicalDiskPath}}} WHERE AssocClass = Win32_LogicalDiskToPartition");
                    using ManagementObjectCollection partitions = partitionSearcher.Get();
                    foreach (ManagementObject partition in partitions.Cast<ManagementObject>())
                    {
                        string partitionPath = partition.Path.RelativePath;
                        using ManagementObjectSearcher diskSearcher = new($"ASSOCIATORS OF {{{partitionPath}}} WHERE AssocClass = Win32_DiskDriveToDiskPartition");
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
                    _ = infoBuilder.AppendLine("Physical Disk Mapping:");
                    _ = infoBuilder.AppendLine($"  Mapped Physical Devices: {diskDrives.Count}");

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

                        _ = infoBuilder.AppendLine($"  Disk #{index + 1}:");
                        _ = infoBuilder.AppendLine($"    Model: {model}");
                        _ = infoBuilder.AppendLine($"    Caption: {caption}");
                        _ = infoBuilder.AppendLine($"    Manufacturer: {manufacturer}");
                        _ = infoBuilder.AppendLine($"    InterfaceType: {interfaceType}");
                        _ = infoBuilder.AppendLine($"    MediaType: {mediaType}");
                        _ = infoBuilder.AppendLine($"    Size: {sizeText}");
                        _ = infoBuilder.AppendLine($"    FirmwareRevision: {firmware}");
                        _ = infoBuilder.AppendLine($"    SerialNumber: {serialNumber}");
                        _ = infoBuilder.AppendLine($"    PNPDeviceID: {pnpDeviceId}");
                    }

                    _ = infoBuilder.AppendLine($"  RAID / Multi-Disk Hint: {(raidCandidate ? "Likely" : "Not detected")}");
                }
                else
                {
                    _ = infoBuilder.AppendLine("Physical Disk Mapping: n/a");
                }
            }
            catch (Exception ex)
            {
                _ = infoBuilder.AppendLine($"WMI Storage Details: unavailable ({ex.GetType().Name})");
            }

            return infoBuilder.ToString().TrimEnd();
        }

        internal static string Ellipsize(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || maxLen <= 0)
            {
                return string.Empty;
            }

            if (text.Length <= maxLen)
            {
                return text;
            }

            return maxLen <= 3 ? text.Substring(0, maxLen) : string.Concat(text.AsSpan(0, maxLen - 3), "...");
        }


        public static string GetRepositoryDirectory(string proj = ".Forms", string subDir = "Ressources\\Logs")
        {
            // 1. Hol den vollen Assembly-Namen (z.B. "FormsSystemStatsWidget" oder "FormsSystemStatsWidget.Something")
            string? fullAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            if (string.IsNullOrEmpty(fullAssemblyName))
            {
                fullAssemblyName = AppDomain.CurrentDomain.FriendlyName;
            }

            // 2. Namespace bis zum ersten Punkt holen (z.B. "FormsSystemStatsWidget")
            int firstDot = fullAssemblyName.IndexOf('.');
            string baseNamespace = firstDot > 0 ? fullAssemblyName.Substring(0, firstDot) : fullAssemblyName;

            // 3. Den Wunschnamen des Ziel-Projektordners bauen (z.B. "FormsSystemStatsWidget.Forms")
            string targetProjectName = baseNamespace + proj;

            // Start-Verzeichnis (z.B. ...\bin\Release\win-x64\)
            DirectoryInfo? directory = new(AppDomain.CurrentDomain.BaseDirectory);

            while (directory != null)
            {
                // Option A: Wir stehen im Root (repos\alarmclock-kisser\) und der Zielordner ist ein direktes Unterverzeichnis
                string potentialTargetDir = Path.Combine(directory.FullName, targetProjectName);
                if (Directory.Exists(potentialTargetDir))
                {
                    return Path.Combine(potentialTargetDir, subDir);
                }

                // Option B: Wir sind durch das Hochwandern bereits direkt im Zielordner gelandet
                if (string.Equals(directory.Name, targetProjectName, StringComparison.OrdinalIgnoreCase))
                {
                    return Path.Combine(directory.FullName, subDir);
                }

                // Ein Level höher springen
                directory = directory.Parent;
            }

            // Fallback, falls nichts gefunden wurde
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, subDir);
        }


        // Public static Method to get all 'llama-server.exe' processes and return them
        public static List<Process> GetLlamaServerProcesses(int? port = null)
        {
            List<Process> llamaProcesses = [];
            try
            {
                Process[] allProcesses = Process.GetProcesses();
                foreach (Process proc in allProcesses)
                {
                    if (port.HasValue)
                    {
                        // Only include processes that have a network connection on the specified port
                        llamaProcesses.Add(proc);
                    }

                    try
                    {
                        if (string.Equals(proc.ProcessName, "llama-server", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(proc.MainModule?.FileName, "llama-server.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            llamaProcesses.Add(proc);
                        }
                    }
                    catch
                    {
                        // Ignore processes we can't access
                    }
                }
            }
            catch
            {
                // Handle any exceptions that may occur when retrieving processes
            }
            return llamaProcesses;
        }

        public static int? KillLlamaServerProcesses(IEnumerable<Process>? processes = null)
        {
            int killCount = 0;
            try
            {
                IEnumerable<Process> targetProcesses = processes ?? GetLlamaServerProcesses();
                foreach (Process proc in targetProcesses)
                {
                    try
                    {
                        proc.Kill();
                        killCount++;
                    }
                    catch
                    {
                        // Ignore processes we can't kill
                    }
                }
            }
            catch
            {
                // Handle any exceptions that may occur when retrieving or killing processes
            }
            return killCount;
        }

    }
}
