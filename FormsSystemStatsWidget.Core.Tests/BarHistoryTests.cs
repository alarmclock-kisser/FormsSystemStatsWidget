using System;
using System.Collections.Generic;
using System.Globalization;
using FormsSystemStatsWidget.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FormsSystemStatsWidget.Core.Tests;

[TestClass]
public sealed class BarHistoryTests
{
    [TestMethod]
    public void ComputeRamStats_MinMaxAvg_OverFiveMinuteWindow()
    {
        DateTime now = DateTime.UtcNow;
        var samples = new List<BarHistory.RamSample>
        {
            new(now - TimeSpan.FromMinutes(10), 99.9), // outside window -> ignored
            new(now - TimeSpan.FromMinutes(4), 10.0),
            new(now - TimeSpan.FromMinutes(2), 20.0),
            new(now, 15.0),
        };

        var stats = BarHistory.ComputeRamStats(samples, now, TimeSpan.FromMinutes(5));

        Assert.IsNotNull(stats);
        Assert.AreEqual(10.0, stats!.Value.MinUsedGb, 1e-9);
        Assert.AreEqual(20.0, stats.Value.MaxUsedGb, 1e-9);
        Assert.AreEqual(15.0, stats.Value.AvgUsedGb, 1e-9);
        Assert.AreEqual(3, stats.Value.Count);
    }

    [TestMethod]
    public void ComputeGpuStats_LoadsAndWatts()
    {
        DateTime now = DateTime.UtcNow;
        var samples = new List<BarHistory.GpuSample>
        {
            new(now - TimeSpan.FromMinutes(4), 10.0, 50.0, 4.0, 16.0),
            new(now - TimeSpan.FromMinutes(1), 30.0, 150.0, 8.0, 16.0),
        };

        var stats = BarHistory.ComputeGpuStats(samples, now, TimeSpan.FromMinutes(5));

        Assert.IsNotNull(stats);
        Assert.AreEqual(100.0, stats!.Value.AvgWatts, 1e-9);
        Assert.AreEqual(150.0, stats.Value.PeakWatts, 1e-9);
        Assert.AreEqual(10.0, stats.Value.MinLoadPct, 1e-9);
        Assert.AreEqual(30.0, stats.Value.MaxLoadPct, 1e-9);
        Assert.AreEqual(20.0, stats.Value.AvgLoadPct, 1e-9);
    }

    [TestMethod]
    public void Tooltips_AreMultiline_AndContainRequiredValues()
    {
        var fmt = CultureInfo.InvariantCulture;
        DateTime now = DateTime.UtcNow;
        var ramStats = new BarHistory.RamStats(10.0, 20.0, 15.0, 3, TimeSpan.FromMinutes(4));
        string ramTip = BarHistory.BuildRamToolTip(ramStats, 32.0, 15.0, TimeSpan.FromMinutes(5), fmt);
        string[] ramLines = ramTip.Split(Environment.NewLine);
        Assert.IsTrue(ramLines.Length >= 5, $"RAM tooltip should be multiline, got: {ramTip}");
        StringAssert.Contains(ramTip, "10.0");
        StringAssert.Contains(ramTip, "20.0");
        StringAssert.Contains(ramTip, "15.0");

        var gpuStats = new BarHistory.GpuStatsSummary(100.0, 150.0, 10.0, 30.0, 20.0, 6.0, 2, TimeSpan.FromMinutes(4));
        string gpuTip = BarHistory.BuildGpuToolTip(gpuStats, "GPU", 8.0, 16.0, TimeSpan.FromMinutes(5), fmt);
        string[] gpuLines = gpuTip.Split(Environment.NewLine);
        Assert.IsTrue(gpuLines.Length >= 7, $"GPU tooltip should be multiline, got: {gpuTip}");
        StringAssert.Contains(gpuTip, "100.0");
        StringAssert.Contains(gpuTip, "10.00");
        StringAssert.Contains(gpuTip, "30.00");
        StringAssert.Contains(gpuTip, "20.00");
    }

    [TestMethod]
    public void ComputeBytesPerSecond_UsesRealElapsed_NotFixedInterval()
    {
        // 420ms nominal interval, but tick actually took 1.68s (4x slower):
        // old logic would report 4x too high; new logic divides by real elapsed.
        double rate = BarHistory.ComputeBytesPerSecond(deltaBytes: 1680, elapsedSeconds: 1.68, intervalMsFallback: 420);
        Assert.AreEqual(1000.0, rate, 1e-9);

        // Implausible elapsed falls back to intervalMs.
        double fallback = BarHistory.ComputeBytesPerSecond(deltaBytes: 420, elapsedSeconds: 0, intervalMsFallback: 420);
        Assert.AreEqual(1000.0, fallback, 1e-9);
    }

