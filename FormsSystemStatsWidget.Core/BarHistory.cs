using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FormsSystemStatsWidget.Core
{
    /// <summary>
    /// Rolling 5-minute helpers for the custom load-bar tooltips (RAM + GPUs).
    /// Pure/stateless so it can be unit-tested; the WinForms widget only keeps the queues.
    /// </summary>
    public static class BarHistory
    {
        public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(5);

        public readonly record struct RamSample(DateTime Utc, double UsedGb);

        public readonly record struct GpuSample(DateTime Utc, double LoadPct, double Watts, double VramUsedGb, double VramTotalGb);

        public readonly record struct RamStats(double MinUsedGb, double MaxUsedGb, double AvgUsedGb, int Count, TimeSpan Covered);

        public readonly record struct GpuStatsSummary(
            double AvgWatts,
            double PeakWatts,
            double MinLoadPct,
            double MaxLoadPct,
            double AvgLoadPct,
            double AvgVramUsedGb,
            int Count,
            TimeSpan Covered);

        public static void PruneOlderThan<T>(Queue<T> queue, Func<T, DateTime> timestampOf, DateTime nowUtc, TimeSpan window)
        {
            DateTime cutoff = nowUtc - window;
            while (queue.Count > 0 && timestampOf(queue.Peek()) < cutoff)
            {
                queue.Dequeue();
            }
        }

        public static RamStats? ComputeRamStats(IEnumerable<RamSample> samples, DateTime nowUtc, TimeSpan window)
        {
            DateTime cutoff = nowUtc - window;
            double min = double.MaxValue;
            double max = double.MinValue;
            double sum = 0;
            int count = 0;
            DateTime oldest = DateTime.MaxValue;

            foreach (var s in samples)
            {
                if (s.Utc < cutoff)
                {
                    continue;
                }

                if (s.Utc < oldest)
                {
                    oldest = s.Utc;
                }

                if (s.UsedGb < min)
                {
                    min = s.UsedGb;
                }

                if (s.UsedGb > max)
                {
                    max = s.UsedGb;
                }

                sum += s.UsedGb;
                count++;
            }

            if (count == 0)
            {
                return null;
            }

            TimeSpan covered = nowUtc - oldest;
            if (covered < TimeSpan.Zero)
            {
                covered = TimeSpan.Zero;
            }

            return new RamStats(min, max, sum / count, count, covered);
        }

        public static GpuStatsSummary? ComputeGpuStats(IEnumerable<GpuSample> samples, DateTime nowUtc, TimeSpan window)
        {
            DateTime cutoff = nowUtc - window;
            double minLoad = double.MaxValue;
            double maxLoad = double.MinValue;
            double sumLoad = 0;
            double sumWatts = 0;
            double peakWatts = double.MinValue;
            double sumVram = 0;
            int count = 0;
            DateTime oldest = DateTime.MaxValue;

            foreach (var s in samples)
            {
                if (s.Utc < cutoff)
                {
                    continue;
                }

                if (s.Utc < oldest)
                {
                    oldest = s.Utc;
                }

                if (s.LoadPct < minLoad)
                {
                    minLoad = s.LoadPct;
                }

                if (s.LoadPct > maxLoad)
                {
                    maxLoad = s.LoadPct;
                }

                sumLoad += s.LoadPct;
                sumWatts += s.Watts;
                sumVram += s.VramUsedGb;
                if (s.Watts > peakWatts)
                {
                    peakWatts = s.Watts;
                }

                count++;
            }

            if (count == 0)
            {
                return null;
            }

            TimeSpan covered = nowUtc - oldest;
            if (covered < TimeSpan.Zero)
            {
                covered = TimeSpan.Zero;
            }

            return new GpuStatsSummary(
                sumWatts / count,
                peakWatts,
                minLoad,
                maxLoad,
                sumLoad / count,
                sumVram / count,
                count,
                covered);
        }

        /// <summary>
        /// Kombinierte ("gesamt") GPU-Statistik über alle GPUs:
        /// Watts und VRAM werden summiert (Gesamt-Leistungsaufnahme),
        /// Load wird über die GPUs gemittelt (Gesamt-Auslastung).
        /// Samples beider GPUs werden per Zeitstempel zusammengeführt; das Widget
        /// sampled beide GPUs im selben Tick mit identischem Timestamp, daher passt das.
        /// Unpaarige Samples (z.B. direkt nach Start) fallen auf Mittelwert-Logik zurück.
        /// Reine Funktion, gut testbar.
        /// </summary>
        public static GpuStatsSummary? ComputeCombinedGpuStats(
            IEnumerable<GpuSample> first,
            IEnumerable<GpuSample> second,
            DateTime nowUtc,
            TimeSpan window)
        {
            DateTime cutoff = nowUtc - window;
            var firstInWindow = first.Where(s => s.Utc >= cutoff).ToList();
            var secondInWindow = second.Where(s => s.Utc >= cutoff).ToList();

            if (firstInWindow.Count == 0)
            {
                return ComputeGpuStats(secondInWindow, nowUtc, window);
            }

            if (secondInWindow.Count == 0)
            {
                return ComputeGpuStats(firstInWindow, nowUtc, window);
            }

            var secondByTime = new Dictionary<DateTime, GpuSample>();
            foreach (var s in secondInWindow)
            {
                secondByTime[s.Utc] = s; // identischer Tick-Timestamp; Duplikate -> letzter gewinnt
            }

            double minLoad = double.MaxValue;
            double maxLoad = double.MinValue;
            double sumLoad = 0;
            double sumWatts = 0;
            double peakWatts = double.MinValue;
            double sumVram = 0;
            int count = 0;
            DateTime oldest = DateTime.MaxValue;

            foreach (var s1 in firstInWindow)
            {
                double watts;
                double meanLoad;
                double vramSum;
                if (secondByTime.TryGetValue(s1.Utc, out var s2))
                {
                    watts = s1.Watts + s2.Watts;
                    meanLoad = (s1.LoadPct + s2.LoadPct) / 2.0;
                    vramSum = s1.VramUsedGb + s2.VramUsedGb;
                }
                else
                {
                    // Kein Partner-Sample: Einzelwert als Näherung (kein Bias durch Weglassen).
                    watts = s1.Watts;
                    meanLoad = s1.LoadPct;
                    vramSum = s1.VramUsedGb;
                }

                if (s1.Utc < oldest)
                {
                    oldest = s1.Utc;
                }

                if (meanLoad < minLoad)
                {
                    minLoad = meanLoad;
                }

                if (meanLoad > maxLoad)
                {
                    maxLoad = meanLoad;
                }

                if (watts > peakWatts)
                {
                    peakWatts = watts;
                }

                sumLoad += meanLoad;
                sumWatts += watts;
                sumVram += vramSum;
                count++;
            }

            if (count == 0)
            {
                return null;
            }

            TimeSpan covered = nowUtc - oldest;
            if (covered < TimeSpan.Zero)
            {
                covered = TimeSpan.Zero;
            }

            return new GpuStatsSummary(
                sumWatts / count,
                peakWatts,
                minLoad,
                maxLoad,
                sumLoad / count,
                sumVram / count,
                count,
                covered);
        }
        /// <summary>
        /// RAM tooltip: one value per line. Required: Min/Max/Avg Used GB (F1).
        /// Extra (useful): total + current % + window coverage, so the averages can be interpreted.
        /// </summary>
        public static string BuildRamToolTip(RamStats? stats, double totalGb, double currentUsedGb, TimeSpan window, IFormatProvider? format = null)
        {
            format ??= CultureInfo.CurrentCulture;
            string windowText = FormatWindow(window, stats?.Covered);
            if (stats is null)
            {
                return string.Join(Environment.NewLine,
                    "RAM (last 5 min)",
                    "no data yet",
                    $"Total: {totalGb.ToString("F1", format)} GB");
            }

            double pct = totalGb > 0 ? (currentUsedGb / totalGb) * 100.0 : 0.0;
            return string.Join(Environment.NewLine,
                $"RAM ({windowText})",
                $"MinUsed: {stats.Value.MinUsedGb.ToString("F1", format)} GB",
                $"MaxUsed: {stats.Value.MaxUsedGb.ToString("F1", format)} GB",
                $"AvgUsed: {stats.Value.AvgUsedGb.ToString("F1", format)} GB",
                $"Current: {currentUsedGb.ToString("F1", format)} GB / {totalGb.ToString("F1", format)} GB ({pct.ToString("F2", format)} %)");
        }

        /// <summary>
        /// GPU tooltip: one value per line. Required: AvgWatts (F1), Min/Max/Avg Load (F2).
        /// Extra (useful for LLM/VRAM workloads): PeakWatts + VRAM avg/current.
        /// </summary>
        public static string BuildGpuToolTip(GpuStatsSummary? stats, string gpuLabel, double currentVramUsedGb, double currentVramTotalGb, TimeSpan window, IFormatProvider? format = null)
        {
            format ??= CultureInfo.CurrentCulture;
            string windowText = FormatWindow(window, stats?.Covered);
            string header = $"{gpuLabel} ({windowText})";
            if (stats is null)
            {
                return string.Join(Environment.NewLine,
                    header,
                    "no data yet",
                    $"VRAM: {currentVramUsedGb.ToString("F1", format)} / {currentVramTotalGb.ToString("F1", format)} GB");
            }

            return string.Join(Environment.NewLine,
                header,
                $"AvgWatts: {stats.Value.AvgWatts.ToString("F1", format)} W",
                $"PeakWatts: {stats.Value.PeakWatts.ToString("F1", format)} W",
                $"MinLoad: {stats.Value.MinLoadPct.ToString("F2", format)} %",
                $"MaxLoad: {stats.Value.MaxLoadPct.ToString("F2", format)} %",
                $"AvgLoad: {stats.Value.AvgLoadPct.ToString("F2", format)} %",
                $"AvgVRAM: {stats.Value.AvgVramUsedGb.ToString("F1", format)} GB",
                $"VRAM: {currentVramUsedGb.ToString("F1", format)} / {currentVramTotalGb.ToString("F1", format)} GB",
                "Tip: hold Ctrl for total");
        }

        /// <summary>
        /// Gesamt-Tooltip über alle GPUs (Ctrl+Hover): Watts/VRAM summiert, Load gemittelt.
        /// Ein Wert pro Zeile, gleiche Formate wie der Einzel-Tooltip (Watts F1, Load F2, VRAM F1).
        /// </summary>
        public static string BuildCombinedGpuToolTip(GpuStatsSummary? stats, int gpuCount, double currentVramUsedGbSum, double currentVramTotalGbSum, TimeSpan window, IFormatProvider? format = null)
        {
            format ??= CultureInfo.CurrentCulture;
            string windowText = FormatWindow(window, stats?.Covered);
            string header = gpuCount > 1
                ? $"GPUs total ({gpuCount}) ({windowText})"
                : $"GPU total ({windowText})";
            if (stats is null)
            {
                return string.Join(Environment.NewLine,
                    header,
                    "no data yet",
                    $"VRAM: {currentVramUsedGbSum.ToString("F1", format)} / {currentVramTotalGbSum.ToString("F1", format)} GB");
            }

            return string.Join(Environment.NewLine,
                header,
                $"AvgWatts: {stats.Value.AvgWatts.ToString("F1", format)} W",
                $"PeakWatts: {stats.Value.PeakWatts.ToString("F1", format)} W",
                $"MinLoad: {stats.Value.MinLoadPct.ToString("F2", format)} %",
                $"MaxLoad: {stats.Value.MaxLoadPct.ToString("F2", format)} %",
                $"AvgLoad: {stats.Value.AvgLoadPct.ToString("F2", format)} %",
                $"AvgVRAM: {stats.Value.AvgVramUsedGb.ToString("F1", format)} GB",
                $"VRAM: {currentVramUsedGbSum.ToString("F1", format)} / {currentVramTotalGbSum.ToString("F1", format)} GB",
                "without Ctrl = per-GPU");
        }

        private static string FormatWindow(TimeSpan window, TimeSpan? covered)
        {
            int totalMin = (int)Math.Round(window.TotalMinutes);
            if (covered is null || covered.Value >= window - TimeSpan.FromSeconds(5))
            {
                return $"last {totalMin} min";
            }

            if (covered.Value.TotalSeconds < 90)
            {
                return $"last {(int)Math.Max(1, Math.Round(covered.Value.TotalSeconds))} s";
            }

            return $"last {covered.Value.TotalMinutes:F0} min";
        }

        /// <summary>
        /// Testable rate helper for network throughput: bytes delta / real elapsed seconds.
        /// Falls elapsed unplausibel ist, wird auf intervalMs zurückgegriffen.
        /// </summary>
        public static double ComputeBytesPerSecond(long deltaBytes, double elapsedSeconds, int intervalMsFallback)
        {
            double safeDelta = Math.Max(0, (double)deltaBytes);
            double elapsed = elapsedSeconds;
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0.05 || elapsed > 120.0)
            {
                elapsed = Math.Max(1, intervalMsFallback) / 1000.0;
            }

            return safeDelta / Math.Max(0.001, elapsed);
        }
    }
}
