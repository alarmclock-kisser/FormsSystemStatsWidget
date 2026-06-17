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


        // Load llama-server.exe GGUF model
        private void toolStripMenuItem_loadLlamaCppServer_Click(object? sender, EventArgs e)
        {
            string? selectedModel = this.toolStripComboBox_ggufModels.SelectedItem as string ?? this.toolStripComboBox_ggufModels.Text.Trim();
            this.ContextMenuStrip?.Close();

            if (!this.TryResolveSelectedModel(ref selectedModel))
            {
                return;
            }

            LlamaServerLaunchOptions options = this.ReadLaunchOptions(selectedModel!);

            // Persist the chosen values so subsequent model loads use them
            this._persistentSettings.UserTopP = options.TopP;
            this._persistentSettings.UserMinP = options.MinP;
            this._persistentSettings.UserTopK = options.TopK;
            this.SavePersistentSettings();

            var sb = BuildLlamaServerCommand(options);
            string inferenceParams = BuildInferenceParamsComment(options);

            // Generate the correct multiline format with the Windows line-continuation character (^)
            string command = sb.ToString().Trim();
            command = ArgsSplitRegex().Replace(command, " ^" + Environment.NewLine + " ");

            if (!this.ConfirmAndOptionallySaveBatch(command, inferenceParams, options.SelectedModel))
            {
                return;
            }

            try
            {
                // Clear logs
                this._debugConsoleForm?.ClearLogs();
                // Logger.Clear();

                this.StartLlamaServerProcess(sb.ToString().Trim());
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(this, $"Failed to start llama-server with the selected model. Error: {ex.Message}", "Error Starting Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private sealed record LlamaServerLaunchOptions(
            string SelectedModel,
            string? MmprojFilePath,
            int ContextSize,
            int BatchSize,
            string SplitMode,
            int[] TensorSplit,
            bool FlashAttention,
            int GpuLayersCount,
            int NumParallelSlots,
            bool NoWarmup,
            bool KvOffload,
            bool FitMode,
            bool Thinking,
            int? ReasoningBudget,
            float TopP,
            float MinP,
            int TopK,
            string KvCacheQuant,
            bool ToolCalling,
            string AdditionalArgs);

        private bool TryResolveSelectedModel(ref string? selectedModel)
        {
            if (string.IsNullOrEmpty(selectedModel))
            {
                _ = MessageBox.Show(this, "No model selected. Please select a model from the dropdown list.", "No Model Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!File.Exists(selectedModel))
            {
                // Try to find the model file name with or without extension in the specified models directory
                string searchTerm = selectedModel;
                selectedModel = LlamaCppModelLoader.ModelFilePaths.FirstOrDefault(path => path.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                if (!File.Exists(selectedModel))
                {
                    _ = MessageBox.Show(this, $"The selected model file does not exist:\n{selectedModel}", "Model File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }

            return true;
        }

        private LlamaServerLaunchOptions ReadLaunchOptions(string selectedModel)
        {
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
            bool thinking = this.toolStripMenuItem_thinking.Checked;
            int? reasoningBudget = this.toolStripTextBox_reasoningBudget.Text.Trim() != "" && int.TryParse(this.toolStripTextBox_reasoningBudget.Text.Trim(), out int parsedReasoningBudget) ? parsedReasoningBudget : null;
            float topP = this.toolStripTextBox_topP.Text.Trim() != "" && float.TryParse(this.toolStripTextBox_topP.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float parsedTopP) ? parsedTopP : 0.9f;
            float minP = this.toolStripTextBox_minP.Text.Trim() != "" && float.TryParse(this.toolStripTextBox_minP.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float parsedMinP) ? parsedMinP : 0.0f;
            int topK = this.toolStripTextBox_topK.Text.Trim() != "" && int.TryParse(this.toolStripTextBox_topK.Text.Trim(), out int parsedTopK) ? parsedTopK : 40;
            string kvCacheQuant = (this.toolStripComboBox_cacheType.SelectedItem as string ?? "f16").ToLowerInvariant();
            bool toolCalling = this.toolStripMenuItem_toolCalls.Checked;
            string additionalArgs = this.toolStripTextBox_additionalArgs.Text.Trim();

            return new LlamaServerLaunchOptions(
                selectedModel, mmprojFilePath, contextSize, batchSize, splitMode, tensorSplit,
                flashAttention, gpuLayersCount, numParallelSlots, noWarmup, kvOffload, fitMode,
                thinking, reasoningBudget, topP, minP, topK, kvCacheQuant, toolCalling, additionalArgs);
        }

        // Aggregate CMD call (Single Line)
        private static StringBuilder BuildLlamaServerCommand(LlamaServerLaunchOptions o)
        {
            var sb = new StringBuilder();
            _ = sb.Append($"llama-server ");
            _ = sb.Append($"-m \"{o.SelectedModel}\" ");
            if (o.MmprojFilePath != null)
            {
                _ = sb.Append($"--mmproj \"{o.MmprojFilePath}\" ");
            }
            _ = sb.Append($"-c {o.ContextSize} ");
            _ = sb.Append($"-b {o.BatchSize} ");
            _ = sb.Append($"-ub {(Math.Max((int) (o.BatchSize / 4), 512))} ");
            if (o.SplitMode != "none")
            {
                _ = sb.Append($"-sm {o.SplitMode} ");
                if (o.TensorSplit.Length > 0)
                {
                    _ = sb.Append($"-ts {string.Join(",", o.TensorSplit)} ");
                }
            }
            _ = sb.Append("-fa " + (o.FlashAttention ? "on " : "off "));
            _ = sb.Append("-ngl " + o.GpuLayersCount + " ");
            _ = sb.Append("-np " + o.NumParallelSlots + " ");
            if (o.NoWarmup)
            {
                _ = sb.Append("--no-warmup ");
                _ = sb.Append("--no-mmap ");
            }
            if (o.KvOffload)
            {
                _ = sb.Append("--kv-offload ");
            }
            else
            {
                _ = sb.Append("--no-kv-offload ");
            }
            _ = sb.Append("-fit " + (o.FitMode ? "on " : "off "));
            if (o.Thinking == false)
            {
                _ = sb.Append("--reasoning off ");
            }
            if (o.ReasoningBudget.HasValue && o.Thinking)
            {
                _ = sb.Append($"--reasoning-budget {o.ReasoningBudget.Value} ");
            }
            if (o.ToolCalling)
            {
                _ = sb.Append("--tools all ");
            }
            if (o.KvCacheQuant != "f16")
            {
                _ = sb.Append($"--cache-type-k {o.KvCacheQuant} ");
                _ = sb.Append($"--cache-type-v {o.KvCacheQuant} ");
            }
            if (!string.IsNullOrEmpty(o.AdditionalArgs))
            {
                _ = sb.Append(o.AdditionalArgs + " ");
            }

            return sb;
        }

        // Add non-load args for inference (tempetrature etc.) as comment in BAT file
        private static string BuildInferenceParamsComment(LlamaServerLaunchOptions o)
        {
            string inferenceParams = $":: temperature={LlamaOllamaBridge.UserDefinedTemperature.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)} " + Environment.NewLine;
            inferenceParams += $":: repetition_penalty={LlamaOllamaBridge.UserDefinedRepetitionPenalty.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)} " + Environment.NewLine;
            inferenceParams += $":: top_p={o.TopP.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)} " + Environment.NewLine;
            inferenceParams += $":: min_p={o.MinP.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)} " + Environment.NewLine;
            inferenceParams += $":: top_k={o.TopK.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)} " + Environment.NewLine;
            inferenceParams += $":: reasoning_budget={o.ReasoningBudget ?? 0} " + Environment.NewLine;
            return inferenceParams;
        }

        // Show the aggregated command for confirmation and optionally persist it as a .BAT file.
        // Returns false if the user aborted (Cancel).
        private bool ConfirmAndOptionallySaveBatch(string command, string inferenceParams, string selectedModel)
        {
            DialogResult result = MessageBox.Show(this, $"The following command will be executed to start llama-server with the selected model and options:\n\n{command}\n\nDo you want to save the current configuration?\nPress Yes to save, No to proceed without saving, or Cancel to abort.", "Confirm Command", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Cancel)
            {
                return false;
            }

            if (result == DialogResult.Yes)
            {
                this.SaveModelLoadBatch(command, inferenceParams, selectedModel);
            }

            return true;
        }

        private void SaveModelLoadBatch(string command, string inferenceParams, string selectedModel)
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
            if (dlg.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                // Write the header, the wrapped command + inference params, and the final "pause"
                File.WriteAllText(dlg.FileName, $"@echo off{Environment.NewLine}title llama-server: {Path.GetFileNameWithoutExtension(selectedModel)}{Environment.NewLine}{command}{Environment.NewLine + Environment.NewLine}{inferenceParams}{Environment.NewLine}pause");
                Logger.Log($" -- Saved batch file for loading model: {dlg.FileName} -- ");
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(this, $"Failed to save the batch file. Error: {ex.Message}", "Error Saving Batch File", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                // Read comment lines from the batch file to extract inference parameters and update the LlamaOllamaBridge properties accordingly (so they are applied when the user starts a new chat after loading the model with the BAT file)
                string[] lines = File.ReadAllLines(batFilePath);
                string[] inferenceParamsLines = lines.Where(line => line.TrimStart().StartsWith("::")).ToArray();
                LlamaOllamaBridge.UserDefinedTemperature = inferenceParamsLines.Select(line => line.TrimStart().Substring(2).Trim()).Where(param => param.StartsWith("temperature=")).Select(param => param.Substring("temperature=".Length)).Select(value => float.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float temp) ? temp : 0.75f).FirstOrDefault();
                LlamaOllamaBridge.UserDefinedRepetitionPenalty = inferenceParamsLines.Select(line => line.TrimStart().Substring(2).Trim()).Where(param => param.StartsWith("repetition_penalty=")).Select(param => param.Substring("repetition_penalty=".Length)).Select(value => float.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float penalty) ? penalty : 1.1f).FirstOrDefault();
                LlamaOllamaBridge.UserDefinedTopP = inferenceParamsLines.Select(line => line.TrimStart().Substring(2).Trim()).Where(param => param.StartsWith("top_p=")).Select(param => param.Substring("top_p=".Length)).Select(value => float.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float topP) ? topP : 0.9f).FirstOrDefault();
                LlamaOllamaBridge.UserDefinedMinP = inferenceParamsLines.Select(line => line.TrimStart().Substring(2).Trim()).Where(param => param.StartsWith("min_p=")).Select(param => param.Substring("min_p=".Length)).Select(value => float.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float minP) ? minP : 0.0f).FirstOrDefault();
                LlamaOllamaBridge.UserDefinedTopK = inferenceParamsLines.Select(line => line.TrimStart().Substring(2).Trim()).Where(param => param.StartsWith("top_k=")).Select(param => param.Substring("top_k=".Length)).Select(value => int.TryParse(value, out int topK) ? topK : 40).FirstOrDefault();
                LlamaOllamaBridge.UserDefinedReasoningBudget = inferenceParamsLines.Select(line => line.TrimStart().Substring(2).Trim()).Where(param => param.StartsWith("reasoning_budget=")).Select(param => param.Substring("reasoning_budget=".Length)).Select(value => int.TryParse(value, out int reasoningBudget) ? reasoningBudget : 2048).FirstOrDefault();

                Logger.Log($"Loaded inference parameters from batch file: Temperature={LlamaOllamaBridge.UserDefinedTemperature}, RepetitionPenalty={LlamaOllamaBridge.UserDefinedRepetitionPenalty}, TopP={LlamaOllamaBridge.UserDefinedTopP}, MinP={LlamaOllamaBridge.UserDefinedMinP}, TopK={LlamaOllamaBridge.UserDefinedTopK}, ReasoningBudget={LlamaOllamaBridge.UserDefinedReasoningBudget}");

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
    }
}
