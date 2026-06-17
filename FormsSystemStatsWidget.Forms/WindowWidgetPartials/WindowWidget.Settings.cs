using FormsSystemStatsWidget.Core;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Windows.Forms.Timer;

namespace FormsSystemStatsWidget.Forms
{
    public partial class WindowWidget
    {
        private void ApplyPersistentSettings()
        {
            this._updateIntervalMs = Math.Max(50, this._persistentSettings.UpdateIntervalMs);
            this.UpdateTimer.Interval = this._updateIntervalMs;
            this.toolStripTextBox_interval.Text = this._updateIntervalMs.ToString(CultureInfo.InvariantCulture);

            this.toolStripTextBox_diagramColor.Text = this._persistentSettings.DiagramColorHex;
            this.toolStripTextBox_percentageColor.Text = this._persistentSettings.PerCorePercentColor;

            this.showUsageToolStripMenuItem.Checked = this._persistentSettings.ShowPerCorePercent;
            this.toolStripTextBox_percentageColor.Enabled = this.showUsageToolStripMenuItem.Checked;

            this.alwaysOnTopToolStripMenuItem.Checked = this._persistentSettings.AlwaysOnTop;
            this.TopMost = this._persistentSettings.AlwaysOnTop;

            this.toolStripTextBox_threshold.Text = this._persistentSettings.TrafficThresholdText;

            this.showTokenssToolStripMenuItem.Checked = this._persistentSettings.ShowTokensPerSecond;

            this.toolStripMenuItem_visuallyFormatLog.Checked = this._persistentSettings.DebugConsoleFormattedLog;
            this.toolStripMenuItem_includeRawChunksLog.Checked = this._persistentSettings.DebugConsoleIncludeRawChunks;
            this.toolStripMenuItem_logGenerationSpeed.Checked = this._persistentSettings.DebugConsoleLogGenerationSpeed;
            this.toolStripMenuItem_hideCmd.Checked = this._persistentSettings.HideCmd;

            this.toolStripTextBox_opacity.Text = this._persistentSettings.WindowOpacity.ToString() + "%";
            this.toolStripTextBox_opacity_KeyDown(this.toolStripTextBox_opacity, new KeyEventArgs(Keys.Enter));
            this.toolStripTextBox_modelsDirectory.Text = this._persistentSettings.GgufModelDirectory;
            this.toolStripTextBox_contextSize.Text = this._persistentSettings.ContextSize.ToString();
            this.toolStripTextBox_batchSize.Text = this._persistentSettings.BatchSize.ToString();
            this.toolStripTextBox_gpuLayersCount.Text = this._persistentSettings.GpuLayersCount.ToString();
            this.toolStripMenuItem_noWarmup.Checked = this._persistentSettings.NoWarmup;
            this.toolStripMenuItem_fitMode.Checked = this._persistentSettings.FitMode;
            this.KVoffload_ToolStripMenuItem.Checked = this._persistentSettings.KvOffload;
            this.toolStripComboBox_cacheType.Text = this._persistentSettings.KvCacheType;
            this.toolStripMenuItem_toolCalls.Checked = this._persistentSettings.LlamaServerToolCalling;
            this.toolStripTextBox_temperature.Text = this._persistentSettings.Temperature.ToString("0.0000", CultureInfo.InvariantCulture);
            this.toolStripTextBox_repetationPenalty.Text = this._persistentSettings.RepetitionPenalty.ToString("0.0000", CultureInfo.InvariantCulture);
            this.toolStripMenuItem_thinking.Checked = this._persistentSettings.Thinking;
            this.toolStripTextBox_reasoningBudget.Text = this._persistentSettings.ReasoningBudget.ToString();
            this.toolStripTextBox_additionalArgs.Text = this._persistentSettings.AdditionalLoadArgs;
            this.toolStripTextBox_additionalArgs_KeyDown(this.toolStripTextBox_additionalArgs, new KeyEventArgs(Keys.Enter));

            this.toolStripTextBox_modelsDirectory.Text = this._persistentSettings.GgufModelDirectory;
            this.toolStripTextBox_modelsDirectory_KeyDown(this.toolStripTextBox_modelsDirectory, new KeyEventArgs(Keys.Enter));

            LlamaOllamaBridge.EnableFormattedLogging = this.toolStripMenuItem_visuallyFormatLog.Checked;

            // Load persisted Llama sampling parameters into UI and bridge
            try
            {
                this.toolStripTextBox_topP.Text = this._persistentSettings.UserTopP.ToString(System.Globalization.CultureInfo.InvariantCulture);
                this.toolStripTextBox_minP.Text = this._persistentSettings.UserMinP.ToString(System.Globalization.CultureInfo.InvariantCulture);
                this.toolStripTextBox_topK.Text = this._persistentSettings.UserTopK.ToString(CultureInfo.InvariantCulture);

                // Apply to bridge defaults
                LlamaOllamaBridge.UserDefinedTemperature = this._persistentSettings.Temperature;
                LlamaOllamaBridge.UserDefinedRepetitionPenalty = this._persistentSettings.RepetitionPenalty;
                LlamaOllamaBridge.UserDefinedReasoningBudget = this._persistentSettings.ReasoningBudget;
                LlamaOllamaBridge.UserDefinedTopP = this._persistentSettings.UserTopP;
                LlamaOllamaBridge.UserDefinedMinP = this._persistentSettings.UserMinP;
                LlamaOllamaBridge.UserDefinedTopK = this._persistentSettings.UserTopK;
            }
            catch
            {
                // ignore malformed persisted values
            }
            LlamaOllamaBridge.EnableRawChunkLogging = this.toolStripMenuItem_includeRawChunksLog.Checked;

            this.enableSmartPromptOptimizationsToolStripMenuItem.Checked = this._persistentSettings.SmartPromptEnabled;
            this.toolStripTextBox_promptSafetyRatio.Text = this._persistentSettings.SmartPromptSafetyRatio.ToString("0.00", CultureInfo.InvariantCulture);
            this.toolStripTextBox_smartBudgetRatio.Text = this._persistentSettings.SmartPromptBudgetRatio.ToString("0.00", CultureInfo.InvariantCulture);
            this.toolStripTextBox_largeMessageThresholdChars.Text = this._persistentSettings.SmartPromptLargeMessageThresholdChars.ToString(CultureInfo.InvariantCulture);
            this.toolStripTextBox_skeletonMaxLines.Text = this._persistentSettings.SmartPromptSkeletonMaxLines.ToString(CultureInfo.InvariantCulture);
            this.toolStripTextBox_focusKeywordLimit.Text = this._persistentSettings.SmartPromptFocusKeywordLimit.ToString(CultureInfo.InvariantCulture);
            this.toolStripTextBox_tailKeepBonusChars.Text = this._persistentSettings.SmartPromptTailKeepBonusChars.ToString(CultureInfo.InvariantCulture);

            SmartPromptOptimizationSettings.IsEnabled = this.enableSmartPromptOptimizationsToolStripMenuItem.Checked;
            SmartPromptOptimizationSettings.PromptSafetyRatio = this._persistentSettings.SmartPromptSafetyRatio;
            SmartPromptOptimizationSettings.SmartBudgetRatio = this._persistentSettings.SmartPromptBudgetRatio;
            SmartPromptOptimizationSettings.LargeMessageThresholdChars = this._persistentSettings.SmartPromptLargeMessageThresholdChars;
            SmartPromptOptimizationSettings.SkeletonMaxLines = this._persistentSettings.SmartPromptSkeletonMaxLines;
            SmartPromptOptimizationSettings.FocusKeywordLimit = this._persistentSettings.SmartPromptFocusKeywordLimit;
            SmartPromptOptimizationSettings.TailKeepBonusChars = this._persistentSettings.SmartPromptTailKeepBonusChars;

            this.toolStripMenuItem_blackOutMode.Checked = this._persistentSettings.BlackOutMode;
        }

