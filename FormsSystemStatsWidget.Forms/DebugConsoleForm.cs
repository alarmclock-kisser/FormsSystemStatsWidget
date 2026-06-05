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

        public DebugConsoleForm()
        {
            this.Text = "Debug Console";
            this.StartPosition = FormStartPosition.Manual;
            this.Width = 900;
            this.Height = 520;

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

            this.Controls.Add(this._logTextBox);
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
                this.BeginInvoke(new Action<string>(this.AppendLogLine), text);
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
                this.BeginInvoke(new Action(this.ClearLogs));
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
            Directory.CreateDirectory(logsDirectory);

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
                    MessageBox.Show($"Logs saved successfully at \n{sfd.FileName}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving logs to \n{sfd.FileName}: \n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

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
