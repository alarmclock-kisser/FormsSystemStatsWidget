using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FormsSystemStatsWidget.OnnxGenaiServer.Engine;

/// <summary>
/// Überwacht den Python-Engine-Prozess: Start, Health-Check, Restart bei Crash, sauberes Shutdown.
/// </summary>
public sealed class PythonProcessSupervisor : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly string _pythonExecutable;
    private readonly string _engineModulePath;
    private readonly int _port;
    private readonly int _startupTimeoutMs;
    private readonly int _maxRestarts;
    private readonly int _restartIntervalMs;
    private readonly OnnxGenaiServerOptions _options;
    private readonly EngineProcessRegistry _registry;

    private Process? _process;
    private EngineProcessEntry? _ownedEntry;
    private Guid _instanceId;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;
    private bool _shuttingDown;
    private int _restartCount;
    private CancellationTokenSource? _healthMonitorCancellation;
    private Task? _healthMonitorTask;

    public bool IsRunning => _process is { HasExited: false };
    public int RestartCount => _restartCount;
    public string? LastError { get; private set; }

    public PythonProcessSupervisor(ILogger logger, OnnxGenaiServerOptions options)
    {
        _logger = logger;
        _options = options;
        _pythonExecutable = options.PythonExecutable ?? "python";
        _engineModulePath = options.PythonEngineModule ?? "onnx_engine.server";
        _port = options.PythonEnginePort ?? 8081;
        _startupTimeoutMs = Math.Max(1, options.StartupTimeoutSeconds) * 1000;
        _maxRestarts = Math.Max(0, options.MaxPythonRestarts);
        _restartIntervalMs = Math.Max(1, options.PythonRestartIntervalSeconds) * 1000;
        _registry = new EngineProcessRegistry(
            options.EngineProcessRegistryPath,
            options.RegistryLockTimeoutSeconds,
            logger);
    }

    public string BaseUrl => $"http://127.0.0.1:{_port}";

    /// <summary>
    /// Startet den Python-Engine-Prozess und wartet bis er bereit ist.
    /// </summary>
    public async Task<bool> StartAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (IsRunning && _ownedEntry is not null
                && TryReadHealth(_ownedEntry.Port, out var health)
                && health is not null
                && HealthBelongsToEntry(health, _ownedEntry))
            {
                return true;
            }
            _shuttingDown = false;
            var ready = await Task.Run(
                () => _registry.ExecuteLocked(entries => ReconcileAndStart(entries, ct)),
                ct);
            if (ready)
            {
                StartHealthMonitor();
            }

            return ready;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool ReconcileAndStart(List<EngineProcessEntry> entries, CancellationToken ct)
    {
        if (_options.SingleEngineOnly && !ReconcileExistingProcesses(entries))
        {
            return false;
        }

        if (!_options.SingleEngineOnly)
        {
            RemoveStaleEntries(entries);
        }

        if (ct.IsCancellationRequested)
        {
            return false;
        }

        return StartOwnedProcess(entries, ct);
    }

    private void RemoveStaleEntries(List<EngineProcessEntry> entries)
    {
        foreach (var entry in entries.ToArray())
        {
            if (!TryGetProcess(entry.Pid, out var process) || process is null)
            {
                entries.Remove(entry);
                continue;
            }
            using (process)
            {
                if (!WindowsProcessIdentityReader.TryRead(process, out var identity) || identity is null)
                {
                    entry.State = "Unknown";
                    continue;
                }
                if (identity.ProcessStartTimeUtc != entry.ProcessStartTimeUtc)
                {
                    entries.Remove(entry);
                }
            }
        }
    }

    private bool ReconcileExistingProcesses(List<EngineProcessEntry> entries)
    {
        var visitedPids = new HashSet<int>();
        foreach (var entry in entries.ToArray())
        {
            if (!TryGetProcess(entry.Pid, out var process) || process is null)
            {
                entries.Remove(entry);
                _logger.LogInformation("[ONNX-LIFECYCLE] Removed stale registry entry for PID {Pid}", entry.Pid);
                continue;
            }

            using (process)
            {
                if (!WindowsProcessIdentityReader.TryRead(process, out var identity) || identity is null)
                {
                    visitedPids.Add(entry.Pid);
                    entry.State = "Unknown";
                    _logger.LogWarning(
                        "[ONNX-LIFECYCLE] Cannot verify registered engine PID {Pid}; refusing to terminate or start another engine.",
                        entry.Pid);
                    return false;
                }

                if (identity.ProcessStartTimeUtc != entry.ProcessStartTimeUtc)
                {
                    entries.Remove(entry);
                    _logger.LogWarning(
                        "[ONNX-LIFECYCLE] PID {Pid} was reused; registry start time does not match. Entry removed without killing process.",
                        entry.Pid);
                    continue;
                }

                visitedPids.Add(entry.Pid);
                if (!MatchesEntry(identity, entry))
                {
                    entry.State = "Unknown";
                    _logger.LogWarning(
                        "[ONNX-LIFECYCLE] PID {Pid} is alive but its executable/command line no longer matches the registry; refusing to kill it.",
                        entry.Pid);
                    return false;
                }

                RefreshEntryFromHealth(entry);
                _logger.LogWarning(
                    "[ONNX-LIFECYCLE] Found existing engine process PID {Pid}. Port: {Port}. State: {State}.",
                    entry.Pid, entry.Port, entry.State);
                if (!StopVerifiedProcess(process, entry))
                {
                    return false;
                }

                entries.Remove(entry);
            }
        }

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId || visitedPids.Contains(process.Id))
                {
                    continue;
                }

                if (!WindowsProcessIdentityReader.TryRead(process, out var identity)
                    || identity is null
                    || !IsConfiguredPythonExecutable(identity.ExecutablePath)
                    || !TryGetEngineArguments(identity.CommandLine, out var arguments, out var port, out var commandLineInstanceId))
                {
                    continue;
                }

                var orphanedEntry = CreateEntry(identity, arguments, port, commandLineInstanceId);
                RefreshEntryFromHealth(orphanedEntry);
                _logger.LogWarning(
                    "[ONNX-LIFECYCLE] Found unregistered engine process PID {Pid}. Port: {Port}. State: {State}.",
                    orphanedEntry.Pid, orphanedEntry.Port, orphanedEntry.State);
                if (!StopVerifiedProcess(process, orphanedEntry))
                {
                    orphanedEntry.State = "Unknown";
                    entries.Add(orphanedEntry);
                    return false;
                }
            }
        }

        return true;
    }

    private bool StartOwnedProcess(List<EngineProcessEntry> entries, CancellationToken ct)
    {
        if (_process is not null)
        {
            if (!HasExited(_process))
            {
                _logger.LogError("[ONNX-LIFECYCLE] Previous Python process PID {Pid} is still alive; refusing to start a duplicate.", _process.Id);
                return false;
            }
            _process.Dispose();
            _process = null;
            _ownedEntry = null;
        }

        _instanceId = Guid.NewGuid();
        var instanceId = _instanceId.ToString("D");
        var arguments = new[]
        {
            "-m", _engineModulePath,
            "--port", _port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--instance-id", instanceId
        };
        var psi = new ProcessStartInfo
        {
            FileName = _pythonExecutable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        string pythonEnginePath = Path.Combine(AppContext.BaseDirectory, "PyDualOnnxInferenceEngine");
        if (Directory.Exists(pythonEnginePath))
        {
            string? inheritedPythonPath = psi.Environment.TryGetValue("PYTHONPATH", out var existingPythonPath)
                ? existingPythonPath
                : null;
            psi.Environment["PYTHONPATH"] = string.IsNullOrWhiteSpace(inheritedPythonPath)
                ? pythonEnginePath
                : pythonEnginePath + Path.PathSeparator + inheritedPythonPath;
        }
        psi.Environment["ONNX_ENGINE_INSTANCE_ID"] = instanceId;
        psi.Environment["ONNX_ENGINE_IDLE_SHUTDOWN_ENABLED"] = _options.IdleAutoShutdownEnabled ? "true" : "false";
        psi.Environment["ONNX_ENGINE_IDLE_SHUTDOWN_SECONDS"] = Math.Max(1, _options.IdleAutoShutdownSeconds)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);

        _logger.LogInformation(
            "Starte Python-Engine: {Python} -m {Module} --port {Port} (Instance {InstanceId})",
            _pythonExecutable, _engineModulePath, _port, instanceId);

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.ErrorDataReceived += OnErrorDataReceived;
        process.OutputDataReceived += OnOutputDataReceived;
        process.Exited += (_, _) => OnOwnedProcessExited(instanceId, process.Id);
        try
        {
            if (!process.Start())
            {
                process.Dispose();
                return false;
            }
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Starten des Python-Prozesses");
            LastError = ex.Message;
            process.Dispose();
            return false;
        }

        _process = process;
        var identityVerified = WindowsProcessIdentityReader.TryRead(process, out var identity);
        if (!identityVerified || identity is null)
        {
            try
            {
                var executablePath = process.MainModule?.FileName ?? _pythonExecutable;
                identity = new WindowsProcessIdentity(
                    process.Id,
                    Environment.ProcessId,
                    new DateTimeOffset(process.StartTime.ToUniversalTime()),
                    Path.GetFullPath(executablePath),
                    string.Empty);
                _logger.LogWarning(
                    "[ONNX-LIFECYCLE] Native identity read failed for new PID {Pid}; registering Unknown and allowing only health-confirmed graceful shutdown.",
                    process.Id);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException)
            {
                _logger.LogError(ex, "[ONNX-LIFECYCLE] Cannot record newly started Python PID {Pid}; refusing to terminate it.", process.Id);
                LastError = "Could not verify Python process identity";
                return false;
            }
        }

        var entry = CreateEntry(identity, arguments, _port, instanceId);
        if (!identityVerified)
        {
            entry.State = "Unknown";
        }

        _ownedEntry = entry;
        entries.Add(entry);

        if (!WaitForReady(process, entry, ct))
        {
            _logger.LogWarning("Python-Engine nicht bereit nach {Timeout}ms", _startupTimeoutMs);
            LastError = "Startup timeout or instance identity mismatch";
            StopVerifiedProcess(process, entry);
            if (process.HasExited)
            {
                entries.Remove(entry);
            }

            return false;
        }

        _logger.LogInformation("Python-Engine bereit (PID {Pid}, Instance {InstanceId})", process.Id, instanceId);
        LastError = null;
        return true;
    }

    private bool WaitForReady(Process process, EngineProcessEntry entry, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < _startupTimeoutMs && !ct.IsCancellationRequested)
        {
            if (process.HasExited)
            {
                _logger.LogError("Python-Prozess beendet sich während des Startups (ExitCode {Code})", process.ExitCode);
                return false;
            }

            if (TryReadHealth(entry.Port, out var health)
                && health is not null
                && health.ProcessId == entry.Pid
                && string.Equals(health.InstanceId, entry.InstanceId, StringComparison.Ordinal))
            {
                ApplyHealth(entry, health);
                return true;
            }

            Thread.Sleep(250);
        }
        return false;
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null)
        {
            _logger.LogDebug("[Python-stderr] {Line}", e.Data);
        }
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null)
        {
            _logger.LogDebug("[Python-stdout] {Line}", e.Data);
        }
    }

    /// <summary>
    /// Überprüft ob der Python-Prozess noch läuft. Falls nicht, versucht Restart.
    /// </summary>
    public async Task<bool> CheckAndRestartAsync(CancellationToken ct = default)
    {
        if (_shuttingDown || _disposed)
        {
            return false;
        }

        if (IsRunning)
        {
            return true;
        }

        if (_restartCount >= _maxRestarts)
        {
            _logger.LogError("Max. Restart-Anzahl ({Max}) erreicht. Python-Engine nicht mehr verfügbar.", _maxRestarts);
            return false;
        }

        _restartCount++;
        _logger.LogWarning("Python-Prozess beendet. Versuche Restart {Count}/{Max}...", _restartCount, _maxRestarts);

        await Task.Delay(_restartIntervalMs, ct);
        return await StartAsync(ct);
    }

    /// <summary>
    /// Beendet den Python-Prozess sauber.
    /// </summary>
    public async Task StopAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _shuttingDown = true;
            await StopHealthMonitorAsync();
            var process = _process;
            var entry = _ownedEntry;
            if (process is null || entry is null)
            {
                return;
            }

            var stopped = HasExited(process) || StopVerifiedProcess(process, entry);
            if (stopped)
            {
                try
                {
                    _registry.Remove(_instanceId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ONNX-LIFECYCLE] Failed to remove registry entry for PID {Pid}", entry.Pid);
                }
                _logger.LogInformation("[ONNX-LIFECYCLE] Python process PID {Pid} exited", entry.Pid);
            }
            else
            {
                _registry.Update(_instanceId, current => current.State = "Unknown");
            }

            process.Dispose();
            _process = null;
            _ownedEntry = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void SetModelState(string state, string? modelPath = null)
    {
        if (_instanceId == Guid.Empty)
        {
            return;
        }

        try
        {
            _registry.Update(_instanceId, entry =>
            {
                entry.State = state;
                entry.Loaded = state is "Loaded" or "Generating";
                entry.ModelPath = modelPath is null ? null : Path.GetFileName(modelPath);
                entry.LastHeartbeatUtc = DateTimeOffset.UtcNow;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ONNX-LIFECYCLE] Could not update registry state to {State}", state);
        }
    }

    private bool StopVerifiedProcess(Process process, EngineProcessEntry entry)
    {
        if (HasExited(process))
        {
            return true;
        }

        var healthConfirmed = TryReadHealth(entry.Port, out var health)
            && health is not null
            && HealthBelongsToEntry(health, entry);
        if (healthConfirmed)
        {
            _logger.LogInformation("[ONNX-LIFECYCLE] Requesting graceful shutdown for PID {Pid} on port {Port}.", entry.Pid, entry.Port);
            try
            {
                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.GracefulShutdownTimeoutSeconds))
                };
                using var response = client.PostAsync($"http://127.0.0.1:{entry.Port}/shutdown", new StringContent(string.Empty))
                    .GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[ONNX-LIFECYCLE] Graceful shutdown for PID {Pid} returned HTTP {Status}.", entry.Pid, (int)response.StatusCode);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "[ONNX-LIFECYCLE] Graceful shutdown request failed for PID {Pid}.", entry.Pid);
            }

            if (WaitForExit(process, _options.GracefulShutdownTimeoutSeconds))
            {
                _logger.LogInformation("[ONNX-LIFECYCLE] Process PID {Pid} exited gracefully.", entry.Pid);
                return true;
            }
        }
        else
        {
            _logger.LogWarning(
                "[ONNX-LIFECYCLE] Health identity for PID {Pid} could not be confirmed; skipping HTTP shutdown request.",
                entry.Pid);
        }

        if (HasExited(process))
        {
            return true;
        }

        if (!MatchesEntry(process, entry))
        {
            _logger.LogError(
                "[ONNX-LIFECYCLE] Refusing to kill PID {Pid}: process identity changed or cannot be verified.",
                entry.Pid);
            return false;
        }

        _logger.LogWarning(
            "[ONNX-LIFECYCLE] WARNING: engine PID {Pid} did not exit gracefully. Force terminating the verified engine process.",
            entry.Pid);
        try
        {
            process.Kill(entireProcessTree: false);
            if (WaitForExit(process, _options.ForceKillTimeoutSeconds))
            {
                _logger.LogInformation("[ONNX-LIFECYCLE] Force-terminated engine PID {Pid} exited.", entry.Pid);
                return true;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _logger.LogError(ex, "[ONNX-LIFECYCLE] Failed to terminate verified engine PID {Pid}.", entry.Pid);
        }

        entry.State = "Unknown";
        _logger.LogError("[ONNX-LIFECYCLE] Engine PID {Pid} is still alive; new engine start is blocked.", entry.Pid);
        return false;
    }

    private bool WaitForExit(Process process, int timeoutSeconds)
    {
        try
        {
            return process.WaitForExit(Math.Max(1, timeoutSeconds) * 1000) || process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private bool MatchesEntry(Process process, EngineProcessEntry entry)
    {
        return WindowsProcessIdentityReader.TryRead(process, out var identity)
            && identity is not null
            && MatchesEntry(identity, entry);
    }

    private bool MatchesEntry(WindowsProcessIdentity identity, EngineProcessEntry entry)
    {
        if (identity.ProcessId != entry.Pid
            || identity.ProcessStartTimeUtc != entry.ProcessStartTimeUtc
            || !PathsEqual(identity.ExecutablePath, entry.ExecutablePath)
            || !WindowsProcessIdentityReader.TrySplitCommandLine(identity.CommandLine, out var commandLine)
            || commandLine.Length < 2)
        {
            return false;
        }

        var arguments = commandLine.Skip(1).ToArray();
        return ComputeArgumentsHash(arguments) == entry.ArgumentsHash
            && TryGetEngineArguments(identity.CommandLine, out var actualArguments, out var port, out var instanceId)
            && port == entry.Port
            && arguments.SequenceEqual(actualArguments, StringComparer.Ordinal)
            && (entry.CommandLineInstanceId is null
                || string.Equals(entry.CommandLineInstanceId, instanceId, StringComparison.Ordinal));
    }

    private bool TryGetEngineArguments(
        string commandLine,
        out string[] arguments,
        out int port,
        out string? commandLineInstanceId)
    {
        arguments = [];
        port = 0;
        commandLineInstanceId = null;
        if (!WindowsProcessIdentityReader.TrySplitCommandLine(commandLine, out var commandLineParts)
            || commandLineParts.Length < 2)
        {
            return false;
        }

        arguments = commandLineParts.Skip(1).ToArray();
        string? module = null;
        for (var index = 0; index < arguments.Length - 1; index++)
        {
            if (arguments[index] == "-m")
            {
                module = arguments[index + 1];
            }
            else if (arguments[index] == "--port"
                && int.TryParse(arguments[index + 1], out var parsedPort))
            {
                port = parsedPort;
            }
            else if (arguments[index] == "--instance-id")
            {
                commandLineInstanceId = arguments[index + 1];
            }
        }

        return string.Equals(module, _engineModulePath, StringComparison.Ordinal)
            && port is > 0 and <= 65535
            && (commandLineInstanceId is null || Guid.TryParse(commandLineInstanceId, out _));
    }

    private bool IsConfiguredPythonExecutable(string executablePath)
    {
        if (Path.IsPathRooted(_pythonExecutable) || File.Exists(_pythonExecutable))
        {
            try
            {
                return PathsEqual(executablePath, Path.GetFullPath(_pythonExecutable));
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        return string.Equals(
            Path.GetFileName(executablePath),
            Path.GetFileName(_pythonExecutable),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private EngineProcessEntry CreateEntry(
        WindowsProcessIdentity identity,
        string[] arguments,
        int port,
        string? commandLineInstanceId)
    {
        var now = DateTimeOffset.UtcNow;
        return new EngineProcessEntry
        {
            InstanceId = Guid.TryParse(commandLineInstanceId, out var parsedInstanceId)
                ? parsedInstanceId.ToString("D")
                : Guid.NewGuid().ToString("D"),
            CommandLineInstanceId = commandLineInstanceId,
            Pid = identity.ProcessId,
            ParentPid = identity.ParentProcessId,
            ProcessStartTimeUtc = identity.ProcessStartTimeUtc,
            RegisteredAtUtc = now,
            LastHeartbeatUtc = now,
            Port = port,
            State = "Starting",
            ExecutablePath = identity.ExecutablePath,
            ArgumentsHash = ComputeArgumentsHash(arguments),
            Loaded = false,
            LastActivityUtc = now,
            EngineModule = _engineModulePath
        };
    }

    private static string ComputeArgumentsHash(IEnumerable<string> arguments)
    {
        var normalized = string.Join('\0', arguments);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private bool TryReadHealth(int port, out PythonEngineHealth? health)
    {
        health = null;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var response = client.GetAsync($"http://127.0.0.1:{port}/health").GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            using var document = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            var root = document.RootElement;
            var state = root.TryGetProperty("state", out var stateValue)
                ? stateValue.GetString() ?? "Unknown"
                : root.TryGetProperty("status", out var statusValue)
                    ? statusValue.GetString() ?? "Unknown"
                    : "Unknown";
            var loaded = root.TryGetProperty("loaded", out var loadedValue)
                ? loadedValue.GetBoolean()
                : root.TryGetProperty("ready", out var readyValue) && readyValue.GetBoolean();
            var lastActivity = root.TryGetProperty("last_activity_utc", out var activityValue)
                && DateTimeOffset.TryParse(activityValue.GetString(), out var parsedActivity)
                ? parsedActivity
                : DateTimeOffset.UtcNow;
            health = new PythonEngineHealth(
                root.TryGetProperty("process_id", out var pidValue) ? pidValue.GetInt32() : null,
                root.TryGetProperty("instance_id", out var instanceValue) ? instanceValue.GetString() : null,
                state,
                loaded,
                lastActivity);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool HealthBelongsToEntry(PythonEngineHealth health, EngineProcessEntry entry)
    {
        if (health.ProcessId.HasValue && health.ProcessId.Value != entry.Pid)
        {
            return false;
        }

        if (entry.CommandLineInstanceId is not null
            && !string.Equals(health.InstanceId, entry.CommandLineInstanceId, StringComparison.Ordinal))
        {
            return false;
        }
        return health.ProcessId.HasValue
            || (health.InstanceId is null && entry.CommandLineInstanceId is null);
    }

    private void RefreshEntryFromHealth(EngineProcessEntry entry)
    {
        if (TryReadHealth(entry.Port, out var health) && health is not null && HealthBelongsToEntry(health, entry))
        {
            ApplyHealth(entry, health);
        }
        else
        {
            entry.State = "Unknown";
        }
    }

    private static void ApplyHealth(EngineProcessEntry entry, PythonEngineHealth health)
    {
        entry.State = health.State;
        entry.Loaded = health.Loaded;
        entry.LastActivityUtc = health.LastActivityUtc;
        entry.LastHeartbeatUtc = DateTimeOffset.UtcNow;
    }

    private void StartHealthMonitor()
    {
        _healthMonitorCancellation = new CancellationTokenSource();
        _healthMonitorTask = Task.Run(() => HealthMonitorAsync(_healthMonitorCancellation.Token));
    }

    private async Task HealthMonitorAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.HealthCheckIntervalSeconds)));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                var process = _process;
                var entry = _ownedEntry;
                if (process is null || entry is null)
                {
                    return;
                }

                if (HasExited(process))
                {
                    _registry.Remove(_instanceId);
                    return;
                }
                if (TryReadHealth(entry.Port, out var health)
                    && health is not null
                    && HealthBelongsToEntry(health, entry))
                {
                    _registry.Update(_instanceId, current => ApplyHealth(current, health));
                }
                else
                {
                    _registry.Update(_instanceId, current => current.State = "Unknown");
                    _logger.LogWarning("[ONNX-LIFECYCLE] Health check failed for live engine PID {Pid}; state is Unknown.", entry.Pid);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task StopHealthMonitorAsync()
    {
        var cancellation = _healthMonitorCancellation;
        var task = _healthMonitorTask;
        _healthMonitorCancellation = null;
        _healthMonitorTask = null;
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        if (task is not null)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
        }
        cancellation.Dispose();
    }

    private void OnOwnedProcessExited(string instanceId, int processId)
    {
        _logger.LogInformation("[ONNX-LIFECYCLE] Engine process PID {Pid} exited; removing registry entry.", processId);
        if (Guid.TryParse(instanceId, out var parsedInstanceId))
        {
            try
            {
                _registry.Remove(parsedInstanceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ONNX-LIFECYCLE] Could not remove exited process PID {Pid} from registry.", processId);
            }
        }
    }

    private static bool TryGetProcess(int processId, out Process? process)
    {
        try
        {
            process = Process.GetProcessById(processId);
            return true;
        }
        catch (ArgumentException)
        {
            process = null;
            return false;
        }
        catch (InvalidOperationException)
        {
            process = null;
            return false;
        }
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed record PythonEngineHealth(
        int? ProcessId,
        string? InstanceId,
        string State,
        bool Loaded,
        DateTimeOffset LastActivityUtc);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAsync();
        _lock.Dispose();
    }
}
