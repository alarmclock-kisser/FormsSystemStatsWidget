using FormsSystemStatsWidget.Core;
using System.Globalization;

namespace FormsSystemStatsWidget.Forms
{
    public partial class WindowWidget
    {
        private delegate bool ParameterParser<T>(string text, out T value);

        /// <summary>
        /// Shared handler for the numeric "commit on Enter" parameter text boxes.
        /// Collapses the duplicated parse/validate/reset/persist boilerplate into a single
        /// guard-clause flow shared by all llama parameter and smart-prompt inputs.
        /// </summary>
        /// <typeparam name="T">Parsed value type.</typeparam>
        /// <param name="e">Key event of the originating text box.</param>
        /// <param name="box">The text box that raised the event.</param>
        /// <param name="tryParse">Parser returning whether the trimmed input is valid; always assigns the out value.</param>
        /// <param name="format">Formats an accepted value back into the text box.</param>
        /// <param name="invalidMessage">Message shown when validation fails.</param>
        /// <param name="fallbackText">Text written back into the box on invalid input.</param>
        /// <param name="onValid">Applies an accepted value (UI echo, bridge, persistence).</param>
        /// <param name="onInvalid">Optional handling for invalid input; receives the parsed-or-default value.</param>
        private void HandleParameterCommit<T>(
            KeyEventArgs e,
            ToolStripTextBox box,
            ParameterParser<T> tryParse,
            Func<T, string> format,
            string invalidMessage,
            string fallbackText,
            Action<T> onValid,
            Action<T>? onInvalid = null)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            e.Handled = true;

            string entered = box.Text.Trim();
            if (tryParse(entered, out T value))
            {
                box.Text = format(value);
                onValid(value);
                return;
            }

            _ = MessageBox.Show(this, invalidMessage, "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            box.Text = fallbackText;
            onInvalid?.Invoke(value);
        }

        private static bool TryParseFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private static string FormatInvariant(float value) => value.ToString(CultureInfo.InvariantCulture);
        private static string FormatInvariant(int value) => value.ToString(CultureInfo.InvariantCulture);

        private void PersistTemperature(float value)
        {
            this._persistentSettings.Temperature = value;
            this.SavePersistentSettings();
        }

        private void toolStripTextBox_temperature_KeyDown(object sender, KeyEventArgs e)
        {
            this.HandleParameterCommit(
                e,
                this.toolStripTextBox_temperature,
                (string text, out float value) => TryParseFloat(text, out value) && value >= 0,
                FormatInvariant,
                "Please enter a valid positive number for temperature.",
                "0.75",
                value =>
                {
                    LlamaOllamaBridge.UserDefinedTemperature = value;
                    this.PersistTemperature(value);
                },
                this.PersistTemperature);
        }

        private void PersistRepetitionPenalty(float value)
        {
            this._persistentSettings.RepetitionPenalty = value;
            this.SavePersistentSettings();
        }

        private void toolStripTextBox_repetationPenalty_KeyDown(object sender, KeyEventArgs e)
        {
            this.HandleParameterCommit(
                e,
                this.toolStripTextBox_repetationPenalty,
                (string text, out float value) => TryParseFloat(text, out value) && value > 0,
                FormatInvariant,
                "Please enter a valid positive number for repetition penalty.",
                "1.1",
                value =>
                {
                    LlamaOllamaBridge.UserDefinedRepetitionPenalty = value;
                    this.PersistRepetitionPenalty(value);
                },
                this.PersistRepetitionPenalty);
        }

        private void PersistContextSize(int value)
        {
            this._persistentSettings.ContextSize = value;
            this.SavePersistentSettings();
        }

        private void toolStripTextBox_contextSize_KeyDown(object sender, KeyEventArgs e)
        {
            this.HandleParameterCommit(
                e,
                this.toolStripTextBox_contextSize,
                (string text, out int value) => int.TryParse(text, out value) && value > 0,
                FormatInvariant,
                "Please enter a valid positive integer for context size.",
                "16384",
                this.PersistContextSize,
                this.PersistContextSize);
        }

        private void PersistBatchSize(int value)
        {
            this._persistentSettings.BatchSize = value;
            this.SavePersistentSettings();
        }

        private void toolStripTextBox_batchSize_KeyDown(object sender, KeyEventArgs e)
        {
            this.HandleParameterCommit(
                e,
                this.toolStripTextBox_batchSize,
                (string text, out int value) => int.TryParse(text, out value) && value > 0,
                FormatInvariant,
                "Please enter a valid positive integer for batch size.",
                "1024",
                this.PersistBatchSize,
                this.PersistBatchSize);
        }

