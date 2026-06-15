using System;
using System.IO;
using System.Text.Json;
using FormsSystemStatsWidget.Core;

namespace FormsSystemStatsWidget.Forms
{
    internal sealed class WidgetPersistentSettings
    {
        public int UpdateIntervalMs { get; set; } = 420;

        public string DiagramColorHex { get; set; } = "#FFFFFF";
        
        public int WindowOpacity { get; set; } = 100;
        
        public string PerCorePercentColor { get; set; } = "#FFFFFF";

        public bool ShowPerCorePercent { get; set; } = true;

        public bool AlwaysOnTop { get; set; }

        public string TrafficThresholdText { get; set; } = "1 MB/s";

        public bool ShowTokensPerSecond { get; set; } = true;

        public bool DebugConsoleFormattedLog { get; set; } = true;

        public bool DebugConsoleIncludeRawChunks { get; set; } = true;

        public bool DebugConsoleLogGenerationSpeed { get; set; }

        public bool SmartPromptEnabled { get; set; } = true;

        public double SmartPromptSafetyRatio { get; set; } = 0.90;

        public double SmartPromptBudgetRatio { get; set; } = 0.75;

        public int SmartPromptLargeMessageThresholdChars { get; set; } = 2400;

        public int SmartPromptSkeletonMaxLines { get; set; } = 60;

        public int SmartPromptFocusKeywordLimit { get; set; } = 12;

        public int SmartPromptTailKeepBonusChars { get; set; } = 500;



        // Persisted Llama sampling parameters (used for model load defaults / UI)
        public string GgufModelDirectory { get; set; } = "";
        public int ContextSize { get; set; } = 2048;
        public int BatchSize { get; set; } = 512;
        public int GpuLayersCount { get; set; } = 0;
        public int NumberParallelSlots { get; set; }
        public bool NoWarmup { get; set; } = false;
        public bool FitMode { get; set; } = false;
        public bool KvOffload { get; set; } = false;
        public string KvCacheType { get; set; } = "f16";
        public bool LlamaServerToolCalling { get; set; } = false;
        public float Temperature { get; set; } = 0.7f;
        public float RepetitionPenalty { get; set; } = 1.1f;
        public int ThinkingBudget { get; set; } = 0;
        public int ReasoningBudget { get; set; } = 0;
        public double UserTopP { get; set; } = 0.9;
        public double UserMinP { get; set; } = 0.0;
        public int UserTopK { get; set; } = 40;
        public bool HideCmd { get; internal set; }
        public string OpenAIApiUrl { get; set; } = "";
        public int LlamaCppServerPort { get; set; }
        public int OllamaPort { get; set; }
        public string AdditionalLoadArgs { get; set; } = "--mlock";
    }

    internal static class WidgetPersistentSettingsStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true
        };

        private static readonly string SettingsDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FormsSystemStatsWidget");

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectoryPath, "WidgetPersistentSettings.json");

        public static WidgetPersistentSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return new WidgetPersistentSettings();
                }

                string json = File.ReadAllText(SettingsFilePath);
                WidgetPersistentSettings? settings = JsonSerializer.Deserialize<WidgetPersistentSettings>(json, SerializerOptions);
                return settings ?? new WidgetPersistentSettings();
            }
            catch (Exception ex)
            {
                Logger.Log($"[Settings] Laden fehlgeschlagen: {ex.Message}");
                return new WidgetPersistentSettings();
            }
        }

        public static void Save(WidgetPersistentSettings settings)
        {
            try
            {
                _ = Directory.CreateDirectory(SettingsDirectoryPath);
                string json = JsonSerializer.Serialize(settings, SerializerOptions);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                Logger.Log($"[Settings] Speichern fehlgeschlagen: {ex.Message}");
            }
        }
    }
}
