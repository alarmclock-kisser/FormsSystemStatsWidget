using System.Text;
using System.Text.Json;
using FormsSystemStatsWidget.OnnxGenaiServer.OpenAi;

namespace FormsSystemStatsWidget.OnnxGenaiServer.Engine;

public sealed class OnnxGenaiEngine : IAsyncDisposable
{
    private readonly OnnxGenaiServerOptions _options;
    private readonly ILogger<OnnxGenaiEngine> _logger;
    private readonly HttpClient _httpClient;
    private string? _loadedModelId;
    private string _pythonServerBaseUrl = string.Empty;

    public DateTime StartTimeUtc { get; } = DateTime.UtcNow;
    public bool IsReady { get; private set; }
    public string? LastError { get; private set; }
    public OnnxGenaiServerOptions Options => _options;
    public string? LoadedModelId => _loadedModelId;

    public OnnxGenaiEngine(OnnxGenaiServerOptions options, ILogger<OnnxGenaiEngine> logger)
    {
        _options = options;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
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
            _pythonServerBaseUrl = _options.PythonServerUrl ?? "http://localhost:8080";
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

        var requestBody = new
        {
            model = _loadedModelId, prompt,
            temperature = parameters.Temperature, top_p = parameters.TopP,
            top_k = parameters.TopK, max_tokens = parameters.MaxNewTokens,
            repeat_penalty = parameters.RepeatPenalty, stream = true
        };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // HTTP-Aufruf: Fehlerbehandlung OHNE yield in catch
        HttpResponseMessage? response = null;
        bool httpError = false;
        try
        {
            response = await _httpClient.PostAsync($"{_pythonServerBaseUrl}/v1/completions", content, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Python-ONNX-Server nicht erreichbar: {Url}", _pythonServerBaseUrl);
            httpError = true;
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            httpError = true;
        }

        if (httpError)
        {
            yield return new GenerationResult(string.Empty, 0, 0, "error");
            yield break;
        }

        // Stream lesen (response ist hier nicht null)
        var resp = response!;
        using (resp)
        {
            var stream = await resp.Content.ReadAsStreamAsync(ct);
            using (stream)
            {
                var reader = new StreamReader(stream);
                using (reader)
                {
                    var sb = new StringBuilder();
                    var promptTokens = 0;
                    var completionTokens = 0;
                    var lastFinishReason = "stop";

                    string? line;
                    while ((line = await reader.ReadLineAsync(ct)) != null)
                    {
                        if (ct.IsCancellationRequested) { lastFinishReason = "length"; break; }
                        if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;
                        var data = line["data: ".Length..].Trim();
                        if (data == "[DONE]") break;

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
                            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                            {
                                var choice = choices[0];
                                chunkText = choice.TryGetProperty("text", out var t) ? t.GetString() : null;
                                chunkFinishReason = choice.TryGetProperty("finish_reason", out var fr) ? fr.GetString() : null;
                            }
                        }
                        catch (JsonException) { continue; }

                        if (chunkText is not null) sb.Append(chunkText);

                        if (chunkFinishReason is not null)
                        {
                            lastFinishReason = chunkFinishReason;
                            yield return new GenerationResult(sb.ToString(), promptTokens, completionTokens, chunkFinishReason);
                        }
                        else
                        {
                            yield return new GenerationResult(sb.ToString(), promptTokens, completionTokens, "ongoing");
                        }
                    }

                    if (sb.Length > 0)
                    {
                        yield return new GenerationResult(sb.ToString(), promptTokens, completionTokens, lastFinishReason);
                    }
                }
            }
        }
    }

    public async Task<float[]?> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        if (!IsReady) return null;
        var requestBody = new { model = _loadedModelId, input = text };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        try
        {
            using var response = await _httpClient.PostAsync($"{_pythonServerBaseUrl}/v1/embeddings", content, ct);
            if (!response.IsSuccessStatusCode) return null;
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
            {
                var embedding = data[0].GetProperty("embedding");
                return embedding.EnumerateArray().Select(e => e.GetSingle()).ToArray();
            }
            return null;
        }
        catch { return null; }
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        await Task.CompletedTask;
    }
}

public sealed class GenerationParameters
{
    public float Temperature { get; init; } = 0.8f;
    public float TopP { get; init; } = 0.9f;
    public int TopK { get; init; } = 40;
    public int MaxNewTokens { get; init; } = 1024;
    public float RepeatPenalty { get; init; } = 1.1f;
}
