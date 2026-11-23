using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FormsSystemStatsWidget.Core;

namespace FormsSystemStatsWidget.Forms
{
    public partial class WindowWidget
    {
        private const int RemoteDefaultPort = 6553;
        private const int RemoteMaxMessageBytes = 1024 * 1024; // 1 MB max. Nachrichtengröße

        private TcpListener? _remoteListener;
        private CancellationTokenSource? _remoteHostingCts;
        private TcpClient? _remoteClient;
        private NetworkStream? _remoteClientStream;
        private readonly object _remoteLock = new();
        private readonly ToolStripMenuItem _remoteOpenDialogMenuItem = new("Open Remote Connection Dialog...");

        private void InitializeRemoteMenu()
        {
            this._remoteOpenDialogMenuItem.Click += this.remoteConnectToolStripMenuItem_Click;
            this.remoteConnectToolStripMenuItem.DropDownItems.Insert(0, new ToolStripSeparator());
            this.remoteConnectToolStripMenuItem.DropDownItems.Insert(0, this._remoteOpenDialogMenuItem);
        }

        /// <summary>True, wenn der Host gerade auf Remote-Verbindungen hört.</summary>
        internal bool IsRemoteHosting => this._remoteListener != null;

        // ==================== Context-Menu Event Handler ====================

        private void remoteConnectToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            using var dialog = new RemoteConnectionDialog(this.GetRemotePort(), () => this.IsRemoteHosting, this.StartRemoteHosting, this.StopRemoteHosting);
            if (dialog.ShowDialog(this) == DialogResult.OK && dialog.ConnectedWindow != null)
            {
                dialog.ConnectedWindow.Show();
            }
        }

        private void remoteAllowAllConnectionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.remoteAllowAllConnectionsToolStripMenuItem.Checked
                && MessageBox.Show(this,
                    "Allow all connections skips the approval prompt for anyone who can reach this port. Traffic is unencrypted and has no authentication. Continue?",
                    "Unattended remote access",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                this.remoteAllowAllConnectionsToolStripMenuItem.Checked = false;
                return;
            }

