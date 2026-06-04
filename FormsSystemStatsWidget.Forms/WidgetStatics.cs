using System;
using System.Collections.Generic;
using System.Management;
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

            infoBuilder.AppendLine("Environment:");
            infoBuilder.AppendLine($"  OS: {RuntimeInformation.OSDescription}");
            infoBuilder.AppendLine($"  Runtime: {RuntimeInformation.FrameworkDescription}");
            infoBuilder.AppendLine($"  Process: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}, OS64: {(Environment.Is64BitOperatingSystem ? "Yes" : "No")}");
            infoBuilder.AppendLine($"  Machine: {Environment.MachineName}");
            infoBuilder.AppendLine($"  Logical CPU Cores: {Environment.ProcessorCount}");

            try
            {
                DriveInfo driveInfo = new(rootPath);
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
                using ManagementObjectSearcher logicalDiskSearcher = new($"SELECT * FROM Win32_LogicalDisk WHERE DeviceID='{driveId}'");
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

                HashSet<string> diskDeviceIds = new(StringComparer.OrdinalIgnoreCase);
                List<ManagementObject> diskDrives = new();

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
    }
}
