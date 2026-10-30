using System.Text;
using System.Text.Json;

namespace FormsSystemStatsWidget.OnnxGenaiServer.Engine;

/// <summary>
/// IPC-Client für die Python-Engine. Kommuniziert via JSON-over-HTTP.
/// Endpunkte: /load, /unload, /generate (SSE-streaming), /health, /shutdown
/// </summary>
public sealed class PythonIpcClient : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly string _baseUrl;

    public PythonIpcClient(ILogger logger, string baseUrl)
    {
        _logger = logger;
        _baseUrl = baseUrl;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    /// <summary>
    /// Lädt das Modell in der Python-Engine (inkl. Ausführungs-Tunables).
    /// </summary>
    public async Task<bool> LoadModelAsync(string modelPath, string modelLayout = "Auto", int contextLength = 0, OnnxExecutionOptions? execution = null, CancellationToken ct = default)
    {
        try
        {
            execution ??= new OnnxExecutionOptions();
            var payload = new
            {
                model_path = modelPath,
                model_layout = modelLayout.ToLowerInvariant(),
                context_length = Math.Max(0, contextLength),
                provider = execution.Provider,
                allow_cpu_fallback = execution.AllowCpuFallback,
                stage0_device = execution.Stage0Device,
                stage1_device = execution.Stage1Device,
                max_concurrent_generations = Math.Max(1, execution.MaxConcurrentGenerations),
                session_options = new
                {
                    graph_optimization_level = execution.GraphOptimization,
                    execution_mode = execution.ExecutionMode,
                    intra_op_num_threads = execution.IntraOpThreads,
                    inter_op_num_threads = execution.InterOpThreads,
                    enable_mem_pattern = execution.EnableMemPattern,
                    enable_cpu_mem_arena = execution.EnableCpuMemArena,
                    enable_profiling = execution.EnableProfiling,
                    disable_prepacking = execution.DisablePrepacking,
                },
                cuda_options = new
                {
                    arena_extend_strategy = execution.ArenaExtendStrategy,
                    gpu_mem_limit = execution.GpuMemLimitBytes,
                    cudnn_conv_algo_search = execution.CudnnConvAlgoSearch,
                    do_copy_in_default_stream = execution.CopyInDefaultStream,
                    enable_cuda_graph = execution.UseCudaGraphs,
                    use_tf32 = execution.UseTf32,
                },
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync($"{_baseUrl}/load", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "Python-Engine Load fehlgeschlagen (HTTP {StatusCode}): {ResponseBody}",
                    (int)response.StatusCode,
                    errorContent);
                return false;
            }
            _logger.LogInformation("Modell geladen: {Path}", modelPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden des Modells: {Path}", modelPath);
            return false;
        }
    }

    /// <summary>
    /// Entlädt das Modell aus der Python-Engine.
    /// </summary>
    public async Task<bool> UnloadModelAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/unload", new StringContent(""), ct);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Modell entladen");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Entladen des Modells");
            return false;
        }
    }

    /// <summary>
    /// Prüft ob die Python-Engine erreichbar ist.
    /// </summary>
    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Startet eine Generation und streamt die Ergebnisse.
    /// </summary>
    public IAsyncEnumerable<PythonGenerationResult> GenerateAsync(
        string prompt,
        GenerationParameters parameters,
        CancellationToken ct = default)
    {
        var payload = new
        {
            prompt,
            temperature = parameters.Temperature,
            top_p = parameters.TopP,
            typical_p = parameters.TypicalP,
            top_k = parameters.TopK,
            max_tokens = parameters.MaxNewTokens,
            repeat_penalty = parameters.RepeatPenalty,
            repeat_last_n = parameters.RepeatLastN,
            min_p = parameters.MinP,
            presence_penalty = parameters.PresencePenalty,
            frequency_penalty = parameters.FrequencyPenalty,
            seed = parameters.Seed,
            stop = parameters.StopSequences,
            stream = true
        };
        return GenerateCoreAsync(payload, ct);
    }

    public IAsyncEnumerable<PythonGenerationResult> GenerateChatAsync(
        IReadOnlyList<PythonChatMessage> messages,
        GenerationParameters parameters,
        bool enableThinking = false,
        CancellationToken ct = default)
    {
        var payload = new
        {
            messages = messages.Select(message => new
            {
                role = message.Role,
                content = message.Content,
                name = message.Name
            }),
            enable_thinking = enableThinking,
            temperature = parameters.Temperature,
            top_p = parameters.TopP,
            typical_p = parameters.TypicalP,
            top_k = parameters.TopK,
            max_tokens = parameters.MaxNewTokens,
            repeat_penalty = parameters.RepeatPenalty,
            repeat_last_n = parameters.RepeatLastN,
            min_p = parameters.MinP,
            presence_penalty = parameters.PresencePenalty,
            frequency_penalty = parameters.FrequencyPenalty,
            seed = parameters.Seed,
            stop = parameters.StopSequences,
            stream = true
        };
        return GenerateCoreAsync(payload, ct);
    }

    private async IAsyncEnumerable<PythonGenerationResult> GenerateCoreAsync(
        object payload,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage? response = null;
        bool httpError = false;
        bool cancelled = false;
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/generate")
            {
                Content = content
            };
            response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                ct);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Python-Engine nicht erreichbar: {Url}", _baseUrl);
            httpError = true;
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            cancelled = true;
        }

        if (httpError)
        {
            yield return new PythonGenerationResult(string.Empty, 0, 0, "error");
            yield break;
        }
        if (cancelled)
        {
            yield return new PythonGenerationResult(string.Empty, 0, 0, "cancelled");
            yield break;
        }

        using (response!)
        {
            var stream = await response.Content.ReadAsStreamAsync(ct);
            using (stream)
            {
                var reader = new StreamReader(stream);
                using (reader)
                {
                    var sb = new StringBuilder();
                    var promptTokens = 0;
                    var completionTokens = 0;
                    var lastFinishReason = "stop";
                    var finishReported = false;
                    GenerationTimings? lastTimings = null;

                    string? line;
                    while ((line = await reader.ReadLineAsync(ct)) != null)
                    {
                        if (ct.IsCancellationRequested) { lastFinishReason = "cancelled"; break; }
                        if (!line.StartsWith("data: ", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var data = line["data: ".Length..].Trim();
                        if (data == "[DONE]")
                        {
                            break;
                        }

                        string? chunkText = null;
                        string? chunkFinishReason = null;
                        try
                        {
                            using var doc = JsonDocument.Parse(data);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("usage", out var usage))
                            {
                                promptTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : promptTokens;
                                completionTokens = usage.TryGetProperty("completion_tokens", out var ct2) ? ct2.GetInt32() : completionTokens;
                            }
                            if (root.TryGetProperty("timings", out var timings) && timings.ValueKind == JsonValueKind.Object)
                            {
                                lastTimings = ParseTimings(timings);
                            }
                            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                            {
                                var choice = choices[0];
                                if (choice.TryGetProperty("text", out var textElement))
                                {
                                    chunkText = textElement.GetString();
                                }
                                else if (choice.TryGetProperty("delta", out var delta)
                                    && delta.ValueKind == JsonValueKind.Object
                                    && delta.TryGetProperty("content", out var contentElement))
                                {
                                    chunkText = contentElement.GetString();
                                }
                                chunkFinishReason = choice.TryGetProperty("finish_reason", out var fr) ? fr.GetString() : null;
                            }
                        }
                        catch (JsonException) { continue; }

                        if (chunkText is not null)
                        {
                            sb.Append(chunkText);
                        }

                        if (chunkFinishReason is not null)
                        {
                            lastFinishReason = chunkFinishReason;
                            finishReported = true;
                            yield return new PythonGenerationResult(sb.ToString(), promptTokens, completionTokens, chunkFinishReason, lastTimings);
                        }
                        else
                        {
                            yield return new PythonGenerationResult(sb.ToString(), promptTokens, completionTokens, "ongoing");
                        }
                    }

                    if (sb.Length > 0 && !finishReported)
                    {
                        yield return new PythonGenerationResult(sb.ToString(), promptTokens, completionTokens, lastFinishReason, lastTimings);
                    }
                }
            }
        }
    }

    private static GenerationTimings? ParseTimings(JsonElement timings)    {
        try
        {
            static int GetInt(JsonElement e, string name) =>
                e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
            static double GetDouble(JsonElement e, string name) =>
                e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0.0;

            return new GenerationTimings(
                GetInt(timings, "prompt_tokens"),
                GetInt(timings, "completion_tokens"),
                GetInt(timings, "context_tokens"),
                GetDouble(timings, "ttft_ms"),
                GetDouble(timings, "decode_ms"),
                GetDouble(timings, "total_ms"),
                GetDouble(timings, "prompt_tps"),
                GetDouble(timings, "gen_tps"));
        }
        catch
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        await Task.CompletedTask;
    }
}

