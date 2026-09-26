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
    /// Lädt das Modell in der Python-Engine.
    /// </summary>
    public async Task<bool> LoadModelAsync(string modelPath, CancellationToken ct = default)
    {
        try
        {
            var payload = new { model_path = modelPath };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/load", content, ct);
            response.EnsureSuccessStatusCode();
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
    public async IAsyncEnumerable<PythonGenerationResult> GenerateAsync(
        string prompt,
        GenerationParameters parameters,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new
        {
            prompt,
            temperature = parameters.Temperature,
            top_p = parameters.TopP,
            top_k = parameters.TopK,
            max_tokens = parameters.MaxNewTokens,
            repeat_penalty = parameters.RepeatPenalty,
            stream = true
        };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage? response = null;
        bool httpError = false;
        bool cancelled = false;
        try
        {
            response = await _httpClient.PostAsync($"{_baseUrl}/generate", content, ct);
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

                    string? line;
                    while ((line = await reader.ReadLineAsync(ct)) != null)
                    {
                        if (ct.IsCancellationRequested) { lastFinishReason = "cancelled"; break; }
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
                            yield return new PythonGenerationResult(sb.ToString(), promptTokens, completionTokens, chunkFinishReason);
                        }
                        else
                        {
                            yield return new PythonGenerationResult(sb.ToString(), promptTokens, completionTokens, "ongoing");
                        }
                    }

                    if (sb.Length > 0)
                    {
                        yield return new PythonGenerationResult(sb.ToString(), promptTokens, completionTokens, lastFinishReason);
                    }
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        await Task.CompletedTask;
    }
}

/// <summary>
/// Ergebnis einer Python-Generation.
/// </summary>
public sealed record PythonGenerationResult(string Text, int PromptTokens, int CompletionTokens, string FinishReason);
