using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using FormsSystemStatsWidget.Core;

namespace FormsSystemStatsWidget.Forms
{
    public partial class WindowWidget
    {
        private CloudIpSyncService? _cloudIpSyncService;
        private CloudIpSyncOptions _cloudIpSyncOptions = new();
        private CloudIpSyncStatus _cloudIpSyncStatus = CloudIpSyncStatus.Disabled;
        private bool _cloudIpSyncMenuInitialized;
        private bool _cloudIpSyncStartupHandled;
        private bool _cloudToggleClickPending;

        private readonly ToolStripMenuItem _cloudEnabledMenuItem = new("Enable automatic uploads");
        private readonly ToolStripMenuItem _cloudConfigMenuItem = new("Config...");
        private readonly ToolStripMenuItem _cloudSyncNowMenuItem = new("Sync now");
        private readonly ToolStripMenuItem _cloudStatusMenuItem = new("Status: off") { Enabled = false };

        private void InitializeCloudIpSyncMenu()
        {
            if (this._cloudIpSyncMenuInitialized)
            {
                return;
            }

            this._cloudIpSyncMenuInitialized = true;

            this._cloudEnabledMenuItem.Click += (_, _) =>
            {
                this.CloseWidgetContextMenu();
                this.ToggleCloudIpUpdates();
            };
            this._cloudConfigMenuItem.Click += this.CloudConfigMenuItem_Click;
            this._cloudSyncNowMenuItem.Click += async (_, _) => await this.CloudSyncNowAsync();
            this._cloudStatusMenuItem.Enabled = false;

            this.cloudIpUpdatesToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                this._cloudEnabledMenuItem,
                this._cloudConfigMenuItem,
                this._cloudSyncNowMenuItem,
                new ToolStripSeparator(),
                this._cloudStatusMenuItem
            });
            this.cloudIpUpdatesToolStripMenuItem.CheckOnClick = false;
            this.cloudIpUpdatesToolStripMenuItem.MouseDown += this.CloudIpUpdatesToolStripMenuItem_MouseDown;
            this.cloudIpUpdatesToolStripMenuItem.MouseLeave += (_, _) => this._cloudToggleClickPending = false;
            this.cloudIpUpdatesToolStripMenuItem.Click += this.CloudIpUpdatesToolStripMenuItem_Click;

