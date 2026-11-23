using System;
using System.Drawing;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using FormsSystemStatsWidget.Core;

namespace FormsSystemStatsWidget.Forms
{
    /// <summary>
    /// Remote-Control-Window (Client-Seite): spiegelt die Stats des Host-Widgets wider
    /// (thick green border) and forwards supported actions to the host.
    /// </summary>
    public sealed class RemoteControlWindow : Form
    {
        private const int BorderThickness = 4;

        private readonly TcpClient _client;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly string _hostIp;
        private readonly int _hostPort;
        private readonly Label _titleLabel;
        private readonly Label _cpuAvgLabel;
        private readonly DynamicGradientProgressBar _ramBar;
        private readonly Label _ramLabel;
        private readonly DynamicGradientProgressBar _gpuBar;
        private readonly Label _gpuLabel;
        private readonly DynamicGradientProgressBar _vramBar;
        private readonly Label _vramLabel;
        private readonly Label _topTasksLabel;
        private readonly Panel _contentPanel;
        private readonly ContextMenuStrip _remoteMenu;
        private readonly ToolStripMenuItem _alwaysOnTopItem;
        private readonly ToolStripMenuItem _blackOutItem;
        private Point? _dragStartLocation;

        public RemoteControlWindow(string hostIp, int hostPort, TcpClient client)
        {
            this._client = client;
            this._hostIp = hostIp;
            this._hostPort = hostPort;

            this.Text = $"FSSWidget Remote ({hostIp}:{hostPort})";
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ClientSize = new Size(280, 300);
            this.DoubleBuffered = true;

            // Four-pixel green frame around the remote view.
            var borderPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.LimeGreen,
                Padding = new Padding(BorderThickness)
            };
            var innerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SystemColors.Control
            };
            this._contentPanel = innerPanel;
            borderPanel.Controls.Add(innerPanel);
            this.Controls.Add(borderPanel);

            // Titel (Traffic)
            this._titleLabel = new Label
            {
                AutoSize = false,
                Location = new Point(8, 8),
                Size = new Size(250, 20),
                Font = new Font("Bahnschrift Condensed", 10F, FontStyle.Bold),
                Text = "Up: --  Down: --"
            };

            // CPU-Durchschnitt
            this._cpuAvgLabel = new Label
            {
                AutoSize = false,
                Location = new Point(8, 34),
                Size = new Size(250, 18),
                Font = new Font("Bahnschrift Condensed", 9.75F),
                Text = "CPU avg: -"
            };

            // RAM
            this._ramBar = new DynamicGradientProgressBar { Location = new Point(8, 56), Size = new Size(250, 14) };
            this._ramLabel = new Label
            {
                AutoSize = false,
                Location = new Point(8, 72),
                Size = new Size(250, 16),
                Font = new Font("Bahnschrift Condensed", 9.75F),
                Text = "RAM: -"
            };

            // GPU
            this._gpuBar = new DynamicGradientProgressBar { Location = new Point(8, 94), Size = new Size(250, 14) };
            this._gpuLabel = new Label
            {
                AutoSize = false,
                Location = new Point(8, 110),
                Size = new Size(250, 16),
                Font = new Font("Bahnschrift Condensed", 9.75F),
                Text = "GPU: -"
            };

            // VRAM
            this._vramBar = new DynamicGradientProgressBar { Location = new Point(8, 132), Size = new Size(250, 14) };
            this._vramLabel = new Label
            {
                AutoSize = false,
                Location = new Point(8, 148),
                Size = new Size(250, 16),
                Font = new Font("Bahnschrift Condensed", 9.75F),
                Text = "VRAM: -"
            };

            // Top Tasks
            this._topTasksLabel = new Label
            {
                AutoSize = false,
                Location = new Point(8, 172),
                Size = new Size(250, 60),
                Font = new Font("Bahnschrift Condensed", 9.75F),
                Text = "  -% idle\r\n  -% idle\r\n  -% idle"
            };

            innerPanel.Controls.Add(this._titleLabel);
            innerPanel.Controls.Add(this._cpuAvgLabel);
            innerPanel.Controls.Add(this._ramBar);
            innerPanel.Controls.Add(this._ramLabel);
            innerPanel.Controls.Add(this._gpuBar);
            innerPanel.Controls.Add(this._gpuLabel);
            innerPanel.Controls.Add(this._vramBar);
            innerPanel.Controls.Add(this._vramLabel);
            innerPanel.Controls.Add(this._topTasksLabel);

