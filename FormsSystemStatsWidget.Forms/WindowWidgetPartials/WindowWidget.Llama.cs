using FormsSystemStatsWidget.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace FormsSystemStatsWidget.Forms
{
    public partial class WindowWidget
    {
        [GeneratedRegex(@" (?=-)")]
        private static partial Regex ArgsSplitRegex();



        private async void rerouteAPILlamacppOllamaToolStripMenuItem_CheckedChanged(object? sender, EventArgs e)
        {
            var menuItem = (ToolStripMenuItem?) sender;
            if (menuItem == null)
            {
                return;
            }
            menuItem.Enabled = false;

            if (menuItem.Checked)
            {
                int llamaPort = int.TryParse(this.toolStripTextBox_llamacppPort.Text.Trim(), out int parsedLlamaPort) ? parsedLlamaPort : 8080;
                this.toolStripTextBox_llamacppPort.Text = llamaPort.ToString();
                int ollamaPort = int.TryParse(this.toolStripTextBox_ollamaPort.Text.Trim(), out int parsedOllamaPort) ? parsedOllamaPort : 11434;
                this.toolStripTextBox_ollamaPort.Text = ollamaPort.ToString();

                bool isStarted = await LlamaOllamaBridge.StartAsync(llamaPort, ollamaPort);

                // Jetzt ContextMenu wieder einklappen / schließen
                this.ContextMenuStrip?.Close();

                if (isStarted)
                {
                    this.label_routingPortsInfo.Text = $"Port {llamaPort} to {ollamaPort}";
                    this.label_routingPortsInfo.ForeColor = Color.DarkGreen;
                    this.label_routingPortsInfo.Visible = true;
                }
                else
                {
                    menuItem.CheckedChanged -= this.rerouteAPILlamacppOllamaToolStripMenuItem_CheckedChanged;
                    menuItem.Checked = false;
                    menuItem.CheckedChanged += this.rerouteAPILlamacppOllamaToolStripMenuItem_CheckedChanged;

                    this.label_routingPortsInfo.Text = $"Port {llamaPort} to {ollamaPort} failed";
                    this.label_routingPortsInfo.ForeColor = Color.Red;
                    this.label_routingPortsInfo.Visible = true;

                    MessageBox.Show($"Connection to llama-server (Port {llamaPort}) failed or Port {ollamaPort} is blocked!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                LlamaOllamaBridge.Stop(); // Keine asynchrone Task mehr nötig beim Stoppen

                this.label_routingPortsInfo.Text = "Port ----- to -----";
                this.label_routingPortsInfo.ForeColor = Color.Black;
                this.label_routingPortsInfo.Visible = false;
            }

            menuItem.Enabled = true;
        }

        private void openDebugConsoleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this._debugConsoleForm == null || this._debugConsoleForm.IsDisposed)
            {
                this._debugConsoleForm = new DebugConsoleForm();
                this._debugConsoleForm.FormClosed += (_, _) => this._debugConsoleForm = null;

                Point openPosition = new(this.Left + this.Width + 8, this.Top);
                this._debugConsoleForm.Location = openPosition;
                this._debugConsoleForm.Show();

                this._debugConsoleForm.ClearLogs();
                string[] recentEntries = Logger.GetRecentEntries();
                foreach (string recentEntry in recentEntries)
                {
                    this._debugConsoleForm.AppendLogLine(recentEntry);
                }

                this._debugConsoleForm.AppendLogLine("Debug-Console opened. (Formatted Logging: " + LlamaOllamaBridge.EnableFormattedLogging + ", Raw Chunk Logging: " + LlamaOllamaBridge.EnableRawChunkLogging + ", Log Generation Speed: " + this.toolStripMenuItem_logGenerationSpeed.Checked + ")");
            }
            else
            {
                this._debugConsoleForm.Close();
                this._debugConsoleForm = null;
            }
        }

        private void toolStripMenuItem_visuallyFormatLog_Click(object sender, EventArgs e)
        {
            LlamaOllamaBridge.EnableFormattedLogging = this.toolStripMenuItem_visuallyFormatLog.Checked;
        }

        private void toolStripMenuItem_includeRawChunksLog_Click(object sender, EventArgs e)
        {
            LlamaOllamaBridge.EnableRawChunkLogging = this.toolStripMenuItem_includeRawChunksLog.Checked;
        }

        private void toolStripTextBox_modelsDirectory_Leave(object sender, EventArgs e)
        {
            // Validate the path and update the setting if it's a valid directory
            string path = this.toolStripTextBox_modelsDirectory.Text.Trim();
            if (Directory.Exists(path))
            {
                LlamaCppModelLoader.GgufModelsDirectory = Path.GetFullPath(path);
            }
            else
            {
                MessageBox.Show(this, "The specified directory does not exist. Please enter a valid path.", "Invalid Directory", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_modelsDirectory.Text = LlamaCppModelLoader.GgufModelsDirectory;
            }
        }


        // Load llama-server.exe GGUF model
        private void toolStripMenuItem_loadLlamaCppServer_Click(object sender, EventArgs e)
        {
            string? selectedModel = this.toolStripComboBox_ggufModels.SelectedItem as string;
            if (selectedModel == null)
            {
                MessageBox.Show(this, "No model selected. Please select a model from the dropdown list.", "No Model Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!File.Exists(selectedModel))
            {
                // Try to find the model file name with or without extension in the specified models directory
                selectedModel = LlamaCppModelLoader.ModelFilePaths.FirstOrDefault(path => path.Contains(selectedModel, StringComparison.OrdinalIgnoreCase));
                if (!File.Exists(selectedModel))
                {
                    MessageBox.Show(this, $"The selected model file does not exist:\n{selectedModel}", "Model File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            string? mmprojFilePath = this.toolStripMenuItem_loadMmproj.Checked ? LlamaCppModelLoader.GetModelMmprojFilePath(selectedModel) : null;

            int contextSize = this.toolStripTextBox_contextSize.Text.Trim() != "" && int.TryParse(this.toolStripTextBox_contextSize.Text.Trim(), out int parsedContextSize) ? parsedContextSize : 8192;
            int batchSize = this.toolStripTextBox_batchSize.Text.Trim() != "" && int.TryParse(this.toolStripTextBox_batchSize.Text.Trim(), out int parsedBatchSize) ? parsedBatchSize : 1024;
            string splitMode = this.toolStripComboBox_splitMode.SelectedItem as string ?? "none";
            // Parse tensor split configuration from x, y, z, ... format into int[] (if mode is none => [])
            int[] tensorSplit = splitMode != "none" && this.toolStripTextBox_tensorSplit.Text.Trim() != "" ? this.toolStripTextBox_tensorSplit.Text.Trim().Split(',').Select(s => int.TryParse(s.Trim(), out int value) ? value : 0).Where(v => v > 0).ToArray() : [];
            bool flashAttention = this.toolStripMenuItem_flashAttention.Checked;
            int gpuLayersCount = this.toolStripTextBox_gpuLayersCount.Text.Trim() != "" && int.TryParse(this.toolStripTextBox_gpuLayersCount.Text.Trim(), out int parsedGpuLayerCount) ? parsedGpuLayerCount : 0;
            int numParallelSlots = this.toolStripTextBox_numberParallelSlots.Text.Trim() != "" && int.TryParse(this.toolStripTextBox_numberParallelSlots.Text.Trim(), out int parsedParallelSlots) ? parsedParallelSlots : 1;
            bool noWarmup = this.toolStripMenuItem_noWarmup.Checked;
            bool kvOffload = this.KVoffload_ToolStripMenuItem.Checked;
            bool fitMode = this.toolStripMenuItem_fitMode.Checked;
            int? thinkingBudget = this.toolStripTextBox_thinkingBudget.Text.Trim() != "" && int.TryParse(this.toolStripTextBox_thinkingBudget.Text.Trim(), out int parsedThinkingBudget) ? parsedThinkingBudget : null;

            // Aggregate CMD call
            var sb = new StringBuilder();
            sb.Append($"llama-server ");
            sb.Append($"-m \"{selectedModel}\" ");
            if (mmprojFilePath != null)
            {
                sb.Append($"--mmproj \"{mmprojFilePath}\" ");
            }
            sb.Append($"-c {contextSize} ");
            sb.Append($"-b {batchSize} ");
            if (splitMode != "none")
            {
                sb.Append($"-sm {splitMode} ");
                if (tensorSplit.Length > 0)
                {
                    sb.Append($"-ts {string.Join(",", tensorSplit)} ");
                }
            }
            sb.Append("-fa " + (flashAttention ? "on " : "off "));
            sb.Append("-ngl " + gpuLayersCount + " ");
            sb.Append("-np " + numParallelSlots + " ");
            if (noWarmup)
            {
                sb.Append("--no-warmup ");
            }
            if (kvOffload)
            {
                sb.Append("--kv-offload ");
            }
            else
            {
                sb.Append("--no-kv-offload ");
            }
            sb.Append("-fit " + (fitMode ? "on " : "off "));
            if (thinkingBudget.HasValue)
            {
                sb.Append($"-tb {thinkingBudget.Value} ");
            }

            // Get multiline string, split at every arg (starting with " -" or " --")
            string command = sb.ToString().Trim();
            command = ArgsSplitRegex().Replace(command, Environment.NewLine + " ");

            // Show the aggregated command in a MessageBox for confirmation
            DialogResult result = MessageBox.Show(this, $"The following command will be executed to start llama-server with the selected model and options:\n\n{command}\n\nDo you want to proceed?", "Confirm Command", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {sb}",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to start llama-server with the selected model. Error: {ex.Message}", "Error Starting Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void toolStripComboBox_splitMode_SelectedChanged(object sender, EventArgs e)
        {
            string? selectedSplitMode = this.toolStripComboBox_splitMode.SelectedItem as string;
            if (selectedSplitMode == null || selectedSplitMode == "none")
            {
                this.toolStripMenuItem_tensorSplit.Enabled = false;
                this.toolStripTextBox_tensorSplit.Enabled = false;
                this.toolStripTextBox_tensorSplit.Text = "";
            }
            else
            {
                this.toolStripMenuItem_tensorSplit.Enabled = true;
                this.toolStripTextBox_tensorSplit.Enabled = true;

                // Get GPUs VRAM capacities in int rounded GB, set default tensor split config to gpu1_vram, gpu2_vram, ...
                long gpu1VramGb = this.Gpu != null ? (long) Math.Round(this.Gpu.GetTotalVramBytes() / 1_073_741_824.0) : 0;
                long gpu2VramGb = this.Gpu2 != null ? (long) Math.Round(this.Gpu2.GetTotalVramBytes() / 1_073_741_824.0) : 0;
                if (gpu1VramGb > 0 && gpu2VramGb > 0)
                {
                    this.toolStripTextBox_tensorSplit.Text = $"{gpu1VramGb},{gpu2VramGb}";
                }
                else
                {
                    this.toolStripTextBox_tensorSplit.Text = "";
                }
            }
        }

        private void toolStripComboBox_ggufModels_SelectedIndexChanged(object sender, EventArgs e)
        {
            string? selectedModel = this.toolStripComboBox_ggufModels.SelectedItem as string;
            if (selectedModel == null)
            {
                return;
            }

            string? mmprojPath = LlamaCppModelLoader.GetModelMmprojFilePath(selectedModel);
            this.toolStripMenuItem_loadMmproj.Enabled = mmprojPath != null;
            if (!this.toolStripMenuItem_loadMmproj.Enabled || mmprojPath == null)
            {
                this.toolStripMenuItem_loadMmproj.Text = "No MMProj available.";
                this.toolStripMenuItem_loadMmproj.Checked = false;
            }
            else
            {
                float mmprojSizeMb = new FileInfo(mmprojPath).Length / 1_048_576f;
                this.toolStripMenuItem_loadMmproj.Text = $"Try load MMProj (+ {mmprojSizeMb:F2} MB)";
                this.toolStripMenuItem_loadMmproj.Checked = true;
            }
        }

        private void toolStripTextBox_temperature_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_temperature.Text.Trim();
            if (float.TryParse(entered, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float temperature) && temperature > 0)
            {
                this.toolStripTextBox_temperature.Text = temperature.ToString(System.Globalization.CultureInfo.InvariantCulture);
                LlamaOllamaBridge.UserDefinedTemperature = (double) temperature;
            }
            else
            {
                MessageBox.Show(this, "Please enter a valid positive number for temperature.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_temperature.Text = "0.75";
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_repetationPenalty_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_repetationPenalty.Text.Trim();
            if (float.TryParse(entered, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float penalty) && penalty > 0)
            {
                this.toolStripTextBox_repetationPenalty.Text = penalty.ToString(System.Globalization.CultureInfo.InvariantCulture);
                LlamaOllamaBridge.UserDefinedRepetitionPenalty = (double) penalty;
            }
            else
            {
                MessageBox.Show(this, "Please enter a valid positive number for repetition penalty.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_repetationPenalty.Text = "1.1";
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_contextSize_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_contextSize.Text.Trim();
            if (int.TryParse(entered, out int contextSize) && contextSize > 0)
            {
                this.toolStripTextBox_contextSize.Text = contextSize.ToString();
            }
            else
            {
                MessageBox.Show(this, "Please enter a valid positive integer for context size.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_contextSize.Text = "16384";
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_batchSize_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_batchSize.Text.Trim();
            if (int.TryParse(entered, out int batchSize) && batchSize > 0)
            {
                this.toolStripTextBox_batchSize.Text = batchSize.ToString();
            }
            else
            {
                MessageBox.Show(this, "Please enter a valid positive integer for batch size.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_batchSize.Text = "1024";
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_tensorSplit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_tensorSplit.Text.Trim();
            if (entered == "")
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }
            else
            {
                // Validate wheter the entered config is in correct format (x,y,z,...) and all values are positive integers and not more integers than GPUs available
                string[] parts = entered.Split(',');
                if (parts.Length > GpuStats.GpuNames.Count)
                {
                    MessageBox.Show(this, $"Please enter a valid tensor split configuration with at most {GpuStats.GpuNames.Count} values (for {GpuStats.GpuNames.Count} GPUs).", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.toolStripTextBox_tensorSplit.Text = "";
                }
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_gpuLayersCount_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_gpuLayersCount.Text.Trim();
            if (int.TryParse(entered, out int gpuLayerCount) && gpuLayerCount >= 0)
            {
                this.toolStripTextBox_gpuLayersCount.Text = gpuLayerCount.ToString();
            }
            else
            {
                MessageBox.Show(this, "Please enter a valid non-negative integer for GPU layers count.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_gpuLayersCount.Text = "0";
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_numberParallelSlots_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_numberParallelSlots.Text.Trim();
            if (int.TryParse(entered, out int parallelSlots) && parallelSlots > 0)
            {
                this.toolStripTextBox_numberParallelSlots.Text = parallelSlots.ToString();
            }
            else
            {
                MessageBox.Show(this, "Please enter a valid positive integer for number of parallel slots.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_numberParallelSlots.Text = "1";
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_thinkingBudget_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_thinkingBudget.Text.Trim();
            if (int.TryParse(entered, out int thinkingBudget) && thinkingBudget >= 0)
            {
                this.toolStripTextBox_thinkingBudget.Text = thinkingBudget.ToString();
                LlamaOllamaBridge.UserDefinedThinkingBudget = thinkingBudget;
            }
            else
            {
                MessageBox.Show(this, "Please enter a valid non-negative integer for thinking budget (tokens).", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_thinkingBudget.Text = "0";
            }
        }

    }
}
