using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FormsSystemStatsWidget.Core;

namespace FormsSystemStatsWidget.Forms
{
    /// <summary>
    /// Konfiguration für automatische WAN-IP-Uploads zu Google Drive oder OneDrive.
    /// Kontopasswörter werden nie abgefragt; die Anmeldung läuft über OAuth im Browser.
    /// </summary>
    internal sealed class CloudDriveConfigDialog : Form
    {
        private readonly ComboBox _providerCombo;
        private readonly Label _providerHintLabel;
        private readonly LinkLabel _consoleLink;
        private readonly TextBox _clientIdTextBox;
        private readonly Label _tenantLabel;
        private readonly TextBox _tenantTextBox;
        private readonly Label _clientSecretLabel;
        private readonly TextBox _clientSecretTextBox;
        private readonly TextBox _refreshTokenTextBox;
        private readonly CheckBox _showSecretsCheckBox;
        private readonly Button _signInButton;
        private readonly Button _copyLinkButton;
        private readonly Label _signInStatusLabel;
        private readonly Label _accountLabel;
        private readonly Button _testButton;
        private readonly Button _saveButton;
        private readonly Button _cancelButton;
        private readonly Label _resultLabel;

        private readonly CloudIpSyncOptions _options;
        private string? _authorizationUrl;
        private CancellationTokenSource? _signInCts;
        private CancellationTokenSource? _testCts;
        private bool _busy;
        private bool _suppressProviderChange;

        public CloudDriveConfigDialog(CloudIpSyncOptions options)
        {
            this._options = options?.Clone() ?? new CloudIpSyncOptions();

            this.Text = "IP-Updates via Cloud Drive";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(700, 760);
            this.AutoScaleMode = AutoScaleMode.Font;

            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Padding = new Padding(12, 10, 12, 10)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scrollPanel.Controls.Add(root);

            // Row 0 scrolls; row 1 keeps the result line and the buttons always visible.
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(scrollPanel, 0, 0);
            this.Controls.Add(layout);

            var header = new Label
            {
                Text = "☁ Automatic WAN IP updates",
                AutoSize = true,
                Font = new Font(this.Font.FontFamily, 12f, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 4)
            };

            var description = new Label
            {
                Text = "FSSWidget uploads a small plaintext file named <HostPCName>.ipdata to your cloud drive. "
                    + "It contains the current WAN IPv4 address (and IPv6 when available), the host name and a UTC timestamp. "
                    + "Uploads run at startup, whenever the WAN IP changes, and once per hour as a safety net.\n\n"
                    + "FSSWidget never asks for or stores your Google/Microsoft account password. "
                    + "Only OAuth tokens and the optional client secret are stored, encrypted with "
                    + "Windows DPAPI for your Windows user.",
                AutoSize = true,
                MaximumSize = new Size(620, 0),
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 0, 0, 8)
            };

            AddRootRow(root, header);
            AddRootRow(root, description);

