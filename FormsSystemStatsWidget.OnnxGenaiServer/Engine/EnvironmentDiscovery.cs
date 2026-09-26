using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FormsSystemStatsWidget.OnnxGenaiServer.Configuration;

namespace FormsSystemStatsWidget.OnnxGenaiServer.Engine;

/// <summary>
/// CUDA/ORT Discovery (R14, R61).
/// Ermittelt CUDA-Geräte, ORT-Version, .NET-Version, OS, CPU, RAM.
/// </summary>
public static class EnvironmentDiscovery
{
    /// <summary>
    /// Entdeckt CUDA-Geräte via nvidia-smi.
    /// </summary>
    public static CudaDiscoveryResult DiscoverCuda()
    {
        var result = new CudaDiscoveryResult
        {
            IsAvailable = false,
            DeviceCount = 0,
            Devices = []
        };

        try
        {
            var nvidiaSmi = FindNvidiaSmi();
            if (nvidiaSmi is null)
            {
                result.Warnings.Add("nvidia-smi nicht gefunden — CUDA nicht verfügbar");
                return result;
            }

            // nvidia-smi -L für Geräte-Liste
            var listOutput = RunCommand(nvidiaSmi, "-L");
            if (string.IsNullOrWhiteSpace(listOutput))
            {
                result.Warnings.Add("nvidia-smi -L lieferte keine Ausgabe");
                return result;
            }

            var lines = listOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("GPU", StringComparison.OrdinalIgnoreCase))
                {
                    var device = ParseGpuLine(line);
                    if (device is not null)
                    {
                        result.Devices.Add(device);
                    }
                }
            }

            result.DeviceCount = result.Devices.Count;
            result.IsAvailable = result.DeviceCount > 0;

            // nvidia-smi --query-gpu=driver_version --format=csv,noheader
            var driverOutput = RunCommand(nvidiaSmi, "--query-gpu=driver_version --format=csv,noheader");
            if (!string.IsNullOrWhiteSpace(driverOutput))
            {
                result.DriverVersion = driverOutput.Split('\n')[0].Trim();
            }

            // nvidia-smi --query-gpu=compute_cap --format=csv,noheader
            var capOutput = RunCommand(nvidiaSmi, "--query-gpu=compute_cap --format=csv,noheader");
            if (!string.IsNullOrWhiteSpace(capOutput))
            {
                var caps = capOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                for (int i = 0; i < Math.Min(caps.Length, result.Devices.Count); i++)
                {
                    result.Devices[i].ComputeCapability = caps[i];
                }
            }