/// <summary>
/// Ausführungs-Tunables für /load (Session-/CUDA-Optionen, Devices, Queue).
/// </summary>
public sealed record OnnxExecutionOptions
{
    public string Provider { get; init; } = "cuda";
    public bool AllowCpuFallback { get; init; }
    public int Stage0Device { get; init; }
    public int Stage1Device { get; init; } = 1;
    public int MaxConcurrentGenerations { get; init; } = 1;
    public int IntraOpThreads { get; init; }
    public int InterOpThreads { get; init; }
    public string ExecutionMode { get; init; } = "sequential";
    public string GraphOptimization { get; init; } = "all";
    public bool EnableMemPattern { get; init; } = true;
    public bool EnableCpuMemArena { get; init; } = true;
    public bool EnableProfiling { get; init; }
    public bool DisablePrepacking { get; init; }
    public string ArenaExtendStrategy { get; init; } = "kNextPowerOfTwo";
    public long GpuMemLimitBytes { get; init; }
    public string CudnnConvAlgoSearch { get; init; } = "EXHAUSTIVE";
    public bool CopyInDefaultStream { get; init; } = true;
    public bool UseCudaGraphs { get; init; }
    public bool UseTf32 { get; init; } = true;
};

/// <summary>
/// Ergebnis einer Python-Generation.
/// </summary>
public sealed record PythonGenerationResult(string Text, int PromptTokens, int CompletionTokens, string FinishReason, GenerationTimings? Timings = null);

/// <summary>
/// Per-generation timings from the Python engine (PP/TG, TTFT, context size).
/// </summary>
public sealed record GenerationTimings(
    int PromptTokens,
    int CompletionTokens,
    int ContextTokens,
    double TtftMs,
    double DecodeMs,
    double TotalMs,
    double PromptTps,
    double GenTps);

public sealed record PythonChatMessage(string Role, string? Content, string? Name);
