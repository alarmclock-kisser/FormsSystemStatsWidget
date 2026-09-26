using System.Text.Json;

namespace FormsSystemStatsWidget.OnnxGenaiServer.Engine;

internal sealed class EngineProcessRegistry
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _registryPath;
    private readonly string _mutexName;
    private readonly TimeSpan _lockTimeout;
    private readonly ILogger _logger;

    public EngineProcessRegistry(string? configuredPath, int lockTimeoutSeconds, ILogger logger)
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(configuredPath) && string.IsNullOrWhiteSpace(localApplicationData))
            throw new InvalidOperationException("LocalApplicationData is unavailable for the ONNX process registry.");

        _registryPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(localApplicationData, "FormsSystemStatsWidget", "onnx-engine", "engine-processes.json")
            : Path.GetFullPath(configuredPath);
        _mutexName = OperatingSystem.IsWindows()
            ? @"Global\FormsSystemStatsWidget.OnnxEngine.ProcessRegistry"
            : "FormsSystemStatsWidget.OnnxEngine.ProcessRegistry";
        _lockTimeout = TimeSpan.FromSeconds(Math.Max(1, lockTimeoutSeconds));
        _logger = logger;
    }

    public string RegistryPath => _registryPath;

    public T ExecuteLocked<T>(Func<List<EngineProcessEntry>, T> operation)
    {
        using var mutex = new Mutex(false, _mutexName);
        var ownsMutex = false;
        try
        {
            try
            {
                ownsMutex = mutex.WaitOne(_lockTimeout);
            }
            catch (AbandonedMutexException)
            {
                ownsMutex = true;
                _logger.LogWarning("[ONNX-LIFECYCLE] Registry mutex was abandoned; recovering under exclusive lock.");
            }

            if (!ownsMutex)
                throw new TimeoutException($"Timed out waiting for ONNX process registry lock {_mutexName}.");

            var entries = ReadEntries();
            var result = operation(entries);
            WriteEntries(entries);
            return result;
        }
        finally
        {
            if (ownsMutex)
                mutex.ReleaseMutex();
        }
    }

    public void Remove(Guid instanceId)
    {
        ExecuteLocked(entries =>
        {
            entries.RemoveAll(entry => entry.InstanceId == instanceId.ToString("D"));
            return true;
        });
    }

    public void Update(Guid instanceId, Action<EngineProcessEntry> update)
    {
        ExecuteLocked(entries =>
        {
            var entry = entries.FirstOrDefault(candidate => candidate.InstanceId == instanceId.ToString("D"));
            if (entry is not null)
                update(entry);
            return true;
        });
    }

    private List<EngineProcessEntry> ReadEntries()
    {
        if (!File.Exists(_registryPath))
            return [];

        try
        {
            using var stream = File.OpenRead(_registryPath);
            var document = JsonSerializer.Deserialize<EngineProcessRegistryDocument>(stream, SerializerOptions);
            if (document is null || document.Version != 1 || document.Processes is null)
                throw new JsonException("Unsupported or incomplete registry document.");
            return document.Processes;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "[ONNX-LIFECYCLE] Registry file is corrupt or unreadable: {Path}", _registryPath);
            try
            {
                var recoveryPath = $"{_registryPath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
                File.Move(_registryPath, recoveryPath, overwrite: true);
                _logger.LogWarning("[ONNX-LIFECYCLE] Preserved corrupt registry as {RecoveryPath}", recoveryPath);
            }
            catch (Exception recoveryException) when (recoveryException is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(recoveryException, "[ONNX-LIFECYCLE] Could not preserve corrupt registry file.");
            }
            return [];
        }
    }

    private void WriteEntries(List<EngineProcessEntry> entries)
    {
        var directory = Path.GetDirectoryName(_registryPath)
            ?? throw new InvalidOperationException("The registry path has no parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{_registryPath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            var document = new EngineProcessRegistryDocument { Processes = entries };
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, document, SerializerOptions);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, _registryPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}

internal sealed class EngineProcessRegistryDocument
{
    public int Version { get; set; } = 1;
    public List<EngineProcessEntry> Processes { get; set; } = [];
}

internal sealed class EngineProcessEntry
{
    public string InstanceId { get; set; } = string.Empty;
    public int Pid { get; set; }
    public int ParentPid { get; set; }
    public DateTimeOffset ProcessStartTimeUtc { get; set; }
    public DateTimeOffset RegisteredAtUtc { get; set; }
    public DateTimeOffset LastHeartbeatUtc { get; set; }
    public int Port { get; set; }
    public string State { get; set; } = "Starting";
    public string? ModelPath { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public string ArgumentsHash { get; set; } = string.Empty;
    public string? CommandLineInstanceId { get; set; }
    public bool Loaded { get; set; }
    public DateTimeOffset LastActivityUtc { get; set; }
    public string EngineModule { get; set; } = string.Empty;
}