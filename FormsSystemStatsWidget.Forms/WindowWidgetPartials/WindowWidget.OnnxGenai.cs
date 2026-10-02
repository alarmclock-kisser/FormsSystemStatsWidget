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
        // ONNX parameter submenus (Sampling / Execution / Limits), built in code.
        // Existing items are regrouped into them; new tunables follow the same
        // textbox/combobox/check patterns as the rest of the menu.
        // ------------------------------------------------------------------

        private static readonly string[] OnnxKvExecutionModes = ["sequential", "parallel"];
        private static readonly string[] OnnxKvGraphOptimizations = ["all", "extended", "basic", "disable_all"];
        private static readonly string[] OnnxKvArenaStrategies = ["kNextPowerOfTwo", "kSameAsRequested"];
        private static readonly string[] OnnxKvCudnnSearchModes = ["EXHAUSTIVE", "HEURISTIC", "DEFAULT"];

        private ToolStripMenuItem? _menuOnnxSampling;
        private ToolStripMenuItem? _menuOnnxExecution;
        private ToolStripMenuItem? _menuOnnxLimits;

        private ToolStripMenuItem? _menuOnnxMinP;
        private ToolStripTextBox? _boxOnnxMinP;
        private ToolStripMenuItem? _menuOnnxTypicalP;
        private ToolStripTextBox? _boxOnnxTypicalP;
        private ToolStripMenuItem? _menuOnnxPresencePenalty;
        private ToolStripTextBox? _boxOnnxPresencePenalty;
        private ToolStripMenuItem? _menuOnnxFrequencyPenalty;
        private ToolStripTextBox? _boxOnnxFrequencyPenalty;
        private ToolStripMenuItem? _menuOnnxRepeatLastN;
        private ToolStripTextBox? _boxOnnxRepeatLastN;
        private ToolStripMenuItem? _menuOnnxSeed;
        private ToolStripTextBox? _boxOnnxSeed;
        private ToolStripMenuItem? _menuOnnxStopSequences;
        private ToolStripTextBox? _boxOnnxStopSequences;
        private ToolStripMenuItem? _menuOnnxEnableThinking;
        private ToolStripMenuItem? _menuOnnxSystemPrompt;
        private ToolStripTextBox? _boxOnnxSystemPrompt;

        private ToolStripMenuItem? _menuOnnxStage0Device;
        private ToolStripTextBox? _boxOnnxStage0Device;
        private ToolStripMenuItem? _menuOnnxStage1Device;
        private ToolStripTextBox? _boxOnnxStage1Device;
        private ToolStripMenuItem? _menuOnnxIntraOpThreads;
        private ToolStripTextBox? _boxOnnxIntraOpThreads;
        private ToolStripMenuItem? _menuOnnxInterOpThreads;
        private ToolStripTextBox? _boxOnnxInterOpThreads;
        private ToolStripMenuItem? _menuOnnxExecutionMode;
        private ToolStripComboBox? _comboOnnxExecutionMode;
        private ToolStripMenuItem? _menuOnnxGraphOptimization;
        private ToolStripComboBox? _comboOnnxGraphOptimization;
        private ToolStripMenuItem? _menuOnnxMemPattern;
        private ToolStripMenuItem? _menuOnnxCpuMemArena;
        private ToolStripMenuItem? _menuOnnxProfiling;
        private ToolStripMenuItem? _menuOnnxDisablePrepacking;
        private ToolStripMenuItem? _menuOnnxGpuMemLimitMb;
        private ToolStripTextBox? _boxOnnxGpuMemLimitMb;
        private ToolStripMenuItem? _menuOnnxArenaStrategy;
        private ToolStripComboBox? _comboOnnxArenaStrategy;
        private ToolStripMenuItem? _menuOnnxCudnnSearch;
        private ToolStripComboBox? _comboOnnxCudnnSearch;
        private ToolStripMenuItem? _menuOnnxCopyDefaultStream;
        private ToolStripMenuItem? _menuOnnxCudaGraphs;
        private ToolStripMenuItem? _menuOnnxTf32;
        private ToolStripMenuItem? _menuOnnxCpuFallback;

        private ToolStripMenuItem? _menuOnnxMaxConcurrent;
        private ToolStripTextBox? _boxOnnxMaxConcurrent;

        private void BuildOnnxParameterSubmenus()
        {
            var host = this.toolStripMenuItem_loadOnnxGenaiServer;

            // Regroup existing items out of the top level (fields stay valid).
            host.DropDownItems.Remove(this.toolStripMenuItem_onnxTemperature);
            host.DropDownItems.Remove(this.toolStripMenuItem_onnxTopP);
            host.DropDownItems.Remove(this.toolStripMenuItem_onnxTopK);
            host.DropDownItems.Remove(this.toolStripMenuItem_onnxRepeatPenalty);
            host.DropDownItems.Remove(this.toolStripMenuItem_onnxContextLength);
            host.DropDownItems.Remove(this.toolStripMenuItem_onnxMaxTokens);
            host.DropDownItems.Remove(this.toolStripComboBox_onnxExecutionProvider);

            this._menuOnnxSampling = new ToolStripMenuItem { Text = "Sampling ..." };
            this._menuOnnxSampling.DropDownItems.AddRange(new ToolStripItem[]
            {
                this.toolStripMenuItem_onnxTemperature,
                this.toolStripMenuItem_onnxTopP,
                this.toolStripMenuItem_onnxTopK,
                this.CreateOnnxInputMenu("Min P", out this._menuOnnxMinP, out this._boxOnnxMinP, "0.0"),
                this.CreateOnnxInputMenu("Typical P", out this._menuOnnxTypicalP, out this._boxOnnxTypicalP, "1.0"),
                this.toolStripMenuItem_onnxRepeatPenalty,
                this.CreateOnnxInputMenu("Repeat Last N (0 = full history)", out this._menuOnnxRepeatLastN, out this._boxOnnxRepeatLastN, "0"),
                this.CreateOnnxInputMenu("Presence Penalty", out this._menuOnnxPresencePenalty, out this._boxOnnxPresencePenalty, "0.0"),
                this.CreateOnnxInputMenu("Frequency Penalty", out this._menuOnnxFrequencyPenalty, out this._boxOnnxFrequencyPenalty, "0.0"),
                this.CreateOnnxInputMenu("Seed (empty = random)", out this._menuOnnxSeed, out this._boxOnnxSeed, string.Empty),
                this.CreateOnnxInputMenu("Stop Sequences (; separated)", out this._menuOnnxStopSequences, out this._boxOnnxStopSequences, string.Empty, boxWidth: 200),
                this.CreateOnnxCheckMenu("Enable Thinking", @default: false, out this._menuOnnxEnableThinking),
                this.CreateOnnxInputMenu("System Prompt", out this._menuOnnxSystemPrompt, out this._boxOnnxSystemPrompt, string.Empty, boxWidth: 280),
            });

            this._menuOnnxExecution = new ToolStripMenuItem { Text = "Execution ..." };
            this._menuOnnxExecution.DropDownItems.AddRange(new ToolStripItem[]
            {
                this.toolStripComboBox_onnxExecutionProvider,
                this.CreateOnnxInputMenu("Stage 0 Device", out this._menuOnnxStage0Device, out this._boxOnnxStage0Device, "0"),
                this.CreateOnnxInputMenu("Stage 1 Device", out this._menuOnnxStage1Device, out this._boxOnnxStage1Device, "1"),
                this.CreateOnnxInputMenu("Intra-Op Threads (0 = auto)", out this._menuOnnxIntraOpThreads, out this._boxOnnxIntraOpThreads, "0"),
                this.CreateOnnxInputMenu("Inter-Op Threads (0 = auto)", out this._menuOnnxInterOpThreads, out this._boxOnnxInterOpThreads, "0"),
                this.CreateOnnxComboMenu("Execution Mode", OnnxKvExecutionModes, "sequential", out this._menuOnnxExecutionMode, out this._comboOnnxExecutionMode),
                this.CreateOnnxComboMenu("Graph Optimization", OnnxKvGraphOptimizations, "all", out this._menuOnnxGraphOptimization, out this._comboOnnxGraphOptimization),
                this.CreateOnnxCheckMenu("Memory Pattern", @default: true, out this._menuOnnxMemPattern),
                this.CreateOnnxCheckMenu("CPU Memory Arena", @default: true, out this._menuOnnxCpuMemArena),
                this.CreateOnnxCheckMenu("Profiling (writes ORT profiles)", @default: false, out this._menuOnnxProfiling),
                this.CreateOnnxCheckMenu("Disable Prepacking", @default: false, out this._menuOnnxDisablePrepacking),
                this.CreateOnnxInputMenu("GPU Mem Limit MB (0 = unlimited)", out this._menuOnnxGpuMemLimitMb, out this._boxOnnxGpuMemLimitMb, "0"),
                this.CreateOnnxComboMenu("Arena Strategy", OnnxKvArenaStrategies, "kNextPowerOfTwo", out this._menuOnnxArenaStrategy, out this._comboOnnxArenaStrategy),
                this.CreateOnnxComboMenu("cuDNN Conv Search", OnnxKvCudnnSearchModes, "EXHAUSTIVE", out this._menuOnnxCudnnSearch, out this._comboOnnxCudnnSearch),
                this.CreateOnnxCheckMenu("Copy In Default Stream", @default: true, out this._menuOnnxCopyDefaultStream),
                this.CreateOnnxCheckMenu("CUDA Graphs (experimental)", @default: false, out this._menuOnnxCudaGraphs),
                this.CreateOnnxCheckMenu("TF32", @default: true, out this._menuOnnxTf32),
                this.CreateOnnxCheckMenu("CPU Fallback", @default: false, out this._menuOnnxCpuFallback),
            });

            this._menuOnnxLimits = new ToolStripMenuItem { Text = "Limits ..." };
            this._menuOnnxLimits.DropDownItems.AddRange(new ToolStripItem[]
            {
                this.toolStripMenuItem_onnxContextLength,
                this.toolStripMenuItem_onnxMaxTokens,
                this.CreateOnnxInputMenu("Max Concurrent Generations", out this._menuOnnxMaxConcurrent, out this._boxOnnxMaxConcurrent, "1"),
            });

            // Top level keeps: Model Root Directory, models combo, submenus, Hide Console.
            host.DropDownItems.Add(this._menuOnnxSampling);
            host.DropDownItems.Add(this._menuOnnxExecution);
            host.DropDownItems.Add(this._menuOnnxLimits);

            foreach (var submenu in new[] { this._menuOnnxSampling, this._menuOnnxExecution, this._menuOnnxLimits })
            {
                submenu.DropDown.Closing += this.KeepSelectedSubMenuOpenForItemClicks;
            }

            this._comboOnnxExecutionMode!.SelectedIndexChanged += this.OnnxSubmenuSettingChanged;
            this._comboOnnxGraphOptimization!.SelectedIndexChanged += this.OnnxSubmenuSettingChanged;
            this._comboOnnxArenaStrategy!.SelectedIndexChanged += this.OnnxSubmenuSettingChanged;
            this._comboOnnxCudnnSearch!.SelectedIndexChanged += this.OnnxSubmenuSettingChanged;

            foreach (var check in new[]
            {
                this._menuOnnxEnableThinking!, this._menuOnnxMemPattern!, this._menuOnnxCpuMemArena!,
                this._menuOnnxProfiling!, this._menuOnnxDisablePrepacking!, this._menuOnnxCopyDefaultStream!,
                this._menuOnnxCudaGraphs!, this._menuOnnxTf32!, this._menuOnnxCpuFallback!,
            })
            {
                check.CheckedChanged += this.OnnxSubmenuSettingChanged;
            }
        }

        private ToolStripMenuItem CreateOnnxInputMenu(string text, out ToolStripMenuItem menu, out ToolStripTextBox box, string defaultText, int boxWidth = 100)
        {
            var localBox = new ToolStripTextBox { Size = new Size(boxWidth, 23), Text = defaultText };
            localBox.KeyDown += (s, e) => this.PersistOnnxSettingsOnEnter(e);
            menu = new ToolStripMenuItem { Text = text };
            menu.DropDownItems.Add(localBox);
            box = localBox;
            return menu;
        }

        private ToolStripMenuItem CreateOnnxComboMenu(string text, string[] items, string defaultText, out ToolStripMenuItem menu, out ToolStripComboBox combo)
        {
            var localCombo = new ToolStripComboBox { Size = new Size(160, 23), Text = defaultText };
            localCombo.Items.AddRange(items);
            menu = new ToolStripMenuItem { Text = text };
            menu.DropDownItems.Add(localCombo);
            combo = localCombo;
            return menu;
        }

        private ToolStripMenuItem CreateOnnxCheckMenu(string text, bool @default, out ToolStripMenuItem menu)
        {
            menu = new ToolStripMenuItem { Text = text, CheckOnClick = true, Checked = @default };
            return menu;
        }

        private void OnnxSubmenuSettingChanged(object? sender, EventArgs e)
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

        private void ApplyOnnxSubmenuSettings()
        {
            var settings = this._persistentSettings;
            SetOnnxBoxText(this._boxOnnxMinP, settings.OnnxMinP.ToString(System.Globalization.CultureInfo.InvariantCulture));
            SetOnnxBoxText(this._boxOnnxTypicalP, settings.OnnxTypicalP.ToString(System.Globalization.CultureInfo.InvariantCulture));
            SetOnnxBoxText(this._boxOnnxPresencePenalty, settings.OnnxPresencePenalty.ToString(System.Globalization.CultureInfo.InvariantCulture));
            SetOnnxBoxText(this._boxOnnxFrequencyPenalty, settings.OnnxFrequencyPenalty.ToString(System.Globalization.CultureInfo.InvariantCulture));
            SetOnnxBoxText(this._boxOnnxRepeatLastN, settings.OnnxRepeatLastN.ToString(System.Globalization.CultureInfo.InvariantCulture));
            SetOnnxBoxText(this._boxOnnxSeed, settings.OnnxSeed);
            SetOnnxBoxText(this._boxOnnxStopSequences, settings.OnnxStopSequences);
            SetOnnxBoxText(this._boxOnnxSystemPrompt, settings.OnnxSystemPrompt);
            SetOnnxBoxText(this._boxOnnxStage0Device, settings.OnnxStage0Device.ToString(System.Globalization.CultureInfo.InvariantCulture));
            SetOnnxBoxText(this._boxOnnxStage1Device, settings.OnnxStage1Device.ToString(System.Globalization.CultureInfo.InvariantCulture));
            SetOnnxBoxText(this._boxOnnxIntraOpThreads, settings.OnnxIntraOpThreads.ToString(System.Globalization.CultureInfo.InvariantCulture));
            SetOnnxBoxText(this._boxOnnxInterOpThreads, settings.OnnxInterOpThreads.ToString(System.Globalization.CultureInfo.InvariantCulture));
            SetOnnxBoxText(this._boxOnnxGpuMemLimitMb, settings.OnnxGpuMemLimitMb.ToString(System.Globalization.CultureInfo.InvariantCulture));
            SetOnnxBoxText(this._boxOnnxMaxConcurrent, settings.OnnxMaxConcurrentGenerations.ToString(System.Globalization.CultureInfo.InvariantCulture));

            SetOnnxComboText(this._comboOnnxExecutionMode, settings.OnnxExecutionMode);
            SetOnnxComboText(this._comboOnnxGraphOptimization, settings.OnnxGraphOptimization);
            SetOnnxComboText(this._comboOnnxArenaStrategy, settings.OnnxArenaExtendStrategy);
            SetOnnxComboText(this._comboOnnxCudnnSearch, settings.OnnxCudnnConvAlgoSearch);

            SetOnnxCheckState(this._menuOnnxEnableThinking, settings.OnnxEnableThinking);
            SetOnnxCheckState(this._menuOnnxMemPattern, settings.OnnxEnableMemPattern);
            SetOnnxCheckState(this._menuOnnxCpuMemArena, settings.OnnxEnableCpuMemArena);
            SetOnnxCheckState(this._menuOnnxProfiling, settings.OnnxEnableProfiling);
            SetOnnxCheckState(this._menuOnnxDisablePrepacking, settings.OnnxDisablePrepacking);
            SetOnnxCheckState(this._menuOnnxCopyDefaultStream, settings.OnnxCopyInDefaultStream);
            SetOnnxCheckState(this._menuOnnxCudaGraphs, settings.OnnxUseCudaGraphs);
            SetOnnxCheckState(this._menuOnnxTf32, settings.OnnxUseTf32);
            SetOnnxCheckState(this._menuOnnxCpuFallback, settings.OnnxAllowCpuFallback);
        }

        private static void SetOnnxBoxText(ToolStripTextBox? box, string text)
        {
            if (box is not null)
            {
                box.Text = text;
            }
        }

        private static void SetOnnxComboText(ToolStripComboBox? combo, string text)
        {
            if (combo is null)
            {
                return;
            }

            int index = combo.Items.IndexOf(text);
            combo.SelectedIndex = index >= 0 ? index : -1;
            if (index < 0)
            {
                combo.Text = text;
            }
        }

        private static void SetOnnxCheckState(ToolStripMenuItem? menu, bool @checked)
        {
            if (menu is not null)
            {
                menu.Checked = @checked;
            }
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
            foreach (string extraArg in this.BuildOnnxSamplingArgs())
            {
                startInfo.ArgumentList.Add(extraArg);
            }
            foreach (string extraArg in this.BuildOnnxExecutionArgs(executionProvider))
            {
                startInfo.ArgumentList.Add(extraArg);
            }

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

        private List<string> BuildOnnxSamplingArgs()
        {
            var args = new List<string>();
            double minP = this.TryParseOnnxBoxDouble(this._boxOnnxMinP, out double parsedMinP) ? parsedMinP : 0.0;
            double typicalP = this.TryParseOnnxBoxDouble(this._boxOnnxTypicalP, out double parsedTypicalP) ? parsedTypicalP : 1.0;
            double presencePenalty = this.TryParseOnnxBoxDouble(this._boxOnnxPresencePenalty, out double parsedPresence) ? parsedPresence : 0.0;
            double frequencyPenalty = this.TryParseOnnxBoxDouble(this._boxOnnxFrequencyPenalty, out double parsedFrequency) ? parsedFrequency : 0.0;
            int repeatLastN = this.TryParseOnnxBoxInt(this._boxOnnxRepeatLastN, out int parsedRepeatLastN) ? parsedRepeatLastN : 0;

            args.Add($"--OnnxGenaiServer:MinP={minP.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            args.Add($"--OnnxGenaiServer:TypicalP={typicalP.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            args.Add($"--OnnxGenaiServer:PresencePenalty={presencePenalty.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            args.Add($"--OnnxGenaiServer:FrequencyPenalty={frequencyPenalty.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            args.Add($"--OnnxGenaiServer:RepeatLastN={repeatLastN.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

            if (int.TryParse(this._boxOnnxSeed?.Text.Trim() ?? string.Empty, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int seed))
            {
                args.Add($"--OnnxGenaiServer:DefaultSeed={seed.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }

            string stopSequences = this._boxOnnxStopSequences?.Text.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(stopSequences))
            {
                args.Add($"--OnnxGenaiServer:StopSequences={stopSequences}");
            }

            if (this._menuOnnxEnableThinking?.Checked == true)
            {
                args.Add("--OnnxGenaiServer:EnableThinking=true");
            }

            string systemPrompt = this._boxOnnxSystemPrompt?.Text.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                args.Add($"--OnnxGenaiServer:SystemPrompt={systemPrompt}");
            }

            return args;
        }

        private List<string> BuildOnnxExecutionArgs(string executionProvider)
        {
            var args = new List<string>();
            int stage0 = this.TryParseOnnxBoxInt(this._boxOnnxStage0Device, out int parsedStage0) ? parsedStage0 : 0;
            int stage1 = this.TryParseOnnxBoxInt(this._boxOnnxStage1Device, out int parsedStage1) ? parsedStage1 : 1;
            int intraThreads = this.TryParseOnnxBoxInt(this._boxOnnxIntraOpThreads, out int parsedIntra) ? parsedIntra : 0;
            int interThreads = this.TryParseOnnxBoxInt(this._boxOnnxInterOpThreads, out int parsedInter) ? parsedInter : 0;
            long gpuMemMb = long.TryParse(this._boxOnnxGpuMemLimitMb?.Text.Trim() ?? "0", System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long parsedGpuMem) ? parsedGpuMem : 0;
            int maxConcurrent = this.TryParseOnnxBoxInt(this._boxOnnxMaxConcurrent, out int parsedMaxConcurrent) ? parsedMaxConcurrent : 1;

            args.Add($"--OnnxGenaiServer:Stage0Device={stage0.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            args.Add($"--OnnxGenaiServer:Stage1Device={stage1.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            args.Add($"--OnnxGenaiServer:IntraOpThreads={intraThreads.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            args.Add($"--OnnxGenaiServer:InterOpThreads={interThreads.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            args.Add($"--OnnxGenaiServer:ExecutionMode={this._comboOnnxExecutionMode?.Text.Trim() ?? "sequential"}");
            args.Add($"--OnnxGenaiServer:GraphOptimization={this._comboOnnxGraphOptimization?.Text.Trim() ?? "all"}");
            args.Add($"--OnnxGenaiServer:GpuMemLimitMb={gpuMemMb.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            args.Add($"--OnnxGenaiServer:ArenaExtendStrategy={this._comboOnnxArenaStrategy?.Text.Trim() ?? "kNextPowerOfTwo"}");
            args.Add($"--OnnxGenaiServer:CudnnConvAlgoSearch={this._comboOnnxCudnnSearch?.Text.Trim() ?? "EXHAUSTIVE"}");
            args.Add($"--OnnxGenaiServer:MaxConcurrentGenerations={Math.Max(1, maxConcurrent).ToString(System.Globalization.CultureInfo.InvariantCulture)}");

            AddOnnxBoolArg(args, "EnableMemPattern", this._menuOnnxMemPattern?.Checked ?? true);
            AddOnnxBoolArg(args, "EnableCpuMemArena", this._menuOnnxCpuMemArena?.Checked ?? true);
            AddOnnxBoolArg(args, "EnableProfiling", this._menuOnnxProfiling?.Checked ?? false);
            AddOnnxBoolArg(args, "DisablePrepacking", this._menuOnnxDisablePrepacking?.Checked ?? false);
            AddOnnxBoolArg(args, "CopyInDefaultStream", this._menuOnnxCopyDefaultStream?.Checked ?? true);
            AddOnnxBoolArg(args, "UseCudaGraphs", this._menuOnnxCudaGraphs?.Checked ?? false);
            AddOnnxBoolArg(args, "UseTf32", this._menuOnnxTf32?.Checked ?? true);
            AddOnnxBoolArg(args, "AllowCpuFallback", this._menuOnnxCpuFallback?.Checked ?? false);
            return args;
        }

        private static void AddOnnxBoolArg(List<string> args, string name, bool value)
        {
            args.Add($"--OnnxGenaiServer:{name}={(value ? "true" : "false")}");
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
            double minP = 0;
            double typicalP = 0;
            double presencePenalty = 0;
            double frequencyPenalty = 0;
            int repeatLastN = 0;
            int stage0Device = 0;
            int stage1Device = 0;
            int intraOpThreads = 0;
            int interOpThreads = 0;
            long gpuMemLimitMb = 0;
            int maxConcurrent = 0;
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
            else if (!this.TryParseOnnxBoxDouble(this._boxOnnxMinP, out minP) || minP < 0 || minP > 1)
            {
                error = "Min P must be between 0 and 1.";
            }
            else if (!this.TryParseOnnxBoxDouble(this._boxOnnxTypicalP, out typicalP) || typicalP <= 0 || typicalP > 1)
            {
                error = "Typical P must be greater than 0 and at most 1.";
            }
            else if (!this.TryParseOnnxBoxDouble(this._boxOnnxPresencePenalty, out presencePenalty) || !double.IsFinite(presencePenalty))
            {
                error = "Presence Penalty must be a number.";
            }
            else if (!this.TryParseOnnxBoxDouble(this._boxOnnxFrequencyPenalty, out frequencyPenalty) || !double.IsFinite(frequencyPenalty))
            {
                error = "Frequency Penalty must be a number.";
            }
            else if (!this.TryParseOnnxBoxInt(this._boxOnnxRepeatLastN, out repeatLastN) || repeatLastN < 0)
            {
                error = "Repeat Last N must be a non-negative whole number (0 = full history).";
            }
            else if (!string.IsNullOrWhiteSpace(this._boxOnnxSeed?.Text) && !int.TryParse(this._boxOnnxSeed.Text.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                error = "Seed must be a whole number or empty (random).";
            }
            else if (!this.TryParseOnnxBoxInt(this._boxOnnxStage0Device, out stage0Device) || stage0Device < 0)
            {
                error = "Stage 0 Device must be a non-negative whole number.";
            }
            else if (!this.TryParseOnnxBoxInt(this._boxOnnxStage1Device, out stage1Device) || stage1Device < 0)
            {
                error = "Stage 1 Device must be a non-negative whole number.";
            }
            else if (!this.TryParseOnnxBoxInt(this._boxOnnxIntraOpThreads, out intraOpThreads) || intraOpThreads < 0)
            {
                error = "Intra-Op Threads must be a non-negative whole number (0 = auto).";
            }
            else if (!this.TryParseOnnxBoxInt(this._boxOnnxInterOpThreads, out interOpThreads) || interOpThreads < 0)
            {
                error = "Inter-Op Threads must be a non-negative whole number (0 = auto).";
            }
            else if (!long.TryParse(this._boxOnnxGpuMemLimitMb?.Text.Trim() ?? "0", System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out gpuMemLimitMb) || gpuMemLimitMb < 0)
            {
                error = "GPU Mem Limit must be a non-negative whole number of MB (0 = unlimited).";
            }
            else if (!this.TryParseOnnxBoxInt(this._boxOnnxMaxConcurrent, out maxConcurrent) || maxConcurrent < 1)
            {
                error = "Max Concurrent Generations must be at least 1.";
            }
            else if (!IsOnnxComboValue(this._comboOnnxExecutionMode, OnnxKvExecutionModes))
            {
                error = "Execution Mode must be sequential or parallel.";
            }
            else if (!IsOnnxComboValue(this._comboOnnxGraphOptimization, OnnxKvGraphOptimizations))
            {
                error = "Graph Optimization must be all, extended, basic or disable_all.";
            }
            else if (!IsOnnxComboValue(this._comboOnnxArenaStrategy, OnnxKvArenaStrategies))
            {
                error = "Arena Strategy must be kNextPowerOfTwo or kSameAsRequested.";
            }
            else if (!IsOnnxComboValue(this._comboOnnxCudnnSearch, OnnxKvCudnnSearchModes))
            {
                error = "cuDNN Conv Search must be EXHAUSTIVE, HEURISTIC or DEFAULT.";
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
            this._persistentSettings.OnnxMinP = minP;
            this._persistentSettings.OnnxTypicalP = typicalP;
            this._persistentSettings.OnnxPresencePenalty = presencePenalty;
            this._persistentSettings.OnnxFrequencyPenalty = frequencyPenalty;
            this._persistentSettings.OnnxRepeatLastN = repeatLastN;
            this._persistentSettings.OnnxSeed = this._boxOnnxSeed?.Text.Trim() ?? string.Empty;
            this._persistentSettings.OnnxStopSequences = this._boxOnnxStopSequences?.Text.Trim() ?? string.Empty;
            this._persistentSettings.OnnxEnableThinking = this._menuOnnxEnableThinking?.Checked ?? false;
            this._persistentSettings.OnnxSystemPrompt = this._boxOnnxSystemPrompt?.Text.Trim() ?? string.Empty;
            this._persistentSettings.OnnxStage0Device = stage0Device;
            this._persistentSettings.OnnxStage1Device = stage1Device;
            this._persistentSettings.OnnxIntraOpThreads = intraOpThreads;
            this._persistentSettings.OnnxInterOpThreads = interOpThreads;
            this._persistentSettings.OnnxExecutionMode = this._comboOnnxExecutionMode?.Text.Trim() ?? "sequential";
            this._persistentSettings.OnnxGraphOptimization = this._comboOnnxGraphOptimization?.Text.Trim() ?? "all";
            this._persistentSettings.OnnxEnableMemPattern = this._menuOnnxMemPattern?.Checked ?? true;
            this._persistentSettings.OnnxEnableCpuMemArena = this._menuOnnxCpuMemArena?.Checked ?? true;
            this._persistentSettings.OnnxEnableProfiling = this._menuOnnxProfiling?.Checked ?? false;
            this._persistentSettings.OnnxDisablePrepacking = this._menuOnnxDisablePrepacking?.Checked ?? false;
            this._persistentSettings.OnnxGpuMemLimitMb = gpuMemLimitMb;
            this._persistentSettings.OnnxArenaExtendStrategy = this._comboOnnxArenaStrategy?.Text.Trim() ?? "kNextPowerOfTwo";
            this._persistentSettings.OnnxCudnnConvAlgoSearch = this._comboOnnxCudnnSearch?.Text.Trim() ?? "EXHAUSTIVE";
            this._persistentSettings.OnnxCopyInDefaultStream = this._menuOnnxCopyDefaultStream?.Checked ?? true;
            this._persistentSettings.OnnxUseCudaGraphs = this._menuOnnxCudaGraphs?.Checked ?? false;
            this._persistentSettings.OnnxUseTf32 = this._menuOnnxTf32?.Checked ?? true;
            this._persistentSettings.OnnxAllowCpuFallback = this._menuOnnxCpuFallback?.Checked ?? false;
            this._persistentSettings.OnnxMaxConcurrentGenerations = maxConcurrent;
            return true;
        }

        private bool TryParseOnnxBoxDouble(ToolStripTextBox? box, out double value)
        {
            value = 0;
            return box is not null
                && double.TryParse(box.Text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        private bool TryParseOnnxBoxInt(ToolStripTextBox? box, out int value)
        {
            value = 0;
            return box is not null
                && int.TryParse(box.Text.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        private static bool IsOnnxComboValue(ToolStripComboBox? combo, string[] allowed)
        {
            string text = combo?.Text.Trim() ?? string.Empty;
            return allowed.Any(item => string.Equals(item, text, StringComparison.OrdinalIgnoreCase));
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
