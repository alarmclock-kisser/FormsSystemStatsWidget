using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public static class LlamaOllamaBridge
{
    private static HttpListener? _listener;
    private static bool _isRunning;
    private static string _detectedModelName = "local-llama-model";
    private static readonly HttpClient _httpClient = new HttpClient();

    /// <summary>
    /// Prüft den llama-server und startet den nativen HTTP-Proxy auf dem Ollama-Port.
    /// </summary>
    public static async Task<bool> StartAsync(int llamacppPort = 8080, int ollamaPort = 11434)
    {
        // 1. Verbindungstest zu llama-server (Modellnamen auslesen)
        try
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(3);
            var response = await _httpClient.GetAsync($"http://localhost:{llamacppPort}/v1/models");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonNode.Parse(content);
                var modelId = json?["data"]?[0]?["id"]?.ToString();
                if (!string.IsNullOrEmpty(modelId))
                {
                    _detectedModelName = modelId;
                }
            }
            else
            {
                return false;
            }
        }
        catch
        {
            return false; // llama-server offline
        }

        // 2. Native HttpListener-Instanz hochfahren
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{ollamaPort}/");
            _listener.Start();
            _isRunning = true;

            // Den Listening-Loop in den Hintergrund schieben, um nichts zu blockieren
            _ = Task.Run(() => ListenLoopAsync(llamacppPort));
            return true;
        }
        catch
        {
            return false; // Port blockiert (z.B. durch echtes Ollama)
        }
    }

    private static async Task ListenLoopAsync(int llamacppPort)
    {
        while (_isRunning && _listener != null)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                // Jeden Request entkoppelt verarbeiten (wichtig für parallele Anfragen)
                _ = Task.Run(() => HandleRequestAsync(context, llamacppPort));
            }
            catch
            {
                break;
            }
        }
    }

    private static async Task HandleRequestAsync(HttpListenerContext context, int llamacppPort)
    {
        var request = context.Request;
        var response = context.Response;
        var path = request.Url?.AbsolutePath ?? "";

        try
        {
            // Endpunkt 1: VS Copilot fragt ab, welche Modelle da sind
            if (request.HttpMethod == "GET" && path == "/api/tags")
            {
                var tagsData = new
                {
                    models = new[]
                    {
                        new {
                            name = _detectedModelName,
                            model = _detectedModelName,
                            modified_at = DateTime.UtcNow,
                            details = new { format = "gguf", family = "llama" }
                        }
                    }
                };
                await SendJsonResponseAsync(response, tagsData);
            }
            // Endpunkt 2: Detail-Abfrage für VS Copilot
            else if (request.HttpMethod == "POST" && path == "/api/show")
            {
                var showData = new { details = new { format = "gguf", family = "llama" } };
                await SendJsonResponseAsync(response, showData);
            }
            // Endpunkt 3: Das eigentliche Chat-Streaming
            else if (request.HttpMethod == "POST" && path == "/api/chat")
            {
                using var reader = new StreamReader(request.InputStream);
                var body = await reader.ReadToEndAsync();
                var ollamaReq = JsonNode.Parse(body);

                // Konvertierung ins OpenAI-Format für llama-server
                var openAiReq = new JsonObject
                {
                    ["model"] = _detectedModelName,
                    ["messages"] = ollamaReq?["messages"]?.DeepClone(),
                    ["stream"] = ollamaReq?["stream"] ?? true
                };

                var upstreamReq = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:{llamacppPort}/v1/chat/completions")
                {
                    Content = new StringContent(openAiReq.ToJsonString(), Encoding.UTF8, "application/json")
                };

                var upstreamRes = await _httpClient.SendAsync(upstreamReq, HttpCompletionOption.ResponseHeadersRead);

                response.ContentType = "application/x-javascript; charset=utf-8";
                response.StatusCode = (int) upstreamRes.StatusCode;

                // Falls VS kein Streaming verlangt (Fallback)
                if (ollamaReq?["stream"]?.GetValue<bool>() == false)
                {
                    var resContent = await upstreamRes.Content.ReadAsStringAsync();
                    var openAiRes = JsonNode.Parse(resContent);
                    var text = openAiRes?["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";

                    var ollamaRes = new JsonObject
                    {
                        ["model"] = _detectedModelName,
                        ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = text },
                        ["done"] = true
                    };
                    byte[] buffer = Encoding.UTF8.GetBytes(ollamaRes.ToJsonString() + "\n");
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                // Live-Streaming Übersetzung: OpenAI SSE -> Ollama NDJSON
                else
                {
                    using var responseStream = await upstreamRes.Content.ReadAsStreamAsync();
                    using var streamReader = new StreamReader(responseStream);
                    using var writer = new StreamWriter(response.OutputStream, new UTF8Encoding(false));

                    string? line;
                    while ((line = await streamReader.ReadLineAsync()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                        {
                            continue;
                        }

                        var data = line["data: ".Length..].Trim();
                        if (data == "[DONE]")
                        {
                            break;
                        }

                        try
                        {
                            var openAiChunk = JsonNode.Parse(data);
                            var contentChunk = openAiChunk?["choices"]?[0]?["delta"]?["content"]?.ToString() ?? "";

                            if (!string.IsNullOrEmpty(contentChunk))
                            {
                                var ollamaChunk = new JsonObject
                                {
                                    ["model"] = _detectedModelName,
                                    ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = contentChunk },
                                    ["done"] = false
                                };
                                await writer.WriteLineAsync(ollamaChunk.ToJsonString());
                                await writer.FlushAsync();
                            }
                        }
                        catch { }
                    }

                    // Finaler Abschluss-Chunk
                    var finalChunk = new JsonObject { ["model"] = _detectedModelName, ["done"] = true };
                    await writer.WriteLineAsync(finalChunk.ToJsonString());
                    await writer.FlushAsync();
                }
                response.OutputStream.Close();
            }
            else
            {
                response.StatusCode = (int) HttpStatusCode.NotFound;
                response.OutputStream.Close();
            }
        }
        catch
        {
            try { response.StatusCode = (int) HttpStatusCode.InternalServerError; response.OutputStream.Close(); } catch { }
        }
    }

    private static async Task SendJsonResponseAsync(HttpListenerResponse response, object data)
    {
        var json = JsonSerializer.Serialize(data);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }

    /// <summary>
    /// Stoppt den Proxy-Server sauber.
    /// </summary>
    public static void Stop()
    {
        _isRunning = false;
        if (_listener != null)
        {
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch { }
            _listener = null;
        }
    }
}