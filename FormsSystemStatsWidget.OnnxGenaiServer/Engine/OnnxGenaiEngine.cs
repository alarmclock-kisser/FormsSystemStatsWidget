using System.Text;
using System.Text.Json;
using FormsSystemStatsWidget.OnnxGenaiServer.OpenAi;

namespace FormsSystemStatsWidget.OnnxGenaiServer.Engine;

public sealed class OnnxGenaiEngine : IAsyncDisposable
{
    private readonly OnnxGenaiServerOptions _options;
    private readonly ILogger<OnnxGenaiEngine> _logger;
    private readonly HttpClient _httpClient;
    private readonly PythonProcessSupervisor _pythonSupervisor;
    private readonly PythonIpcClient _pythonIpc;
    private string? _loadedModelId;
    private string _pythonServerBaseUrl = string.Empty;

    public DateTime StartTimeUtc { get; } = DateTime.UtcNow;
    public bool IsReady { get; private set; }
    public string? LastError { get; private set; }
    public OnnxGenaiServerOptions Options => _options;
    public string? LoadedModelId => _loadedModelId;
    public bool IsPythonEngineRunning => _pythonSupervisor.IsRunning;
    public int PythonRestartCount => _pythonSupervisor.RestartCount;

    public OnnxGenaiEngine(OnnxGenaiServerOptions options, ILogger<OnnxGenaiEngine> logger)
    {
        _options = options;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        // Python-Process-Supervision + IPC
        _pythonSupervisor = new PythonProcessSupervisor(_logger, options);
        _pythonIpc = new PythonIpcClient(_logger, _pythonSupervisor.BaseUrl);
    }

    public sealed record ModelEntry(string Id, string RootDir, string OnnxPath, IReadOnlyList<string> JsonFiles);

    public List<ModelEntry> DiscoverModels()
    {
        var result = new List<ModelEntry>();
        var root = _options.ModelRootDirectory;
        if (!Directory.Exists(root))
        {
            _logger.LogWarning("ModelRootDirectory existiert nicht: {Root}", root);
            return result;
        }
        foreach (var subDir in Directory.EnumerateDirectories(root))
        {
            var id = Path.GetFileName(subDir);
            var onnxFiles = Directory.EnumerateFiles(subDir, "*.onnx", SearchOption.TopDirectoryOnly)
                .Where(f => !f.EndsWith(".onnx.data", StringComparison.OrdinalIgnoreCase)).ToList();
            if (onnxFiles.Count == 0) continue;
            var jsonFiles = Directory.EnumerateFiles(subDir, "*.json", SearchOption.TopDirectoryOnly).ToList();
            if (jsonFiles.Count == 0) continue;
            result.Add(new ModelEntry(id, subDir, onnxFiles[0], jsonFiles));
        }
        return result;
    }

    public IReadOnlyList<string> ListModelIds() => DiscoverModels().Select(m => m.Id).ToList();

    public ModelsResponse BuildModelsResponse()
    {
        var models = DiscoverModels();
        return new ModelsResponse
        {
            Data = models.Select(m => new ModelInfo
            {
                Id = m.Id,
                Created = (long)(Directory.GetLastWriteTimeUtc(m.RootDir) - DateTime.UnixEpoch).TotalSeconds
            }).ToList()
        };
    }

    public async Task InitializeAsync()
    {
        try
        {
            var models = DiscoverModels();
            if (models.Count == 0)
            {
                LastError = $"Keine validen Modell-Rootdirs in {_options.ModelRootDirectory}";
                _logger.LogError(LastError);
                return;
            }
            var model = models.FirstOrDefault(m => m.Id.Equals(_options.DefaultModel, StringComparison.OrdinalIgnoreCase)) ?? models[0];
            _loadedModelId = model.Id;

            // R3: Stage Loading Orchestrierung — Partition-Discovery + Validation
            var cudaOpts = new CudaOptions
            {
                Enabled = _options.ExecutionProvider.Equals("Cuda", StringComparison.OrdinalIgnoreCase),
                Stage0Device = 0,
                Stage1Device = 1
            };
            var partition = ModelPartitioner.Discover(model.RootDir, cudaOpts, _logger);
            if (partition.IsPartitioned)
            {
                var validation = PartitionValidator.ValidatePartition(model.RootDir, partition, _logger);
                if (!validation.IsValid)
                {
                    _logger.LogWarning("Partition-Validierung mit {Count} Errors: {Issues}",
                        validation.Errors.Count, string.Join("; ", validation.Errors.Select(e => e.Message)));
                }
                _logger.LogInformation("Partition: Stage0={S0} (GPU{D0}), Stage1={S1} (GPU{D1}), {BT} Boundary-Tensoren",
                    Path.GetFileName(partition.Stage0Model), partition.Stage0Device,
                    Path.GetFileName(partition.Stage1Model), partition.Stage1Device,
                    partition.BoundaryTensors.Count);
            }
            else
            {
                _logger.LogInformation("Modell {Id} ist nicht partitioniert — Single-Stage-Modus", model.Id);
            }

            // Phase 3b: Python-Process-Supervision + IPC
            _logger.LogInformation("Starte Python-Engine-Prozess...");
            var pythonReady = await _pythonSupervisor.StartAsync();
            if (!pythonReady)
            {
                _logger.LogWarning("Python-Engine nicht gestartet. Fallback zu externem Python-Server.");
                _pythonServerBaseUrl = _options.PythonServerUrl ?? "http://localhost:8080";
            }
            else
            {
                _pythonServerBaseUrl = _pythonSupervisor.BaseUrl;
                _logger.LogInformation("Lade Modell in Python-Engine: {Path}", model.RootDir);
                _pythonSupervisor.SetModelState("Loading", model.RootDir);
                var loaded = await _pythonIpc.LoadModelAsync(model.RootDir);
                _pythonSupervisor.SetModelState(loaded ? "Loaded" : "Unloaded", model.RootDir);
                if (!loaded)
                {
                    _logger.LogWarning("Modell konnte nicht in Python-Engine geladen werden");
                    LastError = $"Python engine failed to load model: {model.RootDir}";
                    return;
                }
            }

            _logger.LogInformation("ONNX GenAI Engine initialisiert. Modell: {Id}, Python-Server: {Url}", model.Id, _pythonServerBaseUrl);
            IsReady = true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _logger.LogError(ex, "Fehler bei der Initialisierung");
        }
        await Task.CompletedTask;
    }