            this._persistentSettings.RemoteAllowAllConnections = this.remoteAllowAllConnectionsToolStripMenuItem.Checked;
            this.SavePersistentSettings();
            Logger.Log($"[Remote] Allow all connections: {(this._persistentSettings.RemoteAllowAllConnections ? "enabled" : "disabled")}");
        }

        private void remoteStartHostingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.remoteStartHostingToolStripMenuItem.Checked)
            {
                this.StartRemoteHosting();
            }
            else
            {
                this.StopRemoteHosting();
            }
        }

        // ==================== Hosting (Server-Seite) ====================

        /// <summary>Startet den Remote-Host-Listener auf dem konfigurierten Port.</summary>
        private void StartRemoteHosting()
        {
            if (MessageBox.Show(this,
                $"Hosting opens FSSWidget remote control on TCP port {this.GetRemotePort()}. WAN access requires router port forwarding and a Windows Firewall rule. This connection is not encrypted (no TLS). Start hosting?",
                "Start remote hosting",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                this.UpdateRemoteMenuState();
                return;
            }

            lock (this._remoteLock)
            {
                if (this.IsRemoteHosting)
                {
                    return;
                }

                int port = this.GetRemotePort();
                try
                {
                    this._remoteListener = new TcpListener(IPAddress.Any, port);
                    this._remoteListener.Start();
                    this._remoteHostingCts = new CancellationTokenSource();
                    CancellationToken token = this._remoteHostingCts.Token;

                    _ = Task.Run(() => this.RemoteAcceptLoopAsync(token), token);
                    Logger.Log($"[Remote] Hosting started on port {port}");
                }
                catch (SocketException ex)
                {
                    Logger.Log($"[Remote] Failed to start hosting on port {port}: {ex.Message}");
                    MessageBox.Show(this, $"Failed to start remote hosting on port {port}:\n{ex.Message}", "Remote Hosting", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this._remoteListener = null;
                }
            }

            this.UpdateRemoteMenuState();
        }

        /// <summary>Stoppt den Remote-Host-Listener und trennt eine aktive Client-Verbindung.</summary>
        private void StopRemoteHosting()
        {
            lock (this._remoteLock)
            {
                try { this._remoteHostingCts?.Cancel(); } catch { }
                this._remoteHostingCts = null;

                try { this._remoteListener?.Stop(); } catch { }
                this._remoteListener = null;

                this.DisconnectRemoteClient();
                Logger.Log("[Remote] Hosting stopped");
            }

            this.UpdateRemoteMenuState();
        }

        /// <summary>Accept-Loop: nimmt genau einen Client an, lehnt weitere ab.</summary>
        private async Task RemoteAcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient? client = null;
                try
                {
                    if (this._remoteListener == null)
                    {
                        return;
                    }

                    client = await this._remoteListener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException ex)
                {
                    Logger.Log($"[Remote] Accept failed: {ex.Message}");
                    if (!token.IsCancellationRequested)
                    {
                        this.InvokeOnUiThread(this.StopRemoteHosting);
                    }
                    return;
                }

                if (client == null)
                {
                    continue;
                }

                try
                {
                    string remoteEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                    bool allow = this._persistentSettings.RemoteAllowAllConnections;
                    string? computerName = await this.ReadRemoteHelloAsync(client).ConfigureAwait(false);
                    if (computerName == null)
                    {
                        Logger.Log($"[Remote] Invalid handshake from {remoteEndPoint}; connection rejected");
                        client.Close();
                        continue;
                    }

                    string endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                    remoteEndPoint = $"{computerName} with IP {endpoint}";

                    if (!allow)
                    {
                        bool allowed = this.InvokeOnUiThread<bool>(() =>
                        {
                            return MessageBox.Show(
                                this,
                                $"{remoteEndPoint} wants to connect. Allow connection?",
                                "Remote Connection Request",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question) == DialogResult.Yes;
                        }, false);

                        if (!allowed)
                        {
                            Logger.Log($"[Remote] Connection from {remoteEndPoint} denied by user");
                            try { client.Close(); } catch { }
                            continue;
                        }
                    }

                    lock (this._remoteLock)
                    {
                        if (this._remoteClient != null)
                        {
                            Logger.Log($"[Remote] Second connection from {client.Client.RemoteEndPoint} rejected - one client already connected");
                            try { client.Close(); } catch { }
                            continue;
                        }

                        this._remoteClient = client;
                        this._remoteClientStream = client.GetStream();
                    }

                    Logger.Log($"[Remote] Client connected: {client.Client.RemoteEndPoint}");
                    _ = Task.Run(() => this.RemoteClientSessionAsync(client, token), token);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[Remote] Error handling new connection: {ex.Message}");
                    try { client.Close(); } catch { }
                }
            }
        }

        /// <summary>Liest die "hello"-Handshake-Nachricht eines neuen Clients (Client-Computername).</summary>
        private async Task<string?> ReadRemoteHelloAsync(TcpClient client)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                NetworkStream stream = client.GetStream();
                string json = await this.ReadRemoteLineAsync(stream, timeoutCts.Token).ConfigureAwait(false);
                RemoteMessage? message = RemoteMessage.Deserialize(json);
                return message?.Type == "hello" ? message.ComputerName : null;
            }
            catch (Exception ex)
            {
                Logger.Log($"[Remote] Hello handshake failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Client-Session: empfängt "click"/"move"-Befehle und pusht Stats an den Client.
        /// Läuft, bis die Verbindung getrennt wird oder der Hosting-Token abgebrochen wird.
        /// </summary>
        private async Task RemoteClientSessionAsync(TcpClient client, CancellationToken token)
        {
            try
            {
                NetworkStream stream = client.GetStream();

                // Welcome senden
                await this.SendRemoteMessageAsync(stream, new RemoteMessage { Type = "welcome" }, token).ConfigureAwait(false);

                var readTask = Task.Run(async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        string json = await this.ReadRemoteLineAsync(stream, token).ConfigureAwait(false);
                        if (string.IsNullOrWhiteSpace(json))
                        {
                            continue;
                        }

                        RemoteMessage? message = RemoteMessage.Deserialize(json);
                        if (message == null)
                        {
                            Logger.Log("[Remote] Invalid message received from client");
                            continue;
                        }

                        if (message.Type == "bye")
                        {
                            break;
                        }

                        this.HandleRemoteClientCommand(message);
                    }
                }, token);

                // Stats-Push-Loop: pushen im Update-Interval, bis Client weg ist
                while (!token.IsCancellationRequested && !readTask.IsFaulted)
                {
                    await Task.Delay(this._updateIntervalMs, token).ConfigureAwait(false);
                    try
                    {
                        RemoteStatsData stats = this.BuildRemoteStatsSnapshot();
                        NetworkStream? activeStream;
                        lock (this._remoteLock)
                        {
                            activeStream = this._remoteClientStream;
                        }

                        if (activeStream != null && !readTask.IsFaulted)
                        {
                            await this.SendRemoteMessageAsync(activeStream, new RemoteMessage { Type = "stats", Data = stats }, token).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[Remote] Stats push failed: {ex.Message}");
                        break;
                    }

                    if (readTask.IsFaulted || readTask.IsCompleted)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // normaler Abbruch
            }
            catch (ObjectDisposedException)
            {
                // Verbindung getrennt
            }
            catch (Exception ex)
            {
                Logger.Log($"[Remote] Client session error: {ex.Message}");
            }
            finally
            {
                lock (this._remoteLock)
                {
                    if (ReferenceEquals(this._remoteClient, client))
                    {
                        this.DisconnectRemoteClient();
                    }
                }

                try { client.Close(); } catch { }
                Logger.Log("[Remote] Client disconnected");
            }
        }

        /// <summary>Trennt die aktive Client-Verbindung und räumt auf.</summary>
        private void DisconnectRemoteClient()
        {
            try { this._remoteClientStream?.Dispose(); } catch { }
            this._remoteClientStream = null;
            try { this._remoteClient?.Close(); } catch { }
            this._remoteClient = null;
        }

        // ==================== Client-Befehle (ausführen) ====================

        private void HandleRemoteClientCommand(RemoteMessage message)
        {
            switch (message.Type)
            {
                case "move":
                    if (message.X.HasValue && message.Y.HasValue)
                    {
                        this.InvokeOnUiThread(() =>
                        {
                            try
                            {
                                this.Location = new System.Drawing.Point(this.Location.X + message.X.Value, this.Location.Y + message.Y.Value);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"[Remote] Move failed: {ex.Message}");
                            }
                        });
                    }
                    break;

                case "click":
                    if (!string.IsNullOrWhiteSpace(message.Action))
                    {
                        this.ExecuteRemoteAction(message.Action, message.Value ?? string.Empty);
                    }
                    break;

                default:
                    Logger.Log($"[Remote] Unknown message type from client: {message.Type}");
                    break;
            }
        }

        /// <summary>Führt eine Remote-Aktion auf dem Host-Widget aus (auf dem UI-Thread).</summary>
        private void ExecuteRemoteAction(string action, string value)
        {
            this.InvokeOnUiThread(() =>
            {
                try
                {
                    switch (action)
                    {
                        case "alwaysOnTop":
                            bool topMost = value == "true";
                            this.alwaysOnTopToolStripMenuItem.Checked = topMost;
                            this.alwaysOnTopToolStripMenuItem_CheckedChanged(this.alwaysOnTopToolStripMenuItem, EventArgs.Empty);
                            break;

                        case "toggleAlwaysOnTop":
                            this.alwaysOnTopToolStripMenuItem.Checked = !this.alwaysOnTopToolStripMenuItem.Checked;
                            this.alwaysOnTopToolStripMenuItem_CheckedChanged(this.alwaysOnTopToolStripMenuItem, EventArgs.Empty);
                            break;

                        case "blackOutMode":
                            this.toolStripMenuItem_blackOutMode.Checked = value.Length == 0
                                ? !this.toolStripMenuItem_blackOutMode.Checked
                                : value == "true";
                            this.toolStripMenuItem_blackOutMode_CheckedChanged(this.toolStripMenuItem_blackOutMode, EventArgs.Empty);
                            break;

                        case "openDebugConsole":
                            this.openDebugConsoleToolStripMenuItem_Click(this.openDebugConsoleToolStripMenuItem, EventArgs.Empty);
                            break;

                        case "updateInterval":
                            if (int.TryParse(value, out int interval) && interval >= 50)
                            {
                                this.toolStripTextBox_interval.Text = interval.ToString();
                                this.ApplyIntervalFromText();
                            }
                            break;

                        case "diagramColor":
                            if (!string.IsNullOrEmpty(value))
                            {
                                this.toolStripTextBox_diagramColor.Text = value;
                                this.toolStripTextBox_diagramColor_TextChanged(this.toolStripTextBox_diagramColor, EventArgs.Empty);
                            }
                            break;

                        case "showUsage":
                            bool showUsage = value == "true";
                            this.showUsageToolStripMenuItem.Checked = showUsage;
                            this.toolStripTextBox_percentageColor.Enabled = showUsage;
                            this._persistentSettings.ShowPerCorePercent = showUsage;
                            this.SavePersistentSettings();
                            break;

                        case "close":
                        case "closeWidget":
                            // Schließt das Host-Widget (nutzt den expliziten Close-Pfad)
                            this._explicitWidgetCloseRequested = true;
                            this.Close();
                            break;

                        default:
                            Logger.Log($"[Remote] Unknown action from client: {action}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"[Remote] Action '{action}' failed: {ex.Message}");
                }
            });
        }

        // ==================== Stats-Snapshot-Builder ====================

        /// <summary>Erstellt einen Stats-Snapshot des Host-Widgets für den Remote-Client.</summary>
        internal RemoteStatsData BuildRemoteStatsSnapshot()
        {
            var stats = new RemoteStatsData();

            float[] coreUsages = [];
            try
            {
                coreUsages = CpuStats.GetThreadUsages();
            }
            catch (Exception ex)
            {
                Logger.Log($"[Remote] CPU usage snapshot failed: {ex.Message}");
            }

            stats.CoreUsages = coreUsages;
            stats.CpuAvgPercent = coreUsages.Length > 0 ? coreUsages.Average() * 100d : 0d;

            try
            {
                stats.RamTotalGb = Math.Round(CpuStats.GetTotalMemoryBytes() / 1_073_741_824.0, 3);
                stats.RamUsedGb = Math.Round(CpuStats.GetUsedMemoryBytes() / 1_073_741_824.0, 3);
                stats.PageFileUsedGb = CpuStats.GetUsedPageFileGb();
                stats.PageFileMaxGb = CpuStats.GetMaxPageFileGb();
            }
            catch (Exception ex)
            {
                Logger.Log($"[Remote] RAM snapshot failed: {ex.Message}");
            }

            try
            {
                var gpu = this.Gpu;
                stats.GpuPercent = gpu?.CurrentLoad01 * 100 ?? 0;
                stats.GpuWatts = gpu?.CurrentPowerWatts ?? 0;
                stats.VramTotalGb = Math.Round(gpu?.GetTotalVramBytes() / 1_073_741_824.0 ?? 0, 3);
                stats.VramUsedGb = Math.Round(gpu?.GetUsedVramBytes() / 1_073_741_824.0 ?? 0, 3);
            }
            catch (Exception ex)
            {
                Logger.Log($"[Remote] GPU snapshot failed: {ex.Message}");
            }

            if (this.Gpu2 != null)
            {
                stats.HasSecondGpu = true;
                try
                {
                    stats.Gpu2Percent = this.Gpu2.CurrentLoad01 * 100;
                    stats.Gpu2Watts = this.Gpu2.CurrentPowerWatts ?? 0;
                    stats.Vram2TotalGb = Math.Round(this.Gpu2.GetTotalVramBytes() / 1_073_741_824.0, 3);
                    stats.Vram2UsedGb = Math.Round(this.Gpu2.GetUsedVramBytes() / 1_073_741_824.0, 3);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[Remote] GPU2 snapshot failed: {ex.Message}");
                }
            }

            try
            {
                var topTasks = CpuStats.GetTopCpuProcesses(4);
                foreach (var (processName, cpuPercent) in topTasks)
                {
                    stats.TopTasks.Add(new RemoteTopTask { Name = processName, CpuPercent = cpuPercent });
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[Remote] Top tasks snapshot failed: {ex.Message}");
            }

            try
            {
                stats.TrafficTitle = this.Text ?? string.Empty;
                stats.AlwaysOnTop = this.alwaysOnTopToolStripMenuItem.Checked;
                stats.BlackOutMode = this.toolStripMenuItem_blackOutMode.Checked;
            }
            catch
            {
                stats.TrafficTitle = string.Empty;
            }

            return stats;
        }

        // ==================== Hilfs-Methoden ====================

        /// <summary>Liefert den konfigurierten Remote-Port (Standard 6553).</summary>
        internal int GetRemotePort()
        {
            int port = this._persistentSettings.RemotePort;
            return RemoteMessage.IsValidPort(port) ? port : RemoteDefaultPort;
        }

        /// <summary>Aktualisiert den Text/Checked-State des "Start hosting"-Menu-Items.</summary>
        private void UpdateRemoteMenuState()
        {
            if (this.IsDisposed || this.Disposing)
            {
                return;
            }

            if (this.InvokeRequired)
            {
                _ = this.BeginInvoke(new Action(this.UpdateRemoteMenuState));
                return;
            }

            bool hosting = this.IsRemoteHosting;
            this.remoteStartHostingToolStripMenuItem.Checked = hosting;
            this.remoteStartHostingToolStripMenuItem.Text = hosting ? "⏹ Stop hosting" : "▶ Start hosting";
        }

        /// <summary>Führt eine Aktion auf dem UI-Thread aus (synchron, mit Timeout).</summary>
        private void InvokeOnUiThread(Action action)
        {
            if (this.IsDisposed || this.Disposing)
            {
                return;
            }

            if (!this.InvokeRequired)
            {
                action();
                return;
            }

            try
            {
                this.Invoke(action);
            }
            catch (InvalidOperationException)
            {
            }
        }

        private T InvokeOnUiThread<T>(Func<T> callback, T fallback)
        {
            if (this.IsDisposed || this.Disposing)
            {
                return fallback;
            }

            if (!this.InvokeRequired)
            {
                return callback();
            }

            try
            {
                return (T)this.Invoke(callback);
            }
            catch (InvalidOperationException)
            {
                return fallback;
            }
        }

        /// <summary>Liest eine Zeile (bis '\n') aus einem NetworkStream.</summary>
        private async Task<string> ReadRemoteLineAsync(NetworkStream stream, CancellationToken token)
        {
            var buffer = new StringBuilder();
            byte[] singleByte = new byte[1];

            while (!token.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(singleByte.AsMemory(0, 1), token).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new IOException("Remote connection closed by client");
                }

                char ch = (char)singleByte[0];
                if (ch == '\n')
                {
                    break;
                }

                buffer.Append(ch);
                if (buffer.Length > RemoteMaxMessageBytes)
                {
                    throw new IOException("Remote message too large");
                }
            }

            return buffer.ToString();
        }

        /// <summary>Sendet eine Remote-Nachricht als JSON + '\n' an einen Stream.</summary>
        private async Task SendRemoteMessageAsync(NetworkStream stream, RemoteMessage message, CancellationToken token)
        {
            string json = RemoteMessage.Serialize(message);
            byte[] bytes = Encoding.UTF8.GetBytes(json + "\n");
            await stream.WriteAsync(bytes.AsMemory(), token).ConfigureAwait(false);
        }

        /// <summary>Stoppt das Remote-Hosting beim Schließen des Widgets.</summary>
        private void StopRemoteHostingOnFormClose()
        {
            this.StopRemoteHosting();
        }
    }
}
