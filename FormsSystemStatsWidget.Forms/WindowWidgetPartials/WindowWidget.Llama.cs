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
            if (!this.toolStripMenuItem_execModelLoadBat.Enabled)
            {
                this.toolStripMenuItem_execModelLoadBat.Text = $"'{LlamaOllamaBridge.DetectedModelName}-{LlamaOllamaBridge.QuantizationLevel}'";
            }
            else
            {
                this.toolStripMenuItem_execModelLoadBat.Text = "Execute Model Load .BAT";
            }

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

        private void toolStripTextBox_modelsDirectory_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                // Validate the path and update the setting if it's a valid directory
                string path = this.toolStripTextBox_modelsDirectory.Text.Trim();
                if (Directory.Exists(path))
                {
                    LlamaCppModelLoader.GgufModelsDirectory = Path.GetFullPath(path);

                    string[] modelIds = LlamaCppModelLoader.GetModelFilePaths().Select(path => Path.GetFileNameWithoutExtension(path) ?? "").ToArray();
                    this.toolStripComboBox_ggufModels.Items.Clear();
                    this.toolStripComboBox_ggufModels.Items.AddRange(modelIds);
                }
                else
                {
                    _ = MessageBox.Show(this, "The specified directory does not exist. Please enter a valid path.", "Invalid Directory", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.toolStripTextBox_modelsDirectory.Text = LlamaCppModelLoader.GgufModelsDirectory;
                }
            }

            // Add to persistent settings
            this._persistentSettings.GgufModelDirectory = LlamaCppModelLoader.GgufModelsDirectory;
            this.SavePersistentSettings();
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

        private void toolStripMenuItem_thinking_CheckedChanged(object sender, EventArgs e)
        {
            this._persistentSettings.Thinking = this.toolStripMenuItem_thinking.Checked;
            this.SavePersistentSettings();
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
