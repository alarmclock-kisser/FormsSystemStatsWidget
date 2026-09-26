using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FormsSystemStatsWidget.Core;

namespace FormsSystemStatsWidget.Forms
{
    public partial class WindowWidget
    {
        private const string MainModelLayout = "Main";
        private const string PartitionedModelLayout = "Partitioned";
        private Process? _onnxGenaiServerProcess;
        private bool _keepOnnxSubMenuOpenAfterHideConsoleClick;

        private sealed record OnnxModelChoice(string ModelId, string Layout, string DisplayName)
        {
            public override string ToString() => this.DisplayName;
        }

        // ------------------------------------------------------------------
        // Contextmenu: "Load ONNX-Genai Server"
        // ------------------------------------------------------------------

        private void toolStripMenuItem_loadOnnxGenaiServer_Click(object? sender, EventArgs e)
        {
            if (WidgetStatics.GetOnnxGenaiServerProcesses().Count > 0)
            {
                this.toolStripMenuItem_killOnnxGenaiServer_Click(sender, e);
                return;
            }

            string modelRootDir = this.toolStripTextBox_onnxModelRootDir.Text.Trim();
            OnnxModelChoice? selectedChoice = this.toolStripComboBox_onnxModels.SelectedItem as OnnxModelChoice;
            string selectedModel = selectedChoice?.ModelId ?? this.toolStripComboBox_onnxModels.Text.Trim();

            if (string.IsNullOrEmpty(modelRootDir) || !Directory.Exists(modelRootDir))
            {
                _ = MessageBox.Show(this, $"ONNX Model Root Directory does not exist:\n{modelRootDir}", "Invalid Directory", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrEmpty(selectedModel) || selectedModel.StartsWith("Select", StringComparison.OrdinalIgnoreCase))
            {
                _ = MessageBox.Show(this, "No ONNX model selected. Please select a model from the dropdown list.", "No Model Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string modelRootPath = Path.Combine(modelRootDir, selectedModel);
            if (!Directory.Exists(modelRootPath))
            {
                _ = MessageBox.Show(this, $"Model directory does not exist:\n{modelRootPath}", "Model Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            bool hasRootOnnx = Directory.EnumerateFiles(modelRootPath, "*.onnx", SearchOption.TopDirectoryOnly)
                .Any(f => !f.EndsWith(".onnx.data", StringComparison.OrdinalIgnoreCase));
            bool hasPartitionedStages = HasPartitionedOnnxStages(modelRootPath);
            string modelLayout = selectedChoice?.Layout ?? this._persistentSettings.OnnxModelLayout;
            if (modelLayout == "Auto")
            {
                modelLayout = hasPartitionedStages ? "Partitioned" : "Main";
            }
            if (modelLayout == "Main" && !hasRootOnnx)
            {
                _ = MessageBox.Show(this, $"No main ONNX model found directly in:\n{modelRootPath}", "Main Model Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (modelLayout == "Partitioned" && !hasPartitionedStages)
            {
                _ = MessageBox.Show(this, $"Both partitioned stages were not found in:\n{Path.Combine(modelRootPath, "partitioned")}", "Partitioned Model Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int contextLength = int.TryParse(this.toolStripTextBox_onnxContextLength.Text.Trim(), out int cl) ? cl : 4096;
            int maxTokens = int.TryParse(this.toolStripTextBox_onnxMaxTokens.Text.Trim(), out int mt) ? mt : 1024;
            float temperature = float.TryParse(this.toolStripTextBox_onnxTemperature.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float t) ? t : 0.8f;
            float topP = float.TryParse(this.toolStripTextBox_onnxTopP.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float tp) ? tp : 0.9f;
            int topK = int.TryParse(this.toolStripTextBox_onnxTopK.Text.Trim(), out int tk) ? tk : 40;
            float repeatPenalty = float.TryParse(this.toolStripTextBox_onnxRepeatPenalty.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float rp) ? rp : 1.1f;
            string executionProvider = this.toolStripComboBox_onnxExecutionProvider.SelectedItem as string ?? string.Empty;
            if (string.IsNullOrEmpty(executionProvider) || executionProvider == "-Provider-")
            {
                executionProvider = "Dml";
            }
            if (modelLayout == "Partitioned")
            {
                executionProvider = "Cuda";
            }
            bool hideCmd = this.toolStripMenuItem_onnxHideCmd.Checked;

            string serverDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OnnxGenaiServer");
            string serverExecutable = Path.Combine(serverDirectory, "FormsSystemStatsWidget.OnnxGenaiServer.exe");
            string serverDll = Path.Combine(serverDirectory, "FormsSystemStatsWidget.OnnxGenaiServer.dll");
            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = serverDirectory
            };
            if (File.Exists(serverExecutable))
            {
                startInfo.FileName = serverExecutable;
            }
            else if (File.Exists(serverDll))
            {
                startInfo.FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
                startInfo.ArgumentList.Add(serverDll);
            }
            else
            {
                _ = MessageBox.Show(this, $"ONNX GenAI server runtime was not found:\n{serverDirectory}\n\nPublish the Forms app with its bundled ONNX server.", "Server Runtime Missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            startInfo.ArgumentList.Add($"--OnnxGenaiServer:DefaultModel={selectedModel}");
            startInfo.ArgumentList.Add($"--OnnxGenaiServer:ModelLayout={modelLayout}");
            startInfo.ArgumentList.Add($"--OnnxGenaiServer:ModelRootDirectory={modelRootDir}");
            startInfo.ArgumentList.Add($"--OnnxGenaiServer:ContextLength={contextLength}");
            startInfo.ArgumentList.Add($"--OnnxGenaiServer:MaxTokens={maxTokens}");
            startInfo.ArgumentList.Add($"--OnnxGenaiServer:Temperature={temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            startInfo.ArgumentList.Add($"--OnnxGenaiServer:TopP={topP.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            startInfo.ArgumentList.Add($"--OnnxGenaiServer:TopK={topK}");
            startInfo.ArgumentList.Add($"--OnnxGenaiServer:RepeatPenalty={repeatPenalty.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            startInfo.ArgumentList.Add($"--OnnxGenaiServer:ExecutionProvider={executionProvider}");

            DialogResult result = MessageBox.Show(this,
                $"Model package:\n{modelRootPath}\n\nLayout: {(modelLayout == "Partitioned" ? "partitioned; Stage 0 + Stage 1 on CUDA" : "main ONNX weights")}\nAPI server: {startInfo.FileName}\n\nStart the ONNX GenAI server?",
                "Confirm ONNX-Genai Server Start", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return;
            }

            try
            {
                this._debugConsoleForm?.ClearLogs();
                this.StartOnnxGenaiServerProcess(startInfo, hideCmd);
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(this, $"Failed to start ONNX-Genai Server. Error: {ex.Message}", "Error Starting Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ------------------------------------------------------------------
        // Python-Environment: async Check + Progress-Dialog
        // ------------------------------------------------------------------

        private async Task<bool> EnsureOnnxPythonEnvironmentAsync(bool showProgress = false)
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "setup_onnx_env.py");
            if (!File.Exists(scriptPath))
            {
                return await this.CheckPythonDirectlyAsync();
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{scriptPath}\" --install",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            var output = new StringBuilder();
            var outputLock = new object();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null)
                {
                    return;
                }

                lock (outputLock)
                {
                    output.AppendLine(e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null)
                {
                    return;
                }

                lock (outputLock)
                {
                    output.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            int exitCode = process.ExitCode;
            string outputText;
            lock (outputLock)
            {
                outputText = output.ToString();
            }

            Logger.Log($"[ONNX Python Env] Exit code: {exitCode}");
            Logger.Log(outputText);

            // Only show the dialog if the check FAILED
            if (showProgress && exitCode != 0)
            {
                this.ShowOnnxEnvProgressFailed(outputText);
            }

            return exitCode == 0;
        }

        private async Task<bool> CheckPythonDirectlyAsync()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var process = new Process { StartInfo = startInfo };
                process.Start();
                string output = (await process.StandardOutput.ReadToEndAsync()).Trim();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    Logger.Log("[ONNX Python Env] Python not found in PATH.");
                    return false;
                }

                Logger.Log($"[ONNX Python Env] Found: {output}");
                return true;
            }
            catch
            {
                Logger.Log("[ONNX Python Env] Python not found in PATH.");
                return false;
            }
        }

        // ------------------------------------------------------------------
        // Progress-Dialog für Python-Env-Setup
        // ------------------------------------------------------------------

        private OnnxEnvProgressForm? _onnxEnvProgressForm;

        private void UpdateOnnxEnvProgress(string? line)
        {
            if (this.InvokeRequired)
            {
                try { this.BeginInvoke(new Action(() => this.UpdateOnnxEnvProgress(line))); }
                catch { }
                return;
            }

            if (_onnxEnvProgressForm == null || _onnxEnvProgressForm.IsDisposed)
            {
                _onnxEnvProgressForm = new OnnxEnvProgressForm(this);
                _onnxEnvProgressForm.Show();
            }

            _onnxEnvProgressForm.AppendLine(line);
        }

        private void CloseOnnxEnvProgress()
        {
            if (this.InvokeRequired)
            {
                try { this.BeginInvoke(new Action(this.CloseOnnxEnvProgress)); }
                catch { }
                return;
            }

            if (_onnxEnvProgressForm != null && !_onnxEnvProgressForm.IsDisposed)
            {
                _onnxEnvProgressForm.Close();
                _onnxEnvProgressForm = null;
            }
        }

        private void MarkOnnxEnvProgressFailed()
        {
            if (this.InvokeRequired)
            {
                try { this.BeginInvoke(new Action(this.MarkOnnxEnvProgressFailed)); }
                catch { }
                return;
            }

            if (_onnxEnvProgressForm != null && !_onnxEnvProgressForm.IsDisposed)
            {
                _onnxEnvProgressForm.MarkFailed();
            }
        }

        private void ShowOnnxEnvProgressFailed(string fullLog)
        {
            if (this.InvokeRequired)
            {
                try { this.BeginInvoke(new Action(() => this.ShowOnnxEnvProgressFailed(fullLog))); }
                catch { }
                return;
            }

            _onnxEnvProgressForm = new OnnxEnvProgressForm(this);
            _onnxEnvProgressForm.SetFullLog(fullLog);
            _onnxEnvProgressForm.MarkFailed();
            _onnxEnvProgressForm.Show();
        }

        // ------------------------------------------------------------------
        // ONNX-Genai-Server-Prozess starten
        // ------------------------------------------------------------------

        private void StartOnnxGenaiServerProcess(ProcessStartInfo startInfo, bool hideCmd)
        {
            if (string.IsNullOrWhiteSpace(startInfo.FileName))
            {
                throw new InvalidOperationException("ONNX-Genai Server executable is empty.");
            }

            this.StopTrackedOnnxGenaiServerProcess();

            var cur = this.Cursor;
            this.Cursor = Cursors.WaitCursor;

            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;

            this._onnxGenaiServerProcess = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            this._onnxGenaiServerProcess.OutputDataReceived += this.HandleOnnxGenaiServerOutputDataReceived;
            this._onnxGenaiServerProcess.ErrorDataReceived += this.HandleOnnxGenaiServerOutputDataReceived;

            if (!this._onnxGenaiServerProcess.Start())
            {
                throw new InvalidOperationException("ONNX-Genai Server process could not be started.");
            }

            this.OpenDebugConsoleIfRequested(hideCmd);
            this._onnxGenaiServerProcess.BeginOutputReadLine();
            this._onnxGenaiServerProcess.BeginErrorReadLine();

            this.Cursor = cur;
            string arguments = string.Join(" ", startInfo.ArgumentList.Select(QuoteCommandArgument));
            Logger.Log($"[ONNX-Genai Server] Started: {startInfo.FileName} {arguments}");
        }

        private static string QuoteCommandArgument(string argument) =>
            argument.Any(char.IsWhiteSpace) ? $"\"{argument}\"" : argument;

        private void HandleOnnxGenaiServerOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            string? line = e.Data;
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            Logger.Log(line);
        }

        private void StopTrackedOnnxGenaiServerProcess()
        {
            if (this._onnxGenaiServerProcess == null)
            {
                return;
            }

            try { this._onnxGenaiServerProcess.OutputDataReceived -= this.HandleOnnxGenaiServerOutputDataReceived; } catch { }
            try { this._onnxGenaiServerProcess.ErrorDataReceived -= this.HandleOnnxGenaiServerOutputDataReceived; } catch { }
            try { if (!this._onnxGenaiServerProcess.HasExited) { this._onnxGenaiServerProcess.Kill(true); } } catch { }
            try { this._onnxGenaiServerProcess.Dispose(); } catch { }
            this._onnxGenaiServerProcess = null;
        }

        // ------------------------------------------------------------------
        // Contextmenu-Opening: ONNX-Modelle laden
        // ------------------------------------------------------------------

        private void toolStripTextBox_onnxModelRootDir_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode != System.Windows.Forms.Keys.Enter)
            {
                return;
            }

            string modelRootDir = this.toolStripTextBox_onnxModelRootDir.Text.Trim();
            if (!Directory.Exists(modelRootDir))
            {
                _ = MessageBox.Show(this, $"ONNX model root directory does not exist:\n{modelRootDir}", "Invalid Directory", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.SuppressKeyPress = true;
                return;
            }

            this._persistentSettings.OnnxModelRootDirectory = modelRootDir;
            this.PopulateOnnxModelsList();
            this.SavePersistentSettings();
        }

        private void toolStripComboBox_onnxModels_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!this._onnxSettingsInitialized || this._refreshingOnnxModels)
            {
                return;
            }

            if (this.TryPersistOnnxSettings(out _))
            {
                this.SavePersistentSettings();
            }
        }

        private void toolStripComboBox_onnxExecutionProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!this._onnxSettingsInitialized)
            {
                return;
            }

            if (this.TryPersistOnnxSettings(out _))
            {
                this.SavePersistentSettings();
            }
        }

        private void toolStripMenuItem_onnxHideCmd_CheckedChanged(object? sender, EventArgs e)
        {
            if (!this._onnxSettingsInitialized)
            {
                return;
            }

            if (this.TryPersistOnnxSettings(out _))
            {
                this.SavePersistentSettings();
            }
        }

        private void PopulateOnnxModelsList()
        {
            string modelRootDir = this.toolStripTextBox_onnxModelRootDir.Text.Trim();
            string preferredModel = this._persistentSettings.OnnxModel;
            this._refreshingOnnxModels = true;
            try
            {
                this.toolStripComboBox_onnxModels.Items.Clear();
                this.toolStripComboBox_onnxModels.Text = "No ONNX models found";

                if (string.IsNullOrEmpty(modelRootDir) || !Directory.Exists(modelRootDir))
                {
                    return;
                }

                foreach (var subDir in Directory.EnumerateDirectories(modelRootDir))
                {
                    string id = Path.GetFileName(subDir);
                    bool hasRootOnnx = Directory.EnumerateFiles(subDir, "*.onnx", SearchOption.TopDirectoryOnly)
                        .Any(f => !f.EndsWith(".onnx.data", StringComparison.OrdinalIgnoreCase));
                    bool hasPartitionedStages = HasPartitionedOnnxStages(subDir);
                    bool hasJson = Directory.EnumerateFiles(subDir, "*.json", SearchOption.TopDirectoryOnly).Any();
                    if ((hasRootOnnx || hasPartitionedStages) && hasJson)
                    {
                        if (hasPartitionedStages)
                        {
                            this.toolStripComboBox_onnxModels.Items.Add(new OnnxModelChoice(id, PartitionedModelLayout, $"{id} (Partitioned, 2 stages)"));
                        }
                        if (hasRootOnnx)
                        {
                            this.toolStripComboBox_onnxModels.Items.Add(new OnnxModelChoice(id, MainModelLayout, $"{id} (Main weights)"));
                        }
                    }
                }

                if (this.toolStripComboBox_onnxModels.Items.Count > 0)
                {
                    int preferredIndex = -1;
                    int preferredModelIndex = -1;
                    for (int index = 0; index < this.toolStripComboBox_onnxModels.Items.Count; index++)
                    {
                        if (this.toolStripComboBox_onnxModels.Items[index] is not OnnxModelChoice choice
                            || !string.Equals(choice.ModelId, preferredModel, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (preferredModelIndex < 0)
                        {
                            preferredModelIndex = index;
                        }

                        if (string.Equals(choice.Layout, this._persistentSettings.OnnxModelLayout, StringComparison.OrdinalIgnoreCase))
                        {
                            preferredIndex = index;
                            break;
                        }
                    }
                    this.toolStripComboBox_onnxModels.SelectedIndex = preferredIndex >= 0 ? preferredIndex : preferredModelIndex >= 0 ? preferredModelIndex : 0;
                }
            }
            finally
            {
                this._refreshingOnnxModels = false;
            }
        }

        private static bool HasPartitionedOnnxStages(string modelRootPath)
        {
            string partitionDirectory = Path.Combine(modelRootPath, "partitioned");
            return File.Exists(Path.Combine(partitionDirectory, "model.stage0.onnx"))
                && File.Exists(Path.Combine(partitionDirectory, "model.stage1.onnx"));
        }

        private void toolStripTextBox_onnxContextLength_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) => this.PersistOnnxSettingsOnEnter(e);
        private void toolStripTextBox_onnxMaxTokens_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) => this.PersistOnnxSettingsOnEnter(e);
        private void toolStripTextBox_onnxTemperature_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) => this.PersistOnnxSettingsOnEnter(e);
        private void toolStripTextBox_onnxTopP_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) => this.PersistOnnxSettingsOnEnter(e);
        private void toolStripTextBox_onnxTopK_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) => this.PersistOnnxSettingsOnEnter(e);
        private void toolStripTextBox_onnxRepeatPenalty_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) => this.PersistOnnxSettingsOnEnter(e);

        private void PersistOnnxSettingsOnEnter(System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode != System.Windows.Forms.Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;

            if (!this.TryPersistOnnxSettings(out string error))
            {
                _ = MessageBox.Show(this, error, "Invalid ONNX Setting", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            this.SavePersistentSettings();
        }

        private bool TryPersistOnnxSettings(out string error)
        {
            error = string.Empty;
            int maxTokens = 0;
            double temperature = 0;
            double topP = 0;
            int topK = 0;
            double repeatPenalty = 0;
            if (!int.TryParse(this.toolStripTextBox_onnxContextLength.Text.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int contextLength) || contextLength <= 0)
            {
                error = "Context Length must be a positive whole number.";
            }
            else if (!int.TryParse(this.toolStripTextBox_onnxMaxTokens.Text.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out maxTokens) || maxTokens <= 0)
            {
                error = "Max Tokens must be a positive whole number.";
            }
            else if (!double.TryParse(this.toolStripTextBox_onnxTemperature.Text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out temperature) || !double.IsFinite(temperature) || temperature < 0)
            {
                error = "Temperature must be a number greater than or equal to 0.";
            }
            else if (!double.TryParse(this.toolStripTextBox_onnxTopP.Text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out topP) || !double.IsFinite(topP) || topP < 0 || topP > 1)
            {
                error = "Top P must be between 0 and 1.";
            }
            else if (!int.TryParse(this.toolStripTextBox_onnxTopK.Text.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out topK) || topK < 0)
            {
                error = "Top K must be a non-negative whole number.";
            }
            else if (!double.TryParse(this.toolStripTextBox_onnxRepeatPenalty.Text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out repeatPenalty) || !double.IsFinite(repeatPenalty) || repeatPenalty <= 0)
            {
                error = "Repeat Penalty must be greater than 0.";
            }

            if (error.Length > 0)
            {
                return false;
            }

            this._persistentSettings.OnnxModelRootDirectory = this.toolStripTextBox_onnxModelRootDir.Text.Trim();
            OnnxModelChoice? selectedChoice = this.toolStripComboBox_onnxModels.SelectedItem as OnnxModelChoice;
            this._persistentSettings.OnnxModel = selectedChoice?.ModelId ?? this.toolStripComboBox_onnxModels.Text.Trim();
            if (selectedChoice is not null)
            {
                this._persistentSettings.OnnxModelLayout = selectedChoice.Layout;
            }

            this._persistentSettings.OnnxContextLength = contextLength;
            this._persistentSettings.OnnxMaxTokens = maxTokens;
            this._persistentSettings.OnnxTemperature = temperature;
            this._persistentSettings.OnnxTopP = topP;
            this._persistentSettings.OnnxTopK = topK;
            this._persistentSettings.OnnxRepeatPenalty = repeatPenalty;
            this._persistentSettings.OnnxExecutionProvider = this.toolStripComboBox_onnxExecutionProvider.SelectedItem as string ?? string.Empty;
            this._persistentSettings.OnnxHideConsole = this.toolStripMenuItem_onnxHideCmd.Checked;
            return true;
        }
    }

    // ------------------------------------------------------------------
    // Progress-Dialog für Python-Env-Setup
    // ------------------------------------------------------------------

    internal sealed class OnnxEnvProgressForm : Form
    {
        private readonly ProgressBar _progressBar;
        private readonly TextBox _logBox;
        private readonly Button _copyButton;
        private readonly Button _closeButton;
        private int _progressValue;

        public OnnxEnvProgressForm(IWin32Window owner)
        {
            this.Text = "ONNX Python Environment Setup";
            this.ClientSize = new Size(560, 460);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
            this.TopMost = true;

            _progressBar = new ProgressBar
            {
                Location = new Point(12, 12),
                Size = new Size(536, 25),
                Style = ProgressBarStyle.Continuous,
                Maximum = 100
            };

            _logBox = new TextBox
            {
                Location = new Point(12, 45),
                Size = new Size(536, 355),
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                Font = new Font("Consolas", 9f),
                WordWrap = false
            };

            _copyButton = new Button
            {
                Location = new Point(350, 410),
                Size = new Size(90, 32),
                Text = "Copy Log",
                Visible = false
            };
            _copyButton.Click += (_, _) =>
            {
                try { Clipboard.SetText(_logBox.Text); } catch { }
            };

            _closeButton = new Button
            {
                Location = new Point(450, 410),
                Size = new Size(98, 32),
                Text = "Close",
                Visible = false
            };
            _closeButton.Click += (_, _) => this.Close();

            this.Controls.Add(_progressBar);
            this.Controls.Add(_logBox);
            this.Controls.Add(_copyButton);
            this.Controls.Add(_closeButton);
        }

        public void SetFullLog(string text)
        {
            _logBox.Text = text;
            _logBox.ScrollToCaret();
        }

        public void AppendLine(string? line)
        {
            if (line == null)
            {
                return;
            }

            _logBox.AppendText(line + Environment.NewLine);
            _logBox.ScrollToCaret();

            if (line.Contains("Collecting") || line.Contains("Installing") || line.Contains("Requirement already satisfied"))
            {
                _progressValue = Math.Min(_progressValue + 10, 90);
                _progressBar.Value = _progressValue;
            }
            else if (line.Contains("Successfully installed") || line.Contains("All packages already installed"))
            {
                _progressBar.Value = 100;
            }
        }

        public void MarkFailed()
        {
            _progressBar.Value = 100;
            _logBox.AppendText("\n\n[FAILED] Python environment setup did not complete successfully.\n");
            _logBox.AppendText("Review the log above for details.\n");
            _logBox.ScrollToCaret();
            _copyButton.Visible = true;
            _closeButton.Visible = true;
            this.Text = "ONNX Python Environment Setup - FAILED";
        }
    }
}
