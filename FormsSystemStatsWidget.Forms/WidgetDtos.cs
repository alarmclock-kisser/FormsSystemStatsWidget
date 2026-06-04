using System;
using System.Collections.Generic;
using System.Text;

namespace FormsSystemStatsWidget.Forms
{
    internal sealed class DriveSelection
    {
        public required string RootPath { get; init; }
        public required string DisplayName { get; init; }

        public override string ToString() => this.DisplayName;
    }

    internal readonly record struct RecordingSummary(
            TimeSpan Duration,
            double CpuAverageLoadPercent,
            double? CpuAveragePowerWatts,
            double CpuEnergyWh,
            double GpuAverageLoadPercent,
            double? GpuAveragePowerWatts,
            double GpuEnergyWh,
            double? TotalAveragePowerWatts,
            double TotalEnergyWh);

    internal readonly record struct RecordingSample(
        string CsvLine,
        double CpuLoadPercent,
        double? CpuPackagePowerWatts,
        double GpuAverageLoadPercent,
        double? GpuAveragePowerWatts,
        DateTimeOffset Timestamp,
        double ElapsedSecondsSincePrevious);
}
