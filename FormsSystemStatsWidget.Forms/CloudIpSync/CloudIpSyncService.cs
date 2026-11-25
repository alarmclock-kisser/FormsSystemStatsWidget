using System;
using System.Threading;
using System.Threading.Tasks;
using FormsSystemStatsWidget.Core;

namespace FormsSystemStatsWidget.Forms
{
    /// <summary>
    /// Uploads the current WAN IP data at startup, whenever the WAN IP changes and once per hour
    /// as a safety net. IPv4 is required; IPv6 is uploaded when the connection provides one.
    /// </summary>
    internal sealed class CloudIpSyncService : IDisposable
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan AuthRetryDelay = TimeSpan.FromHours(1);
        private static readonly TimeSpan HourlyInterval = TimeSpan.FromHours(1);

        private readonly object _gate = new();
        private readonly SemaphoreSlim _syncLock = new(1, 1);
        private readonly CloudIpSyncOptions _options;
        private readonly Action<CloudIpSyncOptions>? _optionsChanged;
        private readonly Action<CloudIpSyncStatus>? _statusChanged;

        private CancellationTokenSource? _loopCts;
        private Task? _loopTask;
        private string? _lastUploadedIpv4;
        private string? _lastUploadedIpv6;
        private DateTime _nextHourlyUtc = DateTime.MinValue;
        private DateTime _nextRetryUtc = DateTime.MinValue;
        private bool _disposed;

        public CloudIpSyncService(
            CloudIpSyncOptions options,
            Action<CloudIpSyncOptions>? optionsChanged = null,
            Action<CloudIpSyncStatus>? statusChanged = null)
        {
            this._options = options ?? throw new ArgumentNullException(nameof(options));
            this._optionsChanged = optionsChanged;
            this._statusChanged = statusChanged;
        }

        public bool IsRunning
        {
            get
            {
                lock (this._gate)
                {
                    return this._loopTask != null;
                }
            }
        }

        public void Start()
        {
            lock (this._gate)
            {
                if (this._disposed || this._loopTask != null)
                {
                    return;
                }

                this._loopCts = new CancellationTokenSource();
                CancellationToken token = this._loopCts.Token;
                this._loopTask = Task.Run(() => this.RunAsync(token), token);
            }
        }

