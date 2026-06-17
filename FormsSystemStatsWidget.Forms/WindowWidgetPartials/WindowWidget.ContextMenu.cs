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
                this.toolStripMenuItem_execModelLoadBat.Text = $"'{LlamaOllamaBridge.DetectedModelName}-{LlamaOllamaBridge.QuantizationLevel}'";
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
                this.toolStripMenuItem_execModelLoadBat.Text = "Execute Model Load .BAT";
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
    }
}
