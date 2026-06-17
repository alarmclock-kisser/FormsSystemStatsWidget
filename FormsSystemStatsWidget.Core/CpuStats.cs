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
using System.Management;
using LibreHardwareMonitor.Hardware;
using OhmHardware = OpenHardwareMonitor.Hardware;

namespace FormsSystemStatsWidget.Core
{
    [SupportedOSPlatform("windows")]
    public static class CpuStats
    {
        /// <summary>
        /// Zusammengefasste CPU-Telemetrie aus den verfügbaren Hardware-Sensoren.
        /// </summary>
        /// <param name="AverageTemperatureCelsius">Gemittelte CPU-Temperatur in °C, falls verfügbar.</param>
        /// <param name="PackagePowerWatts">CPU-Leistungsaufnahme in Watt, falls verfügbar.</param>
        public sealed record CpuTelemetrySnapshot(double? AverageTemperatureCelsius, double? PackagePowerWatts);

        private static readonly PerformanceCounter[] _cpuCounters = CreateCpuCounters();
        private static readonly TimeSpan _samplingInterval = TimeSpan.FromMilliseconds(250);
        private static DateTime _lastSampleUtc = DateTime.MinValue;
        private static float[] _lastUsages = [];
        private static readonly Lock _sampleLock = new();
        private static readonly TimeSpan _temperatureSamplingInterval = TimeSpan.FromMilliseconds(500);
        private static readonly Lock _temperatureLock = new();
        private static readonly Computer _hardwareComputer = CreateHardwareComputer();
        private static DateTime _lastTemperatureSampleUtc = DateTime.MinValue;
        private static CpuTelemetrySnapshot _lastCpuTelemetrySnapshot = new(null, null);
        private static readonly Lock _openHardwareMonitorLock = new();
        private static readonly TimeSpan _openHardwareMonitorSamplingInterval = TimeSpan.FromMilliseconds(800);
        private static DateTime _lastOpenHardwareMonitorSampleUtc = DateTime.MinValue;
        private static double? _lastOpenHardwareMonitorPackagePowerWatts;
        private static OhmHardware.Computer? _openHardwareMonitorComputer;
        private static bool _openHardwareMonitorOpened;
        private static bool _openHardwareMonitorUnavailable;
        private static double? _lastCpuEnergySensorReading;
        private static DateTime _lastCpuEnergySampleUtc = DateTime.MinValue;
        private static readonly Lock _processSamplingLock = new();
        private static DateTime _lastProcessSampleUtc = DateTime.MinValue;
        private static Dictionary<int, TimeSpan> _lastProcessCpuTimes = [];

        private static bool EnsureOpenHardwareMonitorOpened()
        {
            if (_openHardwareMonitorUnavailable)
            {
                return false;
            }

            try
            {
                if (_openHardwareMonitorComputer == null)
                {
                    _openHardwareMonitorComputer = new OhmHardware.Computer
                    {
                        IsCpuEnabled = true
                    };
                }

                if (!_openHardwareMonitorOpened)
                {
                    _openHardwareMonitorComputer.Open(false);
                    _openHardwareMonitorOpened = true;
                }

                return true;
            }
            catch
            {
                _openHardwareMonitorUnavailable = true;
                _lastOpenHardwareMonitorPackagePowerWatts = null;
                return false;
            }
        }

