using System;
using System.Windows.Forms;

namespace FormsSystemStatsWidget.Forms
{
    internal sealed class DebugConsoleForm : Form
    {
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

            // SFD at RepoDirectory\.Forms proj\Ressources\Logs\ for .TXT files, initial file name = FSSWidget_Logs_<TimeStamp>
            SaveFileDialog sfd = new()
            {
                InitialDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ressources", "Logs"),
                FileName = $"FSSWidget_Logs_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                RestoreDirectory = true
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    System.IO.File.WriteAllText(sfd.FileName, content);
                    MessageBox.Show($"Logs saved successfully at \n{sfd.FileName}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving logs to \n{sfd.FileName}: \n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

            }
        }
    }
}