            // Remote-Contextmenu (rechter Klick) - leitet Aktionen an den Host weiter
            this._remoteMenu = new ContextMenuStrip();
            this._alwaysOnTopItem = new ToolStripMenuItem("Always on Top") { CheckOnClick = true };
            this._alwaysOnTopItem.CheckedChanged += (_, _) => this.SendActionToHost("alwaysOnTop", this._alwaysOnTopItem.Checked ? "true" : "false");
            this._remoteMenu.Items.Add(this._alwaysOnTopItem);
            this._remoteMenu.Items.Add(new ToolStripSeparator());
            this._blackOutItem = new ToolStripMenuItem("Black Out Mode") { CheckOnClick = true };
            this._blackOutItem.CheckedChanged += (_, _) => this.SendActionToHost("blackOutMode", this._blackOutItem.Checked ? "true" : "false");
            this._remoteMenu.Items.Add(this._blackOutItem);
            this._remoteMenu.Items.Add(new ToolStripSeparator());
            this._remoteMenu.Items.Add(new ToolStripMenuItem("Open Debug Console (on host)", null, (_, _) => this.SendActionToHost("openDebugConsole")));
            this._remoteMenu.Items.Add(new ToolStripSeparator());
            this._remoteMenu.Items.Add(new ToolStripMenuItem("Close widget on host", null, (_, _) => this.SendActionToHost("closeWidget")));