    public sealed record GenerationResult(string Text, int PromptTokens, int CompletionTokens, string FinishReason);

    public async IAsyncEnumerable<GenerationResult> GenerateAsync(
        string prompt, GenerationParameters parameters,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!IsReady)
        {
            yield return new GenerationResult(string.Empty, 0, 0, "error");
            yield break;
        }

        // Phase 3b: Forward request to Python-Engine via IPC
        await foreach (var result in _pythonIpc.GenerateAsync(prompt, parameters, ct))
        {
            yield return new GenerationResult(result.Text, result.PromptTokens, result.CompletionTokens, result.FinishReason);
        }
    }

    public async IAsyncEnumerable<GenerationResult> GenerateChatAsync(
        IReadOnlyList<PythonChatMessage> messages,
        GenerationParameters parameters,
        bool enableThinking = false,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!IsReady)
        {
            yield return new GenerationResult(string.Empty, 0, 0, "error");
            yield break;
        }

        await foreach (var result in _pythonIpc.GenerateChatAsync(messages, parameters, enableThinking, ct))
        {
            yield return new GenerationResult(result.Text, result.PromptTokens, result.CompletionTokens, result.FinishReason);
        }
    }

    /// <summary>
    /// Liefert ein Embedding für den gegebenen Text, oder null, wenn das geladene
    /// Modell keine Embeddings unterstützt (z. B. Qwen3.8-27B).
    /// </summary>
    public Task<float[]?> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        // Qwen3.8-27B ist ein generatives Sprachmodell ohne Embedding-Head.
        // Die OpenAI-API-Route /v1/embeddings gibt 501 zurück.
        return Task.FromResult<float[]?>(null);
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Beende Python-Engine-Prozess...");
        await _pythonSupervisor.DisposeAsync();
        await _pythonIpc.DisposeAsync();
        _httpClient.Dispose();
        _logger.LogInformation("ONNX GenAI Engine disposed");
        await Task.CompletedTask;
    }
}

public sealed class GenerationParameters
{
    public float Temperature { get; init; } = 0.8f;
    public float TopP { get; init; } = 0.9f;
    public float TypicalP { get; init; } = 1.0f;
    public int TopK { get; init; } = 40;
    public int MaxNewTokens { get; init; } = 1024;
    public float RepeatPenalty { get; init; } = 1.1f;
    public float MinP { get; init; }
    public float PresencePenalty { get; init; }
    public float FrequencyPenalty { get; init; }
    public int? Seed { get; init; }
}

public sealed class EngineInfo
{
    public string EngineVersion { get; set; } = "1.0.0";
    public bool IsReady { get; set; }
    public string? LoadedModelId { get; set; }
    public int Stage0Device { get; set; }
    public int Stage1Device { get; set; }
    public int[] AvailableCudaDevices { get; set; } = Array.Empty<int>();
    public string ExecutionProvider { get; set; } = "Dml";
}

public sealed class EngineDiagnostics
{
    public string EngineVersion { get; set; } = "1.0.0";
    public string RuntimeVersion { get; set; } = "";
    public string OS { get; set; } = "";
    public string CPU { get; set; } = "";
    public long RAM { get; set; } = 0;
    public string CUDAVersion { get; set; } = "";
    public int CUDADeviceCount { get; set; } = 0;
    public string ModelInfo { get; set; } = "";
    public string PartitionInfo { get; set; } = "";
    public string ProviderInfo { get; set; } = "";
    public string MemoryInfo { get; set; } = "";
    public string Warnings { get; set; } = "";
    public Dictionary<string, object> CustomFields { get; set; } = new();
}