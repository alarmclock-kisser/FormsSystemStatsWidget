using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using LibreHardwareMonitor.Hardware;

namespace FormsSystemStatsWidget.Core
{
    [SupportedOSPlatform("windows")]
    public static class CpuStats
    {
        private static readonly PerformanceCounter[] _cpuCounters = CreateCpuCounters();
        private static readonly TimeSpan _samplingInterval = TimeSpan.FromMilliseconds(250);
        private static DateTime _lastSampleUtc = DateTime.MinValue;
        private static float[] _lastUsages = [];
        private static readonly Lock _sampleLock = new();
        private static readonly TimeSpan _temperatureSamplingInterval = TimeSpan.FromMilliseconds(500);
        private static readonly Lock _temperatureLock = new();
        private static readonly Computer _hardwareComputer = CreateHardwareComputer();
        private static DateTime _lastTemperatureSampleUtc = DateTime.MinValue;
        private static double? _lastAverageTemperatureCelsius;

        private static PerformanceCounter[] CreateCpuCounters()
        {
            int coreCount = Environment.ProcessorCount;
            var counters = new PerformanceCounter[coreCount];

            for (int i = 0; i < coreCount; i++)
            {
                counters[i] = new PerformanceCounter("Processor", "% Processor Time", i.ToString(), true);
                // erste Probe, damit der nächste Wert „richtig“ ist
                _ = counters[i].NextValue();
            }

            _lastUsages = new float[coreCount];
            return counters;
        }

        private static Computer CreateHardwareComputer()
        {
            var computer = new Computer
            {
                IsCpuEnabled = true,
                IsMotherboardEnabled = true
            };

            try
            {
                computer.Open();
            }
            catch
            {
                return computer;
            }

            return computer;
        }

        private static void EnsureHardwareComputerOpened()
        {
            try
            {
                if (_hardwareComputer.Hardware.Count == 0)
                {
                    _hardwareComputer.Open();
                }
            }
            catch
            {
            }
        }

        private static void UpdateHardwareRecursive(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware subHardware in hardware.SubHardware)
            {
                UpdateHardwareRecursive(subHardware);
            }
        }

        private static void CollectTemperatureSensorsRecursive(IHardware hardware, List<ISensor> sensors)
        {
            foreach (ISensor sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                {
                    sensors.Add(sensor);
                }
            }

            foreach (IHardware subHardware in hardware.SubHardware)
            {
                CollectTemperatureSensorsRecursive(subHardware, sensors);
            }
        }

