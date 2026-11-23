using System.Text.Json;
using System.Text.Json.Serialization;

namespace FormsSystemStatsWidget.Forms
{
    internal sealed class RemoteTopTask
    {
        public string Name { get; set; } = string.Empty;
        public double CpuPercent { get; set; }
    }

    internal sealed class RemoteStatsData
    {
        public double CpuAvgPercent { get; set; }
        public float[] CoreUsages { get; set; } = [];
        public double RamUsedGb { get; set; }
        public double RamTotalGb { get; set; }
        public double PageFileUsedGb { get; set; }
        public double PageFileMaxGb { get; set; }
        public double GpuPercent { get; set; }
        public double GpuWatts { get; set; }
        public double VramUsedGb { get; set; }
        public double VramTotalGb { get; set; }
        public bool HasSecondGpu { get; set; }
        public double Gpu2Percent { get; set; }
        public double Gpu2Watts { get; set; }
        public double Vram2UsedGb { get; set; }
        public double Vram2TotalGb { get; set; }
        public List<RemoteTopTask> TopTasks { get; set; } = [];
        public string TrafficTitle { get; set; } = string.Empty;
        public bool AlwaysOnTop { get; set; }
        public bool BlackOutMode { get; set; }
    }

    internal sealed class RemoteMessage
    {
        public string Type { get; set; } = string.Empty;
        public RemoteStatsData? Data { get; set; }
        public string? Action { get; set; }
        public string? Value { get; set; }
        public int? X { get; set; }
        public int? Y { get; set; }
        public string? ComputerName { get; set; }

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string Serialize(RemoteMessage message) => JsonSerializer.Serialize(message, SerializerOptions);

        public static RemoteMessage? Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<RemoteMessage>(json, SerializerOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static bool IsValidPort(int port) => port is >= 1 and <= 65535;
    }
}
