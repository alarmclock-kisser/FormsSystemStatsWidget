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

        public string StrictToolCallingRulesInjectionPrompt { get; set; } = "\n\n[CRITICAL SYSTEM INSTRUCTIONS FOR TOOLS & EDITS]\n" +
                          "1. NEVER wrap tool calls in Markdown code blocks (e.g., " + "``" + "`json). Output the raw JSON tool format directly.\n" +
                          "2. When using file edit/replace tools, your indentation and leading spaces MUST EXACTLY MATCH the original source file. Do not strip leading spaces.\n" +
                          "3. Output ONLY the valid JSON tool call. Use the exact schema: {\"name\": \"function_name\", \"arguments\": {...}} without extra conversational text.";
        public bool InjectStrictToolCallingRules { get; set; } = true;



        // Persisted Llama sampling parameters (used for model load defaults / UI)
        public string GgufModelDirectory { get; set; } = @"D:\Models\GGUF\Others";
        public int ContextSize { get; set; } = 4096;
        public int BatchSize { get; set; } = 512;
        public int GpuLayersCount { get; set; } = 0;
        public int NumberParallelSlots { get; set; } = 1;
        public bool NoWarmup { get; set; } = false;
        public bool FitMode { get; set; } = true;
        public bool KvOffload { get; set; } = false;
        public string KvCacheType { get; set; } = "f16";
        public bool LlamaServerToolCalling { get; set; } = false;
        public float Temperature { get; set; } = 0.7f;
        public float RepetitionPenalty { get; set; } = 1.1f;
        public float PresencePenalty { get; set; } = 1.0f;
        public string? ReasoningEffort { get; set; } = null;
        public bool Thinking { get; set; } = true;
        public int ReasoningBudget { get; set; } = 2048;
        public double UserTopP { get; set; } = 0.9;
        public double UserMinP { get; set; } = 0.0;
        public int UserTopK { get; set; } = 40;
        public bool HideCmd { get; set; } = true;
        public string OpenAIApiUrl { get; set; } = "";
        public int LlamaCppServerPort { get; set; } = 8080;
        public int OllamaPort { get; set; } = 11434;
        public string AdditionalLoadArgs { get; set; } = "--mlock -t 4 -tb 4";
        public Point WidgetPosition { get; set; } = new(0, 0);
        public bool BlackOutMode { get; set; } = false;

        public bool PrintGenerationStats { get; set; } = false;
        public string AdditionalCopilotSystemPrompt { get; set; } = string.Empty;
        public bool ExtendCopilotSystemPrompt { get; set; } = false;
        public bool AppendParams { get; set; } = false;

        public string OnnxModelRootDirectory { get; set; } = @"D:\Models\ONNX";
        public string OnnxModel { get; set; } = string.Empty;
        public string OnnxModelLayout { get; set; } = "Auto";
        public int OnnxContextLength { get; set; } = 4096;
        public int OnnxMaxTokens { get; set; } = 1024;
        public double OnnxTemperature { get; set; } = 0.8;
        public double OnnxTopP { get; set; } = 0.9;
        public int OnnxTopK { get; set; } = 40;
        public double OnnxRepeatPenalty { get; set; } = 1.1;
        public string OnnxExecutionProvider { get; set; } = "Dml";
        public bool OnnxHideConsole { get; set; } = true;
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
