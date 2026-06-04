using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace FormsSystemStatsWidget.Core
{
    /// <summary>
    /// Static network traffic meter.  Call <see cref="Init"/> once, then
    /// <see cref="Sample(int)"/> on every UI-timer tick.  No internal timer —
    /// runs at the same cadence as the rest of the app.
    /// </summary>
    public static class TrafficStats
    {
        // ── P/Invoke for lightweight process IO ─────────────────────────
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS counters);

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        private static long _lastTotalSent;
        private static long _lastTotalReceived;
        private static Dictionary<int, (string Name, ulong Read, ulong Write)> _prevSnapshot = new();
        private static bool _initialized;
        private static readonly TimeSpan NetworkInterfacesRefreshInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ProcessIoSamplingInterval = TimeSpan.FromMilliseconds(1200);
        private static DateTime _lastNetworkInterfacesRefreshUtc = DateTime.MinValue;
        private static NetworkInterface[] _cachedNetworkInterfaces = [];
        private static DateTime _lastProcessIoSampleUtc = DateTime.MinValue;

        // ── public state ────────────────────────────────────────────────
        public static double UpBytesPerSecond { get; private set; }
        public static double DownBytesPerSecond { get; private set; }

        /// <summary>Name of the process with the highest IO rate.</summary>
        public static string TopTalker { get; private set; } = string.Empty;

        /// <summary>
        /// Processes whose IO rate (read + write) exceeds <see cref="ThresholdBytesPerSecond"/>.
        /// </summary>
        public static IReadOnlyList<(string Name, double IoBytesPerSec)> ActiveProcesses { get; private set; }
            = Array.Empty<(string, double)>();

        /// <summary>IO threshold in bytes/sec (default 10 KB/s).</summary>
        public static double ThresholdBytesPerSecond { get; set; } = 1 * 1024 * 1024; // 1 MB/s

        // ── control ─────────────────────────────────────────────────────
        /// <summary>
        /// Take the first baseline snapshot.  Call once before the first <see cref="Sample"/>.
        /// </summary>
        public static void Init(double thresholdBytesPerSec = 10 * 1024)
        {
            ThresholdBytesPerSecond = thresholdBytesPerSec;
            _cachedNetworkInterfaces = GetNetworkInterfacesSnapshot(forceRefresh: true);
            _lastTotalSent = GetTotalBytesSent(_cachedNetworkInterfaces);
            _lastTotalReceived = GetTotalBytesReceived(_cachedNetworkInterfaces);
            _prevSnapshot = SnapshotProcessIo();
            _lastProcessIoSampleUtc = DateTime.UtcNow;
            _initialized = true;
        }

        /// <summary>
        /// Take a sample and update all public properties.
        /// <paramref name="intervalMs"/> is the elapsed time since the last call
        /// (i.e. the UI-timer interval) so rates are normalised to per-second.
        /// </summary>
        public static void Sample(int intervalMs)
        {
            if (!_initialized)
            {
                return;
            }

            try
            {
                DateTime nowUtc = DateTime.UtcNow;
                _cachedNetworkInterfaces = GetNetworkInterfacesSnapshot(forceRefresh: false);

                long totalSent = GetTotalBytesSent(_cachedNetworkInterfaces);
                long totalReceived = GetTotalBytesReceived(_cachedNetworkInterfaces);

                double factor = 1000.0 / Math.Max(1, intervalMs);
                UpBytesPerSecond = Math.Max(0, totalSent - _lastTotalSent) * factor;
                DownBytesPerSecond = Math.Max(0, totalReceived - _lastTotalReceived) * factor;

                _lastTotalSent = totalSent;
                _lastTotalReceived = totalReceived;

                if ((nowUtc - _lastProcessIoSampleUtc) >= ProcessIoSamplingInterval)
                {
                    try
                    {
                        var current = SnapshotProcessIo();
                        double processElapsedSeconds = Math.Max(0.001d, (nowUtc - _lastProcessIoSampleUtc).TotalSeconds);
                        double processFactor = 1.0d / processElapsedSeconds;
                        var (topName, activeList) = ComputeRates(_prevSnapshot, current, processFactor);
                        _prevSnapshot = current;
                        _lastProcessIoSampleUtc = nowUtc;
                        TopTalker = topName ?? string.Empty;
                        ActiveProcesses = activeList;
                    }
                    catch
                    {
                        _lastProcessIoSampleUtc = nowUtc;
                        TopTalker = string.Empty;
                        ActiveProcesses = Array.Empty<(string, double)>();
                    }
                }
            }
            catch
            {
                // ignore sampling exceptions
            }
        }

        // ── NIC helpers ─────────────────────────────────────────────────
        private static NetworkInterface[] GetNetworkInterfacesSnapshot(bool forceRefresh)
        {
            DateTime nowUtc = DateTime.UtcNow;
            if (!forceRefresh
                && _cachedNetworkInterfaces.Length > 0
                && (nowUtc - _lastNetworkInterfacesRefreshUtc) < NetworkInterfacesRefreshInterval)
            {
                return _cachedNetworkInterfaces;
            }

            try
            {
                _cachedNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                _lastNetworkInterfacesRefreshUtc = nowUtc;
            }
            catch
            {
                if (_cachedNetworkInterfaces.Length == 0)
                {
                    _cachedNetworkInterfaces = [];
                }
            }

            return _cachedNetworkInterfaces;
        }

        private static long GetTotalBytesSent(IReadOnlyList<NetworkInterface> networkInterfaces)
        {
            long total = 0;
            foreach (NetworkInterface ni in networkInterfaces)
            {
                try { total += ni.GetIPv4Statistics().BytesSent; }
                catch { }
            }
            return total;
        }

        private static long GetTotalBytesReceived(IReadOnlyList<NetworkInterface> networkInterfaces)
        {
            long total = 0;
            foreach (NetworkInterface ni in networkInterfaces)
            {
                try { total += ni.GetIPv4Statistics().BytesReceived; }
                catch { }
            }
            return total;
        }

        // ── process IO via P/Invoke (fast, no COM overhead) ─────────────
        private static Dictionary<int, (string Name, ulong Read, ulong Write)> SnapshotProcessIo()
        {
            var result = new Dictionary<int, (string, ulong, ulong)>();
            Process[] procs;
            try { procs = Process.GetProcesses(); }
            catch { return result; }

            foreach (var p in procs)
            {
                try
                {
                    if (GetProcessIoCounters(p.Handle, out var c))
                    {
                        result[p.Id] = (p.ProcessName, c.ReadTransferCount, c.WriteTransferCount);
                    }
                }
                catch { }
                finally { p.Dispose(); }
            }
            return result;
        }

        private static (string? TopName, List<(string Name, double IoBytesPerSec)> Active) ComputeRates(
            Dictionary<int, (string Name, ulong Read, ulong Write)> prev,
            Dictionary<int, (string Name, ulong Read, ulong Write)> curr,
            double factor)
        {
            string? topName = null;
            double topValue = 0;
            var active = new List<(string, double)>();

            foreach (var (pid, cur) in curr)
            {
                if (!prev.TryGetValue(pid, out var prv))
                {
                    continue;
                }

                if (prv.Name != cur.Name)
                {
                    continue;
                }

                double deltaRead = cur.Read >= prv.Read ? (cur.Read - prv.Read) : 0;
                double deltaWrite = cur.Write >= prv.Write ? (cur.Write - prv.Write) : 0;
                double rate = (deltaRead + deltaWrite) * factor;

                if (rate > topValue)
                {
                    topValue = rate;
                    topName = cur.Name;
                }
                if (rate >= ThresholdBytesPerSecond)
                {
                    active.Add((cur.Name, rate));
                }
            }

            active.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            return (topName, active);
        }

        // ── formatting ──────────────────────────────────────────────────
        /// <summary>
        /// Format bytes/sec into a human-readable string with one decimal place.
        /// Units scale automatically: B/s → KB/s → MB/s → GB/s → TB/s.
        /// </summary>
        public static string FormatBytesPerSecond(double bytesPerSec)
        {
            if (double.IsNaN(bytesPerSec) || double.IsInfinity(bytesPerSec))
            {
                return "0.0 B/s";
            }

            double val = bytesPerSec;
            string[] units = ["B/s", "KB/s", "MB/s", "GB/s", "TB/s"];
            int idx = 0;
            while (val >= 1024 && idx < units.Length - 1)
            {
                val /= 1024.0;
                idx++;
            }
            return $"{val:0.0} {units[idx]}";
        }
    }
}