        public void Stop()
        {
            CancellationTokenSource? cts;
            Task? task;

            lock (this._gate)
            {
                cts = this._loopCts;
                task = this._loopTask;
                this._loopCts = null;
                this._loopTask = null;
            }

            if (cts == null)
            {
                return;
            }

            try { cts.Cancel(); } catch { }

            if (task == null)
            {
                cts.Dispose();
                return;
            }

            _ = task.ContinueWith(
                completed =>
                {
                    _ = completed.Exception;
                    cts.Dispose();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public async Task<CloudIpSyncStatus> SyncNowAsync(CancellationToken cancellationToken = default)
        {
            return await this.SyncOnceAsync(knownIps: null, cancellationToken).ConfigureAwait(false);
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            this.Report(new CloudIpSyncStatus(CloudIpSyncState.Starting, "Starting..."));

            try
            {
                await this.SyncOnceAsync(knownIps: null, cancellationToken).ConfigureAwait(false);

                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);

                    if (DateTime.UtcNow < this._nextRetryUtc)
                    {
                        continue;
                    }

                    WanIpResult ips = await WanIpLookup.GetAsync(cancellationToken).ConfigureAwait(false);
                    bool hourly = DateTime.UtcNow >= this._nextHourlyUtc;
                    bool changed = !string.Equals(ips.Ipv4, this._lastUploadedIpv4, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(ips.Ipv6, this._lastUploadedIpv6, StringComparison.OrdinalIgnoreCase);

                    if (!ips.HasIpv4)
                    {
                        if (hourly || changed || this._lastUploadedIpv4 == null)
                        {
                            await this.SyncOnceAsync(knownIps: ips, cancellationToken).ConfigureAwait(false);
                        }

                        continue;
                    }

                    if (changed || hourly)
                    {
                        await this.SyncOnceAsync(knownIps: ips, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
            catch (Exception ex)
            {
                Logger.Log($"[CloudIpSync] Background loop stopped: {ex.GetType().Name}: {ex.Message}");
                this.Report(new CloudIpSyncStatus(
                    CloudIpSyncState.Error,
                    "Background upload stopped: " + ex.Message));
            }
        }

        private async Task<CloudIpSyncStatus> SyncOnceAsync(
            WanIpResult? knownIps,
            CancellationToken cancellationToken)
        {
            await this._syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                this.Report(new CloudIpSyncStatus(
                    CloudIpSyncState.Syncing,
                    "Uploading to " + this._options.Provider.GetDisplayName() + "..."));

                WanIpResult ips = knownIps ?? await WanIpLookup.GetAsync(cancellationToken).ConfigureAwait(false);
                if (!ips.HasIpv4)
                {
                    this._nextRetryUtc = DateTime.UtcNow.Add(RetryDelay);
                    CloudIpSyncStatus failure = new(
                        CloudIpSyncState.Error,
                        "WAN IPv4 could not be determined. Retrying...");
                    this.Report(failure);
                    return failure;
                }

                string hostName = CloudIpSyncHelpers.GetHostName();
                string fileName = CloudIpSyncHelpers.GetIpDataFileName(hostName);
                string content = CloudIpDataFormatter.Build(hostName, ips.Ipv4, ips.Ipv6, DateTime.UtcNow);

                CloudUploadOutcome outcome = await CloudIpSyncUploader
                    .UploadAsync(this._options, content, fileName, cancellationToken)
                    .ConfigureAwait(false);

                if (outcome.UpdatedToken != null)
                {
                    this._options.AccessToken = outcome.UpdatedToken.AccessToken;
                    this._options.AccessTokenExpiresUtc = outcome.UpdatedToken.ExpiresUtc;
                    if (!string.IsNullOrWhiteSpace(outcome.UpdatedToken.RefreshToken))
                    {
                        this._options.RefreshToken = outcome.UpdatedToken.RefreshToken;
                    }

                    try
                    {
                        this._optionsChanged?.Invoke(this._options.Clone());
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[CloudIpSync] Could not persist refreshed credentials: {ex.GetType().Name}");
                    }
                }

                if (outcome.Success)
                {
                    this._lastUploadedIpv4 = ips.Ipv4;
                    this._lastUploadedIpv6 = ips.Ipv6;
                    this._nextHourlyUtc = DateTime.UtcNow.Add(HourlyInterval);
                    this._nextRetryUtc = DateTime.MinValue;

                    string message = "Last upload " + DateTime.Now.ToString("g")
                        + " · IPv4 " + (ips.Ipv4 ?? "unavailable")
                        + " · IPv6 " + (ips.Ipv6 ?? "unavailable");
                    CloudIpSyncStatus success = new(CloudIpSyncState.Synced, message, DateTime.UtcNow);
                    this.Report(success);
                    Logger.Log($"[CloudIpSync] {outcome.Message}");
                    return success;
                }

                bool needsSignIn = outcome.NeedsSignIn;
                this._nextRetryUtc = DateTime.UtcNow.Add(needsSignIn ? AuthRetryDelay : RetryDelay);
                CloudIpSyncStatus error = new(
                    needsSignIn ? CloudIpSyncState.NeedsSignIn : CloudIpSyncState.Error,
                    outcome.Message);
                this.Report(error);
                Logger.Log($"[CloudIpSync] Upload failed: {outcome.Message}");
                return error;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                this._nextRetryUtc = DateTime.UtcNow.Add(RetryDelay);
                CloudIpSyncStatus error = new(
                    CloudIpSyncState.Error,
                    "Upload failed: " + ex.Message);
                this.Report(error);
                Logger.Log($"[CloudIpSync] Unexpected upload error: {ex.GetType().Name}: {ex.Message}");
                return error;
            }
            finally
            {
                this._syncLock.Release();
            }
        }

        private void Report(CloudIpSyncStatus status)
        {
            try
            {
                this._statusChanged?.Invoke(status);
            }
            catch (Exception ex)
            {
                Logger.Log($"[CloudIpSync] Status callback failed: {ex.GetType().Name}");
            }
        }

        public void Dispose()
        {
            if (this._disposed)
            {
                return;
            }

            this._disposed = true;
            this.Stop();
        }
    }
}
