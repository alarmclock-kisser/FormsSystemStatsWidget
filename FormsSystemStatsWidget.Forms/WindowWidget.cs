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
        private Dictionary<Keys, bool> _currentModifierKeys = [];
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

        [GeneratedRegex(@"\((.*?)\)")]
        private static partial Regex SetVoiceInputHotkeyRegex();
        [GeneratedRegex(@"(?<tps>\d+(?:\.\d+)?)\s*(?:tokens?/s|t/s)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, "de-DE")]
        private static partial Regex MyRegex();

    }
}