        private void toolStripTextBox_interval_Leave(object? sender, EventArgs e)
        {
            this.ApplyIntervalFromText();
        }

        private void toolStripTextBox_interval_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            this.ApplyIntervalFromText();
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void ApplyIntervalFromText()
        {
            string text = this.toolStripTextBox_interval.Text;
            if (int.TryParse(text, out int interval))
            {
                this._updateIntervalMs = interval;
            }

            this.UpdateTimer.Interval = this._updateIntervalMs < 50 ? 50 : this._updateIntervalMs;
            this.toolStripTextBox_interval.Text = this._updateIntervalMs.ToString();
            this._persistentSettings.UpdateIntervalMs = this._updateIntervalMs;
            this.SavePersistentSettings();
        }

        private void toolStripTextBox_threshold_TextChanged(object sender, EventArgs e)
        {
            string text = this.toolStripTextBox_threshold.Text;

            // Try parsing as traffic speed with optional suffixes (e.g. "10 MB/s", "500 KB/s"), remove all spaces before parsing
            // Supported suffixes: B/s, kB/s, KB/s, mB/s, MB/s, gB/s, GB/s (kilo, kibi, mega, mebi, giga, giby -Bytes per second) (case-sensitive (!), s/S doesn't matter mean always seconds)
            text = text.Replace(" ", "");

            Regex regex = new(@"^([0-9]+(?:[.,][0-9]+)?)(?i:([kmg]?b)/(s|m|h|d))$");
            if (regex.IsMatch(text))
            {
                // Consecutive numbers substring with optional decimal point (invariant culture ('.' & ','))
                double? value = text.StartsWith("0x") && int.TryParse(text.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out int hexVal)
                    ? hexVal
                    : double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double numVal)
                        ? numVal
                        : (double?) null;
                if (value == null)
                {
                    return;
                }

                TrafficStats.ThresholdBytesPerSecond = 0; // determine by suffix and value, differentiate between kB/KB, mB/MB, gB/GB (kilo, kibi, mega, mebi, giga, giby -Bytes) (case-sensitive (!))
                string suffix = regex.Match(text).Groups[2].Value;
                long multiplier = suffix switch
                {
                    "B" => 1,
                    "kB" or "kib" => 1_000,
                    "KB" => 1_024,
                    "mB" or "mib" => 1_000_000,
                    "MB" => 1_048_576,
                    "gB" or "gib" => 1_000_000_000,
                    "GB" => 1_073_741_824,
                    _ => 0
                };

                // Consider time unit suffix (s, m, h, d) for seconds, minutes, hours, days, apply multiplier accordingly
                string timeSuffix = regex.Match(text).Groups[3].Value.ToLower();
                multiplier *= timeSuffix switch
                {
                    "s" => 1,
                    "m" => 60,
                    "h" => 3600,
                    "d" => 86400,
                    _ => 1
                };

                TrafficStats.ThresholdBytesPerSecond = value.Value * multiplier;
                this._persistentSettings.TrafficThresholdText = this.toolStripTextBox_threshold.Text;
                this.SavePersistentSettings();
            }
        }

        private void SavePersistentSettings()
        {
            WidgetPersistentSettingsStore.Save(this._persistentSettings);
        }
    }
}
