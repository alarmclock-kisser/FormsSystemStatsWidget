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
        private static string _bridgeBaseUrl = "http://localhost:11434";
        private static string _lastStartError = string.Empty;

        public static string? DetectedModelName => _detectedModelName;
        public static string? QuantizationLevel => _quantizationLevel;
        public static string? ParameterSize => _parameterSize;
        public static string? ModelFamily => _modelFamily;
        public static bool SupportsVision => _supportsVision;

        // Logging settings
        public static bool EnableFormattedLogging = true;
        public static bool EnableRawChunkLogging = true;

        public static string LastStartError => _lastStartError;
        public static bool IsRunning => _isRunning;

        // Dynamische Fallbacks, falls der /props-Endpunkt unerwartet fehlschlägt
        private static int _detectedNumCtx = 4096;
        private static double _detectedTemperature = 0.7;


        // Options / Settings from UI set
        public static double UserDefinedTemperature { get; set; } = 0.7;
        public static double UserDefinedRepetitionPenalty { get; set; } = 1.1;
        public static double UserDefinedTopP { get; set; }
        public static double UserDefinedMinP { get; set; }
        public static int UserDefinedTopK { get; set; }
        public static int UserDefinedReasoningBudget { get; set; }

        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(600) };

        /// <summary>
        /// Checks llama-server reachability, reads model configuration,
        /// and starts the native HTTP Ollama proxy.
        /// </summary>
        public static async Task<bool> StartAsync(string? apiUrl = null, int llamacppPort = 8080, int ollamaPort = 11434)
        {
            _lastStartError = string.Empty;
            _bridgeBaseUrl = $"http://localhost:{ollamaPort}";
            Logger.Log($"[LlamaBridge] Starting Bridge: llama-server ({llamacppPort}) -> Ollama ({ollamaPort})");
            Logger.Log($"[LlamaBridge] Configured Source API URL: '{apiUrl ?? "<null>"}'");

            // 1. Reachability test and detailed metadata query for llama-server
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

                // Create a local timeout token for the first connection probe
                using var ctsModels = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                // A. Read the actual model name via /v1/models
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

                // Create a local timeout token for the second props probe
                using var ctsProps = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                // B. Extract context size and default temperature from /props
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

                // Path-tolerant read of n_ctx
                    var nCtxNode = propsJson?["default_generation_settings"]?["n_ctx"] ?? propsJson?["n_ctx"];
                    if (nCtxNode != null && int.TryParse(nCtxNode.ToString(), out int parsedCtx))
                    {
                        _detectedNumCtx = parsedCtx;
                    }

                // Path-tolerant read of the default temperature
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

            // 2. Start a native HttpListener instance on the Ollama standard port
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{ollamaPort}/");
                Logger.Log($"[LlamaBridge] Starting local listener on http://localhost:{ollamaPort}/");
                _listener.Start();
                _isRunning = true;

                // Dispatch the listening loop to the thread pool
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
                    break; // Exit when Stop() is called
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
                if (await TryHandleOpenAiCompletionsAsync(request, response))
                {
                    return;
                }

                if (await TryHandleOllamaChatAsync(request, response))
                {
                    return;
                }

                if (await TryHandleApiTagsAsync(request, response))
                {
                    return;
                }

                if (await TryHandleApiPsAsync(request, response))
                {
                    return;
                }

                if (await TryHandleApiShowAsync(request, response))
                {
                    return;
                }

                if (await TryHandleRootAsync(request, response))
                {
                    return;
                }

                await HandleUnknownRouteAsync(response, path);
            }
            catch (Exception ex)
            {
                Logger.Log($"[LlamaBridge-Exception] Error processing request: {ex.Message}");
                try { response.StatusCode = (int) HttpStatusCode.InternalServerError; response.OutputStream.Close(); } catch { }
            }
        }

        private static bool IsEndpoint(HttpListenerRequest request, string method, string path)
        {
            return request.HttpMethod == method && string.Equals(request.Url?.AbsolutePath, path, StringComparison.Ordinal);
        }

        private static async Task<bool> TryHandleOpenAiCompletionsAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!IsEndpoint(request, "POST", "/v1/chat/completions"))
            {
                return false;
            }

            Logger.Log("[LlamaBridge] Processing OpenAI-compatible direct stream...");
            using var reader = new StreamReader(request.InputStream);
            string requestBody = await reader.ReadToEndAsync();
            string sanitizedBody = LlamaStreamTransformer.SanitizeIncomingRequest(requestBody, _modelFamily, _detectedNumCtx, UserDefinedTemperature, UserDefinedRepetitionPenalty, UserDefinedTopP, UserDefinedMinP, UserDefinedTopK);

            Logger.Log("========================================");
            Logger.Log("[REQUEST TO LLAMA - AFTER SANITIZE]");
            Logger.Log(sanitizedBody);
            Logger.Log("========================================");

            using var upstreamReq = new HttpRequestMessage(HttpMethod.Post, $"{_llamaServerBaseUrl}/v1/chat/completions")
            {
                Content = new StringContent(sanitizedBody, Encoding.UTF8, "application/json")
            };

            using HttpResponseMessage upstreamRes = await _httpClient.SendAsync(upstreamReq, HttpCompletionOption.ResponseHeadersRead);
            response.StatusCode = (int) upstreamRes.StatusCode;
            response.ContentType = upstreamRes.Content.Headers.ContentType?.ToString() ?? "text/event-stream";
            response.SendChunked = true;

            using Stream upstreamStream = await upstreamRes.Content.ReadAsStreamAsync();
            await LlamaStreamTransformer.TransformOpenAiStreamAsync(upstreamStream, response.OutputStream, _detectedModelName);

            response.OutputStream.Close();
            Logger.Log("[LlamaBridge] OpenAI direct stream successfully ended.");
            return true;
        }

        private static async Task<bool> TryHandleOllamaChatAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!IsEndpoint(request, "POST", "/api/chat"))
            {
                return false;
            }

            Logger.Log("[LlamaBridge] Translating NDJSON-Ollama request...");
            using var reader = new StreamReader(request.InputStream);
            string body = await reader.ReadToEndAsync();
            JsonNode? ollamaReq = JsonNode.Parse(body);

            var openAiReq = new JsonObject
            {
                ["model"] = _detectedModelName,
                ["messages"] = ollamaReq?["messages"]?.DeepClone(),
                ["stream"] = ollamaReq?["stream"] ?? true
            };

            using var upstreamReq = new HttpRequestMessage(HttpMethod.Post, $"{_llamaServerBaseUrl}/v1/chat/completions")
            {
                Content = new StringContent(openAiReq.ToJsonString(), Encoding.UTF8, "application/json")
            };

            using HttpResponseMessage upstreamRes = await _httpClient.SendAsync(upstreamReq, HttpCompletionOption.ResponseHeadersRead);
            response.ContentType = "application/x-javascript; charset=utf-8";
            response.StatusCode = (int) upstreamRes.StatusCode;
            response.SendChunked = true;

            if (ollamaReq?["stream"]?.GetValue<bool>() == false)
            {
                await WriteNonStreamingOllamaResponseAsync(response, upstreamRes);
            }
            else
            {
                await WriteStreamingOllamaResponseAsync(response, upstreamRes);
            }

            response.OutputStream.Close();
            Logger.Log("[LlamaBridge] Ollama NDJSON stream successfully ended.");
            return true;
        }

        private static async Task WriteNonStreamingOllamaResponseAsync(HttpListenerResponse response, HttpResponseMessage upstreamRes)
        {
            string resContent = await upstreamRes.Content.ReadAsStringAsync();
            JsonNode? openAiRes = JsonNode.Parse(resContent);
            string text = openAiRes?["choices"]?[0]?["message"]?["content"]?.ToString() ?? string.Empty;

            var ollamaRes = new JsonObject
            {
                ["model"] = _detectedModelName,
                ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = text },
                ["done"] = true
            };

            byte[] buffer = Encoding.UTF8.GetBytes(ollamaRes.ToJsonString() + "\n");
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        private static async Task WriteStreamingOllamaResponseAsync(HttpListenerResponse response, HttpResponseMessage upstreamRes)
        {
            using Stream responseStream = await upstreamRes.Content.ReadAsStreamAsync();
            using var streamReader = new StreamReader(responseStream);
            using var writer = new StreamWriter(response.OutputStream, new UTF8Encoding(false));

            string? line;
            while ((line = await streamReader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                {
                    continue;
                }

                string data = line["data: ".Length..].Trim();
                if (data == "[DONE]")
                {
                    break;
                }

                try
                {
                    JsonNode? openAiChunk = JsonNode.Parse(data);
                    string contentChunk = openAiChunk?["choices"]?[0]?["delta"]?["content"]?.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(contentChunk))
                    {
                        continue;
                    }

                    var ollamaChunk = new JsonObject
                    {
                        ["model"] = _detectedModelName,
                        ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = contentChunk },
                        ["done"] = false
                    };

                    await writer.WriteLineAsync(ollamaChunk.ToJsonString());
                    await writer.FlushAsync();
                }
                catch
                {
                }
            }

            var finalChunk = new JsonObject { ["model"] = _detectedModelName, ["done"] = true };
            await writer.WriteLineAsync(finalChunk.ToJsonString());
            await writer.FlushAsync();
        }

        private static async Task<bool> TryHandleApiTagsAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!IsEndpoint(request, "GET", "/api/tags"))
            {
                return false;
            }

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
                        details = CreateModelDetails()
                    }
                }
            };

            await SendJsonResponseAsync(response, tagsData);
            return true;
        }

        private static async Task<bool> TryHandleApiPsAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!IsEndpoint(request, "GET", "/api/ps"))
            {
                return false;
            }

            var psData = new
            {
                models = new[]
                {
                    new {
                        name = _detectedModelName,
                        model = _detectedModelName,
                        size = 0,
                        digest = "proxy_identity_digest",
                        details = CreateModelDetails()
                    }
                }
            };

            await SendJsonResponseAsync(response, psData);
            return true;
        }

        private static async Task<bool> TryHandleApiShowAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!IsEndpoint(request, "POST", "/api/show"))
            {
                return false;
            }

            string tempFormatted = _detectedTemperature.ToString("G", CultureInfo.InvariantCulture);
            var capabilities = _supportsVision
                ? new[] { "completion", "tools", "vision" }
                : ["completion", "tools"];

            var showData = new
            {
                modelfile = $"FROM {_detectedModelName}\nPARAMETER temperature {tempFormatted}\nPARAMETER num_ctx {_detectedNumCtx}",
                parameters = $"temperature {tempFormatted}\nnum_ctx {_detectedNumCtx}",
                template = "{{ .System }}\n{{ .Prompt }}",
                details = CreateModelDetails(),
                capabilities = capabilities
            };

            await SendJsonResponseAsync(response, showData);
            return true;
        }

        private static async Task<bool> TryHandleRootAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!IsEndpoint(request, "GET", "/"))
            {
                return false;
            }

            string responseString = "Ollama is running (routed via LlamaOllamaBridge Widget)";
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentType = "text/plain; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
            return true;
        }

        private static async Task HandleUnknownRouteAsync(HttpListenerResponse response, string path)
        {
            Logger.Log($"[LlamaBridge] Unknown path rejected (404): {path}");
            response.StatusCode = (int) HttpStatusCode.NotFound;
            await response.OutputStream.FlushAsync();
            response.OutputStream.Close();
        }

        private static object CreateModelDetails()
        {
            return new
            {
                parent_model = "",
                format = "gguf",
                family = _modelFamily,
                families = new[] { _modelFamily },
                parameter_size = _parameterSize,
                quantization_level = _quantizationLevel
            };
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

            string[] qwenMarkers = ["qwen", "qwq"];
            if (qwenMarkers.Any(marker => normalizedModelName.Contains(marker, StringComparison.Ordinal)))
            {
                return "qwen";
            }

            string[] gemmaMarkers = ["gemma", "medgemma"];
            if (gemmaMarkers.Any(marker => normalizedModelName.Contains(marker, StringComparison.Ordinal)))
            {
                return "gemma";
            }

            string[] mistralMarkers = ["mistral", "mixtral", "ministral", "codestral", "pixtral"];
            if (mistralMarkers.Any(marker => normalizedModelName.Contains(marker, StringComparison.Ordinal)))
            {
                return "mistral";
            }

            string[] llamaMarkers = ["llama", "llama3", "llama-", "meta-llama", "codellama"];
            if (llamaMarkers.Any(marker => normalizedModelName.Contains(marker, StringComparison.Ordinal)))
            {
                return "llama";
            }

            string[] deepSeekMarkers = ["deepseek"];
            if (deepSeekMarkers.Any(marker => normalizedModelName.Contains(marker, StringComparison.Ordinal)))
            {
                return "deepseek";
            }

            string[] phiMarkers = ["phi"];
            if (phiMarkers.Any(marker => normalizedModelName.Contains(marker, StringComparison.Ordinal)))
            {
                return "phi";
            }

            string[] commandRMarkers = ["command-r", "commandr", "aya", "cohere"];
            return commandRMarkers.Any(marker => normalizedModelName.Contains(marker, StringComparison.Ordinal)) ? "command-r" : "llama";
        }

        private static bool DetectVisionSupportByModelName(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return false;
            }

            string normalized = modelName.Trim().ToLowerInvariant();
            string[] visionMarkers = ["vision", "vl", "mmproj", "multimodal", "gemma-3", "gemma-4", "llava", "pixtral", "minicpm-v", "qwen2.5-vl", "qwen-vl", "phi-3-vision"];
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

            string[] markers = ["mmproj", "vision", "multimodal", "image_encoder", "clip"];
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

        public static async Task<bool> SendAudioInputAsync(string uiNotification, string audioBase64, CancellationToken cancellationToken = default)
        {
            if (!_isRunning)
            {
                Logger.Log("[LlamaBridge-Audio] Bridge is not running. Audio request skipped.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(audioBase64))
            {
                Logger.Log("[LlamaBridge-Audio] Audio payload is empty.");
                return false;
            }

            string[] audioContentTypes = ["input_audio", "audio_input"];
            foreach (string audioContentType in audioContentTypes)
            {
                JsonObject requestBody = CreateAudioInputRequest(uiNotification, audioBase64, audioContentType);
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_bridgeBaseUrl}/v1/chat/completions")
                {
                    Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json")
                };

                try
                {
                    using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
                    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        Logger.Log($"[LlamaBridge-Audio] Attempt '{audioContentType}' failed: {(int) response.StatusCode} {response.StatusCode} - {responseBody}");
                        continue;
                    }

                    Logger.Log($"[LlamaBridge-Audio] Audio request accepted using '{audioContentType}'.");

                    string modelResponse = ExtractAssistantResponseText(responseBody);
                    if (!string.IsNullOrWhiteSpace(modelResponse))
                    {
                        Logger.Log($"\n{modelResponse}\n");
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Log($"[LlamaBridge-Audio] Attempt '{audioContentType}' failed with exception: {ex.GetType().Name}: {ex.Message}");
                }
            }

            return false;
        }

        private static JsonObject CreateAudioInputRequest(string uiNotification, string audioBase64, string audioContentType)
        {
            string notificationText = string.IsNullOrWhiteSpace(uiNotification)
                ? "🎤 Audio input"
                : uiNotification;

            var audioPayload = new JsonObject
            {
                ["data"] = audioBase64,
                ["format"] = "wav"
            };

            var audioPart = new JsonObject
            {
                ["type"] = audioContentType,
                [audioContentType] = audioPayload
            };

            return new JsonObject
            {
                ["model"] = _detectedModelName,
                ["stream"] = false,
                ["messages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = notificationText
                            },
                            audioPart
                        }
                    }
                }
            };
        }

        private static string ExtractAssistantResponseText(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return string.Empty;
            }

            try
            {
                JsonNode? responseNode = JsonNode.Parse(responseBody);
                JsonNode? messageContentNode = responseNode?["choices"]?[0]?["message"]?["content"];
                if (messageContentNode == null)
                {
                    return string.Empty;
                }

                if (messageContentNode is JsonValue)
                {
                    return messageContentNode.ToString();
                }

                if (messageContentNode is JsonArray parts)
                {
                    IEnumerable<string> textParts = parts
                        .OfType<JsonObject>()
                        .Where(part => string.Equals(part["type"]?.ToString(), "text", StringComparison.OrdinalIgnoreCase))
                        .Select(part => part["text"]?.ToString() ?? string.Empty)
                        .Where(text => !string.IsNullOrWhiteSpace(text));
                    return string.Join(Environment.NewLine, textParts);
                }

                return messageContentNode.ToJsonString();
            }
            catch
            {
                return responseBody;
            }
        }

        [GeneratedRegex(@"(?i)\b(\d+(?:\.\d+)?[BM])\b", RegexOptions.None, "de-DE")]
        private static partial Regex ModelSizeClassicRegex();
        [GeneratedRegex(@"(?i)\b(?:E|A)(\d+)B\b", RegexOptions.None, "de-DE")]
        private static partial Regex ModelSizeModernRegex();
        public static string[] RefreshModels()
        {
            return LlamaCppModelLoader.GetModelFilePaths();
        }
    }


    public static class Logger
    {
        private const int MaxBufferedLogEntries = 2048;
        private static readonly Lock SyncRoot = new();
        private static readonly Queue<string> BufferedEntries = new();

        private static bool _isStreaming = false;
        private static int _streamChunkCount = 0;

        public static event Action<string>? MessageLogged;

        public static string[] FilteredLoggingPhrases =
        [
            "Source API URL"
        ];

        public static string[] NonRepeatingLoggingPhrases =
        [
            "all slots are idle", "update_slots"
       ];

        public static void Log(string text)
        {
            lock (SyncRoot)
            {
                if (!LlamaOllamaBridge.EnableRawChunkLogging)
                {
                    if (text.Contains("[RAW CHUNK]"))
                    {
                        _isStreaming = true;
                        _streamChunkCount++;
                        Console.Write($"\r ==> Streaming Response: Received {_streamChunkCount} chunks...");
                        return;
                    }

                    if (_isStreaming)
                    {
                        Console.WriteLine();
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