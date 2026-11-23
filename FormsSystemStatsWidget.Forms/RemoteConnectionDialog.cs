using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FormsSystemStatsWidget.Core;
using Timer = System.Windows.Forms.Timer;

namespace FormsSystemStatsWidget.Forms
{
    /// <summary>
    /// Dialog für Remote-Verbindungen: Host-Modus (eigene WAN-IP + Port anzeigen)
    /// und Client-Modus (zu einem Host verbinden). Kein TLS / Firewall-Änderungen.
    /// </summary>
    public sealed class RemoteConnectionDialog : Form
    {
        private static readonly HttpClient WanIpHttpClient = new() { Timeout = TimeSpan.FromSeconds(3) };
        private readonly TextBox _clientIpTextBox;
        private readonly NumericUpDown _clientPortNumeric;
        private readonly Button _connectButton;
        private readonly Button _hostButton;
        private readonly Label _hostInfoLabel;
        private readonly Label _statusLabel;
        private readonly Panel _borderPanel;
        private readonly Timer _refreshTimer;
        private readonly int _hostPort;
        private readonly Func<bool> _isHosting;
        private readonly Action _startHosting;
        private readonly Action _stopHosting;
        private bool _refreshInProgress;

        /// <summary>Die erzeugte Remote-Control-Window (Client-Modus), null wenn nicht verbunden.</summary>
        public RemoteControlWindow? ConnectedWindow { get; private set; }

        public RemoteConnectionDialog(int defaultPort, Func<bool> isHosting, Action startHosting, Action stopHosting)
        {
            this._hostPort = defaultPort;
            this._isHosting = isHosting;
            this._startHosting = startHosting;
            this._stopHosting = stopHosting;
            this.Text = "🌐 Remote Connect";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new System.Drawing.Size(460, 380);

            // Grüner Rahmen (4px) wie bei RemoteControlWindow
            this._borderPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.LimeGreen,
                Padding = new Padding(4)
            };

            var innerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SystemColors.Control
            };
            this._borderPanel.Controls.Add(innerPanel);
            this.Controls.Add(this._borderPanel);