        private void toolStripTextBox_tensorSplit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            e.Handled = true;

            string entered = this.toolStripTextBox_tensorSplit.Text.Trim();
            if (entered == "")
            {
                return;
            }

            // Validate that the entered config is well-formed (x,y,z,...) with at most as many values as GPUs available.
            string[] parts = entered.Split(',');
            if (parts.Length > GpuStats.GpuNames.Count)
            {
                _ = MessageBox.Show(this, $"Please enter a valid tensor split configuration with at most {GpuStats.GpuNames.Count} values (for {GpuStats.GpuNames.Count} GPUs).", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_tensorSplit.Text = "";
            }
        }

        private void PersistGpuLayersCount(int value)
        {
            this._persistentSettings.GpuLayersCount = value;
            this.SavePersistentSettings();
        }

        private void toolStripTextBox_gpuLayersCount_KeyDown(object sender, KeyEventArgs e)
        {
            this.HandleParameterCommit(
                e,
                this.toolStripTextBox_gpuLayersCount,
                (string text, out int value) => int.TryParse(text, out value) && value >= 0,
                FormatInvariant,
                "Please enter a valid non-negative integer for GPU layers count.",
                "0",
                this.PersistGpuLayersCount,
                this.PersistGpuLayersCount);
        }

        private void PersistNumberParallelSlots(int value)
        {
            this._persistentSettings.NumberParallelSlots = value;
            this.SavePersistentSettings();
        }

        private void toolStripTextBox_numberParallelSlots_KeyDown(object sender, KeyEventArgs e)
        {
            this.HandleParameterCommit(
                e,
                this.toolStripTextBox_numberParallelSlots,
                (string text, out int value) => int.TryParse(text, out value) && value > 0,
                FormatInvariant,
                "Please enter a valid positive integer for number of parallel slots.",
                "1",
                this.PersistNumberParallelSlots,
                this.PersistNumberParallelSlots);
        }

        private void PersistReasoningBudget(int value)
        {
            this._persistentSettings.ReasoningBudget = value;
            this.SavePersistentSettings();
        }

        private void toolStripTextBox_reasoningBudget_KeyDown(object sender, KeyEventArgs e)
        {
            this.HandleParameterCommit(
                e,
                this.toolStripTextBox_reasoningBudget,
                (string text, out int value) => int.TryParse(text, out value) && value >= 0,
                FormatInvariant,
                "Please enter a valid non-negative integer for reasoning budget (tokens).",
                "0",
                value =>
                {
                    LlamaOllamaBridge.UserDefinedReasoningBudget = value;
                    this.PersistReasoningBudget(value);
                },
                this.PersistReasoningBudget);
        }

        private void toolStripTextBox_topP_KeyDown(object sender, KeyEventArgs e)
        {
            this.HandleParameterCommit(
                e,
                this.toolStripTextBox_topP,
                (string text, out float value) => TryParseFloat(text, out value) && value > 0 && value <= 1,
                FormatInvariant,
                "Please enter a valid number between 0 and 1 for top-p.",
                "0.9",
                value =>
                {
                    LlamaOllamaBridge.UserDefinedTopP = value;
                    this._persistentSettings.UserTopP = value;
                    this.SavePersistentSettings();
                });
        }

        private void toolStripTextBox_minP_KeyDown(object sender, KeyEventArgs e)
        {
            this.HandleParameterCommit(
                e,
                this.toolStripTextBox_minP,
                (string text, out float value) => TryParseFloat(text, out value) && value >= 0 && value < 1,
                FormatInvariant,
                "Please enter a valid number between 0 and 1 for min-p.",
                "0.0",
                value =>
                {
                    LlamaOllamaBridge.UserDefinedMinP = value;
                    this._persistentSettings.UserMinP = value;
                    this.SavePersistentSettings();
                });
        }

        private void toolStripTextBox_topK_KeyDown(object sender, KeyEventArgs e)
        {
            this.HandleParameterCommit(
                e,
                this.toolStripTextBox_topK,
                (string text, out int value) => int.TryParse(text, out value) && value >= 0,
                FormatInvariant,
                "Please enter a valid non-negative integer for top-k.",
                "40",
                value =>
                {
                    LlamaOllamaBridge.UserDefinedTopK = value;
                    this._persistentSettings.UserTopK = value;
                    this.SavePersistentSettings();
                });
        }
    }
}
