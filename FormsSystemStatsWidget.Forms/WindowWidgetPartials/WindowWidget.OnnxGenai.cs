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
        private Process? _onnxGenaiServerProcess;

        // ------------------------------------------------------------------
        // Contextmenu: "Load ONNX-Genai Server"
        // ------------------------------------------------------------------

        private async void toolStripMenuItem_loadOnnxGenaiServer_Click(object? sender, EventArgs e)
        {
            if (WidgetStatics.GetOnnxGenaiServerProcesses().Count > 0)
            {
                this.toolStripMenuItem_killOnnxGenaiServer_Click(sender, e);
                return;
            }

            string modelRootDir = this.toolStripTextBox_onnxModelRootDir.Text.Trim();
            string? selectedModel = this.toolStripComboBox_onnxModels.SelectedItem as string ?? this.toolStripComboBox_onnxModels.Text.Trim();

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

            string? onnxPath = Directory.EnumerateFiles(modelRootPath, "*.onnx", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(f => !f.EndsWith(".onnx.data", StringComparison.OrdinalIgnoreCase));
            if (onnxPath is null)
            {
                _ = MessageBox.Show(this, $"No .onnx file found in:\n{modelRootPath}", "No ONNX File", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            bool hideCmd = this.toolStripMenuItem_onnxHideCmd.Checked;

            string pythonScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "run_onnx_genai_server.py");
            if (!File.Exists(pythonScript))
            {
                pythonScript = string.Empty;
            }

            string fullCommand;
            if (!string.IsNullOrEmpty(pythonScript))
            {
                fullCommand = $"python \"{pythonScript}\" " +
                    $"--model \"{onnxPath}\" " +
                    $"--context-length {contextLength} " +
                    $"--max-tokens {maxTokens} " +
                    $"--temperature {temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                    $"--top-p {topP.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                    $"--top-k {topK} " +
                    $"--repeat-penalty {repeatPenalty.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                    $"--execution-provider {executionProvider}";
            }
            else
            {
                fullCommand = $"dotnet run --project \"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "FormsSystemStatsWidget.OnnxGenaiServer", "FormsSystemStatsWidget.OnnxGenaiServer.csproj")}\" " +
                    $"--OnnxGenaiServer:DefaultModel={selectedModel} " +
                    $"--OnnxGenaiServer:ModelRootDirectory={modelRootDir} " +
                    $"--OnnxGenaiServer:ContextLength={contextLength} " +
                    $"--OnnxGenaiServer:MaxTokens={maxTokens} " +
                    $"--OnnxGenaiServer:Temperature={temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                    $"--OnnxGenaiServer:TopP={topP.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                    $"--OnnxGenaiServer:TopK={topK} " +
                    $"--OnnxGenaiServer:RepeatPenalty={repeatPenalty.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                    $"--OnnxGenaiServer:ExecutionProvider={executionProvider}";
            }

            DialogResult result = MessageBox.Show(this,
                $"The following command will be executed to start the ONNX-Genai Server:\n\n{fullCommand}\n\nDo you want to proceed?",
                "Confirm ONNX-Genai Server Start", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            try
            {
                this._debugConsoleForm?.ClearLogs();
                this.StartOnnxGenaiServerProcess(fullCommand, hideCmd);
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
                Arguments = $"\"{scriptPath}\" --install --elevated",
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
                if (e.Data == null) return;
                lock (outputLock) output.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lock (outputLock) output.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            int exitCode = process.ExitCode;
            string outputText;
            lock (outputLock) outputText = output.ToString();

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

        private void StartOnnxGenaiServerProcess(string fullCommand, bool hideCmd)
        {
            if (string.IsNullOrWhiteSpace(fullCommand))
                throw new InvalidOperationException("ONNX-Genai Server command is empty.");

            this.StopTrackedOnnxGenaiServerProcess();

            var cur = this.Cursor;
            this.Cursor = Cursors.WaitCursor;

            string trimmed = fullCommand.Trim();
            string executableName;
            string arguments;

            if (trimmed.StartsWith("python ", StringComparison.OrdinalIgnoreCase))
            {
                executableName = "python";
                arguments = trimmed.Substring("python ".Length);
            }
            else if (trimmed.StartsWith("dotnet ", StringComparison.OrdinalIgnoreCase))
            {
                executableName = "dotnet";
                arguments = trimmed.Substring("dotnet ".Length);
            }
            else
            {
                executableName = trimmed.Split(' ')[0];
                arguments = trimmed.Substring(executableName.Length + 1);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executableName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            this._onnxGenaiServerProcess = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            this._onnxGenaiServerProcess.OutputDataReceived += this.HandleOnnxGenaiServerOutputDataReceived;
            this._onnxGenaiServerProcess.ErrorDataReceived += this.HandleOnnxGenaiServerOutputDataReceived;

            if (!this._onnxGenaiServerProcess.Start())
                throw new InvalidOperationException("ONNX-Genai Server process could not be started.");

            this.OpenDebugConsoleIfRequested(hideCmd);
            this._onnxGenaiServerProcess.BeginOutputReadLine();
            this._onnxGenaiServerProcess.BeginErrorReadLine();

            this.Cursor = cur;
            Logger.Log($"[ONNX-Genai Server] Started: {executableName} {arguments}");
        }

        private void HandleOnnxGenaiServerOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            string? line = e.Data;
            if (string.IsNullOrWhiteSpace(line)) return;
            if (!this.toolStripMenuItem_onnxHideCmd.Checked)
            {
                Logger.Log(line);
            }
        }

        private void StopTrackedOnnxGenaiServerProcess()
        {
            if (this._onnxGenaiServerProcess == null) return;
            try { this._onnxGenaiServerProcess.OutputDataReceived -= this.HandleOnnxGenaiServerOutputDataReceived; } catch { }
            try { this._onnxGenaiServerProcess.ErrorDataReceived -= this.HandleOnnxGenaiServerOutputDataReceived; } catch { }
            try { if (!this._onnxGenaiServerProcess.HasExited) this._onnxGenaiServerProcess.Kill(true); } catch { }
            try { this._onnxGenaiServerProcess.Dispose(); } catch { }
            this._onnxGenaiServerProcess = null;
        }

        // ------------------------------------------------------------------
        // Contextmenu-Opening: ONNX-Modelle laden
        // ------------------------------------------------------------------

        private void toolStripTextBox_onnxModelRootDir_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode != System.Windows.Forms.Keys.Enter) return;

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
            if (!this._onnxSettingsInitialized || this._refreshingOnnxModels) return;
            if (this.TryPersistOnnxSettings(out _)) this.SavePersistentSettings();
        }

        private void toolStripComboBox_onnxExecutionProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!this._onnxSettingsInitialized) return;
            if (this.TryPersistOnnxSettings(out _)) this.SavePersistentSettings();
        }

        private void toolStripMenuItem_onnxHideCmd_CheckedChanged(object? sender, EventArgs e)
        {
            if (!this._onnxSettingsInitialized) return;
            if (this.TryPersistOnnxSettings(out _)) this.SavePersistentSettings();
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

                if (string.IsNullOrEmpty(modelRootDir) || !Directory.Exists(modelRootDir)) return;

                foreach (var subDir in Directory.EnumerateDirectories(modelRootDir))
                {
                    string id = Path.GetFileName(subDir);
                    bool hasOnnx = Directory.EnumerateFiles(subDir, "*.onnx", SearchOption.TopDirectoryOnly)
                        .Any(f => !f.EndsWith(".onnx.data", StringComparison.OrdinalIgnoreCase));
                    bool hasJson = Directory.EnumerateFiles(subDir, "*.json", SearchOption.TopDirectoryOnly).Any();
                    if (hasOnnx && hasJson) this.toolStripComboBox_onnxModels.Items.Add(id);
                }

                if (this.toolStripComboBox_onnxModels.Items.Count > 0)
                {
                    int preferredIndex = this.toolStripComboBox_onnxModels.Items.IndexOf(preferredModel);
                    this.toolStripComboBox_onnxModels.SelectedIndex = preferredIndex >= 0 ? preferredIndex : 0;
                }
            }
            finally
            {
                this._refreshingOnnxModels = false;
            }
        }

        private void toolStripTextBox_onnxContextLength_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) => this.PersistOnnxSettingsOnEnter(e);
        private void toolStripTextBox_onnxMaxTokens_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) => this.PersistOnnxSettingsOnEnter(e);
        private void toolStripTextBox_onnxTemperature_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) => this.PersistOnnxSettingsOnEnter(e);
        private void toolStripTextBox_onnxTopP_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) => this.PersistOnnxSettingsOnEnter(e);
        private void toolStripTextBox_onnxTopK_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) => this.PersistOnnxSettingsOnEnter(e);
        private void toolStripTextBox_onnxRepeatPenalty_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) => this.PersistOnnxSettingsOnEnter(e);

        private void PersistOnnxSettingsOnEnter(System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode != System.Windows.Forms.Keys.Enter) return;
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
                error = "Context Length must be a positive whole number.";
            else if (!int.TryParse(this.toolStripTextBox_onnxMaxTokens.Text.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out maxTokens) || maxTokens <= 0)
                error = "Max Tokens must be a positive whole number.";
            else if (!double.TryParse(this.toolStripTextBox_onnxTemperature.Text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out temperature) || !double.IsFinite(temperature) || temperature < 0)
                error = "Temperature must be a number greater than or equal to 0.";
            else if (!double.TryParse(this.toolStripTextBox_onnxTopP.Text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out topP) || !double.IsFinite(topP) || topP < 0 || topP > 1)
                error = "Top P must be between 0 and 1.";
            else if (!int.TryParse(this.toolStripTextBox_onnxTopK.Text.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out topK) || topK < 0)
                error = "Top K must be a non-negative whole number.";
            else if (!double.TryParse(this.toolStripTextBox_onnxRepeatPenalty.Text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out repeatPenalty) || !double.IsFinite(repeatPenalty) || repeatPenalty <= 0)
                error = "Repeat Penalty must be greater than 0.";

            if (error.Length > 0) return false;

            this._persistentSettings.OnnxModelRootDirectory = this.toolStripTextBox_onnxModelRootDir.Text.Trim();
            this._persistentSettings.OnnxModel = this.toolStripComboBox_onnxModels.SelectedItem as string ?? this.toolStripComboBox_onnxModels.Text.Trim();
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
            if (line == null) return;
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
