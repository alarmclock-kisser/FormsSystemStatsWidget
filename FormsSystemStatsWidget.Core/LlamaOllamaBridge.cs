using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FormsSystemStatsWidget.Core
{
    public static partial class LlamaOllamaBridge
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;

        private static HttpListener? _listener;
        private static bool _isRunning;
        private static string _detectedModelName = "local-llama-model";
        private static string _quantizationLevel = "unknown";
        private static string _parameterSize = "unknown";
        private static string _modelFamily = "llama";
        private static bool _supportsVision;
        private static string _llamaServerBaseUrl = "http://localhost:8080";
        private static string _lastStartError = string.Empty;

        // Logging settings
        public static bool EnableFormattedLogging = true;
        public static bool EnableRawChunkLogging = true;

        public static string LastStartError => _lastStartError;

        // Dynamische Fallbacks, falls der /props-Endpunkt unerwartet fehlschlägt
        private static int _detectedNumCtx = 4096;
        private static double _detectedTemperature = 0.7;


        // Options / Settings from UI set
        public static double UserDefinedTemperature { get; set; } = 0.7;
        public static double UserDefinedRepetitionPenalty { get; set; } = 1.1;
        public static int UserDefinedThinkingBudget { get; set; } = 4096;
        public static double UserDefinedTopP { get; set; }
        public static double UserDefinedMinP { get; set; }
        public static int UserDefinedTopK { get; set; }
        public static int UserDefinedReasoningBudget { get; set; }

        private static readonly HttpClient _httpClient = new();

        /// <summary>
        /// Prüft die Erreichbarkeit von llama-server, liest die Modellkonfiguration aus 
        /// und startet den nativen HTTP-Ollama-Proxy.
        /// </summary>
        public static async Task<bool> StartAsync(string? apiUrl = null, int llamacppPort = 8080, int ollamaPort = 11434)
        {
            _lastStartError = string.Empty;
            Logger.Log($"[LlamaBridge] Starting Bridge: llama-server ({llamacppPort}) -> Ollama ({ollamaPort})");
            Logger.Log($"[LlamaBridge] Configured Source API URL: '{apiUrl ?? "<null>"}'");

            // 1. Verbindungstest & detaillierte Metadaten-Abfrage zu llama-server
            try
            {
                string normalizedApiHost = string.Empty;
                if (!string.IsNullOrWhiteSpace(apiUrl))
                {
                    string rawApiUrl = apiUrl.Trim().TrimEnd('/');

                    if (Uri.TryCreate(rawApiUrl, UriKind.Absolute, out Uri? parsedApiUri))
                    {
                        normalizedApiHost = parsedApiUri.Host;
                    }
                    else
                    {
                        normalizedApiHost = rawApiUrl
                            .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
                            .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
                            .Split('/')[0]
                            .Split(':')[0];
                    }
                }

                var candidateBaseUrls = new List<string>();
                if (!string.IsNullOrWhiteSpace(normalizedApiHost))
                {
                    candidateBaseUrls.Add($"http://{normalizedApiHost}:{llamacppPort}");
                }
                candidateBaseUrls.Add($"http://localhost:{llamacppPort}");

                Logger.Log($"[LlamaBridge] Reachability candidates: {string.Join(" | ", candidateBaseUrls.Distinct(StringComparer.OrdinalIgnoreCase))}");

                string? selectedBaseUrl = null;
                foreach (string candidateBaseUrl in candidateBaseUrls.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        Logger.Log($"[LlamaBridge] Probing llama-server models endpoint: {candidateBaseUrl}/v1/models");
                        using var ctsProbe = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                        var probeResponse = await _httpClient.GetAsync($"{candidateBaseUrl}/v1/models", ctsProbe.Token);
                        if (probeResponse.IsSuccessStatusCode)
                        {
                            Logger.Log($"[LlamaBridge] Reachability probe success: {candidateBaseUrl} ({(int) probeResponse.StatusCode})");
                            selectedBaseUrl = candidateBaseUrl;
                            break;
                        }

                        Logger.Log($"[LlamaBridge] Probe failed (HTTP {(int) probeResponse.StatusCode} {probeResponse.StatusCode}) for {candidateBaseUrl}/v1/models");
                    }
                    catch (Exception probeEx)
                    {
                        Logger.Log($"[LlamaBridge] Probe exception for {candidateBaseUrl}/v1/models: {probeEx.GetType().Name}: {probeEx.Message}");
                    }
                }

                if (string.IsNullOrWhiteSpace(selectedBaseUrl))
                {
                    _lastStartError = "llama-server not reachable via configured apiUrl and localhost fallback (/v1/models).";
                    Logger.Log($"[LlamaBridge] Error: {_lastStartError}");
                    return false;
                }

                _llamaServerBaseUrl = selectedBaseUrl;
                Logger.Log($"[LlamaBridge] Using llama-server endpoint: {_llamaServerBaseUrl}");

                // Lokalen Token-Timer für den ersten Verbindungs-Ping erstellen
                using var ctsModels = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                // A. Echten Modellnamen holen via /v1/models
                var response = await _httpClient.GetAsync($"{_llamaServerBaseUrl}/v1/models", ctsModels.Token);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JsonNode.Parse(content);
                    var modelId = json?["data"]?[0]?["id"]?.ToString();

                    if (!string.IsNullOrEmpty(modelId))
                    {
                        _detectedModelName = modelId;
                        _quantizationLevel = ExtractQuantization(modelId);
                        _parameterSize = ExtractParameterSize(modelId);
                        _modelFamily = ExtractModelFamily(modelId);
                        _supportsVision = DetectVisionSupportByModelName(modelId);

                        Logger.Log($"[LlamaBridge] Model detected: {_detectedModelName}");
                        Logger.Log($"[LlamaBridge] Parser result: Family={_modelFamily}, Size={_parameterSize}, Quant={_quantizationLevel}, VisionHeuristic={_supportsVision}");
                    }
                }
                else
                {
                    _lastStartError = $"/v1/models returned status {(int) response.StatusCode} {response.StatusCode} on {_llamaServerBaseUrl}.";
                    Logger.Log($"[LlamaBridge] Error: {_lastStartError}");
                    return false;
                }

                // Create local token timer for the second props ping
                using var ctsProps = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                // B. Extract context size and default temperature live from /props
                var propsResponse = await _httpClient.GetAsync($"{_llamaServerBaseUrl}/props", ctsProps.Token);
                if (propsResponse.IsSuccessStatusCode)
                {
                    var propsContent = await propsResponse.Content.ReadAsStringAsync();
                    var propsJson = JsonNode.Parse(propsContent);

                    bool? supportsVisionFromProps = TryReadVisionSupportFromProps(propsJson);
                    if (supportsVisionFromProps.HasValue)
                    {
                        _supportsVision = supportsVisionFromProps.Value;
                    }

                    // Path-tolerant reading of n_ctx
                    var nCtxNode = propsJson?["default_generation_settings"]?["n_ctx"] ?? propsJson?["n_ctx"];
                    if (nCtxNode != null && int.TryParse(nCtxNode.ToString(), out int parsedCtx))
                    {
                        _detectedNumCtx = parsedCtx;
                    }

                    // Path-tolerant reading of default temperature
                    var tempNode = propsJson?["default_generation_settings"]?["temperature"]
                                   ?? propsJson?["default_generation_settings"]?["params"]?["temperature"]
                                   ?? propsJson?["params"]?["temperature"];

                    if (tempNode != null && double.TryParse(tempNode.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedTemp))
                    {
                        _detectedTemperature = parsedTemp;
                    }

                    Logger.Log($"[LlamaBridge] Server-Props loaded: Context={_detectedNumCtx}, Temp={_detectedTemperature}, Vision={_supportsVision}");
                }
                else
                {
                    Logger.Log($"[LlamaBridge] Warning: /props unavailable on {_llamaServerBaseUrl} (HTTP {(int) propsResponse.StatusCode} {propsResponse.StatusCode}). Continuing with defaults.");
                }
            }
            catch (Exception ex)
            {
                _lastStartError = $"Critical connection error to llama-server: {ex.GetType().Name}: {ex.Message}";
                Logger.Log($"[LlamaBridge] {_lastStartError}");
                return false; // llama-server is offline or unreachable
            }

            // 2. Start native HttpListener instance on the Ollama standard port
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{ollamaPort}/");
                Logger.Log($"[LlamaBridge] Starting local listener on http://localhost:{ollamaPort}/");
                _listener.Start();
                _isRunning = true;

                // Den Listening-Loop entkoppelt in den ThreadPool schieben
                _ = Task.Run(() => ListenLoopAsync(llamacppPort));
                Logger.Log("[LlamaBridge] HttpListener successfully started.");
                return true;
            }
            catch (Exception ex)
            {
                _lastStartError = $"Port {ollamaPort} blocked or listener error: {ex.GetType().Name}: {ex.Message}";
                Logger.Log($"[LlamaBridge] {_lastStartError}");
                return false;
            }
        }

        private static async Task ListenLoopAsync(int llamacppPort)
        {
            while (_isRunning && _listener != null)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context, llamacppPort));
                }
                catch
                {
                    break; // Abbruch bei Stop()
                }
            }
        }

        private static async Task HandleRequestAsync(HttpListenerContext context, int llamacppPort)
        {
            var request = context.Request;
            var response = context.Response;
            var path = request.Url?.AbsolutePath ?? "";

            Logger.Log($"[LlamaBridge-Inbound] {request.HttpMethod} -> {path}");

            try
            {
                // --- ENDPUNKT 1: Der moderne OpenAI-Pass-Through ---
                if (request.HttpMethod == "POST" && path == "/v1/chat/completions")
                {
                    Logger.Log("[LlamaBridge] Processing OpenAI-compatible direct stream...");

                    // Read incoming stream to pass it through the sanitize filter
                    using var reader = new StreamReader(request.InputStream);
                    var requestBody = await reader.ReadToEndAsync();
                    string sanitizedBody = LlamaStreamTransformer.SanitizeIncomingRequest(requestBody, _modelFamily, _detectedNumCtx, UserDefinedTemperature, UserDefinedRepetitionPenalty, UserDefinedTopP, UserDefinedMinP, UserDefinedTopK);

                    Logger.Log("========================================");
                    Logger.Log("[REQUEST TO LLAMA - AFTER SANITIZE]");
                    Logger.Log(sanitizedBody);
                    Logger.Log("========================================");

                    var upstreamReq = new HttpRequestMessage(HttpMethod.Post, $"{_llamaServerBaseUrl}/v1/chat/completions")
                    {
                        Content = new StringContent(sanitizedBody, Encoding.UTF8, "application/json")
                    };

                    var upstreamRes = await _httpClient.SendAsync(upstreamReq, HttpCompletionOption.ResponseHeadersRead);

                    response.StatusCode = (int) upstreamRes.StatusCode;
                    response.ContentType = upstreamRes.Content.Headers.ContentType?.ToString() ?? "text/event-stream";
                    response.SendChunked = true;

                    // Stream-Verarbeitung an die spezialisierte Transformator-Klasse übergeben
                    using (var upstreamStream = await upstreamRes.Content.ReadAsStreamAsync())
                    {
                        await LlamaStreamTransformer.TransformOpenAiStreamAsync(upstreamStream, response.OutputStream, _detectedModelName);
                    }

                    response.OutputStream.Close();
                    Logger.Log("[LlamaBridge] OpenAI direct stream successfully ended.");
                    return;
                }

                // --- ENDPUNKT 2: Legacy/Alternativer Ollama Chat-Übersetzer (Falls Agent-Tools umschalten) ---
                if (request.HttpMethod == "POST" && path == "/api/chat")
                {
                    Logger.Log("[LlamaBridge] Translating NDJSON-Ollama request...");

                    using var reader = new StreamReader(request.InputStream);
                    var body = await reader.ReadToEndAsync();
                    var ollamaReq = JsonNode.Parse(body);

                    var openAiReq = new JsonObject
                    {
                        ["model"] = _detectedModelName,
                        ["messages"] = ollamaReq?["messages"]?.DeepClone(),
                        ["stream"] = ollamaReq?["stream"] ?? true
                    };

                    var upstreamReq = new HttpRequestMessage(HttpMethod.Post, $"{_llamaServerBaseUrl}/v1/chat/completions")
                    {
                        Content = new StringContent(openAiReq.ToJsonString(), Encoding.UTF8, "application/json")
                    };

                    var upstreamRes = await _httpClient.SendAsync(upstreamReq, HttpCompletionOption.ResponseHeadersRead);
                    response.ContentType = "application/x-javascript; charset=utf-8";
                    response.StatusCode = (int) upstreamRes.StatusCode;
                    response.SendChunked = true;

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

                        var finalChunk = new JsonObject { ["model"] = _detectedModelName, ["done"] = true };
                        await writer.WriteLineAsync(finalChunk.ToJsonString());
                        await writer.FlushAsync();
                    }

                    response.OutputStream.Close();
                    Logger.Log("[LlamaBridge] Ollama NDJSON stream successfully ended.");
                    return;
                }

                // --- ENDPOINT 3: Model Tags Manifest for VS Copilot Recognition (GET) ---
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
                                size = 0,
                                digest = "proxy_identity_digest",
                                details = new {
                                    parent_model = "",
                                    format = "gguf",
                                    family = _modelFamily,
                                    families = new[] { _modelFamily },
                                    parameter_size = _parameterSize,
                                    quantization_level = _quantizationLevel
                                }
                            }
                        }
                    };
                    await SendJsonResponseAsync(response, tagsData);
                    return;
                }

                // --- ENDPUNKT 4: Aktive VRAM-Modelle (GET) ---
                if (request.HttpMethod == "GET" && path == "/api/ps")
                {
                    var psData = new
                    {
                        models = new[]
                        {
                            new {
                                name = _detectedModelName,
                                model = _detectedModelName,
                                size = 0,
                                digest = "proxy_identity_digest",
                                details = new {
                                    parent_model = "",
                                    format = "gguf",
                                    family = _modelFamily,
                                    families = new[] { _modelFamily },
                                    parameter_size = _parameterSize,
                                    quantization_level = _quantizationLevel
                                }
                            }
                        }
                    };
                    await SendJsonResponseAsync(response, psData);
                    return;
                }

                // --- ENDPUNKT 5: Detaillierte Capabilities-Injektion für das Chat-Dropdown (POST) ---
                if (request.HttpMethod == "POST" && path == "/api/show")
                {
                    string tempFormatted = _detectedTemperature.ToString("G", CultureInfo.InvariantCulture);

                    var capabilities = _supportsVision
                        ? new[] { "completion", "tools", "vision" }
                        : new[] { "completion", "tools" };

                    var showData = new
                    {
                        modelfile = $"FROM {_detectedModelName}\nPARAMETER temperature {tempFormatted}\nPARAMETER num_ctx {_detectedNumCtx}",
                        parameters = $"temperature {tempFormatted}\nnum_ctx {_detectedNumCtx}",
                        template = "{{ .System }}\n{{ .Prompt }}",
                        details = new
                        {
                            parent_model = "",
                            format = "gguf",
                            family = _modelFamily,
                            families = new[] { _modelFamily },
                            parameter_size = _parameterSize,
                            quantization_level = _quantizationLevel
                        },
                        capabilities = capabilities
                    };
                    await SendJsonResponseAsync(response, showData);
                    return;
                }

                // --- KOSMETISCHER ENDPUNKT 6: Browser-Root-Aufruf ---
                if (request.HttpMethod == "GET" && path == "/")
                {
                    var responseString = "Ollama is running (routed via LlamaOllamaBridge Widget)";
                    byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                    response.ContentType = "text/plain; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.OutputStream.Close();
                    return;
                }

                // Fallback für ungemappte Routen
                Logger.Log($"[LlamaBridge] Unknown path rejected (404): {path}");
                response.StatusCode = (int) HttpStatusCode.NotFound;
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Logger.Log($"[LlamaBridge-Exception] Error processing request: {ex.Message}");
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

        private static string ExtractQuantization(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return "unknown";
            }

            var match = Regex.Match(modelName, @"(?i)\b(Q\d+_[K_A-Z0-9_]+|IQ\d+_[A-Z0-9_]+|Q\d+_\d+|FP16|BF16)\b");
            return match.Success ? match.Value.ToUpper() : "unknown";
        }

        private static string ExtractParameterSize(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return "unknown";
            }

            // Mapping von Marketing-Bezeichnern auf die echten Parameter-Größen
            // Das lässt sich hier zentral erweitern, wenn neue Modelle kommen.
            var sizeMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "e2b", "5B" },   // Beispiel: Gemma-4-E2B (kleinstes)
                { "e4b", "9B" },  // Beispiel: Gemma-4-E4B
                { "a4b", "26B" },  // Beispiel: Gemma-4-A4B
                { "a3b", "30B" }   // Beispiel: Qwen3-A3B
            };

            // 1. Suche nach bekannten Marketing-Markern aus unserem Dictionary
            foreach (var entry in sizeMapping)
            {
                // Sucht z.B. nach "e2b" oder "a4b" in der Modell-ID
                if (modelName.Contains(entry.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }

            // 2. Fallback: Wenn kein Mapping gefunden, versuche die echte Größe aus dem String zu ziehen
            // Sucht z.B. nach "26b" (aber NICHT hinter einem 'a' oder 'e')
            var realSizeMatch = Regex.Match(modelName, @"(?<![ae])\b(\d+)b\b", RegexOptions.IgnoreCase);
            if (realSizeMatch.Success)
            {
                return $"{realSizeMatch.Groups[1].Value}B";
            }

            // 3. Letzter Ausweg: Klassische Regex (7B, 14B etc.)
            var classicMatch = Regex.Match(modelName, @"(?i)\b(\d+(?:\.\d+)?[BM])\b");
            return classicMatch.Success ? classicMatch.Value.ToUpper() : "unknown";
        }


        private static string ExtractModelFamily(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return "llama";
            }

            string normalizedModelName = modelName.Trim().ToLowerInvariant();

            string[] qwenMarkers = { "qwen", "qwq" };
            if (qwenMarkers.Any(marker => normalizedModelName.Contains(marker, StringComparison.Ordinal)))
            {
                return "qwen";
            }

            string[] gemmaMarkers = { "gemma", "medgemma" };
            if (gemmaMarkers.Any(marker => normalizedModelName.Contains(marker, StringComparison.Ordinal)))
            {
                return "gemma";
            }

            string[] mistralMarkers = { "mistral", "mixtral", "ministral", "codestral", "pixtral" };
            if (mistralMarkers.Any(marker => normalizedModelName.Contains(marker, StringComparison.Ordinal)))
            {
                return "mistral";
            }

            string[] llamaMarkers = { "llama", "llama3", "llama-", "meta-llama", "codellama" };
            if (llamaMarkers.Any(marker => normalizedModelName.Contains(marker, StringComparison.Ordinal)))
            {
                return "llama";
            }

            string[] deepSeekMarkers = { "deepseek" };
            if (deepSeekMarkers.Any(marker => normalizedModelName.Contains(marker, StringComparison.Ordinal)))
            {
                return "deepseek";
            }

            string[] phiMarkers = { "phi" };
            if (phiMarkers.Any(marker => normalizedModelName.Contains(marker, StringComparison.Ordinal)))
            {
                return "phi";
            }

            string[] commandRMarkers = { "command-r", "commandr", "aya", "cohere" };
            return commandRMarkers.Any(marker => normalizedModelName.Contains(marker, StringComparison.Ordinal)) ? "command-r" : "llama";
        }

        private static bool DetectVisionSupportByModelName(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return false;
            }

            string normalized = modelName.Trim().ToLowerInvariant();
            string[] visionMarkers = { "vision", "vl", "mmproj", "multimodal", "gemma-3", "gemma-4", "llava", "pixtral", "minicpm-v", "qwen2.5-vl", "qwen-vl", "phi-3-vision" };
            return visionMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
        }

        private static bool? TryReadVisionSupportFromProps(JsonNode? propsJson)
        {
            if (propsJson == null)
            {
                return null;
            }

            bool? knownBool = TryReadBooleanNode(propsJson?["supports_vision"])
                             ?? TryReadBooleanNode(propsJson?["capabilities"]?["vision"])
                             ?? TryReadBooleanNode(propsJson?["model"]?["capabilities"]?["vision"])
                             ?? TryReadBooleanNode(propsJson?["llava"])
                             ?? TryReadBooleanNode(propsJson?["has_vision_encoder"]);
            if (knownBool.HasValue)
            {
                return knownBool.Value;
            }

            string[] markers = { "mmproj", "vision", "multimodal", "image_encoder", "clip" };
            bool markerFound = markers.Any(marker => propsJson?.ToJsonString()?.Contains(marker, StringComparison.OrdinalIgnoreCase) ?? false);
            return markerFound ? true : null;
        }

        private static bool? TryReadBooleanNode(JsonNode? node)
        {
            if (node == null)
            {
                return null;
            }

            string raw = node.ToString().Trim();
            if (bool.TryParse(raw, out bool parsed))
            {
                return parsed;
            }

            if (raw == "1")
            {
                return true;
            }

            if (raw == "0")
            {
                return false;
            }

            return null;
        }

        public static void Stop()
        {
            Logger.Log("[LlamaBridge] Shutting down bridge...");
            _isRunning = false;
            if (_listener != null)
            {
                try { _listener.Stop(); _listener.Close(); } catch { }
                _listener = null;
            }
            Logger.Log("[LlamaBridge] Bridge successfully stopped.");
        }

        [GeneratedRegex(@"(?i)\b(\d+(?:\.\d+)?[BM])\b", RegexOptions.None, "de-DE")]
        private static partial Regex ModelSizeClassicRegex();
        [GeneratedRegex(@"(?i)\b(?:E|A)(\d+)B\b", RegexOptions.None, "de-DE")]
        private static partial Regex ModelSizeModernRegex();
    }


    public static class Logger
    {
        private const int MaxBufferedLogEntries = 2048;
        private static readonly object SyncRoot = new();
        private static readonly Queue<string> BufferedEntries = new();

        private static bool _isStreaming = false;
        private static int _streamChunkCount = 0;

        public static event Action<string>? MessageLogged;

        public static string[] FilteredLoggingPhrases = new[]
        {
            // Source API URL
            "Source API URL"
        };

        // Logging phrases that shall not be repeated / consecutively logged more than once (e.g. "model loaded successfully" or "model loading failed")
        public static string[] NonRepeatingLoggingPhrases = new[]
        {
            // Polling idle every 2 seconds
            "all slots are idle", "update_slots"
            // ...
       };

        private static int SuppressedRepeatedMessageCount = 0;

        public static void Log(string text)
        {
            //if (FilteredLoggingPhrases.Any(phrase => text.Contains(phrase)))
            //{
            //    return;
            //}

            //string lastLogString = BufferedEntries.Count > 0 ? BufferedEntries.Last() : string.Empty;
            //if (NonRepeatingLoggingPhrases.Any(phrase => text.Contains(phrase) && lastLogString.Contains(phrase)))
            //{
            //    // Over-print last log entry with a note about repeated message, instead of adding a new log line (using \r)
            //    SuppressedRepeatedMessageCount++;
            //    text = '\r' + text + $" ({SuppressedRepeatedMessageCount} times)";
            //}

            lock (SyncRoot)
            {
                if (!LlamaOllamaBridge.EnableRawChunkLogging)
                {
                    // WICHTIG: Erst prüfen, BEVOR ein Timestamp hinzugefügt wird!
                    if (text.Contains("[RAW CHUNK]"))
                    {
                        _isStreaming = true;
                        _streamChunkCount++;
                        // Nur Konsole updaten, ohne neue Zeile (\r)
                        Console.Write($"\r ==> Streaming Response: Received {_streamChunkCount} chunks...");
                        return;
                    }

                    // Sobald das Streaming aufhört, packen wir die Zusammenfassung ins UI
                    if (_isStreaming)
                    {
                        Console.WriteLine(); // Den \r Effekt abschließen
                        string finalStreamLog = $"[Stream Completed] Total received chunks: {_streamChunkCount}";

                        if (LlamaOllamaBridge.EnableFormattedLogging)
                        {
                            finalStreamLog = Environment.NewLine + DateTime.Now.ToString("HH:mm:ss.fff") + " :: " + finalStreamLog;
                        }

                        Debug.WriteLine(finalStreamLog);
                        BufferedEntries.Enqueue(finalStreamLog);
                        MessageLogged?.Invoke(finalStreamLog);

                        _isStreaming = false;
                        _streamChunkCount = 0;
                    }

                    if (text.Contains("\"role\":"))
                    {
                        int estimatedTokens = text.Length / 4;
                        string type = text.Contains("\"assistant\"") ? "Response" : "Request";
                        text = $"[Chat {type} Payload] - Summary: approx. {estimatedTokens} tokens sent/received.";
                    }
                }

                // Standard-Logging
                if (LlamaOllamaBridge.EnableFormattedLogging && !text.StartsWith(Environment.NewLine))
                {
                    text = Environment.NewLine + DateTime.Now.ToString("HH:mm:ss.fff") + " :: " + text;
                }

                Debug.WriteLine(text);
                Console.WriteLine(text);

                BufferedEntries.Enqueue(text);
                while (BufferedEntries.Count > MaxBufferedLogEntries)
                {
                    _ = BufferedEntries.Dequeue();
                }

                MessageLogged?.Invoke(text);
            }
        }

        public static string[] GetRecentEntries()
        {
            lock (SyncRoot)
            {
                return BufferedEntries.ToArray();
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                BufferedEntries.Clear();
                _isStreaming = false;
                _streamChunkCount = 0;
            }
        }
    }
}