            // ----- Host-Modus (oben) -----
            var hostGroup = new GroupBox
            {
                Text = "Host mode (receive connections)",
                Location = new System.Drawing.Point(10, 8),
                Size = new System.Drawing.Size(436, 150),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            this._hostInfoLabel = new Label
            {
                AutoSize = false,
                Location = new System.Drawing.Point(12, 24),
                Size = new System.Drawing.Size(300, 48),
                Font = new Font(this.Font, FontStyle.Bold),
                Text = "Loading..."
            };

            this._hostButton = new Button
            {
                Location = new System.Drawing.Point(318, 31),
                Size = new System.Drawing.Size(100, 30),
                Text = isHosting() ? "Stop hosting" : "Start hosting"
            };
            this._hostButton.Click += this.HostButton_Click;

            var firewallInfoLabel = new Label
            {
                AutoSize = false,
                Location = new System.Drawing.Point(12, 76),
                Size = new System.Drawing.Size(410, 60),
                ForeColor = Color.DimGray,
                Text = "For WAN access, forward this TCP port on your router to this PC and allow it through Windows Firewall.\n" +
                       "Remote traffic is not encrypted (no TLS); hosting does not change firewall/router settings."
            };

            hostGroup.Controls.Add(this._hostInfoLabel);
            hostGroup.Controls.Add(this._hostButton);
            hostGroup.Controls.Add(firewallInfoLabel);
            innerPanel.Controls.Add(hostGroup);

            // ----- Client-Modus (unten) -----
            var clientGroup = new GroupBox
            {
                Text = "Client mode (connect to a host)",
                Location = new System.Drawing.Point(10, 168),
                Size = new System.Drawing.Size(436, 120),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var ipLabel = new Label
            {
                Text = "Host IP:",
                Location = new System.Drawing.Point(12, 30),
                AutoSize = true
            };

            this._clientIpTextBox = new TextBox
            {
                Location = new System.Drawing.Point(80, 27),
                Size = new System.Drawing.Size(150, 23)
            };

            var portLabel = new Label
            {
                Text = "Port:",
                Location = new System.Drawing.Point(245, 30),
                AutoSize = true
            };

            this._clientPortNumeric = new NumericUpDown
            {
                Location = new System.Drawing.Point(285, 27),
                Size = new System.Drawing.Size(70, 23),
                Minimum = 1,
                Maximum = 65535,
                Value = Math.Clamp(defaultPort, 1, 65535)
            };

            this._connectButton = new Button
            {
                Text = "Connect",
                Location = new System.Drawing.Point(365, 24),
                Size = new System.Drawing.Size(60, 28)
            };
            this._connectButton.Click += this.ConnectButton_Click;

            this._statusLabel = new Label
            {
                AutoSize = false,
                Location = new System.Drawing.Point(12, 74),
                Size = new System.Drawing.Size(410, 36),
                ForeColor = Color.DimGray,
                Text = "Enter the host's IP address and port, then click Connect."
            };

            clientGroup.Controls.Add(ipLabel);
            clientGroup.Controls.Add(this._clientIpTextBox);
            clientGroup.Controls.Add(portLabel);
            clientGroup.Controls.Add(this._clientPortNumeric);
            clientGroup.Controls.Add(this._connectButton);
            clientGroup.Controls.Add(this._statusLabel);
            innerPanel.Controls.Add(clientGroup);

            // Timer zum Aktualisieren der Host-Info (WAN-IP kann sich ändern)
            this._refreshTimer = new Timer { Interval = 5000 };
            this._refreshTimer.Tick += (_, _) => this.RefreshHostInfo();
            this._refreshTimer.Start();

            this.Load += (_, _) => this.RefreshHostInfo();
        }

        private void HostButton_Click(object? sender, EventArgs e)
        {
            if (this._isHosting())
            {
                this._stopHosting();
            }
            else
            {
                this._startHosting();
            }

            this._hostButton.Text = this._isHosting() ? "Stop hosting" : "Start hosting";
            this.RefreshHostInfo();
        }

        /// <summary>Aktualisiert die angezeigte WAN-IP und den Port.</summary>
        private async void RefreshHostInfo()
        {
            if (this.IsDisposed || this._refreshInProgress)
            {
                return;
            }

            this._refreshInProgress = true;
            try
            {
                string wanIp = await this.GetWanIpAddressAsync();
                if (!this.IsDisposed)
                {
                    this._hostInfoLabel.Text = $"Your WAN IP:   {wanIp}\n{(this._isHosting() ? "Listening" : "Not listening on")} port:   {this._hostPort}";
                }
            }
            finally
            {
                this._refreshInProgress = false;
            }
        }

        /// <summary>Bestimmt die eigene WAN-IP (via DNS-Dienst), Fallback auf LAN-IP.</summary>
        private async Task<string> GetWanIpAddressAsync()
        {
            try
            {
                string ip = (await WanIpHttpClient.GetStringAsync("https://checkip.amazonaws.com")).Trim();
                if (!string.IsNullOrWhiteSpace(ip))
                {
                    return ip;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[Remote] WAN IP lookup failed: {ex.Message}; using the local address if available.");
            }

            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[Remote] Local IP lookup failed: {ex.Message}");
            }

            return "unavailable";
        }

        /// <summary>Client-Modus: verbindet mit dem angegebenen Host und öffnet die RemoteControlWindow.</summary>
        private async void ConnectButton_Click(object? sender, EventArgs e)
        {
            this._connectButton.Enabled = false;
            this._statusLabel.Text = "Connecting...";

            string ipText = this._clientIpTextBox.Text.Trim();
            int port = (int)this._clientPortNumeric.Value;

            if (!IPAddress.TryParse(ipText, out _))
            {
                this._statusLabel.ForeColor = Color.Red;
                this._statusLabel.Text = $"Invalid IP address: '{ipText}'";
                Logger.Log($"[Remote] Client connect failed - invalid IP: {ipText}");
                this._connectButton.Enabled = true;
                return;
            }

            try
            {
                TcpClient? client = null;
                RemoteControlWindow? window = null;

                await Task.Run(() =>
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                    try
                    {
                        var tcp = new TcpClient();
                        tcp.ConnectAsync(IPAddress.Parse(ipText), port, cts.Token).GetAwaiter().GetResult();
                        client = tcp;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[Remote] TCP connect to {ipText}:{port} failed: {ex.Message}");
                    }
                });

                if (client == null || !client.Connected)
                {
                    this._statusLabel.ForeColor = Color.Red;
                    this._statusLabel.Text = $"Could not connect to {ipText}:{port}";
                    Logger.Log($"[Remote] Client connect failed: {ipText}:{port}");
                    this._connectButton.Enabled = true;
                    return;
                }

                // Handshake: "hello" mit Computername senden
                NetworkStream stream = client.GetStream();
                RemoteMessage hello = new() { Type = "hello", ComputerName = Environment.MachineName };
                byte[] helloBytes = Encoding.UTF8.GetBytes(RemoteMessage.Serialize(hello) + "\n");
                await stream.WriteAsync(helloBytes.AsMemory(), CancellationToken.None);

                // Welcome vom Host erwarten (10s Timeout)
                using var welcomeTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                string welcomeJson = await this.ReadWelcomeLineAsync(stream, welcomeTimeout.Token);
                RemoteMessage? welcome = RemoteMessage.Deserialize(welcomeJson);
                if (welcome == null || welcome.Type != "welcome")
                {
                    Logger.Log($"[Remote] No welcome from host after connect to {ipText}:{port}");
                    try { client.Close(); } catch { }
                    this._statusLabel.ForeColor = Color.Red;
                    this._statusLabel.Text = $"Host did not accept the connection ({ipText}:{port})";
                    this._connectButton.Enabled = true;
                    return;
                }

                // RemoteControlWindow erzeugen und an den Client streamen
                window = new RemoteControlWindow(ipText, port, client);
                this.ConnectedWindow = window;
                this.DialogResult = DialogResult.OK;
                this.Close();

                // Stats-Pump starten (läuft im Hintergrund der Control-Window)
                _ = Task.Run(window.StartStatsPumpAsync);
            }
            catch (Exception ex)
            {
                Logger.Log($"[Remote] Client connect error: {ex.Message}");
                this._statusLabel.ForeColor = Color.Red;
                this._statusLabel.Text = $"Connect error: {ex.Message}";
                this._connectButton.Enabled = true;
            }
        }

        /// <summary>Liest die "welcome"-Zeile vom Host-Stream.</summary>
        private async Task<string> ReadWelcomeLineAsync(NetworkStream stream, CancellationToken token)
        {
            var buffer = new StringBuilder();
            byte[] singleByte = new byte[1];

            while (!token.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(singleByte.AsMemory(0, 1), token);
                if (read == 0)
                {
                    throw new System.IO.IOException("Connection closed by host");
                }

                char ch = (char)singleByte[0];
                if (ch == '\n')
                {
                    break;
                }

                buffer.Append(ch);
                if (buffer.Length > 1024 * 1024)
                {
                    throw new System.IO.IOException("Remote handshake response is too large.");
                }
            }

            return buffer.ToString();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this._refreshTimer.Stop();
            }

            base.Dispose(disposing);
        }
    }
}