            this.Shown += (_, _) => this.StartCloudIpSyncIfEnabled();
        }

        private void StartCloudIpSyncIfEnabled()
        {
            if (this._cloudIpSyncStartupHandled)
            {
                return;
            }

            this._cloudIpSyncStartupHandled = true;
            this._cloudIpSyncOptions = CloudIpSyncSettingsMapper.FromPersistent(this._persistentSettings);
            this.cloudIpUpdatesToolStripMenuItem.Checked = this._persistentSettings.CloudIpSyncEnabled;
            this._cloudEnabledMenuItem.Checked = this._persistentSettings.CloudIpSyncEnabled;

            if (!this._persistentSettings.CloudIpSyncEnabled)
            {
                this.SetCloudIpSyncStatus(CloudIpSyncStatus.Disabled);
                return;
            }

            if (!CloudIpSyncSettingsMapper.IsConfigured(this._cloudIpSyncOptions))
            {
                this.SetCloudIpSyncStatus(new CloudIpSyncStatus(
                    CloudIpSyncState.NeedsConfiguration,
                    this.CloudNeedsConfigurationMessage()));

                // The user enabled uploads earlier but never finished (or lost) the configuration.
                // Finish the setup right away instead of failing silently in the background.
                this.BeginInvoke(new Action(this.PromptForCloudConfigurationAtStartup));
                return;
            }

            this.StartCloudIpSync();
        }

        private void PromptForCloudConfigurationAtStartup()
        {
            if (this.IsDisposed || !this._persistentSettings.CloudIpSyncEnabled)
            {
                return;
            }

            if (CloudIpSyncSettingsMapper.IsConfigured(this._persistentSettings))
            {
                this.StartCloudIpSync();
                return;
            }

            if (!this.ShowCloudDriveConfigDialog())
            {
                this.SetCloudIpUpdatesEnabled(false);
                this.SetCloudIpSyncStatus(new CloudIpSyncStatus(
                    CloudIpSyncState.NeedsConfiguration,
                    this.CloudNeedsConfigurationMessage()));
            }
        }

        private void StartCloudIpSync()
        {
            this.StopCloudIpSync();

            this._cloudIpSyncOptions = CloudIpSyncSettingsMapper.FromPersistent(this._persistentSettings);
            if (!CloudIpSyncSettingsMapper.IsConfigured(this._cloudIpSyncOptions))
            {
                this.SetCloudIpSyncStatus(new CloudIpSyncStatus(
                    CloudIpSyncState.NeedsConfiguration,
                    this.CloudNeedsConfigurationMessage()));
                return;
            }

            CloudIpSyncService? service = null;
            service = new CloudIpSyncService(
                this._cloudIpSyncOptions,
                updated => this.PostToUiThread(() =>
                {
                    // Ignore late token refreshes from a service that has already been replaced.
                    if (!ReferenceEquals(this._cloudIpSyncService, service))
                    {
                        return;
                    }

                    CloudIpSyncSettingsMapper.ApplyToPersistent(this._persistentSettings, updated);
                    this.SavePersistentSettings();
                }),
                status => this.PostToUiThread(() =>
                {
                    if (!ReferenceEquals(this._cloudIpSyncService, service))
                    {
                        return;
                    }

                    this.SetCloudIpSyncStatus(status);
                }));

            this._cloudIpSyncService = service;
            service.Start();
            this.SetCloudIpSyncStatus(new CloudIpSyncStatus(CloudIpSyncState.Starting, "Starting..."));
        }

        private void StopCloudIpSync()
        {
            CloudIpSyncService? service = this._cloudIpSyncService;
            this._cloudIpSyncService = null;

            if (service == null)
            {
                return;
            }

            try { service.Stop(); } catch { }
            try { service.Dispose(); } catch { }
        }

        private void CloudIpUpdatesToolStripMenuItem_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            // The item body toggles the check state; the arrow at the right edge opens the
            // submenu (Config..., Sync now). Remember which part was pressed because Click
            // does not carry the mouse position.
            int dpi = this.cloudIpUpdatesToolStripMenuItem.Owner?.DeviceDpi ?? 96;
            var localBounds = new Rectangle(Point.Empty, this.cloudIpUpdatesToolStripMenuItem.Size);
            this._cloudToggleClickPending = CloudIpSyncMenuHitTest.IsToggleClick(localBounds, e.Location, dpi);
        }

        private void CloudIpUpdatesToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            bool toggle = this._cloudToggleClickPending;
            this._cloudToggleClickPending = false;
            if (!toggle)
            {
                return;
            }

            this.CloseWidgetContextMenu();
            this.ToggleCloudIpUpdates();
        }

        private void ToggleCloudIpUpdates()
        {
            if (this._persistentSettings.CloudIpSyncEnabled)
            {
                this.SetCloudIpUpdatesEnabled(false);
                return;
            }

            if (!this.EnsureCloudIpSyncConfigured())
            {
                this.SetCloudIpUpdatesEnabled(false);
                this.SetCloudIpSyncStatus(new CloudIpSyncStatus(
                    CloudIpSyncState.NeedsConfiguration,
                    this.CloudNeedsConfigurationMessage()));
                return;
            }

            this.SetCloudIpUpdatesEnabled(true);
        }

        private void SetCloudIpUpdatesEnabled(bool enabled)
        {
            this._persistentSettings.CloudIpSyncEnabled = enabled;
            this.cloudIpUpdatesToolStripMenuItem.Checked = enabled;
            this._cloudEnabledMenuItem.Checked = enabled;
            this.SavePersistentSettings();

            if (enabled)
            {
                this.StartCloudIpSync();
                return;
            }

            this.StopCloudIpSync();
            this.SetCloudIpSyncStatus(CloudIpSyncStatus.Disabled);
        }

        private void CloseWidgetContextMenu()
        {
            try { this.contextMenuStrip_widget.Close(ToolStripDropDownCloseReason.ItemClicked); } catch { }
        }

        private bool EnsureCloudIpSyncConfigured()
        {
            this._cloudIpSyncOptions = CloudIpSyncSettingsMapper.FromPersistent(this._persistentSettings);
            if (CloudIpSyncSettingsMapper.IsConfigured(this._cloudIpSyncOptions))
            {
                return true;
            }

            return this.ShowCloudDriveConfigDialog();
        }

        private void CloudConfigMenuItem_Click(object? sender, EventArgs e)
        {
            this.CloseWidgetContextMenu();
            this.ShowCloudDriveConfigDialog();
        }

        private bool ShowCloudDriveConfigDialog()
        {
            CloudIpSyncOptions current = CloudIpSyncSettingsMapper.FromPersistent(this._persistentSettings);
            using var dialog = new CloudDriveConfigDialog(current);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Options == null)
            {
                return false;
            }

            CloudIpSyncSettingsMapper.ApplyToPersistent(this._persistentSettings, dialog.Options);
            this.SavePersistentSettings();
            this._cloudIpSyncOptions = dialog.Options.Clone();

            if (!CloudIpSyncSettingsMapper.IsConfigured(this._persistentSettings))
            {
                this._persistentSettings.CloudIpSyncEnabled = false;
                this.cloudIpUpdatesToolStripMenuItem.Checked = false;
                this._cloudEnabledMenuItem.Checked = false;
                this.StopCloudIpSync();
                this.SavePersistentSettings();
                this.SetCloudIpSyncStatus(new CloudIpSyncStatus(
                    CloudIpSyncState.NeedsConfiguration,
                    this.CloudNeedsConfigurationMessage()));
                return false;
            }

            if (this._persistentSettings.CloudIpSyncEnabled)
            {
                this.cloudIpUpdatesToolStripMenuItem.Checked = true;
                this._cloudEnabledMenuItem.Checked = true;
                this.StartCloudIpSync();
            }
            else
            {
                this.cloudIpUpdatesToolStripMenuItem.Checked = false;
                this._cloudEnabledMenuItem.Checked = false;
                this.SetCloudIpSyncStatus(new CloudIpSyncStatus(
                    CloudIpSyncState.Disabled,
                    "Configured. Enable the menu entry to start uploads."));
            }

            return true;
        }

        private async Task CloudSyncNowAsync()
        {
            this.CloseWidgetContextMenu();

            if (!CloudIpSyncSettingsMapper.IsConfigured(this._persistentSettings)
                && !this.ShowCloudDriveConfigDialog())
            {
                return;
            }

            try
            {
                if (this._cloudIpSyncService != null)
                {
                    await this._cloudIpSyncService.SyncNowAsync();
                    return;
                }

                // A one-shot sync does not enable the automatic background uploads.
                this._cloudIpSyncOptions = CloudIpSyncSettingsMapper.FromPersistent(this._persistentSettings);
                using var service = new CloudIpSyncService(
                    this._cloudIpSyncOptions,
                    options => this.PersistCloudIpSyncOptions(options),
                    status => this.PostCloudIpSyncStatus(status));
                await service.SyncNowAsync();
            }
            catch (Exception ex)
            {
                Logger.Log($"[CloudIpSync] Manual sync failed: {ex.Message}");
                this.SetCloudIpSyncStatus(new CloudIpSyncStatus(
                    CloudIpSyncState.Error,
                    "Sync failed: " + ex.Message));
            }
        }

        private void PersistCloudIpSyncOptions(CloudIpSyncOptions options)
        {
            this.PostToUiThread(() =>
            {
                CloudIpSyncSettingsMapper.ApplyToPersistent(this._persistentSettings, options);
                this.SavePersistentSettings();
            });
        }

        private void PostCloudIpSyncStatus(CloudIpSyncStatus status)
        {
            this.PostToUiThread(() => this.SetCloudIpSyncStatus(status));
        }

        private void SetCloudIpSyncStatus(CloudIpSyncStatus status)
        {
            this._cloudIpSyncStatus = status;

            if (!this._cloudIpSyncMenuInitialized || this.IsDisposed)
            {
                return;
            }

            this._cloudStatusMenuItem.Text = BuildCloudStatusText(status);
            this._cloudStatusMenuItem.ForeColor = status.State switch
            {
                CloudIpSyncState.Error
                    or CloudIpSyncState.NeedsSignIn
                    or CloudIpSyncState.NeedsConfiguration => Color.Firebrick,
                CloudIpSyncState.Synced => Color.ForestGreen,
                _ => SystemColors.GrayText
            };
            this.cloudIpUpdatesToolStripMenuItem.ToolTipText = status.Message;
            this._cloudEnabledMenuItem.Checked = this._persistentSettings.CloudIpSyncEnabled;

            if (status.State == CloudIpSyncState.Synced)
            {
                this._persistentSettings.CloudIpSyncLastUploadUtc = status.LastUploadUtc ?? DateTime.UtcNow;
                this._persistentSettings.CloudIpSyncLastStatus = status.Message;
                this.SavePersistentSettings();
            }
        }

        private void UpdateCloudIpSyncMenuState()
        {
            if (!this._cloudIpSyncMenuInitialized)
            {
                return;
            }

            this.cloudIpUpdatesToolStripMenuItem.Checked = this._persistentSettings.CloudIpSyncEnabled;
            this._cloudEnabledMenuItem.Checked = this._persistentSettings.CloudIpSyncEnabled;
            bool configured = CloudIpSyncSettingsMapper.IsConfigured(this._persistentSettings);
            this._cloudSyncNowMenuItem.Enabled = configured;
            this._cloudConfigMenuItem.Text = !configured
                ? "Config... (required)"
                : this._cloudIpSyncStatus.State == CloudIpSyncState.NeedsSignIn
                    ? "Config... (sign in again)"
                    : "Config...";
            this._cloudStatusMenuItem.Text = BuildCloudStatusText(this._cloudIpSyncStatus);
        }

        private string CloudNeedsConfigurationMessage()
        {
            bool isLocal = CloudIpSyncSettingsMapper
                .FromPersistent(this._persistentSettings)
                .Provider.IsLocalDirectory();
            return isLocal
                ? "A local directory is required. Open Config..."
                : "Sign-in required. Open Config...";
        }

        private static string BuildCloudStatusText(CloudIpSyncStatus status)
        {
            string message = status.Message ?? string.Empty;
            if (message.Length > 90)
            {
                message = message[..87] + "...";
            }

            return status.State switch
            {
                CloudIpSyncState.Disabled => message.Length == 0 || string.Equals(message, "Off", StringComparison.Ordinal)
                    ? "Status: off"
                    : "Status: " + message,
                CloudIpSyncState.NeedsConfiguration => "Status: " + message,
                CloudIpSyncState.NeedsSignIn => "Status: sign-in expired",
                CloudIpSyncState.Starting => "Status: starting...",
                CloudIpSyncState.Syncing => "Status: writing...",
                CloudIpSyncState.Synced => "Status: " + message,
                _ => "Status: " + message
            };
        }

        private void PostToUiThread(Action action)
        {
            if (this.IsDisposed || this.Disposing)
            {
                return;
            }

            try
            {
                if (this.InvokeRequired)
                {
                    _ = this.BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch
            {
                // The window may have been closed between the check and BeginInvoke.
            }
        }
    }
}
