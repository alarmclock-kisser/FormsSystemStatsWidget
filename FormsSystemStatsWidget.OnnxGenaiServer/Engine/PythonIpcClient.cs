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
    public async Task<bool> LoadModelAsync(string modelPath, string modelLayout = "Auto", CancellationToken ct = default)
    {
        try
        {
            var payload = new { model_path = modelPath, model_layout = modelLayout.ToLowerInvariant() };
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
            min_p = parameters.MinP,
            presence_penalty = parameters.PresencePenalty,
            frequency_penalty = parameters.FrequencyPenalty,
            seed = parameters.Seed,
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
            min_p = parameters.MinP,
            presence_penalty = parameters.PresencePenalty,
            frequency_penalty = parameters.FrequencyPenalty,
            seed = parameters.Seed,
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
                            yield return new PythonGenerationResult(sb.ToString(), promptTokens, completionTokens, chunkFinishReason);
                        }
                        else
                        {
                            yield return new PythonGenerationResult(sb.ToString(), promptTokens, completionTokens, "ongoing");
                        }
                    }

                    if (sb.Length > 0 && !finishReported)
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

public sealed record PythonChatMessage(string Role, string? Content, string? Name);
