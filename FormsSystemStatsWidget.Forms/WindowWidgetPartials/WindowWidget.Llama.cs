using FormsSystemStatsWidget.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
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
                string apiUrl = this.toolStripTextBox_openAiApiUrl.Text.Trim();
                int? apiUrlPort = apiUrl != "" && Uri.TryCreate(apiUrl, UriKind.Absolute, out Uri? parsedUri) && parsedUri.IsLoopback ? parsedUri.Port : null;
                apiUrl = apiUrl.Replace("http://", "").Replace("https://", "").Split(':').FirstOrDefault() ?? apiUrl;
                int llamaPort = apiUrlPort == null ? int.TryParse(this.toolStripTextBox_llamacppPort.Text.Trim(), out int parsedLlamaPort) ? parsedLlamaPort : 8080 : apiUrlPort.Value;
                this.toolStripTextBox_llamacppPort.Text = llamaPort.ToString();
                int ollamaPort = int.TryParse(this.toolStripTextBox_ollamaPort.Text.Trim(), out int parsedOllamaPort) ? parsedOllamaPort : 11434;
                this.toolStripTextBox_ollamaPort.Text = ollamaPort.ToString();

                bool isStarted = await LlamaOllamaBridge.StartAsync(apiUrl, llamaPort, ollamaPort);

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

                    string bridgeError = LlamaOllamaBridge.LastStartError;
                    string message = string.IsNullOrWhiteSpace(bridgeError)
                        ? $"Connection to llama-server (Port {llamaPort}) failed or Port {ollamaPort} is blocked!"
                        : $"Connection setup failed:{Environment.NewLine}{bridgeError}";

                    MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                this._debugConsoleForm.TopMost = this.alwaysOnTopToolStripMenuItem.Checked;

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
            this._persistentSettings.DebugConsoleFormattedLog = this.toolStripMenuItem_visuallyFormatLog.Checked;
            this.SavePersistentSettings();
        }

        private void toolStripMenuItem_includeRawChunksLog_Click(object sender, EventArgs e)
        {
            LlamaOllamaBridge.EnableRawChunkLogging = this.toolStripMenuItem_includeRawChunksLog.Checked;
            this._persistentSettings.DebugConsoleIncludeRawChunks = this.toolStripMenuItem_includeRawChunksLog.Checked;
            this.SavePersistentSettings();
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
        private void toolStripMenuItem_loadLlamaCppServer_Click(object? sender, EventArgs e)
        {
            string? selectedModel = this.toolStripComboBox_ggufModels.SelectedItem as string;
            this.ContextMenuStrip?.Close();
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
            float topP = this.toolStripTextBox_topP.Text.Trim() != "" && float.TryParse(this.toolStripTextBox_topP.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float parsedTopP) ? parsedTopP : 0.9f;
            float minP = this.toolStripTextBox_minP.Text.Trim() != "" && float.TryParse(this.toolStripTextBox_minP.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float parsedMinP) ? parsedMinP : 0.0f;
            int topK = this.toolStripTextBox_topK.Text.Trim() != "" && int.TryParse(this.toolStripTextBox_topK.Text.Trim(), out int parsedTopK) ? parsedTopK : 40;

            // Persist the chosen values so subsequent model loads use them
            this._persistentSettings.UserTopP = topP;
            this._persistentSettings.UserMinP = minP;
            this._persistentSettings.UserTopK = topK;
            this.SavePersistentSettings();

            // Aggregate CMD call (Single Line)
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

            // HIER DIE ANPASSUNG: Erzeugt das korrekte Multiline-Format mit dem Windows-Line-Continuation-Zeichen (^)
            string command = sb.ToString().Trim();
            command = ArgsSplitRegex().Replace(command, " ^" + Environment.NewLine + " ");

            // Show the aggregated command in a MessageBox for confirmation
            DialogResult result = MessageBox.Show(this, $"The following command will be executed to start llama-server with the selected model and options:\n\n{command}\n\nDo you want to save the current configuration?\nPress Yes to save, No to proceed without saving, or Cancel to abort.", "Confirm Command", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Cancel)
            {
                return;
            }
            if (result == DialogResult.Yes)
            {
                // SFD with default file name "LOAD_[MODELNAME].BAT" and default directory to save in
                string batName = "LOAD_" + Path.GetFileNameWithoutExtension(selectedModel) + ".BAT";
                var dlg = new SaveFileDialog
                {
                    Title = "Save Model Load Configuration as Batch File",
                    Filter = "Batch Files (*.bat)|*.bat",
                    FileName = batName,
                    InitialDirectory = WidgetStatics.GetRepositoryDirectory(".Forms", "Ressources\\LlamaCppLoad_BATs")
                };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        // Schreibt den Header, den sauber umgebrochenen Befehl und das abschließende "pause"
                        File.WriteAllText(dlg.FileName, $"@echo off{Environment.NewLine}title llama-server: {Path.GetFileNameWithoutExtension(selectedModel)}{Environment.NewLine}{command}{Environment.NewLine}{Environment.NewLine}pause");
                        Logger.Log($" -- Saved batch file for loading model: {dlg.FileName} -- ");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Failed to save the batch file. Error: {ex.Message}", "Error Saving Batch File", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }

            try
            {
                // Nutzt sb (die einzeilige Variante), da cmd.exe /c mit echten Zeilenumbrüchen in den Arguments zicken würde
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {sb.ToString().Trim()}",
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

        private void toolStripMenuItem_execModelLoadBat_Click(object? sender, EventArgs e)
        {
            string? selectedBatName = this.toolStripComboBox_modelLoadBats.SelectedItem as string;
            this.ContextMenuStrip?.Close();
            if (string.IsNullOrEmpty(selectedBatName))
            {
                MessageBox.Show(this, "No batch file selected. Please select a batch file from the dropdown list.", "No Batch File Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string batFilePath = Path.Combine(WidgetStatics.GetRepositoryDirectory(".Forms", "Ressources\\LlamaCppLoad_BATs"), selectedBatName + ".BAT");
            if (!File.Exists(batFilePath))
            {
                MessageBox.Show(this, $"The selected batch file does not exist:\n{batFilePath}", "Batch File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batFilePath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to execute the selected batch file. Error: {ex.Message}", "Error Executing Batch File", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ToolStripMenuItem_killLlamaServer_Click(object? sender, EventArgs e)
        {
            try
            {
                int? killed = WidgetStatics.KillLlamaServerProcesses();
                Logger.Log($"[WindowWidget] Killed {killed} llama-server process(es).");
                this.rerouteAPILlamacppOllamaToolStripMenuItem.Checked = false; 
                this.ContextMenuStrip?.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to kill llama-server processes. Error: {ex.Message}", "Error Killing Processes", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_topP_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_topP.Text.Trim();
            if (float.TryParse(entered, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float topP) && topP > 0 && topP <= 1)
            {
                this.toolStripTextBox_topP.Text = topP.ToString(System.Globalization.CultureInfo.InvariantCulture);
                LlamaOllamaBridge.UserDefinedTopP = (double) topP;
                this._persistentSettings.UserTopP = topP;
                this.SavePersistentSettings();
            }
            else
            {
                MessageBox.Show(this, "Please enter a valid number between 0 and 1 for top-p.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_topP.Text = "0.9";
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_minP_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_minP.Text.Trim();
            if (float.TryParse(entered, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float minP) && minP >= 0 && minP < 1)
            {
                this.toolStripTextBox_minP.Text = minP.ToString(System.Globalization.CultureInfo.InvariantCulture);
                LlamaOllamaBridge.UserDefinedMinP = (double) minP;
                this._persistentSettings.UserMinP = minP;
                this.SavePersistentSettings();
            }
            else
            {
                MessageBox.Show(this, "Please enter a valid number between 0 and 1 for min-p.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_minP.Text = "0.0";
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_topK_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_topK.Text.Trim();
            if (int.TryParse(entered, out int topK) && topK >= 0)
            {
                this.toolStripTextBox_topK.Text = topK.ToString();
                LlamaOllamaBridge.UserDefinedTopK = topK;
                this._persistentSettings.UserTopK = topK;
                this.SavePersistentSettings();
            }
            else
            {
                MessageBox.Show(this, "Please enter a valid non-negative integer for top-k.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_topK.Text = "40";
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void showUsageToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            this.toolStripTextBox_percentageColor.Enabled = this.showUsageToolStripMenuItem.Checked;
            this._persistentSettings.ShowPerCorePercent = this.showUsageToolStripMenuItem.Checked;
            this.SavePersistentSettings();
        }

        private void showTokenssToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            this._persistentSettings.ShowTokensPerSecond = this.showTokenssToolStripMenuItem.Checked;
            this.SavePersistentSettings();
        }

        private void toolStripMenuItem_logGenerationSpeed_CheckedChanged(object sender, EventArgs e)
        {
            this._persistentSettings.DebugConsoleLogGenerationSpeed = this.toolStripMenuItem_logGenerationSpeed.Checked;
            this.SavePersistentSettings();
        }

        private void enableSmartPromptOptimizationsToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            SmartPromptOptimizationSettings.IsEnabled = this.enableSmartPromptOptimizationsToolStripMenuItem.Checked;
            this._persistentSettings.SmartPromptEnabled = SmartPromptOptimizationSettings.IsEnabled;
            this.SavePersistentSettings();
        }

        private void toolStripTextBox_promptSafetyRatio_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_promptSafetyRatio.Text.Trim();
            if (double.TryParse(entered, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double value) && value > 0.1 && value <= 1.0)
            {
                SmartPromptOptimizationSettings.PromptSafetyRatio = value;
                this.toolStripTextBox_promptSafetyRatio.Text = value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                this._persistentSettings.SmartPromptSafetyRatio = value;
                this.SavePersistentSettings();
            }
            else
            {
                MessageBox.Show(this, "Please enter a valid number between 0.10 and 1.00 for prompt safety ratio.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_promptSafetyRatio.Text = SmartPromptOptimizationSettings.PromptSafetyRatio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_smartBudgetRatio_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_smartBudgetRatio.Text.Trim();
            if (double.TryParse(entered, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double value) && value > 0.1 && value <= 1.0)
            {
                SmartPromptOptimizationSettings.SmartBudgetRatio = value;
                this.toolStripTextBox_smartBudgetRatio.Text = value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                this._persistentSettings.SmartPromptBudgetRatio = value;
                this.SavePersistentSettings();
            }
            else
            {
                MessageBox.Show(this, "Please enter a valid number between 0.10 and 1.00 for smart budget ratio.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_smartBudgetRatio.Text = SmartPromptOptimizationSettings.SmartBudgetRatio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_largeMessageThresholdChars_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_largeMessageThresholdChars.Text.Trim();
            if (int.TryParse(entered, out int value) && value >= 256)
            {
                SmartPromptOptimizationSettings.LargeMessageThresholdChars = value;
                this.toolStripTextBox_largeMessageThresholdChars.Text = value.ToString();
                this._persistentSettings.SmartPromptLargeMessageThresholdChars = value;
                this.SavePersistentSettings();
            }
            else
            {
                MessageBox.Show(this, "Please enter a valid integer >= 256 for large message threshold chars.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_largeMessageThresholdChars.Text = SmartPromptOptimizationSettings.LargeMessageThresholdChars.ToString();
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_skeletonMaxLines_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_skeletonMaxLines.Text.Trim();
            if (int.TryParse(entered, out int value) && value >= 5)
            {
                SmartPromptOptimizationSettings.SkeletonMaxLines = value;
                this.toolStripTextBox_skeletonMaxLines.Text = value.ToString();
                this._persistentSettings.SmartPromptSkeletonMaxLines = value;
                this.SavePersistentSettings();
            }
            else
            {
                MessageBox.Show(this, "Please enter a valid integer >= 5 for skeleton max lines.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_skeletonMaxLines.Text = SmartPromptOptimizationSettings.SkeletonMaxLines.ToString();
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_focusKeywordLimit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_focusKeywordLimit.Text.Trim();
            if (int.TryParse(entered, out int value) && value >= 1)
            {
                SmartPromptOptimizationSettings.FocusKeywordLimit = value;
                this.toolStripTextBox_focusKeywordLimit.Text = value.ToString();
                this._persistentSettings.SmartPromptFocusKeywordLimit = value;
                this.SavePersistentSettings();
            }
            else
            {
                MessageBox.Show(this, "Please enter a valid integer >= 1 for focus keyword limit.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_focusKeywordLimit.Text = SmartPromptOptimizationSettings.FocusKeywordLimit.ToString();
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_tailKeepBonusChars_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_tailKeepBonusChars.Text.Trim();
            if (int.TryParse(entered, out int value) && value >= 0)
            {
                SmartPromptOptimizationSettings.TailKeepBonusChars = value;
                this.toolStripTextBox_tailKeepBonusChars.Text = value.ToString();
                this._persistentSettings.SmartPromptTailKeepBonusChars = value;
                this.SavePersistentSettings();
            }
            else
            {
                MessageBox.Show(this, "Please enter a valid integer >= 0 for tail keep bonus chars.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_tailKeepBonusChars.Text = SmartPromptOptimizationSettings.TailKeepBonusChars.ToString();
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

    }
}