        private static (double? temperatureCelsius, double? packagePowerWatts) TryGetCpuTelemetryFromOpenHardwareMonitor()
        {
            lock (_openHardwareMonitorLock)
            {
                DateTime nowUtc = DateTime.UtcNow;
                if ((nowUtc - _lastOpenHardwareMonitorSampleUtc) < _openHardwareMonitorSamplingInterval)
                {
                    return (null, _lastOpenHardwareMonitorPackagePowerWatts);
                }

                _lastOpenHardwareMonitorSampleUtc = nowUtc;

                if (!EnsureOpenHardwareMonitorOpened() || _openHardwareMonitorComputer == null)
                {
                    _lastOpenHardwareMonitorPackagePowerWatts = null;
                    return (null, null);
                }

                List<double> preferredTemperatureValues = [];
                List<double> fallbackTemperatureValues = [];
                List<double> preferredPowerValues = [];
                List<double> fallbackPowerValues = [];

                try
                {
                    foreach (OhmHardware.IHardware hardware in _openHardwareMonitorComputer.Hardware)
                    {
                        CollectOpenHardwareMonitorSensorsRecursive(
                            hardware,
                            preferredTemperatureValues,
                            fallbackTemperatureValues,
                            preferredPowerValues,
                            fallbackPowerValues);
                    }
                }
                catch
                {
                    _lastOpenHardwareMonitorPackagePowerWatts = null;
                    return (null, null);
                }

                double? temperatureCelsius = preferredTemperatureValues.Count > 0
                    ? preferredTemperatureValues.Average()
                    : fallbackTemperatureValues.Count > 0
                        ? fallbackTemperatureValues.Average()
                        : null;

                _lastOpenHardwareMonitorPackagePowerWatts = preferredPowerValues.Count > 0
                    ? preferredPowerValues.Average()
                    : fallbackPowerValues.Count > 0
                        ? fallbackPowerValues.Average()
                        : null;

                return (temperatureCelsius, _lastOpenHardwareMonitorPackagePowerWatts);
            }
        }

        private static void CollectOpenHardwareMonitorSensorsRecursive(
            OhmHardware.IHardware hardware,
            List<double> preferredTemperatureValues,
            List<double> fallbackTemperatureValues,
            List<double> preferredPowerValues,
            List<double> fallbackPowerValues)
        {
            hardware.Update();

            CollectCpuPowerSensors(hardware, preferredPowerValues, fallbackPowerValues);
            CollectCpuTemperatureSensors(hardware, preferredTemperatureValues, fallbackTemperatureValues);

            foreach (OhmHardware.IHardware subHardware in hardware.SubHardware)
            {
                CollectOpenHardwareMonitorSensorsRecursive(
                    subHardware,
                    preferredTemperatureValues,
                    fallbackTemperatureValues,
                    preferredPowerValues,
                    fallbackPowerValues);
            }
        }

        private static void CollectCpuPowerSensors(
            OhmHardware.IHardware hardware,
            List<double> preferredPowerValues,
            List<double> fallbackPowerValues)
        {
            foreach (OhmHardware.ISensor sensor in hardware.Sensors)
            {
                if (sensor.SensorType != OhmHardware.SensorType.Power || !sensor.Value.HasValue)
                {
                    continue;
                }

                double value = sensor.Value.Value;
                if (value is <= 0d or >= 500d)
                {
                    continue;
                }

                string sensorName = sensor.Name ?? string.Empty;
                if (!IsCpuScopedPowerSensor(sensor, sensorName))
                {
                    continue;
                }

                if (IsPreferredPowerSensor(sensorName))
                {
                    preferredPowerValues.Add(value);
                }
                else
                {
                    fallbackPowerValues.Add(value);
                }
            }
        }

        private static void CollectCpuTemperatureSensors(
            OhmHardware.IHardware hardware,
            List<double> preferredTemperatureValues,
            List<double> fallbackTemperatureValues)
        {
            foreach (OhmHardware.ISensor sensor in hardware.Sensors)
            {
                if (sensor.SensorType != OhmHardware.SensorType.Temperature || !sensor.Value.HasValue)
                {
                    continue;
                }

                double value = sensor.Value.Value;
                if (value is <= 0d or >= 130d)
                {
                    continue;
                }

                string sensorName = sensor.Name ?? string.Empty;
                if (!IsCpuScopedTemperatureSensor(sensor, sensorName))
                {
                    continue;
                }

                if (IsPreferredTemperatureSensor(sensorName))
                {
                    preferredTemperatureValues.Add(value);
                }
                else
                {
                    fallbackTemperatureValues.Add(value);
                }
            }
        }

