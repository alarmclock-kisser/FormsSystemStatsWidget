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
    public partial class WindowWidget
    {
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
    }
}
