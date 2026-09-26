using System.Diagnostics;
using System.Text;

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

    private Process? _process;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;
    private bool _shuttingDown;
    private int _restartCount;

    public bool IsRunning => _process is { HasExited: false };
    public int RestartCount => _restartCount;
    public string? LastError { get; private set; }

    public PythonProcessSupervisor(
        ILogger logger,
        string pythonExecutable,
        string engineModulePath,
        int port = 8081,
        int startupTimeoutMs = 30000,
        int maxRestarts = 3,
        int restartIntervalMs = 5000)
    {
        _logger = logger;
        _pythonExecutable = pythonExecutable;
        _engineModulePath = engineModulePath;
        _port = port;
        _startupTimeoutMs = startupTimeoutMs;
        _maxRestarts = maxRestarts;
        _restartIntervalMs = restartIntervalMs;
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
            if (IsRunning)
                return true;

            _logger.LogInformation("Starte Python-Engine: {Python} {Module} (Port {Port})",
                _pythonExecutable, _engineModulePath, _port);

            var psi = new ProcessStartInfo
            {
                FileName = _pythonExecutable,
                Arguments = $"-m {_engineModulePath} --port {_port}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_engineModulePath) ?? "."
            };

            _process = new Process { StartInfo = psi };

            _process.ErrorDataReceived += OnErrorDataReceived;
            _process.OutputDataReceived += OnOutputDataReceived;

            try
            {
                _process.Start();
                _process.BeginErrorReadLine();
                _process.BeginOutputReadLine();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Starten des Python-Prozesses");
                LastError = ex.Message;
                return false;
            }

            // Warten bis der Server bereit ist
            var ready = await WaitForReadyAsync(ct);
            if (!ready)
            {
                _logger.LogWarning("Python-Engine nicht bereit nach {Timeout}ms", _startupTimeoutMs);
                LastError = "Startup timeout";
                await StopAsync();
                return false;
            }

            _logger.LogInformation("Python-Engine bereit (PID {Pid})", _process.Id);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<bool> WaitForReadyAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < _startupTimeoutMs && !ct.IsCancellationRequested)
        {
            if (_process is { HasExited: true })
            {
                _logger.LogError("Python-Prozess beendet sich während des Startups (ExitCode {Code})", _process.ExitCode);
                return false;
            }

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var response = await client.GetAsync($"{BaseUrl}/health", ct);
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch (HttpRequestException)
            {
                // Server noch nicht erreichbar
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout, weiter versuchen
            }

            await Task.Delay(500, ct);
        }
        return false;
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null)
            _logger.LogDebug("[Python-stderr] {Line}", e.Data);
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is not null)
            _logger.LogDebug("[Python-stdout] {Line}", e.Data);
    }

    /// <summary>
    /// Überprüft ob der Python-Prozess noch läuft. Falls nicht, versucht Restart.
    /// </summary>
    public async Task<bool> CheckAndRestartAsync(CancellationToken ct = default)
    {
        if (_shuttingDown || _disposed)
            return false;

        if (IsRunning)
            return true;

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
            if (_process is not null)
            {
                try
                {
                    // Versuche sauberes Shutdown via HTTP
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    await client.PostAsync($"{BaseUrl}/shutdown", new StringContent(""));
                }
                catch
                {
                    // Ignoriere Fehler beim sauberen Shutdown
                }

                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill(entireProcessTree: true);
                        _process.WaitForExit(5000);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Fehler beim Killen des Python-Prozesses");
                }

                _process.Dispose();
                _process = null;
                _logger.LogInformation("Python-Prozess beendet");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await StopAsync();
        _lock.Dispose();
    }
}