        private static bool IsLikelyCpuTemperatureSensor(ISensor sensor)
        {
            if (sensor.Hardware.HardwareType == HardwareType.Cpu)
            {
                return true;
            }

            string sensorName = sensor.Name ?? string.Empty;
            return sensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Core", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Tdie", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Tctl", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// CPU-Auslastung pro logischem Prozessor (0.0f - 1.0f).
        /// Nicht-blockierend: liefert gecachte Werte, wenn Intervall noch nicht abgelaufen.
        /// </summary>
        public static Task<float[]> GetThreadUsagesAsync(CancellationToken cancellationToken = default)
        {
            lock (_sampleLock)
            {
                var now = DateTime.UtcNow;
                var elapsed = now - _lastSampleUtc;

                if (elapsed > _samplingInterval * 4)
                {
                    for (int i = 0; i < _cpuCounters.Length; i++)
                    {
                        _ = _cpuCounters[i].NextValue();
                    }
                    Thread.Sleep(_samplingInterval);
                }
                else if (elapsed < _samplingInterval && _lastUsages.Length == _cpuCounters.Length)
                {
                    return Task.FromResult((float[]) _lastUsages.Clone());
                }

                int coreCount = _cpuCounters.Length;
                var usages = new float[coreCount];

                for (int i = 0; i < coreCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    float percent = _cpuCounters[i].NextValue();
                    if (percent < 0f)
                    {
                        percent = 0f;
                    }

                    if (percent > 100f)
                    {
                        percent = 100f;
                    }

                    usages[i] = percent / 100f;
                }

                _lastUsages = usages;
                _lastSampleUtc = now;
                return Task.FromResult((float[]) usages.Clone());
            }
        }

        /// <summary>
        /// Sync-Wrapper, falls du irgendwo keine async-Methode aufrufen willst.
        /// </summary>
        public static float[] GetThreadUsages()
            => GetThreadUsagesAsync().GetAwaiter().GetResult();

        private static (int cols, int rows) CalculateCoreGridDimensions(int coreCount, int width, int height)
        {
            int count = Math.Max(1, coreCount);
            double targetAspectRatio = Math.Max(1, width) / (double) Math.Max(1, height);

            int bestCols = count;
            int bestRows = 1;
            double bestScore = double.MaxValue;

            for (int rows = 1; rows <= count; rows++)
            {
                int cols = (int) Math.Ceiling(count / (double) rows);
                int emptyCells = (cols * rows) - count;

                int normalizedCols = cols;
                int normalizedRows = rows;

                if (targetAspectRatio >= 1.0 && normalizedCols < normalizedRows)
                {
                    (normalizedCols, normalizedRows) = (normalizedRows, normalizedCols);
                }
                else if (targetAspectRatio < 1.0 && normalizedCols > normalizedRows)
                {
                    (normalizedCols, normalizedRows) = (normalizedRows, normalizedCols);
                }

                int balancePenalty = Math.Abs(normalizedCols - normalizedRows) * 10;
                double aspectPenalty = Math.Abs((normalizedCols / (double) normalizedRows) - targetAspectRatio);
                double score = (emptyCells * 25) + balancePenalty + aspectPenalty;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestCols = normalizedCols;
                    bestRows = normalizedRows;
                }
            }

            return (bestCols, bestRows);
        }