            // nvidia-smi --query-gpu=memory.total,memory.used,memory.free --format=csv,noheader
            var memOutput = RunCommand(nvidiaSmi, "--query-gpu=memory.total,memory.used,memory.free --format=csv,noheader");
            if (!string.IsNullOrWhiteSpace(memOutput))
            {
                var mems = memOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                for (int i = 0; i < Math.Min(mems.Length, result.Devices.Count); i++)
                {
                    var parts = mems[i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length >= 3)
                    {
                        result.Devices[i].VramTotalMb = ParseMb(parts[0]);
                        result.Devices[i].VramUsedMb = ParseMb(parts[1]);
                        result.Devices[i].VramFreeMb = ParseMb(parts[2]);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"CUDA Discovery fehlgeschlagen: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Ermittelt die ONNX Runtime Version (falls verfügbar).
    /// </summary>
    public static string? DiscoverOrtVersion()
    {
        try
        {
            // Versuche, die ORT-Assembly zu finden
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Microsoft.ML.OnnxRuntime");
            if (asm is not null)
            {
                return asm.GetName().Version?.ToString();
            }

            // Fallback: Suche in App-Verzeichnis
            var appDir = AppContext.BaseDirectory;
            var ortFiles = Directory.EnumerateFiles(appDir, "Microsoft.ML.OnnxRuntime*.dll", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(appDir, "onnxruntime*.dll", SearchOption.TopDirectoryOnly))
                .ToList();
            if (ortFiles.Count > 0)
            {
                var fileInfo = new FileInfo(ortFiles[0]);
                return fileInfo.LastWriteTime.ToString("yyyy-MM-dd");
            }
        }
        catch
        {
            // ORT nicht verfügbar
        }
        return null;
    }

    /// <summary>
    /// Ermittelt die .NET Runtime Version.
    /// </summary>
    public static string GetDotNetVersion()
    {
        return Environment.Version.ToString();
    }

    /// <summary>
    /// Ermittelt OS-Informationen.
    /// </summary>
    public static string GetOsInfo()
    {
        return RuntimeInformation.OSDescription;
    }

    /// <summary>
    /// Ermittelt CPU-Informationen.
    /// </summary>
    public static string GetCpuInfo()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var result = RunCommand("wmic", "cpu get Name /value");
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var nameLine = lines.FirstOrDefault(l => l.StartsWith("Name=", StringComparison.OrdinalIgnoreCase));
                if (nameLine is not null)
                {
                    return nameLine["Name=".Length..].Trim();
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                var result = RunCommand("lscpu", "");
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var modelLine = lines.FirstOrDefault(l => l.StartsWith("Model name:", StringComparison.OrdinalIgnoreCase));
                if (modelLine is not null)
                {
                    return modelLine["Model name:".Length..].Trim();
                }
            }
        }
        catch
        {
            // CPU-Info nicht verfügbar
        }
        return "Unknown";
    }

    /// <summary>
    /// Ermittelt RAM-Größe in MB.
    /// </summary>
    public static long GetRamMb()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var result = RunCommand("wmic", "computer system get TotalPhysicalMemory /value");
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var memLine = lines.FirstOrDefault(l => l.StartsWith("TotalPhysicalMemory=", StringComparison.OrdinalIgnoreCase));
                if (memLine is not null)
                {
                    var kb = long.Parse(memLine["TotalPhysicalMemory=".Length..].Trim());
                    return kb / 1024;
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                var result = RunCommand("free", "-m");
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var memLine = lines.FirstOrDefault(l => l.StartsWith("Mem:", StringComparison.OrdinalIgnoreCase));
                if (memLine is not null)
                {
                    var parts = memLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        return long.Parse(parts[1]);
                    }
                }
            }
        }
        catch
        {
            // RAM-Info nicht verfügbar
        }
        return 0;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string? FindNvidiaSmi()
    {
        var candidates = new[]
        {
            @"C:\Windows\System32\nvidia-smi.exe",
            @"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe",
            "/usr/bin/nvidia-smi",
            "/usr/local/bin/nvidia-smi"
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // PATH-Suche
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), "nvidia-smi.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string RunCommand(string exe, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null) return string.Empty;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10000);
            return output;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static GpuDeviceInfo? ParseGpuLine(string line)
    {
        // Format: "GPU 0: NVIDIA GeForce RTX 4090 (UUID: GPU-...)"
        var match = System.Text.RegularExpressions.Regex.Match(line, @"^GPU\s+(\d+):\s+(.+?)\s*\(UUID:");
        if (match.Success)
        {
            return new GpuDeviceInfo
            {
                Id = int.Parse(match.Groups[1].Value),
                Name = match.Groups[2].Value.Trim()
            };
        }

        // Fallback: "GPU 0: NVIDIA GeForce RTX 4090"
        var match2 = System.Text.RegularExpressions.Regex.Match(line, @"^GPU\s+(\d+):\s+(.+)$");
        if (match2.Success)
        {
            return new GpuDeviceInfo
            {
                Id = int.Parse(match2.Groups[1].Value),
                Name = match2.Groups[2].Value.Trim()
            };
        }

        return null;
    }

    private static long ParseMb(string text)
    {
        // Format: "24564 MiB"
        var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)");
        if (match.Success)
        {
            return long.Parse(match.Groups[1].Value);
        }
        return 0;
    }
}

/// <summary>
/// CUDA Discovery Ergebnis.
/// </summary>
public sealed class CudaDiscoveryResult
{
    public bool IsAvailable { get; set; }
    public int DeviceCount { get; set; }
    public string? DriverVersion { get; set; }
    public List<GpuDeviceInfo> Devices { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// GPU-Device-Information.
/// </summary>
public sealed class GpuDeviceInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ComputeCapability { get; set; }
    public long VramTotalMb { get; set; }
    public long VramUsedMb { get; set; }
    public long VramFreeMb { get; set; }
}
