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
    [SupportedOSPlatform("windows")]
    public partial class WindowWidget : Form
    {
        private const int MultiGpuClientHeight = 271;
        private const int SingleGpuClientHeight = 223;
        private const long MetricsDirectoryQuotaBytes = 100L * 1024L * 1024L;
        private const long MetricsCurrentFileSoftQuotaBytes = 96L * 1024L * 1024L;
        private const int MetricsQuotaCheckSampleCount = 30;
        private static readonly Encoding RecordingCsvEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        private int _updateIntervalMs = 420;
        private Color _diagramColor = Color.White;
        private Color? _percentageColor = Color.BlueViolet;


        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern int SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern int GetWindowLong(IntPtr hwnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);

        internal const int GWL_EXSTYLE = -20;
        internal const int WS_EX_LAYERED = 0x80000;

        // Audio recording
        internal static readonly AudioHandling Audios = new();

        private bool _isAwaitingHotkeyInput = false;
        private Dictionary<Keys, bool> _currentModifierKeys = new Dictionary<Keys, bool>();
        private Keys? _firstModifierKey = null;
        private Keys? _otherKey = null;


        private Timer UpdateTimer;
        private GpuStats? Gpu;
        private GpuStats? Gpu2 = null;
        private DynamicGradientProgressBar _progRam;
        private DynamicGradientProgressBar _progVram;
        private DynamicGradientProgressBar _progVram2;
        private volatile bool _closing = false;
        private int _tickInProgress = 0;
        private CancellationTokenSource? _recordingCancellationTokenSource;
        private Task? _recordingTask;
        internal static readonly TimeSpan TopTasksSamplingInterval = TimeSpan.FromMilliseconds(1200);
        private IReadOnlyList<(string processName, double cpuPercent)> _cachedTopTasks = Array.Empty<(string processName, double cpuPercent)>();
        private DateTime _lastTopTasksSampleUtc = DateTime.MinValue;
        private DateTime _lastTickDiagnosticsUtc = DateTime.MinValue;
        private DebugConsoleForm? _debugConsoleForm;
        private WidgetPersistentSettings _persistentSettings = new();
        private bool _explicitWidgetCloseRequested;
        private Process? _llamaServerProcess;
        private CancellationTokenSource? _processCts;
        private readonly HashSet<Keys> _processingKeys = [];
        private static readonly Regex TokensPerSecondRegex = MyRegex();
        private double _lastStdOutTokensPerSecond;
        private DateTime _lastStdOutTokensPerSecondUtc = DateTime.MinValue;

        private const int WmSysCommand = 0x0112;
        private const int ScClose = 0xF060;

        internal static float CustomOpacity { get; private set; }

        public WindowWidget()
        {
            this.InitializeComponent();
            this.DoubleBuffered = true;
            this._persistentSettings = WidgetPersistentSettingsStore.Load();
            Logger.MessageLogged += this.HandleLoggerMessageLogged;
            this.ConfigureContextMenuAutoCloseBehavior();

            this.InitializeProgressBars();
            this.InitializeUpdateTimer();
            this.InitializeGpuSelection();
            this.InitializeWidgetMouseHandlers();

            // Restore window position from persistent settings + sanity check to ensure it's not off-screen
            if (this._persistentSettings.WidgetPosition != Point.Empty)
            {
                Rectangle screenBounds = Screen.FromPoint(this._persistentSettings.WidgetPosition).Bounds;
                Size widgetSize = this.Size;
                Point adjustedPosition = this._persistentSettings.WidgetPosition;
                if (adjustedPosition.X < screenBounds.Left)
                {
                    adjustedPosition.X = screenBounds.Left;
                }
                else if (adjustedPosition.X + widgetSize.Width > screenBounds.Right)
                {
                    adjustedPosition.X = screenBounds.Right - widgetSize.Width;
                }
                if (adjustedPosition.Y < screenBounds.Top)
                {
                    adjustedPosition.Y = screenBounds.Top;
                }
                else if (adjustedPosition.Y + widgetSize.Height > screenBounds.Bottom)
                {
                    adjustedPosition.Y = screenBounds.Bottom - widgetSize.Height;
                }
                this.StartPosition = FormStartPosition.Manual;
                this.Location = adjustedPosition;
            }

            // Hook event for when Form is moved, to update the position in persistent settings
            this.Move += (sender, e) =>
            {
                if (!this._explicitWidgetCloseRequested)
                {
                    this._persistentSettings.WidgetPosition = this.Location;
                }
            };

            // Save actually when HandleDestroyed
            this.FormClosing += (sender, e) =>
            {
                this.SavePersistentSettings();
            };

            try { TrafficStats.Init(); }
            catch { }

            this.InitializeLlamaUiSelections();
            this.InitializeBridgeSamplingSettingsFromUi();

            this.enableSmartPromptOptimizationsToolStripMenuItem.Checked = SmartPromptOptimizationSettings.IsEnabled;
            this.toolStripTextBox_promptSafetyRatio.Text = SmartPromptOptimizationSettings.PromptSafetyRatio.ToString("0.00", CultureInfo.InvariantCulture);
            this.toolStripTextBox_smartBudgetRatio.Text = SmartPromptOptimizationSettings.SmartBudgetRatio.ToString("0.00", CultureInfo.InvariantCulture);
            this.toolStripTextBox_largeMessageThresholdChars.Text = SmartPromptOptimizationSettings.LargeMessageThresholdChars.ToString(CultureInfo.InvariantCulture);
            this.toolStripTextBox_skeletonMaxLines.Text = SmartPromptOptimizationSettings.SkeletonMaxLines.ToString(CultureInfo.InvariantCulture);
            this.toolStripTextBox_focusKeywordLimit.Text = SmartPromptOptimizationSettings.FocusKeywordLimit.ToString(CultureInfo.InvariantCulture);
            this.toolStripTextBox_tailKeepBonusChars.Text = SmartPromptOptimizationSettings.TailKeepBonusChars.ToString(CultureInfo.InvariantCulture);

            this.EnsureModelLoadBatsDirectory();

            this.ApplyPersistentSettings();

            this.InitializeAudioHotkeys();
        }

        private void InitializeProgressBars()
        {
            this._progRam = new DynamicGradientProgressBar { Name = "progRam" };
            this._progRam.Size = this.progressBar_ram.Size;
            this._progRam.Location = this.progressBar_ram.Location;

            this._progVram = new DynamicGradientProgressBar { Name = "progVram" };
            this._progVram.Size = this.progressBar_vram.Size;
            this._progVram.Location = this.progressBar_vram.Location;

            this._progVram2 = new DynamicGradientProgressBar { Name = "progVram2" };
            this._progVram2.Size = this.progressBar_vram2.Size;
            this._progVram2.Location = this.progressBar_vram2.Location;

            this.Controls.Add(this._progRam);
            this.Controls.Add(this._progVram);
            this.Controls.Add(this._progVram2);

            this.progressBar_ram.Visible = false;
            this.progressBar_vram.Visible = false;
            this.progressBar_vram2.Visible = false;
        }

        private void InitializeUpdateTimer()
        {
            this.UpdateTimer = new Timer();
            this.UpdateTimer.Interval = this._updateIntervalMs;
            this.UpdateTimer.Tick += this.Timer_Tick;
            this.UpdateTimer.Start();
        }

        private void InitializeGpuSelection()
        {
            this.toolStripComboBox_gpus.Items.Clear();
            this.toolStripComboBox_gpus.Items.AddRange(GpuStats.GpuNames.ToArray());
            if (this.toolStripComboBox_gpus.Items.Count > 0)
            {
                this.toolStripComboBox_gpus.SelectedIndex = 0;
            }

            this.Gpu = new GpuStats(this.toolStripComboBox_gpus.SelectedIndex);
            if (GpuStats.GpuNames.Count > 1)
            {
                this.Gpu2 = new GpuStats(1);
            }

            this.ApplyGpuLayout();
        }

        private void InitializeWidgetMouseHandlers()
        {
            this.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    this.Capture = false;
                    Message m = Message.Create(this.Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
                    this.WndProc(ref m);
                    return;
                }

                if (e.Button == MouseButtons.Right)
                {
                    this.contextMenuStrip_widget.Show(this, e.Location);
                }
            };
        }

        private void InitializeLlamaUiSelections()
        {
            this.PopulateDriveSelections();
            this.ApplyDriveSpeedTestSettingTexts();

            this.toolStripComboBox_ggufModels.Items.Clear();
            this.toolStripComboBox_ggufModels.Items.AddRange(LlamaCppModelLoader.ModelFilePaths.Select(path => Path.GetFileNameWithoutExtension(path)).ToArray());
            if (this.toolStripComboBox_ggufModels.Items.Count > 0)
            {
                this.toolStripComboBox_ggufModels.SelectedIndex = 0;
            }

            this.InitializeSplitModeDefaults();
        }

        private void InitializeSplitModeDefaults()
        {
            this.toolStripComboBox_splitMode.SelectedIndex = 0;
            if (this.toolStripComboBox_gpus.Items.Count <= 1)
            {
                return;
            }

            this.toolStripComboBox_splitMode.SelectedIndex = 2;
            this.toolStripMenuItem_tensorSplit.Enabled = true;
            this.toolStripTextBox_tensorSplit.Enabled = true;

            long gpu1VramGb = this.Gpu != null ? (long) Math.Round(this.Gpu.GetTotalVramBytes() / 1_073_741_824.0) : 0;
            long gpu2VramGb = this.Gpu2 != null ? (long) Math.Round(this.Gpu2.GetTotalVramBytes() / 1_073_741_824.0) : 0;
            this.toolStripTextBox_tensorSplit.Text = gpu1VramGb > 0 && gpu2VramGb > 0
                ? $"{gpu1VramGb},{gpu2VramGb}"
                : string.Empty;
        }

        private void InitializeBridgeSamplingSettingsFromUi()
        {
            LlamaOllamaBridge.UserDefinedTemperature = double.TryParse(this.toolStripTextBox_temperature.Text, out double temperature) ? temperature : 0.3;
            LlamaOllamaBridge.UserDefinedRepetitionPenalty = double.TryParse(this.toolStripTextBox_repetationPenalty.Text, out double repetitionPenalty) ? repetitionPenalty : 1.1;
            LlamaOllamaBridge.UserDefinedTopP = double.TryParse(this.toolStripTextBox_topP.Text, out double topP) ? topP : 0.9;
            LlamaOllamaBridge.UserDefinedMinP = double.TryParse(this.toolStripTextBox_minP.Text, out double minP) ? minP : 0.1;
            LlamaOllamaBridge.UserDefinedTopK = int.TryParse(this.toolStripTextBox_topK.Text, out int topK) ? topK : 40;
        }

        private void EnsureModelLoadBatsDirectory()
        {
            string batsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "llama.cpp_load_BATs");
            if (!Directory.Exists(batsDirectory))
            {
                Directory.CreateDirectory(batsDirectory);
            }
        }

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
            this.toolStripMenuItem_thinking.Checked= this._persistentSettings.Thinking;
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
        }


        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (this.ShouldBlockClose(e))
            {
                e.Cancel = true;
                Logger.Log("[WindowWidget] Unexpected form closing was blocked.");
                return;
            }

            this.ReleaseFormClosingResources();
            base.OnFormClosing(e);
        }

        private bool ShouldBlockClose(FormClosingEventArgs e)
        {
            return e.CloseReason != CloseReason.WindowsShutDown && !this._explicitWidgetCloseRequested;
        }

        private void ReleaseFormClosingResources()
        {
            this._closing = true;
            this.UpdateTimer.Stop();
            Logger.MessageLogged -= this.HandleLoggerMessageLogged;

            if (this._debugConsoleForm != null)
            {
                try { this._debugConsoleForm.Close(); } catch { }
                this._debugConsoleForm = null;
            }

            try { this.Gpu?.Dispose(); } catch { }
            try { this.Gpu2?.Dispose(); } catch { }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmSysCommand)
            {
                long command = m.WParam.ToInt64() & 0xFFF0;
                if (command == ScClose)
                {
                    this._explicitWidgetCloseRequested = true;
                }
            }

            base.WndProc(ref m);
        }

        private void TrySetWindowTitleSafe(string title)
        {
            if (this.IsDisposed || this.Disposing)
            {
                return;
            }

            try
            {
                if (!this.IsHandleCreated)
                {
                    return;
                }

                if (this.InvokeRequired)
                {
                    _ = this.BeginInvoke(new Action(() =>
                    {
                        if (!this.IsDisposed && !this.Disposing)
                        {
                            this.Text = title;
                        }
                    }));
                }
                else
                {
                    this.Text = title;
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void UpdateTitleWithTraffic()
        {
            string up = TrafficStats.FormatBytesPerSecond(TrafficStats.UpBytesPerSecond);
            string down = TrafficStats.FormatBytesPerSecond(TrafficStats.DownBytesPerSecond);
            string top = string.Empty;
            // Only show top talker when both conditions hold:
            // 1) total network traffic is at or above the configured threshold
            // 2) the top process IO rate is at or above the configured threshold
            double netTotal = TrafficStats.UpBytesPerSecond + TrafficStats.DownBytesPerSecond;
            if (netTotal >= TrafficStats.ThresholdBytesPerSecond && !string.IsNullOrEmpty(TrafficStats.TopTalker) && TrafficStats.ActiveProcesses.Count > 0)
            {
                var topEntry = TrafficStats.ActiveProcesses[0];
                if (topEntry.Name == TrafficStats.TopTalker && topEntry.IoBytesPerSec >= TrafficStats.ThresholdBytesPerSecond)
                {
                    top = WidgetStatics.Ellipsize(TrafficStats.TopTalker, 30);
                }
            }
            this.Text = $"\u2191 {up}  \u2193 {down}  {top}";
        }



        private void HandleLoggerMessageLogged(string text)
        {
            Match match = TokensPerSecondRegex.Match(text);
            if (match.Success && double.TryParse(match.Groups["tps"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedTps))
            {
                this._lastStdOutTokensPerSecond = parsedTps;
                this._lastStdOutTokensPerSecondUtc = DateTime.UtcNow;
                LlamaServerStats.UpdateGenerationSpeed((float) parsedTps);
            }

            if (this._debugConsoleForm == null || this._debugConsoleForm.IsDisposed)
            {
                return;
            }

            this._debugConsoleForm.AppendLogLine(text);
        }

        private void ConfigureContextMenuAutoCloseBehavior()
        {
            this.toolStripMenuItem_loadLlamaCppServer.DropDown.Closing += this.KeepSelectedSubMenuOpenForItemClicks;
            this.toolStripMenuItem_execModelLoadBat.DropDown.Closing += this.KeepSelectedSubMenuOpenForItemClicks;
            this.openDebugConsoleToolStripMenuItem.DropDown.Closing += this.KeepSelectedSubMenuOpenForItemClicks;
        }

        private void KeepSelectedSubMenuOpenForItemClicks(object? sender, ToolStripDropDownClosingEventArgs e)
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
            {
                e.Cancel = true;
            }
        }

        private void ApplyGpuLayout()
        {
            bool hasSecondGpu = this.Gpu2 != null;

            this.label_gpuLoad2.Visible = hasSecondGpu;
            this.label_gpuWatts2.Visible = hasSecondGpu;
            this.label_gpuVram2.Visible = hasSecondGpu;
            this._progVram2.Visible = hasSecondGpu;

            int clientHeight = hasSecondGpu ? MultiGpuClientHeight : SingleGpuClientHeight;
            this.ClientSize = new Size(this.ClientSize.Width, clientHeight);

            Size windowSize = new(256, clientHeight + 39);
            this.MinimumSize = windowSize;
            this.MaximumSize = windowSize;
        }




        /// <summary>
        /// TIMER TICK : Main Update-Loop for HW Stats and UI.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Timer_Tick(object? sender, EventArgs e)
        {
            if (this._closing)
            {
                return;
            }

            if (Interlocked.Exchange(ref this._tickInProgress, 1) == 1)
            {
                return;
            }

            try
            {
                Stopwatch tickStopwatch = Stopwatch.StartNew();
                var gpuRef = this.Gpu;

                var threadsTask = Task.Run(() => CpuStats.GetThreadUsages());
                var topTasksTask = this.GetTopTasksSnapshotAsync();
                var ramTask = Task.Run(() =>
                {
                    double total = Math.Round(CpuStats.GetTotalMemoryBytes() / 1_073_741_824.0, 3);
                    double used = Math.Round(CpuStats.GetUsedMemoryBytes() / 1_073_741_824.0, 3);
                    return (total, used);
                });
                var gpuTask = Task.Run(() =>
                {
                    try
                    {
                        double usage = gpuRef?.CurrentLoad01 * 100 ?? 0;
                        double wattage = gpuRef?.CurrentPowerWatts ?? 0;
                        double vramTotal = Math.Round(gpuRef?.GetTotalVramBytes() / 1_073_741_824.0 ?? 0, 3);
                        double vramUsed = Math.Round(gpuRef?.GetUsedVramBytes() / 1_073_741_824.0 ?? 0, 3);
                        return (usage, wattage, vramTotal, vramUsed);
                    }
                    catch
                    {
                        return (0.0, 0.0, 0.0, 0.0);
                    }
                });
                var trafficTask = Task.Run(() => TrafficStats.Sample(this._updateIntervalMs));

                await Task.WhenAll(threadsTask, topTasksTask, ramTask, gpuTask, trafficTask);

                var threads = threadsTask.Result;
                var topTasks = topTasksTask.Result;
                var (ramTotalGb, ramUsedGb) = ramTask.Result;
                var (gpuUsage, gpuWattage, vramTotalGb, vramUsedGb) = gpuTask.Result;

                this.UpdateAverageCpuLoadAndTemperatureLabel(threads);
                this.UpdateTopTasksLabel(topTasks);
                await this.UpdateLlamaServerGenerationSpeedAsync();

                await Task.WhenAll(
                    this.UpdateCpuUsageAsync(threads),
                    this.UpdateRamUsageAsync(ramTotalGb, ramUsedGb),
                    this.UpdateGpuUsageAsync(gpuUsage, gpuWattage),
                    this.UpdateVramUsageAsync(vramTotalGb, vramUsedGb)
                );

                if (this._closing)
                {
                    return;
                }

                this.UpdateTitleWithTraffic();

                this.LogTickDurationIfSlow(tickStopwatch.Elapsed);
            }
            finally
            {
                _ = Interlocked.Exchange(ref this._tickInProgress, 0);
            }
        }

        private async Task UpdateLlamaServerGenerationSpeedAsync()
        {
            if (!this.rerouteAPILlamacppOllamaToolStripMenuItem.Checked || !this.showTokenssToolStripMenuItem.Checked)
            {
                return;
            }

            int llamaPort = int.TryParse(this.toolStripTextBox_llamacppPort.Text.Trim(), out int parsedLlamaPort) ? parsedLlamaPort : 8080;
            string ollamaPortStr = this.toolStripTextBox_ollamaPort.Text.Trim();

            // FIX: Niemals den Text aus dem Label parsen, da Asynchronität zu kaputten Strings wie "Port: ----" führen kann.
            // Immer sauber frisch aus den Config-Textboxen aufbauen!
            string baseText = $"Port {llamaPort} to {ollamaPortStr}";

            double genSpeed;
            if ((DateTime.UtcNow - this._lastStdOutTokensPerSecondUtc) <= TimeSpan.FromSeconds(2))
            {
                genSpeed = this._lastStdOutTokensPerSecond;
            }
            else
            {
                genSpeed = await LlamaServerStats.GetCurrentLlamaServerGenerationStatsAsync(llamaPort) ?? 0f;
            }

            string speedString = genSpeed >= 0.01f ? $"{genSpeed:0.000} tokens/s" : "Idle (0.000 tokens/s)";
            string nextText = $"{baseText}{Environment.NewLine}{speedString}";

            if (this.InvokeRequired)
            {
                _ = this.BeginInvoke(new Action(() =>
                {
                    if (!this.IsDisposed && !this.Disposing)
                    {
                        this.label_routingPortsInfo.Text = nextText;

                        if (genSpeed > 0f && this.toolStripMenuItem_logGenerationSpeed.Checked)
                        {
                            Logger.Log($" --- Llama server generation speed: {genSpeed:0.000} tokens/s");
                        }
                    }
                }));
                return;
            }

            this.label_routingPortsInfo.Text = nextText;
        }

        private Task<IReadOnlyList<(string processName, double cpuPercent)>> GetTopTasksSnapshotAsync()
        {
            DateTime nowUtc = DateTime.UtcNow;
            return (nowUtc - this._lastTopTasksSampleUtc) < TopTasksSamplingInterval
                ? Task.FromResult(this._cachedTopTasks)
                : Task.Run(() =>
            {
                IReadOnlyList<(string processName, double cpuPercent)> sampledTopTasks = CpuStats.GetTopCpuProcesses();
                this._cachedTopTasks = sampledTopTasks;
                this._lastTopTasksSampleUtc = DateTime.UtcNow;
                return sampledTopTasks;
            });
        }

        private void LogTickDurationIfSlow(TimeSpan tickDuration)
        {
            if (!Debugger.IsAttached)
            {
                return;
            }

            if (tickDuration.TotalMilliseconds < this._updateIntervalMs * 1.2)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            if ((nowUtc - this._lastTickDiagnosticsUtc) < TimeSpan.FromSeconds(3))
            {
                return;
            }

            this._lastTickDiagnosticsUtc = nowUtc;
            Debug.WriteLine($"[WidgetTick] Slow tick: {tickDuration.TotalMilliseconds:0} ms (target {this._updateIntervalMs} ms)");
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




        private async Task UpdateCpuUsageAsync(float[] usages)
        {
            Color? percentageColor = this.showUsageToolStripMenuItem.Checked ? this._percentageColor : null;
            var bmp = await CpuStats.RenderCoresBitmapAsync(usages, this.pictureBox_cpu.Width, this.pictureBox_cpu.Height, this._diagramColor, percentageColor, CancellationToken.None);
            if (this._closing) { bmp?.Dispose(); return; }
            this.pictureBox_cpu.Image?.Dispose();
            this.pictureBox_cpu.Image = bmp;
        }

        private void UpdateTopTasksLabel(IReadOnlyList<(string processName, double cpuPercent)> topTasks)
        {
            if (this._closing)
            {
                return;
            }

            if (topTasks.Count == 0)
            {
                this.label_topTasksList.Text = "   -% idle\r\n   -% idle\r\n   -% idle";
                return;
            }

            List<string> lines = new(3);
            for (int index = 0; index < 3; index++)
            {
                if (index < topTasks.Count)
                {
                    (string processName, double cpuPercent) = topTasks[index];
                    string name = WidgetStatics.Ellipsize(processName, 24);
                    string percentText = $"{Math.Round(cpuPercent),3:0}%";
                    lines.Add($"{percentText} {name}");
                }
                else
                {
                    lines.Add("  0% -");
                }
            }

            this.label_topTasksList.Text = string.Join(Environment.NewLine, lines);
        }

        private Task UpdateRamUsageAsync(double totalGb, double usedGb)
        {
            if (this._closing)
            {
                return Task.CompletedTask;
            }

            double percentUsed = totalGb > 0 ? (usedGb / totalGb) * 100 : 0;

            this.label_ram.Text = $"RAM: {usedGb} GB / {totalGb} GB ({percentUsed:0.00}%)";
            this._progRam.Value = Math.Clamp((int) percentUsed, 0, this._progRam.Maximum);


            return Task.CompletedTask;
        }

        private Task UpdateGpuUsageAsync(double usagePercent, double wattage)
        {
            if (this._closing)
            {
                return Task.CompletedTask;
            }

            this.label_gpuUsage.Text = $"GPU: {usagePercent:0.00}%";
            this.label_wattage.Text = $"Watts: {wattage:0.00} W";
            this.label_gpuUsage.ForeColor = usagePercent >= 80 ? Color.Red : Color.Black;

            if (this.Gpu2 != null)
            {
                this.label_gpuLoad2.Text = $"GPU2: {this.Gpu2.CurrentLoad01 * 100:0.00}%";
                this.label_gpuWatts2.Text = $"Watts: {this.Gpu2.CurrentPowerWatts ?? 0:0.00} W";
                this.label_gpuLoad2.ForeColor = (this.Gpu2.CurrentLoad01 * 100) >= 80 ? Color.Red : Color.Black;
            }

            return Task.CompletedTask;
        }

        private Task UpdateVramUsageAsync(double totalGb, double usedGb)
        {
            if (this._closing)
            {
                return Task.CompletedTask;
            }

            double percentUsed = totalGb > 0 ? (usedGb / totalGb) * 100 : 0;
            this.label_vram.Text = $"VRAM: {usedGb} GB / {totalGb} GB ({percentUsed:0.00}%)";
            this._progVram.Value = Math.Clamp((int) percentUsed, 0, this._progVram.Maximum);

            if (this.Gpu2 != null)
            {
                long gpu2TotalBytes = this.Gpu2.GetTotalVramBytes();
                long gpu2UsedBytes = this.Gpu2.GetUsedVramBytes();
                double gpu2TotalGb = Math.Round(gpu2TotalBytes / 1_073_741_824.0, 3);
                double gpu2UsedGb = Math.Round(gpu2UsedBytes / 1_073_741_824.0, 3);
                double gpu2PercentUsed = gpu2TotalBytes > 0 ? (Math.Max(0.0, gpu2UsedBytes) / gpu2TotalBytes) * 100.0 : 0.0;

                this.label_gpuVram2.Text = $"VRAM: {gpu2UsedGb} GB / {gpu2TotalGb} GB ({gpu2PercentUsed:0.00}%)";
                this._progVram2.Value = Math.Clamp((int) gpu2PercentUsed, 0, this._progVram2.Maximum);
            }

            return Task.CompletedTask;
        }

        private void UpdateAverageCpuLoadAndTemperatureLabel(float[] usages)
        {
            if (this._closing)
            {
                return;
            }

            double averageLoadPercent = usages.Length > 0 ? usages.Average() * 100d : 0d;

            // No thermal or wattage getting here, since it always fails and causes frequent Exceptions, just set avg. load to label
            this.label_avgCpuLoadAndTemperature.Text = averageLoadPercent.ToString("0.000") + "%";
            return;


            // NON-EFFECTIVE CODE (CUT-OFF, NEVER REACHED (for a reason (!)))
            //CpuStats.CpuTelemetrySnapshot telemetry = CpuStats.GetCpuTelemetrySnapshot();

            //List<string> parts = new()
            //{
            //    $"{averageLoadPercent:0.00}%"
            //};

            //if (telemetry.AverageTemperatureCelsius.HasValue)
            //{
            //    parts.Add($"{telemetry.AverageTemperatureCelsius.Value:0.0} °C");
            //}

            //if (telemetry.PackagePowerWatts.HasValue)
            //{
            //    parts.Add($"{telemetry.PackagePowerWatts.Value:0.0} W");
            //}

            //this.label_avgCpuLoadAndTemperature.Text = string.Join(" | ", parts);
        }



        // ToolStripMenu Event Handlers
        private void toolStripComboBox_gpus_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.Gpu?.Dispose();
            this.Gpu = new GpuStats(this.toolStripComboBox_gpus.SelectedIndex);
        }

        private void toolStripTextBox_diagramColor_TextChanged(object sender, EventArgs e)
        {
            string hex = this.toolStripTextBox_diagramColor.Text.Replace("#", "");
            if (hex.Length == 6 && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int rgb))
            {
                // rgb is RRGGBB; ensure the color is created opaque by adding alpha 0xFF
                this._diagramColor = Color.FromArgb(unchecked((int) 0xFF000000 | rgb));
                this._persistentSettings.DiagramColorHex = $"#{hex.ToUpperInvariant()}";
                this.SavePersistentSettings();
            }
        }

        private void toolStripTextBox_percentageColor_EnabledChanged(object sender, EventArgs e)
        {
            // Do not clear the stored color when disabling; keep the selected color so it can be re-enabled.
            if (this.toolStripTextBox_percentageColor.Enabled)
            {
                this.toolStripTextBox_percentageColor.Text = this._percentageColor.HasValue ? $"#{this._percentageColor.Value.ToArgb() & 0xFFFFFF:X6}" : "";
            }
        }

        private void toolStripTextBox_diagramColor_DoubleClick(object sender, EventArgs e)
        {
            // Color picker dialog
            using (ColorDialog colorDialog = new())
            {
                colorDialog.AllowFullOpen = true;
                colorDialog.AnyColor = true;
                colorDialog.SolidColorOnly = false;
                colorDialog.Color = this._diagramColor;
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    this._diagramColor = colorDialog.Color;
                    this.toolStripTextBox_diagramColor.Text = $"#{colorDialog.Color.ToArgb() & 0xFFFFFF:X6}";
                }
            }
        }

        private void toolStripTextBox_percentageColor_DoubleClick(object sender, EventArgs e)
        {
            // Color picker dialog
            using (ColorDialog colorDialog = new())
            {
                colorDialog.AllowFullOpen = true;
                colorDialog.AnyColor = true;
                colorDialog.SolidColorOnly = false;
                colorDialog.Color = this._percentageColor ?? Color.White;
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    this._percentageColor = colorDialog.Color;
                    this.toolStripTextBox_percentageColor.Text = $"#{colorDialog.Color.ToArgb() & 0xFFFFFF:X6}";
                    this.toolStripTextBox_percentageColor.Enabled = true;
                }
            }
        }

        private void alwaysOnTopToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            this.TopMost = this.alwaysOnTopToolStripMenuItem.Checked;
            _ = (this._debugConsoleForm?.TopMost = this.alwaysOnTopToolStripMenuItem.Checked);
            this._persistentSettings.AlwaysOnTop = this.alwaysOnTopToolStripMenuItem.Checked;
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



        // Get top tasks with loads %
        internal static string GetTopTaskName(IReadOnlyList<(string processName, double cpuPercent)> topTasks, int index)
        {
            return index < topTasks.Count ? topTasks[index].processName : string.Empty;
        }

        internal static string GetTopTaskPercent(IReadOnlyList<(string processName, double cpuPercent)> topTasks, int index)
        {
            return index < topTasks.Count
                ? FormatRecordingNumber(topTasks[index].cpuPercent)
                : string.Empty;
        }


        //Hotkey (recording)
        private void ResetHotkeyInputState()
        {
            this._isAwaitingHotkeyInput = false;
            this._currentModifierKeys.Clear();
            this._firstModifierKey = null;
            this._otherKey = null;
        }

        // Init / Update / Fill ctxmenu items etc.
        private void contextMenuStrip_widget_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Refresh Models
            string[] modelIds = LlamaCppModelLoader.GetModelFilePaths().Select(path => Path.GetFileNameWithoutExtension(path) ?? "").ToArray();
            this.toolStripComboBox_ggufModels.Items.Clear();
            this.toolStripComboBox_ggufModels.Items.AddRange(modelIds);

            // Get llama-server.exe Processes running
            var processes = WidgetStatics.GetLlamaServerProcesses();

            // Always remove both handlers first to ensure a clean state and avoid duplicates
            this.toolStripMenuItem_loadLlamaCppServer.Click -= this.toolStripMenuItem_loadLlamaCppServer_Click;
            this.toolStripMenuItem_loadLlamaCppServer.Click -= this.ToolStripMenuItem_killLlamaServer_Click;

            if (processes.Count > 0)
            {
                this.toolStripMenuItem_execModelLoadBat.Enabled = false;
                this.toolStripMenuItem_loadLlamaCppServer.Text = $"Kill llama-server ({processes.Count})";

                foreach (ToolStripItem item in this.toolStripMenuItem_loadLlamaCppServer.DropDownItems)
                {
                    item.Visible = false;
                }

                this.toolStripMenuItem_loadLlamaCppServer.Click -= this.toolStripMenuItem_loadLlamaCppServer_Click;
                this.toolStripMenuItem_loadLlamaCppServer.Click += this.ToolStripMenuItem_killLlamaServer_Click;
            }
            else
            {
                this.toolStripMenuItem_execModelLoadBat.Enabled = true;
                this.toolStripMenuItem_loadLlamaCppServer.Text = "Load Model (llama-server.exe)";

                foreach (ToolStripItem item in this.toolStripMenuItem_loadLlamaCppServer.DropDownItems)
                {
                    item.Visible = true;
                }

                this.rerouteAPILlamacppOllamaToolStripMenuItem.Checked = false;
                this.toolStripMenuItem_loadLlamaCppServer.Click -= this.ToolStripMenuItem_killLlamaServer_Click;
                this.toolStripMenuItem_loadLlamaCppServer.Click += this.toolStripMenuItem_loadLlamaCppServer_Click;
            }


            // Get & fill all .BAT files from EXE directory \ llama.cpp_load_BATs \ 
            string batsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "llama.cpp_load_BATs");
            if (!Directory.Exists(batsDirectory))
            {
                Directory.CreateDirectory(batsDirectory);
            }

            string[] batFilePaths = Directory.GetFiles(batsDirectory, "*.bat").ToArray();
            batFilePaths = batFilePaths.OrderByDescending(File.GetLastWriteTime).ToArray();
            string[] batFileNames = batFilePaths.Select(path => Path.GetFileNameWithoutExtension(path)).ToArray();
            this.toolStripComboBox_modelLoadBats.Items.Clear();
            this.toolStripComboBox_modelLoadBats.Items.AddRange(batFileNames);
            if (this.toolStripComboBox_modelLoadBats.Items.Count > 0)
            {
                this.toolStripComboBox_modelLoadBats.SelectedIndex = 0;
            }
        }

        private void toolStripTextBox_opacity_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            this.toolStripTextBox_opacity.Text = this.toolStripTextBox_opacity.Text.Trim().Replace(" ", "").Replace("%", "") + "%";

            float opacity = 0.0f;
            if (float.TryParse(this.toolStripTextBox_opacity.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedOpacity))
            {
                opacity = parsedOpacity;
            }
            else
            {
                // Try parse with percentage sign, e.g. "80%" or as int
                if (this.toolStripTextBox_opacity.Text.EndsWith("%") && float.TryParse(this.toolStripTextBox_opacity.Text.TrimEnd('%').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedPercentage))
                {
                    opacity = parsedPercentage / 100f;
                }
                else if (int.TryParse(this.toolStripTextBox_opacity.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInt))
                {
                    opacity = parsedInt / 100f;
                }
                else
                {
                    opacity = 0.0f;
                }
            }

            opacity = Math.Clamp(opacity, 0.1f, 1.0f);
            this.toolStripTextBox_opacity.Text = (opacity >= 0.99f ? "100" : (opacity * 100).ToString("0")) + "%";

            // Set form + elements + border opacity 
            // this.Opacity = opacity;

            // Wende die Opacity auf alle direkt enthaltenen Controls an
            int initialStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
            _ = SetWindowLong(this.Handle, GWL_EXSTYLE, initialStyle | WS_EX_LAYERED);
            _ = SetLayeredWindowAttributes(this.Handle, 0, (byte) (opacity * 255), 0x00000002);
            if (this._debugConsoleForm != null)
            {
                this._debugConsoleForm.ApplyOpacity(opacity);
            }
            CustomOpacity = opacity;

            // Save persistent settings
            this._persistentSettings.WindowOpacity = (int) (opacity * 100);
            this.SavePersistentSettings();
        }

        private void toolStripMenuItem_configureVoiceInputHotkey_Click(object sender, EventArgs e)
        {
            // 1. Hotkey-Text ersetzen
            string originalText = this.toolStripMenuItem_configureVoiceInputHotkey.Text ?? "Set Voice Input Hotkey ... (<none>)";
            string modifiedText = SetVoiceInputHotkeyRegex().Replace(originalText, " ( ... )");
            this.toolStripMenuItem_configureVoiceInputHotkey.Text = modifiedText;

            // 2. Zustand setzen und Event-Listener aktivieren
            this._isAwaitingHotkeyInput = true;
            // Hier müssten die KeyDown/KeyUp Event-Handler für das Formular registriert werden.
            // Beispiel: this.KeyDown += WindowWidget_KeyDown;
            // Beispiel: this.KeyUp += WindowWidget_KeyUp;

            // 3. CtxMenuStrip offen halten (ist durch den Click-Handler implizit gegeben)
            // ... (Kein Code nötig, da der Event-Handler den Kontext hält)
        }

        private void WindowWidget_KeyDown(object sender, KeyEventArgs e)
        {
            if (!this._isAwaitingHotkeyInput)
            {
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                // Abbrechen, wenn ESC gedrückt wird
                this.ResetHotkeyInputState();
                this.ContextMenuStrip?.Close();
                return;
            }

            // 1. Modifikator-Key erfassen
            if (e.KeyCode == Keys.LControlKey || e.KeyCode == Keys.Alt || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RControlKey || e.KeyCode == Keys.Alt || e.KeyCode == Keys.RWin)
            {
                if (!this._currentModifierKeys.ContainsKey(e.KeyCode))
                {
                    this._currentModifierKeys[e.KeyCode] = false;
                }
                this._currentModifierKeys[e.KeyCode] = true;
                if (this._firstModifierKey == null)
                {
                    this._firstModifierKey = e.KeyCode;
                }
            }
            // 2. Nicht-Modifikator-Key erfassen
            else if (e.KeyCode != Keys.Tab && e.KeyCode != Keys.Enter)
            {
                if (this._otherKey == null)
                {
                    this._otherKey = e.KeyCode;
                }
            }
        }

        private void WindowWidget_KeyUp(object sender, KeyEventArgs e)
        {
            if (!this._isAwaitingHotkeyInput)
            {
                return;
            }

            // KeyUp-Verarbeitung
            if (e.KeyCode == Keys.LControlKey || e.KeyCode == Keys.Alt || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RControlKey || e.KeyCode == Keys.Alt || e.KeyCode == Keys.RWin)
            {
                if (this._currentModifierKeys.ContainsKey(e.KeyCode))
                {
                    this._currentModifierKeys[e.KeyCode] = false;
                }
            }
            // Hier müsste die Logik zur Überprüfung, ob *alle* relevanten Keys Up sind, implementiert werden.
            // Für diesen Scope reicht es, wenn wir das State-Update durch das Event-Handling abdecken.
        }





        [GeneratedRegex(@"\((.*?)\)")]
        private static partial Regex SetVoiceInputHotkeyRegex();
        [GeneratedRegex(@"(?<tps>\d+(?:\.\d+)?)\s*(?:tokens?/s|t/s)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, "de-DE")]
        private static partial Regex MyRegex();

        
    }
}