            // Left-drag moves the local mirror and sends the same delta to the host.
            this.MouseDown += this.HandleRemoteMouseDown;
            this.MouseMove += this.HandleRemoteMouseMove;
            this.MouseUp += this.HandleRemoteMouseUp;
            this.AttachRemoteMouseHandlers(innerPanel);
        }

        private void AttachRemoteMouseHandlers(Control control)
        {
            control.MouseDown += this.HandleRemoteMouseDown;
            control.MouseMove += this.HandleRemoteMouseMove;
            control.MouseUp += this.HandleRemoteMouseUp;
            foreach (Control child in control.Controls)
            {
                this.AttachRemoteMouseHandlers(child);
            }
        }

        private void HandleRemoteMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this._dragStartLocation = Cursor.Position;
                this.Capture = true;
            }
            else if (e.Button == MouseButtons.Right)
            {
                this._remoteMenu.Show(Cursor.Position);
            }
        }

        private void HandleRemoteMouseMove(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || this._dragStartLocation == null)
            {
                return;
            }

            Point current = Cursor.Position;
            int dx = current.X - this._dragStartLocation.Value.X;
            int dy = current.Y - this._dragStartLocation.Value.Y;

            if (dx != 0 || dy != 0)
            {
                this.Location = new Point(this.Location.X + dx, this.Location.Y + dy);
                this._dragStartLocation = current;
                this.SendMoveToHost(new Point(dx, dy));
            }
        }

        private void HandleRemoteMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.Capture = false;
                this._dragStartLocation = null;
            }
        }

        /// <summary>Sendet eine Aktion an den Host.</summary>
        private void SendActionToHost(string action, string? value = null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (!this._client.Connected)
                    {
                        return;
                    }

                    await this._sendLock.WaitAsync();
                    try
                    {
                        NetworkStream stream = this._client.GetStream();
                        RemoteMessage message = new() { Type = "click", Action = action, Value = value };
                        byte[] bytes = Encoding.UTF8.GetBytes(RemoteMessage.Serialize(message) + "\n");
                        await stream.WriteAsync(bytes.AsMemory());
                    }
                    finally
                    {
                        this._sendLock.Release();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"[Remote] Sending action '{action}' failed: {ex.Message}");
                }
            });
        }

        private void SendMoveToHost(Point location)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await this._sendLock.WaitAsync();
                    try
                    {
                        RemoteMessage message = new() { Type = "move", X = location.X, Y = location.Y };
                        byte[] bytes = Encoding.UTF8.GetBytes(RemoteMessage.Serialize(message) + "\n");
                        await this._client.GetStream().WriteAsync(bytes.AsMemory());
                    }
                    finally
                    {
                        this._sendLock.Release();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"[Remote] Sending move failed: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Startet die Stats-Pump: liest "stats"-Nachrichten vom Host und aktualisiert die Labels.
        /// Runs in the background until the connection is closed.
        /// </summary>
        public async Task StartStatsPumpAsync()
        {
            var buffer = new StringBuilder();
            byte[] singleByte = new byte[1];

            try
            {
                NetworkStream stream = this._client.GetStream();

                while (!this.IsDisposed && this._client.Connected)
                {
                    int read = await stream.ReadAsync(singleByte.AsMemory(0, 1), CancellationToken.None);
                    if (read == 0)
                    {
                        break; // Verbindung getrennt
                    }

                    char ch = (char)singleByte[0];
                    if (ch == '\n')
                    {
                        string json = buffer.ToString();
                        buffer.Clear();

                        RemoteMessage? message = RemoteMessage.Deserialize(json);
                        if (message?.Type == "stats" && message.Data != null)
                        {
                            this.ApplyStats(message.Data);
                        }
                        else if (message?.Type == "bye")
                        {
                            break;
                        }
                        else if (message == null)
                        {
                            Logger.Log("[Remote] Invalid server message received.");
                        }
                    }
                    else
                    {
                        buffer.Append(ch);
                        if (buffer.Length > 1024 * 1024)
                        {
                            throw new IOException("Remote server message is too large.");
                        }
                    }
                }

                // Close the mirror when the host disconnects.
                this.SafeClose();
            }
            catch (Exception ex)
            {
                Logger.Log($"[Remote] Stats connection ended: {ex.Message}");
                this.SafeClose();
            }
        }

        private void ApplyStats(RemoteStatsData data)
        {
            if (this.IsDisposed || this.Disposing)
            {
                return;
            }

            Action update = () =>
            {
                this._titleLabel.Text = string.IsNullOrEmpty(data.TrafficTitle) ? "Remote" : data.TrafficTitle;
                this._alwaysOnTopItem.Checked = data.AlwaysOnTop;
                this._blackOutItem.Checked = data.BlackOutMode;
                this._contentPanel.BackColor = data.BlackOutMode ? Color.Black : SystemColors.Control;
                Color textColor = data.BlackOutMode ? Color.White : Color.Black;
                this._titleLabel.ForeColor = textColor;
                this._ramLabel.ForeColor = textColor;
                this._vramLabel.ForeColor = textColor;
                this._topTasksLabel.ForeColor = textColor;

                string cpuText = $"CPU avg: {data.CpuAvgPercent:0.000}%";
                this._cpuAvgLabel.Text = cpuText;
                this._cpuAvgLabel.ForeColor = data.CpuAvgPercent >= 80 ? Color.Red : textColor;

                double ramPercent = data.RamTotalGb > 0 ? (data.RamUsedGb / data.RamTotalGb) * 100 : 0;
                this._ramLabel.Text = $"RAM: {data.RamUsedGb} GB / {data.RamTotalGb} GB ({ramPercent:0.00}%)";
                this._ramBar.Value = Math.Clamp((int)ramPercent, 0, 100);

                double gpuPercent = data.GpuPercent;
                this._gpuLabel.Text = $"GPU: {gpuPercent:0.00}%   Watts: {data.GpuWatts:0.00} W";
                this._gpuLabel.ForeColor = gpuPercent >= 80 ? Color.Red : textColor;
                this._gpuBar.Value = Math.Clamp((int)gpuPercent, 0, 100);

                double vramPercent = data.VramTotalGb > 0 ? (data.VramUsedGb / data.VramTotalGb) * 100 : 0;
                this._vramLabel.Text = $"VRAM: {data.VramUsedGb} GB / {data.VramTotalGb} GB ({vramPercent:0.00}%)";
                this._vramBar.Value = Math.Clamp((int)vramPercent, 0, 100);

                // Top Tasks formatieren
                if (data.TopTasks == null || data.TopTasks.Count == 0)
                {
                    this._topTasksLabel.Text = "   -% idle\r\n   -% idle\r\n   -% idle";
                }
                else
                {
                    string[] lines = new string[3];
                    for (int i = 0; i < 3; i++)
                    {
                        if (i < data.TopTasks.Count)
                        {
                            var task = data.TopTasks[i];
                            string name = WidgetStatics.Ellipsize(task.Name, 24);
                            lines[i] = $"{task.CpuPercent:0}% {name}";
                        }
                        else
                        {
                            lines[i] = "  0% -";
                        }
                    }
                    this._topTasksLabel.Text = string.Join(Environment.NewLine, lines);
                }
            };

            if (this.InvokeRequired)
            {
                try
                {
                    _ = this.BeginInvoke(update);
                }
                catch (InvalidOperationException ex)
                {
                    Logger.Log($"[Remote] Could not dispatch a stats update to the UI: {ex.Message}");
                }
            }
            else
            {
                update();
            }
        }

        private void SafeClose()
        {
            if (this.IsDisposed || this.Disposing)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                try { _ = this.BeginInvoke(new Action(this.SafeClose)); } catch { }
                return;
            }

            this._client.Close();

            this.Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                if (this._client.Connected)
                {
                    // "bye" an den Host senden, damit er die Verbindung sauber trennt
                    NetworkStream stream = this._client.GetStream();
                    byte[] bytes = Encoding.UTF8.GetBytes(RemoteMessage.Serialize(new RemoteMessage { Type = "bye" }) + "\n");
                    stream.Write(bytes, 0, bytes.Length);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[Remote] Disconnect notification failed: {ex.Message}");
            }

            base.OnFormClosing(e);
        }
    }
}
