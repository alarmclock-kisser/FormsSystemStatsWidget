using FormsSystemStatsWidget.Core;
using FormsSystemStatsWidget.Forms.Services;
using Microsoft.VisualBasic;
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

            // Invoke if required
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => this.rerouteAPILlamacppOllamaToolStripMenuItem_CheckedChanged(sender, e)));
                return;
            }

            if (menuItem.Checked)
            {
                await this.EnableBridgeRouteAsync(menuItem);
            }
            else
            {
                this.DisableBridgeRoute();
            }

            menuItem.Enabled = true;
        }

        private async Task EnableBridgeRouteAsync(ToolStripMenuItem menuItem)
        {
            var cur = this.Cursor;
            this.Cursor = Cursors.WaitCursor;

            string apiUrl = this.toolStripTextBox_openAiApiUrl.Text.Trim();
            int? apiUrlPort = apiUrl != "" && Uri.TryCreate(apiUrl, UriKind.Absolute, out Uri? parsedUri) && parsedUri.IsLoopback ? parsedUri.Port : null;
            apiUrl = apiUrl.Replace("http://", "").Replace("https://", "").Split(':').FirstOrDefault() ?? apiUrl;
            int llamaPort = apiUrlPort == null ? int.TryParse(this.toolStripTextBox_llamacppPort.Text.Trim(), out int parsedLlamaPort) ? parsedLlamaPort : 8080 : apiUrlPort.Value;
            this.toolStripTextBox_llamacppPort.Text = llamaPort.ToString();
            int ollamaPort = int.TryParse(this.toolStripTextBox_ollamaPort.Text.Trim(), out int parsedOllamaPort) ? parsedOllamaPort : 11434;
            this.toolStripTextBox_ollamaPort.Text = ollamaPort.ToString();

            bool isStarted = await LlamaOllamaBridge.StartAsync(apiUrl, llamaPort, ollamaPort);
            this.Cursor = cur;
            this.ContextMenuStrip?.Close();

            if (isStarted)
            {
                this.SetRoutingInfoLabel($"Port {llamaPort} to {ollamaPort}", Color.DarkGreen, true);
                return;
            }

            menuItem.CheckedChanged -= this.rerouteAPILlamacppOllamaToolStripMenuItem_CheckedChanged;
            menuItem.Checked = false;
            menuItem.CheckedChanged += this.rerouteAPILlamacppOllamaToolStripMenuItem_CheckedChanged;

            this.SetRoutingInfoLabel($"Port {llamaPort} to {ollamaPort} failed", Color.Red, true);

            string bridgeError = LlamaOllamaBridge.LastStartError;
            string message = string.IsNullOrWhiteSpace(bridgeError)
                ? $"Connection to llama-server (Port {llamaPort}) failed or Port {ollamaPort} is blocked!"
                : $"Connection setup failed:{Environment.NewLine}{bridgeError}";

            _ = MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void DisableBridgeRoute()
        {
            LlamaOllamaBridge.Stop();
            this.SetRoutingInfoLabel("Port ----- to -----", Color.Black, false);
        }

        private void SetRoutingInfoLabel(string text, Color color, bool visible)
        {
            this.label_routingPortsInfo.Text = text;
            this.label_routingPortsInfo.ForeColor = color;
            this.label_routingPortsInfo.Visible = visible;
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
                _ = MessageBox.Show(this, "The specified directory does not exist. Please enter a valid path.", "Invalid Directory", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_modelsDirectory.Text = LlamaCppModelLoader.GgufModelsDirectory;
            }

            // Add to persistent settings
            this._persistentSettings.GgufModelDirectory = LlamaCppModelLoader.GgufModelsDirectory;
            this.SavePersistentSettings();
        }


        // Load llama-server.exe GGUF model
        private void toolStripMenuItem_loadLlamaCppServer_Click(object? sender, EventArgs e)
        {
            string? selectedModel = this.toolStripComboBox_ggufModels.SelectedItem as string;
            this.ContextMenuStrip?.Close();
            if (selectedModel == null)
            {
                _ = MessageBox.Show(this, "No model selected. Please select a model from the dropdown list.", "No Model Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!File.Exists(selectedModel))
            {
                // Try to find the model file name with or without extension in the specified models directory
                selectedModel = LlamaCppModelLoader.ModelFilePaths.FirstOrDefault(path => path.Contains(selectedModel, StringComparison.OrdinalIgnoreCase));
                if (!File.Exists(selectedModel))
                {
                    _ = MessageBox.Show(this, $"The selected model file does not exist:\n{selectedModel}", "Model File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            int? reasoningBudget = this.toolStripTextBox_reasoningBudget.Text.Trim() != "" && int.TryParse(this.toolStripTextBox_reasoningBudget.Text.Trim(), out int parsedReasoningBudget) ? parsedReasoningBudget : null;
            float topP = this.toolStripTextBox_topP.Text.Trim() != "" && float.TryParse(this.toolStripTextBox_topP.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float parsedTopP) ? parsedTopP : 0.9f;
            float minP = this.toolStripTextBox_minP.Text.Trim() != "" && float.TryParse(this.toolStripTextBox_minP.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float parsedMinP) ? parsedMinP : 0.0f;
            int topK = this.toolStripTextBox_topK.Text.Trim() != "" && int.TryParse(this.toolStripTextBox_topK.Text.Trim(), out int parsedTopK) ? parsedTopK : 40;
            string kvCacheQuant = (this.toolStripComboBox_cacheType.SelectedItem as string ?? "f16").ToLowerInvariant();
            bool toolCalling = this.toolStripMenuItem_toolCalls.Checked;
            string additionalArgs = this.toolStripTextBox_additionalArgs.Text.Trim();

            // Persist the chosen values so subsequent model loads use them
            this._persistentSettings.UserTopP = topP;
            this._persistentSettings.UserMinP = minP;
            this._persistentSettings.UserTopK = topK;
            this.SavePersistentSettings();

            // Aggregate CMD call (Single Line)
            var sb = new StringBuilder();
            _ = sb.Append($"llama-server ");
            _ = sb.Append($"-m \"{selectedModel}\" ");
            if (mmprojFilePath != null)
            {
                _ = sb.Append($"--mmproj \"{mmprojFilePath}\" ");
            }
            _ = sb.Append($"-c {contextSize} ");
            _ = sb.Append($"-b {batchSize} ");
            _ = sb.Append($"-ub {(int)(batchSize / 2)} ");
            if (splitMode != "none")
            {
                _ = sb.Append($"-sm {splitMode} ");
                if (tensorSplit.Length > 0)
                {
                    _ = sb.Append($"-ts {string.Join(",", tensorSplit)} ");
                }
            }
            _ = sb.Append("-fa " + (flashAttention ? "on " : "off "));
            _ = sb.Append("-ngl " + gpuLayersCount + " ");
            _ = sb.Append("-np " + numParallelSlots + " ");
            if (noWarmup)
            {
                _ = sb.Append("--no-warmup ");
                _ = sb.Append("--no-mmap ");
            }
            if (kvOffload)
            {
                _ = sb.Append("--kv-offload ");
            }
            else
            {
                _ = sb.Append("--no-kv-offload ");
            }
            _ = sb.Append("-fit " + (fitMode ? "on " : "off "));
            if (thinkingBudget.HasValue)
            {
                _ = sb.Append($"-tb {thinkingBudget.Value} ");
            }
            if (reasoningBudget.HasValue)
            {
                _ = sb.Append($"--reasoning-budget {reasoningBudget.Value} ");
            }
            if (toolCalling)
            {
                _ = sb.Append("--tools all ");
            }
            if (kvCacheQuant != "f16")
            {
                _ = sb.Append($"--cache-type-k {kvCacheQuant} ");
                _ = sb.Append($"--cache-type-v {kvCacheQuant} ");
            }
            if (!string.IsNullOrEmpty(additionalArgs))
            {
                _ = sb.Append(additionalArgs + " ");
            }

            // Generate the correct multiline format with the Windows line-continuation character (^)
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
                    InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "llama.cpp_load_BATs")
                };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        // Write the header, the wrapped command, and the final "pause"
                        File.WriteAllText(dlg.FileName, $"@echo off{Environment.NewLine}title llama-server: {Path.GetFileNameWithoutExtension(selectedModel)}{Environment.NewLine}{command}{Environment.NewLine}{Environment.NewLine}pause");
                        Logger.Log($" -- Saved batch file for loading model: {dlg.FileName} -- ");
                    }
                    catch (Exception ex)
                    {
                        _ = MessageBox.Show(this, $"Failed to save the batch file. Error: {ex.Message}", "Error Saving Batch File", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }

            try
            {
                this.StartLlamaServerProcess(sb.ToString().Trim());
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(this, $"Failed to start llama-server with the selected model. Error: {ex.Message}", "Error Starting Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void toolStripMenuItem_execModelLoadBat_Click(object? sender, EventArgs e)
        {
            this.ContextMenuStrip?.Close();
            if (!this.TryGetSelectedBatFilePath(out string batFilePath))
            {
                return;
            }

            try
            {
                _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batFilePath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = this.toolStripMenuItem_hideCmd.Checked,
                    WindowStyle = this.toolStripMenuItem_hideCmd.Checked ? System.Diagnostics.ProcessWindowStyle.Hidden : System.Diagnostics.ProcessWindowStyle.Normal
                });
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(this, $"Failed to execute the selected batch file. Error: {ex.Message}", "Error Executing Batch File", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool TryGetSelectedBatFilePath(out string batFilePath)
        {
            string? selectedBatName = this.toolStripComboBox_modelLoadBats.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedBatName))
            {
                _ = MessageBox.Show(this, "No batch file selected. Please select a batch file from the dropdown list.", "No Batch File Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                batFilePath = string.Empty;
                return false;
            }

            batFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "llama.cpp_load_BATs", selectedBatName + ".BAT");
            if (!File.Exists(batFilePath))
            {
                _ = MessageBox.Show(this, $"The selected batch file does not exist:\n{batFilePath}", "Batch File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        private void ToolStripMenuItem_killLlamaServer_Click(object? sender, EventArgs e)
        {
            try
            {
                this.StopTrackedLlamaServerProcess();
                int? killed = WidgetStatics.KillLlamaServerProcesses();
                Logger.Log($"[WindowWidget] Killed {killed} llama-server process(es).");
                this.rerouteAPILlamacppOllamaToolStripMenuItem.Checked = false;
                this.ContextMenuStrip?.Close();
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(this, $"Failed to kill llama-server processes. Error: {ex.Message}", "Error Killing Processes", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StartLlamaServerProcess(string fullCommand)
        {
            Logger.Clear();

            if (string.IsNullOrWhiteSpace(fullCommand))
            {
                throw new InvalidOperationException("llama-server command is empty.");
            }

            this.StopTrackedLlamaServerProcess();

            var cur = this.Cursor;
            this.Cursor = Cursors.WaitCursor;

            string trimmed = fullCommand.Trim();
            const string executableName = "llama-server";
            string arguments = trimmed.StartsWith(executableName + " ", StringComparison.OrdinalIgnoreCase)
                ? trimmed[(executableName.Length + 1)..]
                : string.Empty;

            // Always redirect the stream so tokens/sec can be parsed at any time.
            // Windows does not allow attaching to it later.
            bool captureOutput = true;
            bool hideCmd = this.toolStripMenuItem_hideCmd.Checked;

            var startInfo = this.CreateLlamaServerStartInfo(executableName, arguments, captureOutput);

            this._llamaServerProcess = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            if (captureOutput)
            {
                this._llamaServerProcess.OutputDataReceived += this.HandleLlamaServerOutputDataReceived;
                this._llamaServerProcess.ErrorDataReceived += this.HandleLlamaServerOutputDataReceived;
            }

            if (!this._llamaServerProcess.Start())
            {
                throw new InvalidOperationException("llama-server process could not be started.");
            }

            this.OpenDebugConsoleIfRequested(hideCmd);

            if (captureOutput)
            {
                this._llamaServerProcess.BeginOutputReadLine();
                this._llamaServerProcess.BeginErrorReadLine();
            }

            this.Cursor = cur;
        }

        private ProcessStartInfo CreateLlamaServerStartInfo(string executableName, string arguments, bool captureOutput)
        {
            return new ProcessStartInfo
            {
                FileName = executableName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = captureOutput,
                RedirectStandardError = captureOutput,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
        }

        private void OpenDebugConsoleIfRequested(bool hideCmd)
        {
            if (hideCmd)
            {
                return;
            }

            this.Invoke((System.Windows.Forms.MethodInvoker) delegate
            {
                if (this._debugConsoleForm == null || this._debugConsoleForm.IsDisposed)
                {
                    this.openDebugConsoleToolStripMenuItem_Click(this, EventArgs.Empty);
                }
            });
        }

        private void HandleLlamaServerOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            string? line = e.Data;
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            // If the user did not want to hide the console, route llama-server output
            // into the internal debug console instead.
            if (!this.toolStripMenuItem_hideCmd.Checked)
            {
                Logger.Log(line);
            }

            if (!TryExtractTokensPerSecondFromLlamaOutput(line, out double tokensPerSecond))
            {
                return;
            }

            this._lastStdOutTokensPerSecond = tokensPerSecond;
            this._lastStdOutTokensPerSecondUtc = DateTime.UtcNow;

            // This always runs in the background. If "Show tokens/s" is disabled,
            // the UI simply does not read the value. When enabled, the value is ready.
            LlamaServerStats.UpdateGenerationSpeed((float) tokensPerSecond);
        }

        private static bool TryExtractTokensPerSecondFromLlamaOutput(string line, out double tokensPerSecond)
        {
            tokensPerSecond = 0d;

            // Bug fix: GeneratedRegex produces a method, so () is required.
            Match match = TokensPerSecondRegex.Match(line);
            if (!match.Success)
            {
                return false;
            }

            // Bug fix: Use Groups[1] because the regex uses `([\d.]+)` without a named group.
            if (!double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsedValue))
            {
                return false;
            }

            if (parsedValue <= 0.01d)
            {
                return false;
            }

            tokensPerSecond = parsedValue;
            return true;
        }


        private void StopTrackedLlamaServerProcess()
        {
            if (this._llamaServerProcess == null)
            {
                return;
            }

            try
            {
                this._llamaServerProcess.OutputDataReceived -= this.HandleLlamaServerOutputDataReceived;
                this._llamaServerProcess.ErrorDataReceived -= this.HandleLlamaServerOutputDataReceived;
            }
            catch
            {
            }

            try
            {
                if (!this._llamaServerProcess.HasExited)
                {
                    this._llamaServerProcess.Kill(true);
                }
            }
            catch
            {
            }

            try
            {
                this._llamaServerProcess.Dispose();
            }
            catch
            {
            }

            this._llamaServerProcess = null;
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
                this.ApplyDefaultTensorSplitFromGpuVram();
            }
        }

        private void ApplyDefaultTensorSplitFromGpuVram()
        {
            long gpu1VramGb = this.Gpu != null ? (long) Math.Round(this.Gpu.GetTotalVramBytes() / 1_073_741_824.0) : 0;
            long gpu2VramGb = this.Gpu2 != null ? (long) Math.Round(this.Gpu2.GetTotalVramBytes() / 1_073_741_824.0) : 0;
            this.toolStripTextBox_tensorSplit.Text = gpu1VramGb > 0 && gpu2VramGb > 0
                ? $"{gpu1VramGb},{gpu2VramGb}"
                : string.Empty;
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
            if (float.TryParse(entered, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float temperature) && temperature >= 0)
            {
                this.toolStripTextBox_temperature.Text = temperature.ToString(System.Globalization.CultureInfo.InvariantCulture);
                LlamaOllamaBridge.UserDefinedTemperature = (double) temperature;
            }
            else
            {
                _ = MessageBox.Show(this, "Please enter a valid positive number for temperature.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_temperature.Text = "0.75";
            }

            // Save persistant settings
            this._persistentSettings.Temperature = temperature;
            this.SavePersistentSettings();

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
                _ = MessageBox.Show(this, "Please enter a valid positive number for repetition penalty.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_repetationPenalty.Text = "1.1";
            }

            // Save persistant settings
            this._persistentSettings.RepetitionPenalty = penalty;
            this.SavePersistentSettings();

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
                _ = MessageBox.Show(this, "Please enter a valid positive integer for context size.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_contextSize.Text = "16384";
            }

            // Save persistant settings
            this._persistentSettings.ContextSize = contextSize;
            this.SavePersistentSettings();

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
                _ = MessageBox.Show(this, "Please enter a valid positive integer for batch size.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_batchSize.Text = "1024";
            }

            // Save persistant settings
            this._persistentSettings.BatchSize = batchSize;
            this.SavePersistentSettings();

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
                    _ = MessageBox.Show(this, $"Please enter a valid tensor split configuration with at most {GpuStats.GpuNames.Count} values (for {GpuStats.GpuNames.Count} GPUs).", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                _ = MessageBox.Show(this, "Please enter a valid non-negative integer for GPU layers count.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_gpuLayersCount.Text = "0";
            }

            // Save persistant settings
            this._persistentSettings.GpuLayersCount = gpuLayerCount;
            this.SavePersistentSettings();

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
                _ = MessageBox.Show(this, "Please enter a valid positive integer for number of parallel slots.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_numberParallelSlots.Text = "1";
            }

            // Save persistant settings
            this._persistentSettings.NumberParallelSlots = parallelSlots;
            this.SavePersistentSettings();

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
                _ = MessageBox.Show(this, "Please enter a valid non-negative integer for thinking budget (tokens).", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_thinkingBudget.Text = "0";
            }

            // Save persistant settings
            this._persistentSettings.ThinkingBudget = thinkingBudget;
            this.SavePersistentSettings();

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_reasoningBudget_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            string entered = this.toolStripTextBox_reasoningBudget.Text.Trim();
            if (int.TryParse(entered, out int reasoningBudget) && reasoningBudget >= 0)
            {
                this.toolStripTextBox_reasoningBudget.Text = reasoningBudget.ToString();
                LlamaOllamaBridge.UserDefinedReasoningBudget = reasoningBudget;
            }
            else
            {
                _ = MessageBox.Show(this, "Please enter a valid non-negative integer for reasoning budget (tokens).", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_reasoningBudget.Text = "0";
            }

            // Save persistant settings
            this._persistentSettings.ReasoningBudget = reasoningBudget;
            this.SavePersistentSettings();

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
                _ = MessageBox.Show(this, "Please enter a valid number between 0 and 1 for top-p.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                _ = MessageBox.Show(this, "Please enter a valid number between 0 and 1 for min-p.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                _ = MessageBox.Show(this, "Please enter a valid non-negative integer for top-k.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                _ = MessageBox.Show(this, "Please enter a valid number between 0.10 and 1.00 for prompt safety ratio.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                _ = MessageBox.Show(this, "Please enter a valid number between 0.10 and 1.00 for smart budget ratio.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                _ = MessageBox.Show(this, "Please enter a valid integer >= 256 for large message threshold chars.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                _ = MessageBox.Show(this, "Please enter a valid integer >= 5 for skeleton max lines.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                _ = MessageBox.Show(this, "Please enter a valid integer >= 1 for focus keyword limit.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                _ = MessageBox.Show(this, "Please enter a valid integer >= 0 for tail keep bonus chars.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_tailKeepBonusChars.Text = SmartPromptOptimizationSettings.TailKeepBonusChars.ToString();
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
        }

       

        private void toolStripTextBox_percentageColor_TextChanged(object sender, EventArgs e)
        {
            string hex = this.toolStripTextBox_percentageColor.Text.Replace("#", "");
            if (hex.Length == 6 && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int rgb))
            {
                // rgb is RRGGBB; construct an opaque Color (add alpha byte)
                this._percentageColor = Color.FromArgb(unchecked((int) 0xFF000000 | rgb));
            }
            else
            {
                this._percentageColor = null;
            }

            this._persistentSettings.PerCorePercentColor = ("#" + hex.Replace("#", "")).ToUpperInvariant();
            this.SavePersistentSettings();
        }

        private void KVoffload_ToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            this._persistentSettings.KvOffload = this.KVoffload_ToolStripMenuItem.Checked;
            this.SavePersistentSettings();
        }

        private void toolStripComboBox_cacheType_SelectedIndexChanged(object sender, EventArgs e)
        {
            this._persistentSettings.KvCacheType = this.toolStripComboBox_cacheType.Text;
            this.SavePersistentSettings();
        }

        private void toolStripMenuItem_toolCalls_CheckedChanged(object sender, EventArgs e)
        {
            this._persistentSettings.LlamaServerToolCalling = this.toolStripMenuItem_toolCalls.Checked;
            this.SavePersistentSettings();
        }

        private void toolStripMenuItem_noWarmup_CheckedChanged(object sender, EventArgs e)
        {
            this._persistentSettings.NoWarmup = this.toolStripMenuItem_noWarmup.Checked;
            this.SavePersistentSettings();
        }

        private void toolStripMenuItem_fitMode_CheckedChanged(object sender, EventArgs e)
        {
            this._persistentSettings.FitMode = this.toolStripMenuItem_fitMode.Checked;
            this.SavePersistentSettings();
        }

        private void toolStripMenuItem_hideCmd_CheckedChanged(object sender, EventArgs e)
        {
            this._persistentSettings.HideCmd = this.toolStripMenuItem_hideCmd.Checked;
            this.SavePersistentSettings();
        }

        private void toolStripTextBox_openAiApiUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }
            string entered = this.toolStripTextBox_openAiApiUrl.Text.Trim();
            if (Uri.TryCreate(entered, UriKind.Absolute, out Uri? apiUrl))
            {
                this._persistentSettings.OpenAIApiUrl = apiUrl.ToString();
                this.SavePersistentSettings();
            }
            else
            {
                _ = MessageBox.Show(this, "Please enter a valid URL for the OpenAI API.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_openAiApiUrl.Text = string.Empty;
            }
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_ollamaPort_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }
            string entered = this.toolStripTextBox_ollamaPort.Text.Trim();
            if (int.TryParse(entered, out int port) && port > 0 && port <= 65535)
            {
                this._persistentSettings.OllamaPort = port;
                this.SavePersistentSettings();
            }
            else
            {
                _ = MessageBox.Show(this, "Please enter a valid integer between 1 and 65535 for the Ollama server port.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_ollamaPort.Text = "11434";
            }
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_llamacppPort_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }
            string entered = this.toolStripTextBox_llamacppPort.Text.Trim();
            if (int.TryParse(entered, out int port) && port > 0 && port <= 65535)
            {
                this._persistentSettings.LlamaCppServerPort = port;
                this.SavePersistentSettings();
            }
            else
            {
                _ = MessageBox.Show(this, "Please enter a valid integer between 1 and 65535 for the llama.cpp server port.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.toolStripTextBox_llamacppPort.Text = "8080";
            }
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void toolStripTextBox_additionalArgs_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            // With regex count args
            string entered = this.toolStripTextBox_additionalArgs.Text.Trim();
            Regex argSplitRegex = LoadArgsRegex();
            MatchCollection matches = argSplitRegex.Matches(entered);
            this.toolStripMenuItem_additionalArgs.Text = $"Additional Load Args ({matches.Count})";

            this._persistentSettings.AdditionalLoadArgs = this.toolStripTextBox_additionalArgs.Text;
            this.SavePersistentSettings();
        }

        [GeneratedRegex(@"--?[a-zA-Z][\w-]*(?:\s+(?:"".*?""|'.*?'|(?!--?[a-zA-Z])[^\s]+))*")]
        private static partial Regex LoadArgsRegex();
    }
}