        private static bool IsCpuScopedPowerSensor(OhmHardware.ISensor sensor, string sensorName)
        {
            string hardwareName = sensor.Hardware.Name ?? string.Empty;
            return sensor.Hardware.HardwareType == OhmHardware.HardwareType.Cpu
                   || sensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("IA", StringComparison.OrdinalIgnoreCase)
                   || hardwareName.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                   || hardwareName.Contains("Intel", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPreferredPowerSensor(string sensorName)
        {
            return sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("CPU Package", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("CPU Total", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("CPU Cores", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("IA Cores", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCpuScopedTemperatureSensor(OhmHardware.ISensor sensor, string sensorName)
        {
            string hardwareName = sensor.Hardware.Name ?? string.Empty;
            return sensor.Hardware.HardwareType == OhmHardware.HardwareType.Cpu
                   || sensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Core", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Tdie", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
                   || hardwareName.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                   || hardwareName.Contains("Intel", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPreferredTemperatureSensor(string sensorName)
        {
            return sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("CPU Package", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Core Max", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Tdie", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Tctl", StringComparison.OrdinalIgnoreCase);
        }

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
                IsMotherboardEnabled = true,
                IsControllerEnabled = true
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

        private static void CollectCpuRelevantSensorsRecursive(IHardware hardware, List<ISensor> sensors)
        {
            foreach (ISensor sensor in hardware.Sensors)
            {
                if ((sensor.SensorType == SensorType.Temperature
                    || sensor.SensorType == SensorType.Power
                    || sensor.SensorType == SensorType.Voltage
                    || sensor.SensorType == SensorType.Current
                    || sensor.SensorType == SensorType.Energy) && sensor.Value.HasValue)
                {
                    sensors.Add(sensor);
                }
            }

            foreach (IHardware subHardware in hardware.SubHardware)
            {
                CollectCpuRelevantSensorsRecursive(subHardware, sensors);
            }
        }

        private static bool IsLikelyCpuTemperatureSensor(ISensor sensor)
        {
            if (sensor.Hardware.HardwareType == HardwareType.Cpu)
            {
                return true;
            }

            string sensorName = sensor.Name ?? string.Empty;
            string hardwareName = sensor.Hardware.Name ?? string.Empty;
            return sensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Core", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("CCD", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Tdie", StringComparison.OrdinalIgnoreCase)
                    || sensorName.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
                    || hardwareName.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                    || hardwareName.Contains("Package", StringComparison.OrdinalIgnoreCase)
                    || hardwareName.Contains("Ryzen", StringComparison.OrdinalIgnoreCase)
                    || hardwareName.Contains("Core", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPreferredCpuTemperatureSensor(ISensor sensor)
        {
            string sensorName = sensor.Name ?? string.Empty;
            return sensor.Hardware.HardwareType == HardwareType.Cpu
                   || sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("CPU Package", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Tdie", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Tctl", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLikelyCpuPowerSensor(ISensor sensor)
        {
            if (sensor.SensorType != SensorType.Power)
            {
                return false;
            }

            if (sensor.Hardware.HardwareType == HardwareType.Cpu)
            {
                return true;
            }

            string sensorName = sensor.Name ?? string.Empty;
            string hardwareName = sensor.Hardware.Name ?? string.Empty;
            return sensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("PPT", StringComparison.OrdinalIgnoreCase)
                    || sensorName.Contains("SoC", StringComparison.OrdinalIgnoreCase)
                    || sensorName.Contains("IA Cores", StringComparison.OrdinalIgnoreCase)
                    || sensorName.Contains("Processor", StringComparison.OrdinalIgnoreCase)
                    || hardwareName.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                    || hardwareName.Contains("Package", StringComparison.OrdinalIgnoreCase)
                    || hardwareName.Contains("VRM", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPreferredCpuPowerSensor(ISensor sensor)
        {
            string sensorName = sensor.Name ?? string.Empty;
            return sensor.Hardware.HardwareType == HardwareType.Cpu
                   || sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("CPU Package", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("PPT", StringComparison.OrdinalIgnoreCase)
                   || sensorName.Contains("IA Cores", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLikelyCpuVoltageSensor(ISensor sensor)
        {
            if (sensor.SensorType != SensorType.Voltage)
            {
                return false;
            }

            string sensorName = sensor.Name ?? string.Empty;
            string hardwareName = sensor.Hardware.Name ?? string.Empty;
            return sensor.Hardware.HardwareType == HardwareType.Cpu
                || sensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                || sensorName.Contains("Core", StringComparison.OrdinalIgnoreCase)
                || sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase)
                || sensorName.Contains("IA", StringComparison.OrdinalIgnoreCase)
                || sensorName.Contains("Vcore", StringComparison.OrdinalIgnoreCase)
                || hardwareName.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                || hardwareName.Contains("VRM", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLikelyCpuCurrentSensor(ISensor sensor)
        {
            if (sensor.SensorType != SensorType.Current)
            {
                return false;
            }

            string sensorName = sensor.Name ?? string.Empty;
            string hardwareName = sensor.Hardware.Name ?? string.Empty;
            return sensor.Hardware.HardwareType == HardwareType.Cpu
                || sensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                || sensorName.Contains("Core", StringComparison.OrdinalIgnoreCase)
                || sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase)
                || sensorName.Contains("IA", StringComparison.OrdinalIgnoreCase)
                || hardwareName.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                || hardwareName.Contains("VRM", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLikelyCpuEnergySensor(ISensor sensor)
        {
            if (sensor.SensorType != SensorType.Energy)
            {
                return false;
            }

            string sensorName = sensor.Name ?? string.Empty;
            string hardwareName = sensor.Hardware.Name ?? string.Empty;
            return sensor.Hardware.HardwareType == HardwareType.Cpu
                || sensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                || sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase)
                || sensorName.Contains("IA", StringComparison.OrdinalIgnoreCase)
                || sensorName.Contains("Core", StringComparison.OrdinalIgnoreCase)
                || hardwareName.Contains("CPU", StringComparison.OrdinalIgnoreCase);
        }

        private static double? TryCalculatePowerFromVoltageAndCurrent(
            List<double> preferredVoltages,
            List<double> fallbackVoltages,
            List<double> preferredCurrents,
            List<double> fallbackCurrents)
        {
            double? voltage = preferredVoltages.Count > 0
                ? preferredVoltages.Average()
                : fallbackVoltages.Count > 0
                    ? fallbackVoltages.Average()
                    : null;

            double? current = preferredCurrents.Count > 0
                ? preferredCurrents.Average()
                : fallbackCurrents.Count > 0
                    ? fallbackCurrents.Average()
                    : null;

            if (!voltage.HasValue || !current.HasValue)
            {
                return null;
            }

            double watts = voltage.Value * current.Value;
            return watts is > 0d and < 500d ? watts : null;
        }

        private static double? TryCalculatePowerFromEnergySensor(double currentEnergyReading, DateTime nowUtc)
        {
            if (!_lastCpuEnergySensorReading.HasValue || _lastCpuEnergySampleUtc == DateTime.MinValue)
            {
                _lastCpuEnergySensorReading = currentEnergyReading;
                _lastCpuEnergySampleUtc = nowUtc;
                return null;
            }

            double previousEnergyReading = _lastCpuEnergySensorReading.Value;
            DateTime previousTimestamp = _lastCpuEnergySampleUtc;
            _lastCpuEnergySensorReading = currentEnergyReading;
            _lastCpuEnergySampleUtc = nowUtc;

            double delta = currentEnergyReading - previousEnergyReading;
            double elapsedSeconds = (nowUtc - previousTimestamp).TotalSeconds;

            if (delta <= 0d || elapsedSeconds < 0.2d)
            {
                return null;
            }

            double[] candidates =
            [
                delta / elapsedSeconds,
                (delta * 3600d) / elapsedSeconds,
                (delta * 3.6d) / elapsedSeconds
            ];

            foreach (double candidate in candidates)
            {
                if (candidate is > 0d and < 500d)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static double? TryGetCpuTemperatureFromWmi()
        {
            try
            {
                using ManagementObjectSearcher searcher = new("root\\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
                using ManagementObjectCollection results = searcher.Get();

                List<double> temperatureValues = [];
                foreach (ManagementObject result in results.Cast<ManagementObject>())
                {
                    try
                    {
                        if (result["CurrentTemperature"] is ushort rawValue && rawValue > 0)
                        {
                            double celsius = (rawValue / 10d) - 273.15d;
                            if (celsius is > 0d and < 150d)
                            {
                                temperatureValues.Add(celsius);
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                return temperatureValues.Count > 0 ? temperatureValues.Average() : null;
            }
            catch
            {
                return null;
            }
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

        /// <summary>
        /// Liefert die Prozesse mit der höchsten CPU-Last seit der letzten Probe.
        /// </summary>
        /// <param name="maxCount">Maximale Anzahl der zurückgegebenen Prozesse.</param>
        /// <returns>Liste der Prozesse mit CPU-Prozentwerten, absteigend sortiert.</returns>
        public static IReadOnlyList<(string processName, double cpuPercent)> GetTopCpuProcesses(int maxCount = 4)
        {
            lock (_processSamplingLock)
            {
                DateTime nowUtc = DateTime.UtcNow;
                Process[] processes;

                try
                {
                    processes = Process.GetProcesses();
                }
                catch
                {
                    return [];
                }

                try
                {
                    Dictionary<int, TimeSpan> currentProcessCpuTimes = SnapshotProcessCpuTimes(processes);
                    List<(string processName, double cpuPercent)> processUsages =
                        CalculateProcessUsages(processes, currentProcessCpuTimes, nowUtc);

                    _lastProcessCpuTimes = currentProcessCpuTimes;
                    _lastProcessSampleUtc = nowUtc;

                    return processUsages
                        .OrderByDescending(entry => entry.cpuPercent)
                        .ThenBy(entry => entry.processName, StringComparer.OrdinalIgnoreCase)
                        .Take(Math.Max(1, maxCount))
                        .ToArray();
                }
                finally
                {
                    foreach (Process process in processes)
                    {
                        process.Dispose();
                    }
                }
            }
        }

        private static Dictionary<int, TimeSpan> SnapshotProcessCpuTimes(Process[] processes)
        {
            Dictionary<int, TimeSpan> currentProcessCpuTimes = new(processes.Length);

            foreach (Process process in processes)
            {
                try
                {
                    currentProcessCpuTimes[process.Id] = process.TotalProcessorTime;
                }
                catch
                {
                }
            }

            return currentProcessCpuTimes;
        }

        private static List<(string processName, double cpuPercent)> CalculateProcessUsages(
            Process[] processes,
            Dictionary<int, TimeSpan> currentProcessCpuTimes,
            DateTime nowUtc)
        {
            List<(string processName, double cpuPercent)> processUsages = [];

            if (_lastProcessSampleUtc == DateTime.MinValue)
            {
                return processUsages;
            }

            double elapsedSeconds = (nowUtc - _lastProcessSampleUtc).TotalSeconds;
            if (elapsedSeconds <= 0.05d)
            {
                return processUsages;
            }

            foreach (Process process in processes)
            {
                TryAddProcessUsage(process, currentProcessCpuTimes, elapsedSeconds, processUsages);
            }

            return processUsages;
        }

        private static void TryAddProcessUsage(
            Process process,
            Dictionary<int, TimeSpan> currentProcessCpuTimes,
            double elapsedSeconds,
            List<(string processName, double cpuPercent)> processUsages)
        {
            try
            {
                if (!currentProcessCpuTimes.TryGetValue(process.Id, out TimeSpan currentCpuTime)
                    || !_lastProcessCpuTimes.TryGetValue(process.Id, out TimeSpan previousCpuTime))
                {
                    return;
                }

                double cpuTimeDeltaSeconds = (currentCpuTime - previousCpuTime).TotalSeconds;
                if (cpuTimeDeltaSeconds <= 0)
                {
                    return;
                }

                double cpuPercent = (cpuTimeDeltaSeconds / (elapsedSeconds * Environment.ProcessorCount)) * 100d;
                if (cpuPercent < 0.05d)
                {
                    return;
                }

                string processName = string.IsNullOrWhiteSpace(process.ProcessName) ? "n/a" : process.ProcessName;
                processUsages.Add((processName, Math.Min(cpuPercent, 100d)));
            }
            catch
            {
            }
        }

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
            return GetCpuTelemetrySnapshot().AverageTemperatureCelsius;
        }

        /// <summary>
        /// Liefert die aktuell verfügbare CPU-Leistungsaufnahme in Watt.
        /// </summary>
        public static double? GetCpuPackagePowerWatts()
        {
            return GetCpuTelemetrySnapshot().PackagePowerWatts;
        }

        /// <summary>
        /// Liefert eine gemeinsame CPU-Telemetrieprobe für Temperatur und Leistungsaufnahme.
        /// </summary>
        public static CpuTelemetrySnapshot GetCpuTelemetrySnapshot()
        {
            lock (_temperatureLock)
            {
                DateTime nowUtc = DateTime.UtcNow;
                if ((nowUtc - _lastTemperatureSampleUtc) < _temperatureSamplingInterval)
                {
                    return _lastCpuTelemetrySnapshot;
                }

                _lastTemperatureSampleUtc = nowUtc;

                var buckets = new CpuSensorBuckets();
                if (!TryCollectCpuSensorBuckets(buckets))
                {
                    _lastCpuTelemetrySnapshot = new CpuTelemetrySnapshot(null, null);
                    return _lastCpuTelemetrySnapshot;
                }

                double? averageTemperatureCelsius = AveragePreferredOrFallback(buckets.PreferredTemperature, buckets.FallbackTemperature);
                averageTemperatureCelsius ??= TryGetCpuTemperatureFromWmi();

                double? packagePowerWatts = ResolvePackagePower(buckets, nowUtc);

                if (!averageTemperatureCelsius.HasValue || !packagePowerWatts.HasValue)
                {
                    (double? ohmTemperatureCelsius, double? ohmPackagePowerWatts) = TryGetCpuTelemetryFromOpenHardwareMonitor();
                    averageTemperatureCelsius ??= ohmTemperatureCelsius;
                    packagePowerWatts ??= ohmPackagePowerWatts;
                }

                _lastCpuTelemetrySnapshot = new CpuTelemetrySnapshot(averageTemperatureCelsius, packagePowerWatts);
                return _lastCpuTelemetrySnapshot;
            }
        }

        private static bool TryCollectCpuSensorBuckets(CpuSensorBuckets buckets)
        {
            try
            {
                EnsureHardwareComputerOpened();

                foreach (IHardware hardware in _hardwareComputer.Hardware)
                {
                    try
                    {
                        UpdateHardwareRecursive(hardware);

                        List<ISensor> sensors = [];
                        CollectCpuRelevantSensorsRecursive(hardware, sensors);

                        foreach (ISensor sensor in sensors)
                        {
                            ClassifyCpuSensor(sensor, buckets);
                        }
                    }
                    catch
                    {
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ClassifyCpuSensor(ISensor sensor, CpuSensorBuckets buckets)
        {
            double value = sensor.Value!.Value;
            switch (sensor.SensorType)
            {
                case SensorType.Temperature:
                    if (value is <= 0d or >= 150d)
                    {
                        return;
                    }

                    AddSensorValue(buckets.FallbackTemperature, buckets.PreferredTemperature, value, IsLikelyCpuTemperatureSensor(sensor), IsPreferredCpuTemperatureSensor(sensor));
                    break;
                case SensorType.Power:
                    if (value is <= 0d or >= 500d)
                    {
                        return;
                    }

                    AddSensorValue(buckets.FallbackPower, buckets.PreferredPower, value, IsLikelyCpuPowerSensor(sensor), IsPreferredCpuPowerSensor(sensor));
                    break;
                case SensorType.Voltage:
                    if (value is <= 0d or >= 3d)
                    {
                        return;
                    }

                    AddSensorValue(buckets.FallbackVoltage, buckets.PreferredVoltage, value, IsLikelyCpuVoltageSensor(sensor), IsCpuPackageSensor(sensor));
                    break;
                case SensorType.Current:
                    if (value is <= 0d or >= 250d)
                    {
                        return;
                    }

                    AddSensorValue(buckets.FallbackCurrent, buckets.PreferredCurrent, value, IsLikelyCpuCurrentSensor(sensor), IsCpuPackageSensor(sensor));
                    break;
                case SensorType.Energy:
                    if (value <= 0d)
                    {
                        return;
                    }

                    AddSensorValue(buckets.FallbackEnergy, buckets.PreferredEnergy, value, IsLikelyCpuEnergySensor(sensor), IsCpuPackageSensor(sensor));
                    break;
                default:
                    break;
            }
        }

        private static void AddSensorValue(List<double> fallbackValues, List<double> preferredValues, double value, bool isLikely, bool isPreferred)
        {
            if (!isLikely)
            {
                return;
            }

            fallbackValues.Add(value);
            if (isPreferred)
            {
                preferredValues.Add(value);
            }
        }

        private static bool IsCpuPackageSensor(ISensor sensor)
        {
            return sensor.Hardware.HardwareType == HardwareType.Cpu
                || (sensor.Name ?? string.Empty).Contains("Package", StringComparison.OrdinalIgnoreCase);
        }

        private static double? AveragePreferredOrFallback(List<double> preferredValues, List<double> fallbackValues)
        {
            if (preferredValues.Count > 0)
            {
                return preferredValues.Average();
            }

            return fallbackValues.Count > 0 ? fallbackValues.Average() : null;
        }

        private static double? ResolvePackagePower(CpuSensorBuckets buckets, DateTime nowUtc)
        {
            double? packagePowerWatts = AveragePreferredOrFallback(buckets.PreferredPower, buckets.FallbackPower);

            packagePowerWatts ??= TryCalculatePowerFromVoltageAndCurrent(
                buckets.PreferredVoltage,
                buckets.FallbackVoltage,
                buckets.PreferredCurrent,
                buckets.FallbackCurrent);

            if (!packagePowerWatts.HasValue)
            {
                double? energyValue = AveragePreferredOrFallback(buckets.PreferredEnergy, buckets.FallbackEnergy);
                if (energyValue.HasValue)
                {
                    packagePowerWatts = TryCalculatePowerFromEnergySensor(energyValue.Value, nowUtc);
                }
            }

            return packagePowerWatts;
        }

        private sealed class CpuSensorBuckets
        {
            public List<double> PreferredTemperature { get; } = [];
            public List<double> FallbackTemperature { get; } = [];
            public List<double> PreferredPower { get; } = [];
            public List<double> FallbackPower { get; } = [];
            public List<double> PreferredVoltage { get; } = [];
            public List<double> FallbackVoltage { get; } = [];
            public List<double> PreferredCurrent { get; } = [];
            public List<double> FallbackCurrent { get; } = [];
            public List<double> PreferredEnergy { get; } = [];
            public List<double> FallbackEnergy { get; } = [];
        }

        /// <summary>
        /// Malt die CPU-Auslastung pro Kern als Bitmap. Async, da das Rendern bei vielen Kernen etwas dauern kann.
        /// </summary>
        public static Task<Bitmap> RenderCoresBitmapAsync(float[] usages, int width, int height, Color? backColor = null, Color? renderPercentagesColor = null, CancellationToken ct = default)
        {
            backColor ??= Color.White;
            return Task.Run(() => RenderCoresBitmap(usages, width, height, backColor.Value, renderPercentagesColor, ct), ct);
        }

        private static Bitmap RenderCoresBitmap(float[] usages, int width, int height, Color backColor, Color? renderPercentagesColor, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            int count = Math.Max(1, usages?.Length ?? 1);
            var (cols, rows) = CalculateCoreGridDimensions(count, width, height);

            var bmp = new Bitmap(Math.Max(1, width), Math.Max(1, height));
            using var g = Graphics.FromImage(bmp);
            g.Clear(backColor);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

            const int pad = 2;
            int gridW = Math.Max(cols, width - pad * (cols + 1));
            int gridH = Math.Max(rows, height - pad * (rows + 1));
            int cellW = gridW / cols;
            int cellH = gridH / rows;

            using var borderPen = new Pen(Color.Black, 1f);
            using var fillBrush = new SolidBrush(Color.FromArgb(64, 160, 255));
            using var highBrush = new SolidBrush(Color.FromArgb(255, 96, 96));

            for (int i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();
                int x = pad + (i % cols) * (cellW + pad);
                int y = pad + (i / cols) * (cellH + pad);
                float usage = Math.Clamp(usages?[i] ?? 0f, 0f, 1f);

                DrawCoreCell(g, new Rectangle(x, y, cellW, cellH), usage, borderPen, fillBrush, highBrush, renderPercentagesColor);
            }

            return bmp;
        }

        private static void DrawCoreCell(Graphics g, Rectangle rect, float usage, Pen borderPen, SolidBrush fillBrush, SolidBrush highBrush, Color? renderPercentagesColor)
        {
            g.DrawRectangle(borderPen, rect);

            int filledH = (int) Math.Round(usage * rect.Height);
            if (filledH > 0)
            {
                var fillRect = new Rectangle(rect.X + 1, rect.Y + rect.Height - filledH + 1, Math.Max(1, rect.Width - 2), Math.Max(1, filledH - 2));
                SolidBrush brush = usage >= 0.8f ? highBrush : fillBrush;
                g.FillRectangle(brush, fillRect);
            }

            if (renderPercentagesColor.HasValue)
            {
                DrawCorePercentageText(g, rect, usage, renderPercentagesColor.Value);
            }
        }

        private static void DrawCorePercentageText(Graphics g, Rectangle rect, float usage, Color textColor)
        {
            using var textBrush = new SolidBrush(textColor);
            string percentText = $"{Math.Round(usage * 100f)}%";

            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            float fontSize = ComputeFittingFontSize(g, percentText, rect.Width, rect.Height);

            using var textFont = new Font(SystemFonts.DefaultFont.FontFamily, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            g.DrawString(percentText, textFont, textBrush, rect, sf);
        }

        private static float ComputeFittingFontSize(Graphics g, string text, int cellW, int cellH)
        {
            // Start with a size relative to the cell and shrink until the text fits or hits the minimum.
            float maxFont = Math.Min(cellW, cellH) * 0.45f;
            float fontSize = Math.Max(6f, maxFont);

            while (fontSize > 6f)
            {
                using var testFont = new Font(SystemFonts.DefaultFont.FontFamily, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                SizeF size = g.MeasureString(text, testFont);
                if (size.Width <= cellW - 4 && size.Height <= cellH - 4)
                {
                    break;
                }

                fontSize -= 1f;
            }

            return fontSize;
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

            return !GlobalMemoryStatusEx(ref status) ? throw new Win32Exception(Marshal.GetLastWin32Error()) : status;
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