            // ----- Provider -----
            var providerGroup = CreateGroup("Cloud provider");
            var providerTable = CreateFormTable();
            this._providerCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3)
            };
            this._providerCombo.Items.Add(new ProviderItem(CloudIpSyncProvider.GoogleDrive, "Google Drive"));
            this._providerCombo.Items.Add(new ProviderItem(CloudIpSyncProvider.OneDrive, "Microsoft OneDrive"));
            this._providerCombo.SelectedIndexChanged += this.ProviderCombo_SelectedIndexChanged;

            this._providerHintLabel = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(560, 0),
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 6, 0, 2)
            };

            this._consoleLink = new LinkLabel
            {
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 0)
            };
            this._consoleLink.LinkClicked += this.ConsoleLink_LinkClicked;

            AddRow(providerTable, MakeLabel("Provider:"), this._providerCombo);
            AddFullRow(providerTable, this._providerHintLabel);
            AddFullRow(providerTable, this._consoleLink);
            providerGroup.Controls.Add(providerTable);
            AddRootRow(root, providerGroup);

            // ----- OAuth client + browser sign-in -----
            var oauthGroup = CreateGroup("OAuth sign-in");
            var oauthTable = CreateFormTable();

            this._clientIdTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3)
            };

            this._tenantLabel = MakeLabel("Directory (tenant):");
            this._tenantTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                PlaceholderText = "common",
                Margin = new Padding(0, 3, 0, 3)
            };

            this._clientSecretLabel = MakeLabel("Client secret:");
            this._clientSecretTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                UseSystemPasswordChar = true,
                Margin = new Padding(0, 3, 0, 3)
            };

            this._refreshTokenTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                UseSystemPasswordChar = true,
                Margin = new Padding(0, 3, 0, 3)
            };

            this._showSecretsCheckBox = new CheckBox
            {
                Text = "Show secrets",
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 4)
            };
            this._showSecretsCheckBox.CheckedChanged += this.ShowSecretsCheckBox_CheckedChanged;

            this._signInButton = new Button
            {
                Text = "Sign in with browser...",
                AutoSize = true,
                MinimumSize = new Size(160, 30),
                Margin = new Padding(0, 4, 6, 4)
            };
            this._signInButton.Click += async (_, _) => await this.SignInAsync();

            this._copyLinkButton = new Button
            {
                Text = "Copy sign-in link",
                AutoSize = true,
                MinimumSize = new Size(120, 30),
                Enabled = false,
                Margin = new Padding(0, 4, 0, 4)
            };
            this._copyLinkButton.Click += this.CopyLinkButton_Click;

            var signInButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            signInButtons.Controls.Add(this._signInButton);
            signInButtons.Controls.Add(this._copyLinkButton);

            this._signInStatusLabel = new Label
            {
                Text = "Not signed in.",
                AutoSize = true,
                MaximumSize = new Size(560, 0),
                Margin = new Padding(0, 4, 0, 2)
            };

            this._accountLabel = new Label
            {
                Text = "Not connected.",
                AutoSize = true,
                MaximumSize = new Size(560, 0),
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 0, 0, 4)
            };

            var redirectHint = new Label
            {
                Text = "The browser sign-in uses a temporary http://localhost callback. "
                    + "Google: create a \"Desktop app\" OAuth client. "
                    + "Microsoft: add the \"Mobile and desktop applications\" platform with http://localhost.",
                AutoSize = true,
                MaximumSize = new Size(560, 0),
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 4, 0, 0)
            };

            var secretRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            secretRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            secretRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            secretRow.Controls.Add(this._clientSecretTextBox, 0, 0);
            secretRow.Controls.Add(this._showSecretsCheckBox, 1, 0);
            this._showSecretsCheckBox.Margin = new Padding(8, 6, 0, 3);

            AddRow(oauthTable, MakeLabel("OAuth client ID:"), this._clientIdTextBox);
            AddRow(oauthTable, this._tenantLabel, this._tenantTextBox);
            AddRow(oauthTable, this._clientSecretLabel, secretRow);
            AddRow(oauthTable, MakeLabel("Browser sign-in:"), signInButtons);
            AddFullRow(oauthTable, this._signInStatusLabel);
            AddFullRow(oauthTable, this._accountLabel);
            AddFullRow(oauthTable, redirectHint);
            oauthGroup.Controls.Add(oauthTable);
            AddRootRow(root, oauthGroup);

            // ----- Manual fallback -----
            var manualGroup = CreateGroup("Manual credentials (optional fallback)");
            var manualTable = CreateFormTable();

            var manualHint = new Label
            {
                Text = "If browser sign-in is not possible, paste a refresh token from an existing OAuth client. "
                    + "The client ID and secret above must belong to the same OAuth client. "
                    + "The refresh token is masked and stored with DPAPI, never in plaintext.",
                AutoSize = true,
                MaximumSize = new Size(560, 0),
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 0, 0, 4)
            };

            AddFullRow(manualTable, manualHint);
            AddRow(manualTable, MakeLabel("Refresh token:"), this._refreshTokenTextBox);
            manualGroup.Controls.Add(manualTable);
            AddRootRow(root, manualGroup);

            // ----- Result + buttons (pinned, never scrolled away) -----
            this._resultLabel = new Label
            {
                Text = string.Empty,
                AutoSize = true,
                MaximumSize = new Size(620, 0),
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 6, 0, 6)
            };

            var buttonPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0, 4, 0, 0)
            };

            this._cancelButton = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                MinimumSize = new Size(100, 30),
                Margin = new Padding(6, 0, 0, 0)
            };
            this._cancelButton.Click += this.CancelButton_Click;

            this._saveButton = new Button
            {
                Text = "Save",
                AutoSize = true,
                MinimumSize = new Size(100, 30),
                Margin = new Padding(6, 0, 0, 0)
            };
            this._saveButton.Click += this.SaveButton_Click;

            this._testButton = new Button
            {
                Text = "Test upload",
                AutoSize = true,
                MinimumSize = new Size(110, 30),
                Margin = new Padding(6, 0, 0, 0)
            };
            this._testButton.Click += async (_, _) => await this.TestUploadAsync();

            buttonPanel.Controls.Add(this._cancelButton);
            buttonPanel.Controls.Add(this._saveButton);
            buttonPanel.Controls.Add(this._testButton);

            var bottomPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(12, 2, 12, 10)
            };
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottomPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottomPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottomPanel.Controls.Add(this._resultLabel, 0, 0);
            bottomPanel.Controls.Add(buttonPanel, 0, 1);
            layout.Controls.Add(bottomPanel, 0, 1);

            this.AcceptButton = this._saveButton;
            this.CancelButton = this._cancelButton;
            this.FormClosing += (_, _) => this.CancelPendingOperations();

            // Fill the fields after all controls exist.
            this._suppressProviderChange = true;
            this.SelectProvider(this._options.Provider);
            this._suppressProviderChange = false;

            this._clientIdTextBox.Text = this._options.ClientId;
            this._tenantTextBox.Text = string.IsNullOrWhiteSpace(this._options.TenantId) ? "common" : this._options.TenantId;
            this._clientSecretTextBox.Text = this._options.ClientSecret;
            this._refreshTokenTextBox.Text = this._options.RefreshToken;

            if (!string.IsNullOrWhiteSpace(this._options.AccountDisplayName))
            {
                this._accountLabel.Text = "Connected as " + this._options.AccountDisplayName;
                this._signInStatusLabel.Text = "Stored sign-in loaded.";
            }

            this.UpdateProviderUi();
            this.UpdateCopyLinkState();
        }

        public CloudIpSyncOptions? Options { get; private set; }

        private CloudIpSyncProvider SelectedProvider
        {
            get
            {
                return this._providerCombo.SelectedItem is ProviderItem item
                    ? item.Provider
                    : CloudIpSyncProvider.GoogleDrive;
            }
        }

        private void ProviderCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (this._suppressProviderChange)
            {
                return;
            }

            CloudIpSyncProvider newProvider = this.SelectedProvider;
            if (newProvider == this._options.Provider)
            {
                this.UpdateProviderUi();
                return;
            }

            bool hasCredentials = !string.IsNullOrWhiteSpace(this._clientIdTextBox.Text)
                || !string.IsNullOrWhiteSpace(this._clientSecretTextBox.Text)
                || !string.IsNullOrWhiteSpace(this._refreshTokenTextBox.Text)
                || !string.IsNullOrWhiteSpace(this._options.AccountDisplayName);

            if (hasCredentials
                && MessageBox.Show(
                    this,
                    "Switching the cloud provider clears the current OAuth client credentials and sign-in. Continue?",
                    "Change cloud provider",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                this._suppressProviderChange = true;
                this.SelectProvider(this._options.Provider);
                this._suppressProviderChange = false;
                return;
            }

            if (hasCredentials)
            {
                this._clientIdTextBox.Clear();
                this._clientSecretTextBox.Clear();
                this._refreshTokenTextBox.Clear();
                this._options.ClientId = string.Empty;
                this._options.ClientSecret = string.Empty;
                this._options.RefreshToken = string.Empty;
                this._options.AccessToken = string.Empty;
                this._options.AccessTokenExpiresUtc = DateTime.MinValue;
                this._options.AccountDisplayName = string.Empty;
                this._accountLabel.Text = "Not connected.";
                this._signInStatusLabel.Text = "Not signed in.";
                this._authorizationUrl = null;
                this.UpdateCopyLinkState();
            }

            this._options.Provider = newProvider;
            this.UpdateProviderUi();
        }

        private void SelectProvider(CloudIpSyncProvider provider)
        {
            for (int index = 0; index < this._providerCombo.Items.Count; index++)
            {
                if (this._providerCombo.Items[index] is ProviderItem item && item.Provider == provider)
                {
                    this._providerCombo.SelectedIndex = index;
                    return;
                }
            }

            this._providerCombo.SelectedIndex = 0;
        }

        private void UpdateProviderUi()
        {
            CloudIpSyncProvider provider = this.SelectedProvider;
            this._providerHintLabel.Text = provider.GetSetupHint();
            this._consoleLink.Text = provider == CloudIpSyncProvider.OneDrive
                ? "Open Microsoft Entra app registrations..."
                : "Open Google Cloud credentials...";
            this._consoleLink.Tag = provider.GetConsoleUrl();
            this._clientSecretLabel.Text = provider.RequiresClientSecret()
                ? "Client secret:"
                : "Client secret (optional):";

            bool showTenant = provider == CloudIpSyncProvider.OneDrive;
            this._tenantLabel.Visible = showTenant;
            this._tenantTextBox.Visible = showTenant;
        }

        private void ShowSecretsCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            bool show = this._showSecretsCheckBox.Checked;
            this._clientSecretTextBox.UseSystemPasswordChar = !show;
            this._refreshTokenTextBox.UseSystemPasswordChar = !show;
        }

        private async Task SignInAsync()
        {
            if (this._busy)
            {
                this.CancelPendingOperations();
                return;
            }

            CloudIpSyncProvider provider = this.SelectedProvider;
            string tenantId = this._tenantTextBox.Text.Trim();
            string clientId = this._clientIdTextBox.Text.Trim();
            string clientSecret = this._clientSecretTextBox.Text.Trim();

            if (clientId.Length == 0)
            {
                this.SetResult("Enter the OAuth client ID first.", isError: true);
                this._clientIdTextBox.Focus();
                return;
            }

            if (provider.RequiresClientSecret() && clientSecret.Length == 0)
            {
                this.SetResult("Google Drive requires the OAuth client secret.", isError: true);
                this._clientSecretTextBox.Focus();
                return;
            }

            this.SetBusy(true, signIn: true);
            this._authorizationUrl = null;
            this.UpdateCopyLinkState();
            this._signInStatusLabel.ForeColor = SystemColors.ControlText;
            this._signInStatusLabel.Text = "Opening browser...";
            this._signInCts = new CancellationTokenSource();

            try
            {
                var statusProgress = new Progress<string>(text =>
                {
                    if (!this.IsDisposed)
                    {
                        this._signInStatusLabel.Text = text;
                    }
                });
                var urlProgress = new Progress<string>(url =>
                {
                    if (!this.IsDisposed)
                    {
                        this._authorizationUrl = url;
                        this.UpdateCopyLinkState();
                    }
                });

                CloudOAuthTokenResult result = await CloudOAuthClient.SignInAsync(
                    provider,
                    tenantId,
                    clientId,
                    clientSecret,
                    statusProgress,
                    urlProgress,
                    this._signInCts.Token);

                if (this.IsDisposed)
                {
                    return;
                }

                this._options.Provider = provider;
                this._options.TenantId = string.IsNullOrWhiteSpace(tenantId) ? "common" : tenantId;
                this._options.ClientId = clientId;
                this._options.ClientSecret = clientSecret;
                this._options.AccessToken = result.AccessToken;
                this._options.AccessTokenExpiresUtc = result.ExpiresUtc;
                this._options.RefreshToken = string.IsNullOrWhiteSpace(result.RefreshToken)
                    ? this._options.RefreshToken
                    : result.RefreshToken!;
                this._refreshTokenTextBox.Text = this._options.RefreshToken;

                this._signInStatusLabel.Text = "Sign-in complete. Verifying account...";
                string account = await CloudOAuthClient.GetAccountDisplayNameAsync(
                    provider,
                    result.AccessToken,
                    this._signInCts.Token);
                if (this.IsDisposed)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(account))
                {
                    this._options.AccountDisplayName = account;
                    this._accountLabel.Text = "Connected as " + account;
                }
                else
                {
                    this._accountLabel.Text = "Connected.";
                }

                this._signInStatusLabel.Text = "Sign-in complete. You can test the upload or save.";
                this.SetResult("Sign-in successful.", isError: false);
            }
            catch (OperationCanceledException)
            {
                if (!this.IsDisposed)
                {
                    this._signInStatusLabel.Text = "Sign-in canceled.";
                }
            }
            catch (Exception ex)
            {
                if (!this.IsDisposed)
                {
                    this._signInStatusLabel.Text = "Sign-in failed.";
                    this.SetResult(ex.Message, isError: true);
                }

                Logger.Log($"[CloudIpSync] Sign-in failed: {ex.Message}");
            }
            finally
            {
                this._authorizationUrl = null;
                this._signInCts?.Dispose();
                this._signInCts = null;
                if (!this.IsDisposed)
                {
                    this.UpdateCopyLinkState();
                    this.SetBusy(false, signIn: false);
                }
            }
        }

        private async Task TestUploadAsync()
        {
            if (this._busy)
            {
                this.CancelPendingOperations();
                return;
            }

            CloudIpSyncOptions options = this.BuildOptionsFromUi();
            if (!ValidateOptions(options, out string error))
            {
                this.SetResult(error, isError: true);
                return;
            }

            this.SetBusy(true, signIn: false);
            this._testCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

            try
            {
                this.SetResult("Determining WAN IP...", isError: false);
                WanIpResult ips = await WanIpLookup.GetAsync(this._testCts.Token);
                if (this.IsDisposed)
                {
                    return;
                }

                if (!ips.HasIpv4)
                {
                    this.SetResult("WAN IPv4 could not be determined. Check the internet connection.", isError: true);
                    return;
                }

                string hostName = CloudIpSyncHelpers.GetHostName();
                string fileName = CloudIpSyncHelpers.GetIpDataFileName(hostName);
                string content = CloudIpDataFormatter.Build(hostName, ips.Ipv4, ips.Ipv6, DateTime.UtcNow);

                this.SetResult("Uploading test file...", isError: false);
                CloudUploadOutcome outcome = await CloudIpSyncUploader.UploadAsync(
                    options,
                    content,
                    fileName,
                    this._testCts.Token);
                if (this.IsDisposed)
                {
                    return;
                }

                if (outcome.UpdatedToken != null)
                {
                    this._options.AccessToken = outcome.UpdatedToken.AccessToken;
                    this._options.AccessTokenExpiresUtc = outcome.UpdatedToken.ExpiresUtc;
                    if (!string.IsNullOrWhiteSpace(outcome.UpdatedToken.RefreshToken))
                    {
                        this._options.RefreshToken = outcome.UpdatedToken.RefreshToken!;
                        this._refreshTokenTextBox.Text = this._options.RefreshToken;
                    }
                }

                this.SetResult(
                    outcome.Success ? "Test upload succeeded. " + outcome.Message : outcome.Message,
                    isError: !outcome.Success);
            }
            catch (OperationCanceledException)
            {
                if (!this.IsDisposed)
                {
                    this.SetResult("Test upload canceled.", isError: true);
                }
            }
            catch (Exception ex)
            {
                if (!this.IsDisposed)
                {
                    this.SetResult("Test upload failed: " + ex.Message, isError: true);
                }
            }
            finally
            {
                this._testCts?.Dispose();
                this._testCts = null;
                if (!this.IsDisposed)
                {
                    this.SetBusy(false, signIn: false);
                }
            }
        }

        private CloudIpSyncOptions BuildOptionsFromUi()
        {
            CloudIpSyncOptions options = this._options.Clone();
            options.Provider = this.SelectedProvider;
            options.TenantId = string.IsNullOrWhiteSpace(this._tenantTextBox.Text)
                ? "common"
                : this._tenantTextBox.Text.Trim();
            options.ClientId = this._clientIdTextBox.Text.Trim();
            options.ClientSecret = this._clientSecretTextBox.Text.Trim();
            options.RefreshToken = this._refreshTokenTextBox.Text.Trim();

            // Do not reuse an access token that belongs to credentials the user just edited.
            bool credentialsChanged =
                options.Provider != this._options.Provider
                || !string.Equals(options.TenantId, this._options.TenantId, StringComparison.Ordinal)
                || !string.Equals(options.ClientId, this._options.ClientId, StringComparison.Ordinal)
                || !string.Equals(options.ClientSecret, this._options.ClientSecret, StringComparison.Ordinal)
                || !string.Equals(options.RefreshToken, this._options.RefreshToken, StringComparison.Ordinal);
            if (credentialsChanged)
            {
                options.AccessToken = string.Empty;
                options.AccessTokenExpiresUtc = DateTime.MinValue;
                options.AccountDisplayName = string.Empty;
            }

            return options;
        }

        private static bool ValidateOptions(CloudIpSyncOptions options, out string error)
        {
            if (string.IsNullOrWhiteSpace(options.ClientId))
            {
                error = "Enter the OAuth client ID.";
                return false;
            }

            if (options.Provider.RequiresClientSecret() && string.IsNullOrWhiteSpace(options.ClientSecret))
            {
                error = "Google Drive requires the OAuth client secret.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.RefreshToken))
            {
                error = "Sign in with the browser or paste a refresh token.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            CloudIpSyncOptions options = this.BuildOptionsFromUi();
            if (!ValidateOptions(options, out string error))
            {
                this.SetResult(error, isError: true);
                return;
            }

            this.Options = options;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void CancelButton_Click(object? sender, EventArgs e)
        {
            if (this._busy)
            {
                this.CancelPendingOperations();
                return;
            }

            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void CopyLinkButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(this._authorizationUrl))
            {
                return;
            }

            try
            {
                Clipboard.SetText(this._authorizationUrl);
                this.SetResult("Sign-in link copied to the clipboard.", isError: false);
            }
            catch (Exception ex)
            {
                this.SetResult("Could not copy the link: " + ex.Message, isError: true);
            }
        }

        private void ConsoleLink_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
        {
            if (this._consoleLink.Tag is not string url || string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                this.SetResult("Could not open the provider console: " + ex.Message, isError: true);
            }
        }

        private void CancelPendingOperations()
        {
            try { this._signInCts?.Cancel(); } catch { }
            try { this._testCts?.Cancel(); } catch { }
        }

        private void SetBusy(bool busy, bool signIn)
        {
            if (this.IsDisposed)
            {
                return;
            }

            this._busy = busy;
            this._providerCombo.Enabled = !busy;
            this._clientIdTextBox.Enabled = !busy;
            this._tenantTextBox.Enabled = !busy;
            this._clientSecretTextBox.Enabled = !busy;
            this._refreshTokenTextBox.Enabled = !busy;
            this._showSecretsCheckBox.Enabled = !busy;
            this._testButton.Enabled = !busy;
            this._saveButton.Enabled = !busy;
            this._cancelButton.Enabled = true;
            this._signInButton.Enabled = !busy || signIn;
            this._signInButton.Text = signIn ? "Cancel sign-in" : "Sign in with browser...";
            this._copyLinkButton.Enabled = !string.IsNullOrWhiteSpace(this._authorizationUrl);
            this.UseWaitCursor = busy;
        }

        private void UpdateCopyLinkState()
        {
            if (this.IsDisposed)
            {
                return;
            }

            // Keep the link copyable while waiting for the browser sign-in:
            // that is exactly when the browser may have failed to open.
            this._copyLinkButton.Enabled = !string.IsNullOrWhiteSpace(this._authorizationUrl);
        }

        private void SetResult(string text, bool isError)
        {
            if (this.IsDisposed)
            {
                return;
            }

            this._resultLabel.Text = text;
            this._resultLabel.ForeColor = isError ? Color.Firebrick : Color.ForestGreen;
        }

        private static GroupBox CreateGroup(string text)
        {
            return new GroupBox
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = new Padding(8, 6, 8, 8),
                Margin = new Padding(0, 4, 0, 8)
            };
        }

        private static TableLayoutPanel CreateFormTable()
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 0,
                Padding = new Padding(6, 6, 6, 4)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return table;
        }

        private static Label MakeLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 8, 6)
            };
        }

        private static void AddRow(TableLayoutPanel table, Control label, Control control)
        {
            int row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(label, 0, row);
            table.Controls.Add(control, 1, row);
        }

        private static void AddFullRow(TableLayoutPanel table, Control control)
        {
            int row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(control, 0, row);
            table.SetColumnSpan(control, 2);
        }

        private static void AddRootRow(TableLayoutPanel table, Control control)
        {
            int row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(control, 0, row);
        }

        private sealed record ProviderItem(CloudIpSyncProvider Provider, string Text)
        {
            public override string ToString()
            {
                return this.Text;
            }
        }
    }
}
