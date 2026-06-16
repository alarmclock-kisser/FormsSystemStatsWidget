using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace FormsSystemStatsWidget.Forms
{
    internal sealed class DebugConsoleForm : Form
    {
        private const int MaxRepositoryLogFiles = 16;
        private readonly TextBox _logTextBox;
        private readonly ContextMenuStrip _contextMenuStrip_textBox_log;

        internal static int FormWidth { get; private set; } = 700;
        internal static int FormHeight { get; private set; } = 520;


        public DebugConsoleForm()
        {
            this.Text = "Debug Console";
            this.StartPosition = FormStartPosition.Manual;
            this.Width = FormWidth;
            this.Height = FormHeight;

            this._logTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill
            };

            this._contextMenuStrip_textBox_log = new ContextMenuStrip()
            {
                Items =
                {
                    new ToolStripMenuItem("Clear Logs", null, (s, e) => this.ClearLogs()),
                    new ToolStripMenuItem("Save as TXT", null, (s, e) => this.SaveLogsAsTxt()),
                    new ToolStripMenuItem("Word Wrap", null, (s, e) => this.ToggleWordWrap()) { CheckOnClick = true }
                }
            };
            this._logTextBox.ContextMenuStrip = this._contextMenuStrip_textBox_log;

            this.Resize += (s, e) =>
            {
                FormWidth = this.Width;
                FormHeight = this.Height;
            };

            this.Controls.Add(this._logTextBox);

            float opacity = WindowWidget.CustomOpacity;
            if (opacity < 1.0f)
            {
                this.ApplyOpacity(opacity);
            }

            bool blackOutMode = WindowWidget.BlackOutModeEnabled;
            this.ApplyBlackOutMode(blackOutMode);
        }

        private void ToggleWordWrap()
        {
            this._logTextBox.WordWrap = !this._logTextBox.WordWrap;
        }

        public void AppendLogLine(string text)
        {
            if (this.IsDisposed)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                _ = this.BeginInvoke(new Action<string>(this.AppendLogLine), text);
                return;
            }

            this._logTextBox.AppendText(text + Environment.NewLine);
        }

        public void ClearLogs()
        {
            if (this.IsDisposed)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                _ = this.BeginInvoke(new Action(this.ClearLogs));
                return;
            }

            this._logTextBox.Clear();
        }

        public void SaveLogsAsTxt()
        {
            if (this.IsDisposed)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                this.Invoke(() =>
                {
                    this.SaveLogsAsTxt();
                });
            }

            string content = this._logTextBox.Text;
            string logsDirectory = WidgetStatics.GetRepositoryDirectory();
            _ = Directory.CreateDirectory(logsDirectory);

            SaveFileDialog sfd = new()
            {
                InitialDirectory = logsDirectory,
                FileName = $"FSSWidget_Logs_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                RestoreDirectory = true
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, content);
                    PruneRepositoryLogFiles(logsDirectory, sfd.FileName);
                    _ = MessageBox.Show($"Logs saved successfully at \n{sfd.FileName}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show($"Error saving logs to \n{sfd.FileName}: \n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

            }
        }


        internal void ApplyOpacity(float opacity)
        {
            // Wende die Opacity auf alle direkt enthaltenen Controls an
            int initialStyle = WindowWidget.GetWindowLong(this.Handle, WindowWidget.GWL_EXSTYLE);
            _ = WindowWidget.SetWindowLong(this.Handle, WindowWidget.GWL_EXSTYLE, initialStyle | WindowWidget.WS_EX_LAYERED);
            _ = WindowWidget.SetLayeredWindowAttributes(this.Handle, 0, (byte) (opacity * 255), 0x00000002);
        }

        internal void ApplyBlackOutMode(bool enabled)
        {
            this.BackColor = enabled ? System.Drawing.Color.Black : System.Drawing.SystemColors.Control;
            this._logTextBox.BackColor = enabled ? System.Drawing.Color.Black : System.Drawing.SystemColors.Window;
            this._logTextBox.ForeColor = enabled ? System.Drawing.Color.White : System.Drawing.SystemColors.WindowText;

            // Window Handle + Text
            int initialStyle = WindowWidget.GetWindowLong(this.Handle, WindowWidget.GWL_EXSTYLE);
            if (enabled)
            {
                _ = WindowWidget.SetWindowLong(this.Handle, WindowWidget.GWL_EXSTYLE, initialStyle | WindowWidget.WS_EX_LAYERED);

                // Get custom opacity as byte value (0-255) and apply with LWA_ALPHA
                byte opacity = (byte) (WindowWidget.CustomOpacity >= 0.1f && WindowWidget.CustomOpacity <= 1.0f ? WindowWidget.CustomOpacity * 255 : 255);
                _ = WindowWidget.SetLayeredWindowAttributes(this.Handle, 0, opacity, 0x00000002);
            }
            else
            {
                _ = WindowWidget.SetWindowLong(this.Handle, WindowWidget.GWL_EXSTYLE, initialStyle & ~WindowWidget.WS_EX_LAYERED);
            }
        }



        public static void PruneRepositoryLogFiles(string logsDirectory, string preserveFilePath)
        {
            string fullPreserveFilePath = Path.GetFullPath(preserveFilePath);
            FileInfo[] logFiles = new DirectoryInfo(logsDirectory)
                .EnumerateFiles("*.txt", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (FileInfo logFile in logFiles.Skip(MaxRepositoryLogFiles))
            {
                if (string.Equals(logFile.FullName, fullPreserveFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                logFile.Delete();
            }
        }
    }
}