        /// <summary>
        /// Liefert die gemittelte CPU-Temperatur in °C aus Hardware-Sensoren (LibreHardwareMonitor).
        /// Gibt null zurück, wenn keine verwertbaren Temperatursensoren verfügbar sind.
        /// </summary>
        public static double? GetAverageCpuTemperatureCelsius()
        {
            lock (_temperatureLock)
            {
                DateTime nowUtc = DateTime.UtcNow;
                if ((nowUtc - _lastTemperatureSampleUtc) < _temperatureSamplingInterval)
                {
                    return _lastAverageTemperatureCelsius;
                }

                _lastTemperatureSampleUtc = nowUtc;

                var temperatureValues = new List<double>();
                var fallbackTemperatureValues = new List<double>();

                try
                {
                    EnsureHardwareComputerOpened();

                    foreach (IHardware hardware in _hardwareComputer.Hardware)
                    {
                        try
                        {
                            UpdateHardwareRecursive(hardware);

                            var sensors = new List<ISensor>();
                            CollectTemperatureSensorsRecursive(hardware, sensors);

                            foreach (ISensor sensor in sensors)
                            {
                                double value = sensor.Value!.Value;
                                if (value is <= -20d or >= 150d)
                                {
                                    continue;
                                }

                                fallbackTemperatureValues.Add(value);
                                if (IsLikelyCpuTemperatureSensor(sensor))
                                {
                                    temperatureValues.Add(value);
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                    _lastAverageTemperatureCelsius = null;
                    return _lastAverageTemperatureCelsius;
                }

                if (temperatureValues.Count > 0)
                {
                    _lastAverageTemperatureCelsius = temperatureValues.Average();
                }
                else
                {
                    _lastAverageTemperatureCelsius = fallbackTemperatureValues.Count > 0 ? fallbackTemperatureValues.Average() : null;
                }

                return _lastAverageTemperatureCelsius;
            }
        }

        /// <summary>
        /// Malt die CPU-Auslastung pro Kern als Bitmap. Async, da das Rendern bei vielen Kernen etwas dauern kann.
        /// </summary>
        public static Task<Bitmap> RenderCoresBitmapAsync(float[] usages, int width, int height, Color? backColor = null, Color? renderPercentagesColor = null, CancellationToken ct = default)
        {
            backColor ??= Color.White;
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                int count = Math.Max(1, usages?.Length ?? 1);
                var (cols, rows) = CalculateCoreGridDimensions(count, width, height);

                var bmp = new Bitmap(Math.Max(1, width), Math.Max(1, height));
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(backColor.Value);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

                    // Padding and cell sizes
                    int pad = 2;
                    int gridW = width - pad * (cols + 1);
                    int gridH = height - pad * (rows + 1);
                    if (gridW < cols)
                    {
                        gridW = cols;
                    }

                    if (gridH < rows)
                    {
                        gridH = rows;
                    }

                    int cellW = gridW / cols;
                    int cellH = gridH / rows;

                    using var borderPen = new Pen(Color.Black, 1f);
                    using var fillBrush = new SolidBrush(Color.FromArgb(64, 160, 255));
                    using var highBrush = new SolidBrush(Color.FromArgb(255, 96, 96));

                    for (int i = 0; i < count; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        int r = i / cols;
                        int c = i % cols;
                        int x = pad + c * (cellW + pad);
                        int y = pad + r * (cellH + pad);

                        // Outer rect
                        var rect = new Rectangle(x, y, cellW, cellH);
                        g.DrawRectangle(borderPen, rect);

                        // Fill proportionally from bottom based on usage
                        float u = usages?[i] ?? 0;
                        if (u < 0f)
                        {
                            u = 0f;
                        }

                        if (u > 1f)
                        {
                            u = 1f;
                        }

                        int filledH = (int) Math.Round(u * cellH);
                        if (filledH > 0)
                        {
                            var fillRect = new Rectangle(x + 1, y + cellH - filledH + 1, Math.Max(1, cellW - 2), Math.Max(1, filledH - 2));
                            // use red above 80%
                            var brush = u >= 0.8f ? highBrush : fillBrush;
                            g.FillRectangle(brush, fillRect);
                        }

                        // Optionally render the percentage text centered in the cell.
                        if (renderPercentagesColor.HasValue)
                        {
                            using var textBrush = new SolidBrush(renderPercentagesColor.Value);
                            // Percentage text (rounded integer percent)
                            string percentText = $"{Math.Round(u * 100f)}%";

                            // Determine dynamic font size so the text fits inside the cell.
                            // Start with a reasonable maximum relative to cell size and decrease until it fits or reaches a minimum size.
                            float maxFont = Math.Min(cellW, cellH) * 0.45f;
                            float fontSize = Math.Max(6f, maxFont);

                            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

                            // Measure and adjust font size. Use GraphicsUnit.Pixel for consistent measurements in pixels.
                            for (;;)
                            {
                                using var testFont = new Font(SystemFonts.DefaultFont.FontFamily, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                                var size = g.MeasureString(percentText, testFont);
                                // Add a small padding
                                if ((size.Width > cellW - 4 || size.Height > cellH - 4) && fontSize > 6f)
                                {
                                    fontSize -= 1f;
                                    continue;
                                }
                                break;
                            }

                            using var textFont = new Font(SystemFonts.DefaultFont.FontFamily, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                            g.DrawString(percentText, textFont, textBrush, rect, sf);
                        }
                    }
                }

                return bmp;
            }, ct);
        }

        // -------- Speicher (physisch) --------
        // Die Speicherabfragen sind sehr schnell und blockieren nicht nennenswert.
        // Async bringt hier praktisch nichts, daher bleiben sie synchron.

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        private static MEMORYSTATUSEX GetMemoryStatus()
        {
            var status = new MEMORYSTATUSEX
            {
                dwLength = (uint) Marshal.SizeOf<MEMORYSTATUSEX>()
            };

            if (!GlobalMemoryStatusEx(ref status))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return status;
        }

        /// <summary>
        /// Gesamter physischer Speicher in BYTES.
        /// </summary>
        public static long GetTotalMemoryBytes()
        {
            var status = GetMemoryStatus();
            return (long) status.ullTotalPhys;
        }

        /// <summary>
        /// Verwendeter physischer Speicher in BYTES.
        /// </summary>
        public static long GetUsedMemoryBytes()
        {
            var status = GetMemoryStatus();
            ulong used = status.ullTotalPhys - status.ullAvailPhys;
            return (long) used;
        }
    }

}