    [TestMethod]
    public void PruneOlderThan_DropsSamplesOutsideWindow()
    {
        DateTime now = DateTime.UtcNow;
        var q = new Queue<BarHistory.RamSample>();
        q.Enqueue(new BarHistory.RamSample(now - TimeSpan.FromMinutes(6), 1.0));
        q.Enqueue(new BarHistory.RamSample(now - TimeSpan.FromMinutes(1), 2.0));
        BarHistory.PruneOlderThan(q, s => s.Utc, now, TimeSpan.FromMinutes(5));
        Assert.AreEqual(1, q.Count);
        Assert.AreEqual(2.0, q.Peek().UsedGb, 1e-9);
    }

    [TestMethod]
    public void ComputeCombinedGpuStats_SumsWattsAndVram_AveragesLoad()
    {
        DateTime now = DateTime.UtcNow;
        DateTime t1 = now - TimeSpan.FromMinutes(2);
        DateTime t2 = now - TimeSpan.FromMinutes(1);
        var gpu1 = new List<BarHistory.GpuSample>
        {
            new(t1, 10.0, 50.0, 4.0, 16.0),
            new(t2, 30.0, 100.0, 6.0, 16.0),
        };
        var gpu2 = new List<BarHistory.GpuSample>
        {
            new(t1, 20.0, 70.0, 2.0, 8.0),
            new(t2, 40.0, 120.0, 4.0, 8.0),
        };

        var combined = BarHistory.ComputeCombinedGpuStats(gpu1, gpu2, now, TimeSpan.FromMinutes(5));

        Assert.IsNotNull(combined);
        // Watts summiert: (50+70 + 100+120) / 2 = 170 avg, Peak 220
        Assert.AreEqual(170.0, combined!.Value.AvgWatts, 1e-9);
        Assert.AreEqual(220.0, combined.Value.PeakWatts, 1e-9);
        // Load gemittelt: (15 + 35) / 2 = 25 avg, Min 15, Max 35
        Assert.AreEqual(15.0, combined.Value.MinLoadPct, 1e-9);
        Assert.AreEqual(35.0, combined.Value.MaxLoadPct, 1e-9);
        Assert.AreEqual(25.0, combined.Value.AvgLoadPct, 1e-9);
        // VRAM summiert: (6 + 10) / 2 = 8 avg
        Assert.AreEqual(8.0, combined.Value.AvgVramUsedGb, 1e-9);
        Assert.AreEqual(2, combined.Value.Count);
    }

    [TestMethod]
    public void ComputeCombinedGpuStats_SingleGpu_FallsBackToSingleStats()
    {
        DateTime now = DateTime.UtcNow;
        var gpu1 = new List<BarHistory.GpuSample>
        {
            new(now - TimeSpan.FromMinutes(1), 10.0, 50.0, 4.0, 16.0),
            new(now, 30.0, 150.0, 8.0, 16.0),
        };

        var combined = BarHistory.ComputeCombinedGpuStats(gpu1, [], now, TimeSpan.FromMinutes(5));

        Assert.IsNotNull(combined);
        Assert.AreEqual(100.0, combined!.Value.AvgWatts, 1e-9);
        Assert.AreEqual(20.0, combined.Value.AvgLoadPct, 1e-9);
    }

    [TestMethod]
    public void ComputeCombinedGpuStats_IgnoresSamplesOutsideWindow()
    {
        DateTime now = DateTime.UtcNow;
        var gpu1 = new List<BarHistory.GpuSample>
        {
            new(now - TimeSpan.FromMinutes(10), 100.0, 999.0, 99.0, 16.0),
            new(now - TimeSpan.FromMinutes(1), 10.0, 50.0, 4.0, 16.0),
        };
        var gpu2 = new List<BarHistory.GpuSample>
        {
            new(now - TimeSpan.FromMinutes(10), 100.0, 999.0, 99.0, 8.0),
            new(now - TimeSpan.FromMinutes(1), 20.0, 70.0, 2.0, 8.0),
        };

        var combined = BarHistory.ComputeCombinedGpuStats(gpu1, gpu2, now, TimeSpan.FromMinutes(5));

        Assert.IsNotNull(combined);
        Assert.AreEqual(120.0, combined!.Value.AvgWatts, 1e-9);
        Assert.AreEqual(1, combined.Value.Count);
    }

    [TestMethod]
    public void BuildCombinedGpuToolTip_IsMultiline_AndShowsTotals()
    {
        var fmt = CultureInfo.InvariantCulture;
        var stats = new BarHistory.GpuStatsSummary(170.0, 220.0, 15.0, 35.0, 25.0, 8.0, 2, TimeSpan.FromMinutes(4));
        string tip = BarHistory.BuildCombinedGpuToolTip(stats, 2, 10.0, 24.0, TimeSpan.FromMinutes(5), fmt);

        string[] lines = tip.Split(Environment.NewLine);
        Assert.IsTrue(lines.Length >= 8, $"Combined tooltip should be multiline, got: {tip}");
        StringAssert.Contains(tip, "GPUs total (2)");
        StringAssert.Contains(tip, "170.0");
        StringAssert.Contains(tip, "220.0");
        StringAssert.Contains(tip, "15.00");
        StringAssert.Contains(tip, "35.00");
        StringAssert.Contains(tip, "25.00");
        StringAssert.Contains(tip, "10.0 / 24.0");
    }
}
