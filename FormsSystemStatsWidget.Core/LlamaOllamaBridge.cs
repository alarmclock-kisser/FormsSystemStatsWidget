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
using System.Linq;
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
        private static readonly string _bridgeInstanceId = Guid.NewGuid().ToString("N");
        private static long _generationInboundRequestCount;
        private static long _generationUpstreamSendCount;
        private static bool _isRunning;
        private static string _detectedModelId = "local-llama-model";
        private static string _detectedModelName
        {
            get => _detectedModelId;
            set => _detectedModelId = value;
        }
        private static string _quantizationLevel = "unknown";
        private static string _parameterSize = "unknown";
        private static string _modelFamily = "llama";
        private static bool _supportsVision;

        // Echte GGUF-Metadaten aus /v1/models (meta) und /props
        private static long _modelFileSizeBytes;
        private static long _modelNParams;
        private static int _modelNCtx;
        private static string _modelFtype = "unknown";
        private static long _modelNVocab;
        private static long _modelNEmbd;
        private static int _modelNCtxTrain;
        private static string _modelParentModel = "";
        private static string _modelDescription = "";
        private static List<string> _modelTags = new();
        private static LlamaChatTemplateProfile? _chatTemplateProfile;
        private static string _llamaServerBaseUrl = "http://localhost:8080";
        private static string _bridgeBaseUrl = "http://localhost:11434";
        private static string _lastStartError = string.Empty;
        internal static DateTime s_generationStartUtc;
        internal static int s_startContextTokens;

        public static string? DetectedModelName => _detectedModelName;
        public static string DisplayName => !string.IsNullOrEmpty(_detectedModelName) && File.Exists(_detectedModelName) 
            ? Path.GetFileNameWithoutExtension(_detectedModelName) 
            : _detectedModelName;
        public static string? QuantizationLevel => _quantizationLevel;
        public static string? ParameterSize => _parameterSize;
        public static string? ModelFamily => _modelFamily;
        public static bool SupportsVision => _supportsVision;

        // Logging settings
        public static bool EnableFormattedLogging = true;
        public static bool EnableRawChunkLogging = true;

        // Configuration options for context trimming
        public static bool Enabled = false;
        public static int KeepLastMessages = 10;
        public static bool TrimToolResults;
        public static string ToolCallMode = "Keep";

        // Configuration options for loop detection and interjection
        public static LoopDetectionConfig LoopDetectionConfig = new LoopDetectionConfig();

        public static string LastStartError => _lastStartError;
        public static bool IsRunning => _isRunning;

        // Dynamische Fallbacks, falls der /props-Endpunkt unerwartet fehlschlägt
        private static int _detectedNumCtx = 4096;
        private static double _detectedTemperature = 0.7;

        // Options / Settings from UI set
        public static double UserDefinedTemperature { get; set; } = 0.7;
        public static double UserDefinedRepetitionPenalty { get; set; } = 1.1;
        public static double UserDefinedPresencePenalty { get; set; } = 1.0;
        public static string? UserDefinedReasoningEffort { get; set; } = null;
        public static double UserDefinedTopP { get; set; }
        public static double UserDefinedMinP { get; set; }
        public static int UserDefinedTopK { get; set; }
        public static int UserDefinedReasoningBudget { get; set; }

        public static bool GetGenerationStatsText { get; set; } = false;
        public static string? AdditionalCopilotSystemPrompt { get; set; } = null;
        public static bool AppendParams { get; set; }
        public static string ModelLoadArguments { get; set; } = string.Empty;

        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(600) };

        /// <summary>
        /// Checks llama-server reachability, reads model configuration,
        /// and starts the native HTTP Ollama proxy.
        /// </summary>
        public static async Task<bool> StartAsync(string? apiUrl = null, int llamacppPort = 8080, int ollamaPort = 11434)
        {
            _lastStartError = string.Empty;
            _chatTemplateProfile = null;
            _bridgeBaseUrl = $"http://localhost:{ollamaPort}";
            Logger.Log($"[LlamaBridge] Starting Bridge: llama-server ({llamacppPort}) -> Ollama ({ollamaPort})");
            Logger.Log($"[LlamaBridge] Configured Source API URL: '{apiUrl ?? "<null>"}'");

            // 1. Reachability test and detailed metadata query for llama-server
            try
            {
                var candidateBaseUrls = new List<string>();
                string configuredBaseUrl = BuildLlamaServerBaseUrl(apiUrl, llamacppPort);
                if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
                {
                    candidateBaseUrls.Add(configuredBaseUrl);
                }
                else
                {
                    candidateBaseUrls.Add($"http://localhost:{llamacppPort}");
                }

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
                        // Kürze den Modell-Namen: wenn die ID ein Dateipfad ist, nimm nur den File-Name ohne Extension
                        _detectedModelName = SanitizeModelId(modelId);

                        // Extrahiere echte GGUF-Metadaten aus data[0].meta
                        var metaNode = json?["data"]?[0]?["meta"];
                        if (metaNode is JsonObject metaObj)
                        {
                            _modelFtype = metaObj["ftype"]?.GetValue<string>() ?? "unknown";
                            _modelNParams = metaObj["n_params"]?.GetValue<long>() ?? 0;
                            _modelNCtx = metaObj["n_ctx"]?.GetValue<int>() ?? 0;
                            _modelFileSizeBytes = metaObj["size"]?.GetValue<long>() ?? 0;
                            _modelNVocab = metaObj["n_vocab"]?.GetValue<long>() ?? 0;
                            _modelNEmbd = metaObj["n_embd"]?.GetValue<long>() ?? 0;
                            _modelNCtxTrain = metaObj["n_ctx_train"]?.GetValue<int>() ?? 0;
                            _modelParentModel = metaObj["parent_model"]?.GetValue<string>() ?? "";
                            _modelDescription = metaObj["description"]?.GetValue<string>() ?? "";
                        }

                        // Quantisierung: ftype aus meta bevorzugen, Fallback Regex auf den Namen
                        _quantizationLevel = !string.Equals(_modelFtype, "unknown", StringComparison.OrdinalIgnoreCase)
                            ? _modelFtype
                            : ExtractQuantization(_detectedModelName);

                        // Parameter-Größe: n_params aus meta bevorzugen, Fallback Regex auf den Namen
                        _parameterSize = _modelNParams > 0
                            ? FormatParameterSize(_modelNParams)
                            : ExtractParameterSize(_detectedModelName);

                        // Tags: aus data[0].tags (OpenAI-Format) oder meta.general.tags (GGUF)
                        _modelTags = new List<string>();
                        if (json?["data"]?[0]?["tags"] is JsonArray openAiTags)
                        {
                            foreach (var tag in openAiTags)
                            {
                                string? tagStr = tag?.ToString();
                                if (!string.IsNullOrWhiteSpace(tagStr))
                                {
                                    _modelTags.Add(tagStr);
                                }
                            }
                        }
                        if (_modelTags.Count == 0 && metaNode is JsonObject metaObj2 && metaObj2["general"]?["tags"] is JsonArray ggufTags)
                        {
                            foreach (var tag in ggufTags)
                            {
                                string? tagStr = tag?.ToString();
                                if (!string.IsNullOrWhiteSpace(tagStr))
                                {
                                    _modelTags.Add(tagStr);
                                }
                            }
                        }

                        _modelFamily = ExtractModelFamily(_detectedModelName);
                        _supportsVision = DetectVisionSupportByModelName(_detectedModelName);

                        // n_ctx aus meta als Fallback für /props (llama.cpp /v1/models liefert n_ctx nicht im meta, sondern n_ctx_train)
                        // Priorität: Zuerst /props nehmen, da das die tatsächliche Konfiguration des laufenden Servers ist
                        if (_modelNCtx > 0)
                        {
                            _detectedNumCtx = _modelNCtx;
                        }
                        // Zusätzlicher Fallback: n_ctx_train aus /v1/models meta nutzen
                        if (_modelNCtxTrain > 0 && _detectedNumCtx == 4096)
                        {
                            _detectedNumCtx = _modelNCtxTrain;
                        }
                        // Wenn immer noch der Standardwert verwendet wird, versuche /props zu lesen
                        if (_detectedNumCtx == 4096)
                        {
                            Logger.Log("[LlamaBridge] Using /props for context size detection...");
                        }

                        Logger.Log($"[LlamaBridge] Model detected: {_detectedModelName} (raw ID: {modelId})");
                        Logger.Log($"[LlamaBridge] Parser result: Family={_modelFamily}, Size={_parameterSize}, Quant={_quantizationLevel}, VisionHeuristic={_supportsVision}, NParams={_modelNParams}, Ftype={_modelFtype}");
                    }
                }
                else
                {
                    _lastStartError = $"/v1/models returned status {(int) response.StatusCode} {response.StatusCode} on {_llamaServerBaseUrl}.";
                    Logger.Log($"[LlamaBridge] Error: {_lastStartError}");
                    return false;
                }

                // Create a local timeout token for the second props probe
                try
                {
                    using var ctsProps = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                    // B. Extract context size and default temperature from /props
                    var propsResponse = await _httpClient.GetAsync($"{_llamaServerBaseUrl}/props", ctsProps.Token);
                    if (propsResponse.IsSuccessStatusCode)
                    {
                        var propsContent = await propsResponse.Content.ReadAsStringAsync();
                        var propsJson = JsonNode.Parse(propsContent);
                        _chatTemplateProfile = LlamaChatTemplateProfile.FromProps(propsJson);
                        if (_chatTemplateProfile is not null)
                        {
                            Logger.Log($"[LlamaBridge] Chat template loaded dynamically: chars={_chatTemplateProfile.TemplateLength}, supports_tools={_chatTemplateProfile.SupportsTools?.ToString() ?? "unknown"}, supports_tool_calls={_chatTemplateProfile.SupportsToolCalls?.ToString() ?? "unknown"}, supports_parallel_tool_calls={_chatTemplateProfile.SupportsParallelToolCalls?.ToString() ?? "unknown"}, supports_reasoning_effort={_chatTemplateProfile.SupportsReasoningEffort?.ToString() ?? "unknown"}");
                        }

                        bool? supportsVisionFromProps = TryReadVisionSupportFromProps(propsJson);
                        if (supportsVisionFromProps.HasValue)
                        {
                            _supportsVision = supportsVisionFromProps.Value;
                        }

                        // Path-tolerant read of n_ctx - prioritisieren /props, da /v1/models meta n_ctx nicht enthält
                        var nCtxNode = propsJson?["default_generation_settings"]?["n_ctx"] ?? propsJson?["n_ctx"];
                        if (nCtxNode != null && int.TryParse(nCtxNode.ToString(), out int parsedCtx))
                        {
                            _detectedNumCtx = parsedCtx;
                            Logger.Log($"[LlamaBridge] Context size from /props: {_detectedNumCtx}");
                        }
                        // Fallback: n_ctx_train aus /v1/models meta, falls /props fehlschlägt
                        if (_detectedNumCtx == 4096 && _modelNCtxTrain > 0)
                        {
                            _detectedNumCtx = _modelNCtxTrain;
                            Logger.Log($"[LlamaBridge] Context size from /v1/models n_ctx_train: {_detectedNumCtx}");
                        }

                        // Path-tolerant read of n_ctx_train (nur wenn /v1/models meta es nicht geliefert hat)
                        var nCtxTrainNode = propsJson?["default_generation_settings"]?["n_ctx_train"] ?? propsJson?["n_ctx_train"];
                        if (_modelNCtxTrain == 0 && nCtxTrainNode != null && int.TryParse(nCtxTrainNode.ToString(), out int parsedCtxTrain))
                        {
                            _modelNCtxTrain = parsedCtxTrain;
                        }

                        // Path-tolerant read of n_vocab und n_embd (nur wenn /v1/models meta sie nicht geliefert hat)
                        var nVocabNode = propsJson?["default_generation_settings"]?["n_vocab"] ?? propsJson?["n_vocab"];
                        if (_modelNVocab == 0 && nVocabNode != null && long.TryParse(nVocabNode.ToString(), out long parsedNVocab))
                        {
                            _modelNVocab = parsedNVocab;
                        }

                        var nEmbdNode = propsJson?["default_generation_settings"]?["n_embd"] ?? propsJson?["n_embd"];
                        if (_modelNEmbd == 0 && nEmbdNode != null && long.TryParse(nEmbdNode.ToString(), out long parsedNEmbd))
                        {
                            _modelNEmbd = parsedNEmbd;
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
                catch (Exception propsEx)
                {
                    Logger.Log($"[LlamaBridge] Warning: /props probe failed on {_llamaServerBaseUrl}: {propsEx.GetType().Name}: {propsEx.Message}. Continuing with defaults.");
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
                // Register a wildcard host prefix so http.sys accepts ANY Host header
                // (localhost, 127.0.0.1, [::1], LAN IP). A hostname-specific prefix like
                // "http://localhost:PORT/" rejects non-matching hosts with 400 Invalid Hostname
                // BEFORE the request ever reaches HandleRequestAsync (breaks PowerToys, which
                // may resolve 'localhost' to 127.0.0.1/::1 and send that as the Host header).
                _listener.Prefixes.Add($"http://*:{ollamaPort}/");
                Logger.Log($"[LlamaBridge] Starting local listener on http://*:{ollamaPort}/ (wildcard host)");
                try
                {
                    _listener.Start();
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 5)
                {
                    _listener.Close();
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://localhost:{ollamaPort}/");
                    Logger.Log($"[LlamaBridge] Wildcard listener requires elevated URL ACL ({ex.Message}). Retrying on http://localhost:{ollamaPort}/ without administrator rights.");
                    _listener.Start();
                }
                _isRunning = true;

                // Dispatch the listening loop to the thread pool
                _ = Task.Run(() => ListenLoopAsync(llamacppPort));
                Logger.Log("[LlamaBridge] HttpListener successfully started.");
                return true;
            }
            catch (Exception ex)
            {
                try { _listener?.Close(); } catch { }
                _listener = null;
                _isRunning = false;
                _lastStartError = ex is HttpListenerException listenerException && listenerException.ErrorCode == 5
                    ? $"The bridge could not bind localhost:{ollamaPort} without administrator rights. A Windows HTTP URL ACL or another listener configuration is required: {ex.Message}"
                    : $"Could not start the bridge listener on port {ollamaPort}. The port may already be in use: {ex.GetType().Name}: {ex.Message}";
                Logger.Log($"[LlamaBridge] {_lastStartError}");
                return false;
            }
        }

        private static string BuildLlamaServerBaseUrl(string? apiUrl, int configuredPort)
        {
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                return string.Empty;
            }

            string rawApiUrl = apiUrl.Trim().TrimEnd('/');
            if (!Uri.TryCreate(rawApiUrl, UriKind.Absolute, out Uri? parsedApiUri)
                || (parsedApiUri.Scheme != Uri.UriSchemeHttp && parsedApiUri.Scheme != Uri.UriSchemeHttps))
            {
                return string.Empty;
            }

            int port = parsedApiUri.IsDefaultPort ? configuredPort : parsedApiUri.Port;
            return new UriBuilder(parsedApiUri.Scheme, parsedApiUri.Host, port).Uri.GetLeftPart(UriPartial.Authority);
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
            bool isGenerationRequest = request.HttpMethod == "POST" &&
                (string.Equals(path, "/v1/chat/completions", StringComparison.Ordinal) ||
                 string.Equals(path, "/api/chat", StringComparison.Ordinal));
            string? streamRequestId = null;
            long requestStartedTimestamp = Stopwatch.GetTimestamp();
            if (isGenerationRequest)
            {
                streamRequestId = Interlocked.Increment(ref _generationInboundRequestCount).ToString(CultureInfo.InvariantCulture);
                LlamaAgentLoopDiagnostics.LogLifecycle(
                    streamRequestId, "RequestStarted",
                    $"correlation_id={GetBridgeCorrelationId(streamRequestId)},method={request.HttpMethod},path={path},inbound_ordinal={streamRequestId},client_request_id={SafeCorrelationHeader(request, "X-Request-ID")},traceparent={SafeCorrelationHeader(request, "traceparent")},user_agent={SafeCorrelationHeader(request, "User-Agent")}");
            }
            else
            {
                Logger.Log($"[LlamaBridge-Inbound] {request.HttpMethod} -> {path}");
            }

            try
            {
                if (streamRequestId != null &&
                    await TryHandleOpenAiCompletionsAsync(request, response, streamRequestId))
                {
                    return;
                }

                if (streamRequestId != null &&
                    await TryHandleOllamaChatAsync(request, response, streamRequestId))
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

                if (await TryHandleV1ModelsAsync(request, response))
                {
                    return;
                }

                if (await TryHandleUpstreamPassthroughAsync(request, response))
                {
                    return;
                }

                await HandleUnknownRouteAsync(response, path);
            }
            catch (Exception ex)
            {
                Logger.Log($"[LlamaBridge-Exception{(streamRequestId == null ? string.Empty : $" #{streamRequestId}")}] Error processing request: {ex.Message}");
                if (streamRequestId != null)
                {
                    LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "HandlerException", $"exception={ex.GetType().Name}");
                }
                try { response.StatusCode = (int) HttpStatusCode.InternalServerError; response.OutputStream.Close(); } catch { }
            }
            finally
            {
                if (streamRequestId != null)
                {
                    LlamaAgentLoopDiagnostics.LogLifecycle(
                        streamRequestId, "RequestEnded",
                        $"http_status={response.StatusCode},elapsed_ms={Stopwatch.GetElapsedTime(requestStartedTimestamp).TotalMilliseconds:F0}");
                }
            }
        }

        private static string GetBridgeCorrelationId(string requestId) => $"{_bridgeInstanceId}:{requestId}";

        private static string ApplyChatTemplateCompatibility(string requestBody, string requestId)
        {
            if (_chatTemplateProfile is null)
            {
                return requestBody;
            }

            JsonObject request = JsonNode.Parse(requestBody) as JsonObject
                ?? throw new JsonException("Sanitized request is not a JSON object.");
            if (!_chatTemplateProfile.ApplyCompatibility(request))
            {
                return requestBody;
            }

            Logger.Log($"[LlamaBridge][Template #{requestId}] Applied dynamic template compatibility: reasoning_effort_supported={_chatTemplateProfile.SupportsReasoningEffort?.ToString() ?? "unknown"},parallel_tool_calls_supported={_chatTemplateProfile.SupportsParallelToolCalls?.ToString() ?? "unknown"},enable_thinking_mapped={request["chat_template_kwargs"]?["enable_thinking"]?.ToString() ?? "<unchanged>"}.");
            return request.ToJsonString();
        }

        private static async Task<HttpResponseMessage> SendUpstreamChatRequestAsync(string requestBody, string requestId, string route)
        {
            IReadOnlyList<string> fallbackCandidates = LlamaChatTemplateProfile.BuildCompatibilityRetryCandidates(requestBody);
            for (int attempt = 0; attempt <= fallbackCandidates.Count; attempt++)
            {
                string candidateBody = attempt == 0 ? requestBody : fallbackCandidates[attempt - 1];
                using var upstreamRequest = new HttpRequestMessage(HttpMethod.Post, $"{_llamaServerBaseUrl}/v1/chat/completions")
                {
                    Content = new StringContent(candidateBody, Encoding.UTF8, "application/json")
                };
                upstreamRequest.Headers.Add("X-Bridge-Request-ID", GetBridgeCorrelationId(requestId));

                long upstreamOrdinal = Interlocked.Increment(ref _generationUpstreamSendCount);
                LlamaAgentLoopDiagnostics.LogLifecycle(
                    requestId, "UpstreamStarted",
                    $"upstream_ordinal={upstreamOrdinal},attempt={attempt + 1},bridge_request_id={GetBridgeCorrelationId(requestId)},method=POST,path={route},sanitized_request_hash={LlamaAgentLoopDiagnostics.HashRequestBody(candidateBody)},sanitized_messages_hash={LlamaAgentLoopDiagnostics.HashRequestMessages(candidateBody)}");

                HttpResponseMessage upstreamResponse = await _httpClient.SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead);
                bool retryableStatus = upstreamResponse.StatusCode == HttpStatusCode.BadRequest ||
                                       upstreamResponse.StatusCode == HttpStatusCode.UnprocessableEntity;
                if (!retryableStatus || attempt == fallbackCandidates.Count)
                {
                    return upstreamResponse;
                }

                _ = await upstreamResponse.Content.ReadAsByteArrayAsync();
                Logger.Log($"[LlamaBridge #{requestId}] Upstream rejected request candidate {attempt + 1} with HTTP {(int)upstreamResponse.StatusCode}; retrying with compatibility candidate {attempt + 2}/{fallbackCandidates.Count + 1}.");
                LlamaAgentLoopDiagnostics.LogLifecycle(
                    requestId, "UpstreamCompatibilityRetry",
                    $"rejected_attempt={attempt + 1},status={(int)upstreamResponse.StatusCode},next_attempt={attempt + 2},route={route}");
                upstreamResponse.Dispose();
            }

            throw new InvalidOperationException("No llama-server request attempt produced a response.");
        }

        private static async Task WriteUpstreamErrorResponseAsync(HttpListenerResponse response, HttpResponseMessage upstreamResponse, string requestId)
        {
            byte[] errorBody = await upstreamResponse.Content.ReadAsByteArrayAsync();
            response.StatusCode = (int)upstreamResponse.StatusCode;
            response.ContentType = upstreamResponse.Content.Headers.ContentType?.ToString() ?? "application/json; charset=utf-8";
            response.ContentLength64 = errorBody.Length;
            if (errorBody.Length > 0)
            {
                await response.OutputStream.WriteAsync(errorBody);
            }
            response.Close();
            LlamaAgentLoopDiagnostics.LogLifecycle(
                requestId, "UpstreamErrorForwarded",
                $"status={(int)upstreamResponse.StatusCode},body_length={errorBody.Length}");
        }

        private static string ApplyLoopDetection(string requestId, string sanitizedBody, out bool shouldAbort)
        {
            JsonObject request = JsonNode.Parse(sanitizedBody) as JsonObject
                ?? throw new JsonException("Sanitized request is not a JSON object.");
            JsonArray messages = request["messages"] as JsonArray
                ?? throw new JsonException("Sanitized request is missing its messages array.");
            var detector = new LoopDetectionService(LoopDetectionConfig);
            LoopDetectionResult result = detector.ApplyToConversation(messages);
            shouldAbort = result.ShouldAbort;
            LlamaAgentLoopDiagnostics.LogLifecycle(
                requestId, "LoopDetectionDecision",
                $"scope=conversation,message_count={messages.Count},repeat_count={result.RepeatCount},interjections={result.InterjectionCount},detected={result.IsLoopDetected},interjection_applied={result.InterjectionMessage is not null},abort_applied={result.ShouldAbort}");
            return request.ToJsonString();
        }

        private static async Task SendLoopAbortAsync(HttpListenerResponse response, string requestId)
        {
            response.StatusCode = 429;
            response.ContentType = "application/json; charset=utf-8";
            byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                error = new
                {
                    message = "Generation stopped because repeated assistant actions continued after loop interjections.",
                    type = "loop_detected",
                    request_id = requestId
                }
            }));
            response.ContentLength64 = body.Length;
            await response.OutputStream.WriteAsync(body);
            response.Close();
        }

        private static string SafeCorrelationHeader(HttpListenerRequest request, string headerName)
        {
            string? value = request.Headers[headerName];
            if (string.IsNullOrWhiteSpace(value))
            {
                return "<absent>";
            }

            value = value.Replace('\r', ' ').Replace('\n', ' ');
            return value.Length <= 120 ? value : string.Concat(value.AsSpan(0, 120), "...");
        }

        private static bool IsEndpoint(HttpListenerRequest request, string method, string path)
        {
            return request.HttpMethod == method && string.Equals(request.Url?.AbsolutePath, path, StringComparison.Ordinal);
        }

        private static async Task<bool> TryHandleOpenAiCompletionsAsync(HttpListenerRequest request, HttpListenerResponse response, string streamRequestId)
        {
            if (!IsEndpoint(request, "POST", "/v1/chat/completions"))
            {
                return false;
            }

            Logger.Log($"[LlamaBridge][OpenAI #{streamRequestId}] Processing OpenAI-compatible direct stream...");
            using var reader = new StreamReader(request.InputStream);
            string requestBody = await reader.ReadToEndAsync();
            string sanitizedBody = LlamaStreamTransformer.SanitizeIncomingRequest(
                requestBody, _modelFamily, _detectedNumCtx,
                UserDefinedTemperature, UserDefinedRepetitionPenalty, UserDefinedPresencePenalty,
                UserDefinedTopP, UserDefinedMinP, UserDefinedTopK, UserDefinedReasoningEffort, UserDefinedReasoningBudget,
                trimThinkingBlocks: Enabled, keepLastMessages: KeepLastMessages,
                trimToolResults: TrimToolResults, toolCallMode: ToolCallMode);
            sanitizedBody = ApplyChatTemplateCompatibility(sanitizedBody, streamRequestId);
            ToolHistoryInspection history = LlamaAgentLoopDiagnostics.BeginRequest(
                "/v1/chat/completions", streamRequestId, requestBody, sanitizedBody, _modelFamily);
            if (!history.IsValid)
            {
                await SendInvalidToolHistoryAsync(response, streamRequestId, history);
                return true;
            }

            sanitizedBody = ApplyLoopDetection(streamRequestId, sanitizedBody, out bool shouldAbort);
            if (shouldAbort)
            {
                await SendLoopAbortAsync(response, streamRequestId);
                return true;
            }

            Logger.Log("========================================");
            Logger.Log("[REQUEST TO LLAMA - AFTER SANITIZE]");
            LogMessageLayout(sanitizedBody);
            LogUpstreamRequestMetadata("/v1/chat/completions", sanitizedBody);
            Logger.Log("========================================");

            if (GetGenerationStatsText)
            {
                s_generationStartUtc = DateTime.Now;
                LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "StatsProbeStarted", "method=GET,path=/slots,purpose=baseline_context_tokens");
                s_startContextTokens = await LlamaServerStats.GetCurrentContextTokensAsync();
                LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "StatsProbeCompleted", $"purpose=baseline_context_tokens,tokens={s_startContextTokens}");
            }

            using HttpResponseMessage upstreamRes = await SendUpstreamChatRequestAsync(sanitizedBody, streamRequestId, "/v1/chat/completions");
            LogUpstreamResponseHeaders(streamRequestId, upstreamRes);
            if (!upstreamRes.IsSuccessStatusCode)
            {
                await WriteUpstreamErrorResponseAsync(response, upstreamRes, streamRequestId);
                return true;
            }

            response.StatusCode = (int) upstreamRes.StatusCode;
            response.ContentType = upstreamRes.Content.Headers.ContentType?.ToString() ?? "text/event-stream";
            response.SendChunked = true;

            using Stream upstreamStream = await upstreamRes.Content.ReadAsStreamAsync();
            LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "UpstreamStreamStarted", "mode=openai_sse");
            LlamaStreamTransformer.OpenAiStreamTransformResult streamResult =
                await LlamaStreamTransformer.TransformOpenAiStreamWithDiagnosticsAsync(upstreamStream, response.OutputStream, _detectedModelName, GetGenerationStatsText, streamRequestId);
            LlamaAgentLoopDiagnostics.LogLifecycle(
                streamRequestId, "BridgeStreamProcessingEnded",
                $"done_received={streamResult.DoneReceived},finish_reason={streamResult.FinalFinishReason ?? "<none>"},completion={streamResult.Status}");

            if (streamResult.Status == LlamaStreamTransformer.OpenAiStreamCompletionStatus.LoopAborted)
            {
                Logger.Log($"[LlamaBridge][OpenAI #{streamRequestId}] Stream aborted by loop guard.");
                response.Abort();
                return true;
            }

            if (streamResult.ClientDisconnected)
            {
                LlamaAgentLoopDiagnostics.LogLifecycle(
                    streamRequestId, "ClientDisconnected",
                    $"observed=true,evidence=downstream_write_failure,completion={streamResult.Status}");
                Logger.Log($"[COPILOT_CONNECTION_CLOSED #{streamRequestId}] Bridge detected downstream write failure; stream status={streamResult.Status}.");
                response.Abort();
            }
            else
            {
                response.OutputStream.Close();
                LlamaAgentLoopDiagnostics.LogLifecycle(
                    streamRequestId, "ClientDisconnected",
                    "observed=false,evidence=no_downstream_write_failure;client_read_ack=unavailable");
                LlamaAgentLoopDiagnostics.LogLifecycle(
                    streamRequestId, "DownstreamCompleted",
                    "terminal_write_and_response_close=success,client_read_ack=unavailable");
            }

            Logger.Log($"[LlamaBridge][OpenAI #{streamRequestId}] Direct stream ended: {streamResult.Status}.");
            return true;
        }

        private static void LogUpstreamResponseHeaders(string requestId, HttpResponseMessage response)
        {
            static string HeaderValue(HttpResponseMessage message, string name)
            {
                if (!message.Headers.TryGetValues(name, out IEnumerable<string>? values) &&
                    !message.Content.Headers.TryGetValues(name, out values))
                {
                    return "<absent>";
                }

                string value = string.Join(",", values).Replace('\r', ' ').Replace('\n', ' ');
                return value.Length <= 120 ? value : string.Concat(value.AsSpan(0, 120), "...");
            }

            LlamaAgentLoopDiagnostics.LogLifecycle(
                requestId, "UpstreamResponseHeaders",
                $"status={(int)response.StatusCode},x_request_id={HeaderValue(response, "X-Request-ID")},x_task_id={HeaderValue(response, "X-Task-ID")},content_type={response.Content.Headers.ContentType?.ToString() ?? "<absent>"}");
        }

        private static void LogMessageLayout(string requestBody)
        {
            try
            {
                JsonNode? root = JsonNode.Parse(requestBody);
                if (root?["messages"] is not JsonArray messages)
                {
                    Logger.Log("[LlamaBridge] Sanitized request contains no messages array.");
                    return;
                }

                string layout = string.Join(", ", messages.Select((message, index) =>
                    $"{index}={message?["role"]?.ToString() ?? "<missing>"}"));
                Logger.Log($"[LlamaBridge] Sanitized message layout: [{layout}]");
            }
            catch (Exception ex)
            {
                Logger.Log($"[LlamaBridge] Could not inspect sanitized message layout: {ex.Message}");
            }
        }

        private static void LogUpstreamRequestMetadata(string route, string requestBody)
        {
            JsonObject? root;
            try
            {
                root = JsonNode.Parse(requestBody) as JsonObject;
            }
            catch (JsonException ex)
            {
                Logger.Log($"[LlamaBridge][Request Metadata] Could not parse outbound JSON ({ex.GetType().Name}, length={requestBody.Length}).");
                return;
            }

            if (root == null)
            {
                Logger.Log($"[LlamaBridge][Request Metadata] Outbound JSON was not an object (length={requestBody.Length}).");
                return;
            }

            static string SafeValue(string? value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return "<absent>";
                }

                string safeValue = value.Replace("\r", " ").Replace("\n", " ");
                return safeValue.Length <= 120 ? safeValue : string.Concat(safeValue.AsSpan(0, 120), "...");
            }

            static string Scalar(JsonNode? value)
            {
                if (value == null)
                {
                    return "<absent>";
                }

                if (value is JsonObject obj)
                {
                    return $"object(keys={string.Join(",", obj.Select(pair => SafeValue(pair.Key)))})";
                }
                if (value is JsonArray array)
                {
                    return $"array(count={array.Count})";
                }

                return SafeValue(value.ToString());
            }

            static int ToolCallCount(JsonObject? message)
            {
                return message?["tool_calls"] is JsonArray calls ? calls.Count : 0;
            }

            static int ArgumentsLength(JsonNode? arguments)
            {
                return arguments?.ToString().Length ?? 0;
            }

            JsonArray messages = root["messages"] as JsonArray ?? [];
            JsonArray tools = root["tools"] as JsonArray ?? [];
            JsonObject? lastMessage = messages.LastOrDefault() as JsonObject;
            JsonObject? latestAssistantToolMessage = null;
            JsonObject? lastToolResult = null;
            for (int index = 0; index < messages.Count; index++)
            {
                if (messages[index] is not JsonObject message)
                {
                    continue;
                }

                string role = message["role"]?.ToString() ?? string.Empty;
                if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) && ToolCallCount(message) > 0)
                {
                    latestAssistantToolMessage = message;
                }
                if (string.Equals(role, "tool", StringComparison.OrdinalIgnoreCase))
                {
                    lastToolResult = message;
                }
            }

            JsonArray? latestToolCalls = latestAssistantToolMessage?["tool_calls"] as JsonArray;
            string toolCallDetails = latestToolCalls == null
                ? "<none>"
                : string.Join(";", latestToolCalls.Select(call =>
                {
                    JsonObject? function = call?["function"] as JsonObject;
                    return $"id={SafeValue(call?["id"]?.ToString())},type={SafeValue(call?["type"]?.ToString())},name={SafeValue(function?["name"]?.ToString())},arguments_length={ArgumentsLength(function?["arguments"])}";
                }));

            string latestCallId = latestToolCalls?.LastOrDefault()?["id"]?.ToString() ?? string.Empty;
            string lastToolCallId = lastToolResult?["tool_call_id"]?.ToString() ?? string.Empty;
            string correlation = lastToolResult == null
                ? "<no tool result>"
                : latestToolCalls?.Any(call => string.Equals(call?["id"]?.ToString(), lastToolCallId, StringComparison.Ordinal)) == true
                    ? "true"
                    : "false";
            JsonObject? templateKwargs = root["chat_template_kwargs"] as JsonObject;
            string templateKwargKeys = templateKwargs == null
                ? "<absent>"
                : string.Join(",", templateKwargs.Select(pair => SafeValue(pair.Key)));
            JsonNode? toolChoice = root["tool_choice"];
            string toolChoiceSummary = toolChoice is JsonObject toolChoiceObject
                ? $"type={Scalar(toolChoiceObject["type"])},name={Scalar(toolChoiceObject["function"]?["name"])}"
                : Scalar(toolChoice);

            Logger.Log(
                $"[LlamaBridge][Request Metadata] route={route}, model={SafeValue(root["model"]?.ToString())}, stream={Scalar(root["stream"])}, " +
                $"max_tokens={Scalar(root["max_tokens"])}, max_completion_tokens={Scalar(root["max_completion_tokens"])}, n_predict={Scalar(root["n_predict"])}, " +
                $"temperature={Scalar(root["temperature"])}, top_p={Scalar(root["top_p"])}, top_k={Scalar(root["top_k"])}, min_p={Scalar(root["min_p"])}, " +
                $"repeat_penalty={Scalar(root["repeat_penalty"])}, " +
                $"reasoning_effort={Scalar(root["reasoning_effort"])}, reasoning_budget={Scalar(root["reasoning_budget"])}, thinking_budget_tokens={Scalar(root["thinking_budget_tokens"])}, " +
                $"reasoning={Scalar(root["reasoning"])}, reasoning_format={Scalar(root["reasoning_format"])}, chat_template_kwargs_keys=[{templateKwargKeys}], " +
                $"enable_thinking={Scalar(templateKwargs?["enable_thinking"])}, force_nonempty_content={Scalar(templateKwargs?["force_nonempty_content"])}, " +
                $"parallel_tool_calls={Scalar(root["parallel_tool_calls"])}, tool_choice={toolChoiceSummary}, number_of_tools={tools.Count}, " +
                $"number_of_messages={messages.Count}, last_message_role={SafeValue(lastMessage?["role"]?.ToString())}, " +
                $"last_message_has_tool_calls={ToolCallCount(lastMessage) > 0}, last_message_tool_call_count={ToolCallCount(lastMessage)}, " +
                $"last_tool_result_present={lastToolResult != null}, last_tool_result_call_id={SafeValue(lastToolCallId)}, " +
                $"last_tool_result_content_length={lastToolResult?["content"]?.ToString().Length ?? 0}, " +
                $"latest_assistant_tool_call_id={SafeValue(latestCallId)}, last_tool_result_matches_latest_assistant_call={correlation}, " +
                $"latest_assistant_tool_calls=[{toolCallDetails}], bridge_reasoning_effort={SafeValue(LlamaOllamaBridge.UserDefinedReasoningEffort)}, " +
                $"bridge_reasoning_budget={LlamaOllamaBridge.UserDefinedReasoningBudget}.");
        }

        private static async Task<bool> TryHandleOllamaChatAsync(HttpListenerRequest request, HttpListenerResponse response, string streamRequestId)
        {
            if (!IsEndpoint(request, "POST", "/api/chat"))
            {
                return false;
            }

            Logger.Log($"[LlamaBridge][Ollama #{streamRequestId}] Translating NDJSON-Ollama request...");
            using var reader = new StreamReader(request.InputStream);
            string body = await reader.ReadToEndAsync();
            JsonNode? ollamaReq = JsonNode.Parse(body);

            // Build messages array, handling Ollama's separate 'system' field
            var messagesArray = new JsonArray();
            if (ollamaReq is JsonObject ollamaObj)
            {
                bool hasSystemInMessages = false;
                if (ollamaObj["messages"] is JsonArray ollamaMessages)
                {
                    foreach (JsonNode? msg in ollamaMessages)
                    {
                        if (msg is JsonObject msgObj &&
                            string.Equals(msgObj["role"]?.ToString(), "system", StringComparison.OrdinalIgnoreCase))
                        {
                            hasSystemInMessages = true;
                            break;
                        }
                    }
                }

                string? systemPrompt = ollamaObj["system"]?.GetValue<string>();
                if (!hasSystemInMessages && !string.IsNullOrWhiteSpace(systemPrompt))
                {
                    messagesArray.Add(new JsonObject { ["role"] = "system", ["content"] = systemPrompt });
                }

                if (ollamaObj["messages"] is JsonArray ollamaMessages2)
                {
                    foreach (JsonNode? msg in ollamaMessages2)
                    {
                        if (msg is JsonObject msgObj)
                        {
                            messagesArray.Add(msgObj.DeepClone());
                        }
                    }
                }
            }

            var openAiReq = new JsonObject
            {
                ["model"] = _detectedModelName,
                ["messages"] = messagesArray,
                ["stream"] = ollamaReq?["stream"]?.DeepClone() ?? JsonValue.Create(true)
            };
            if (ollamaReq is JsonObject ollamaRequest)
            {
                foreach (string field in new[] { "tools", "tool_choice", "parallel_tool_calls" })
                {
                    if (ollamaRequest[field] is JsonNode value)
                    {
                        openAiReq[field] = value.DeepClone();
                    }
                }
            }

            // Sanitize: ensures system message is first, trims context, normalizes tool history
            string sanitizedBody = LlamaStreamTransformer.SanitizeIncomingRequest(
                openAiReq.ToJsonString(), _modelFamily, _detectedNumCtx,
                UserDefinedTemperature, UserDefinedRepetitionPenalty, UserDefinedPresencePenalty,
                UserDefinedTopP, UserDefinedMinP, UserDefinedTopK, UserDefinedReasoningEffort, UserDefinedReasoningBudget,
                trimThinkingBlocks: Enabled, keepLastMessages: KeepLastMessages,
                trimToolResults: TrimToolResults, toolCallMode: ToolCallMode);
            sanitizedBody = ApplyChatTemplateCompatibility(sanitizedBody, streamRequestId);
            ToolHistoryInspection history = LlamaAgentLoopDiagnostics.BeginRequest(
                "/api/chat->/v1/chat/completions", streamRequestId, openAiReq.ToJsonString(), sanitizedBody, _modelFamily,
                requireToolCallIds: false, inboundRequestBody: body);
            if (!history.IsValid)
            {
                await SendInvalidToolHistoryAsync(response, streamRequestId, history);
                return true;
            }

            sanitizedBody = ApplyLoopDetection(streamRequestId, sanitizedBody, out bool shouldAbort);
            if (shouldAbort)
            {
                await SendLoopAbortAsync(response, streamRequestId);
                return true;
            }

            Logger.Log("========================================");
            Logger.Log("[REQUEST TO LLAMA - AFTER SANITIZE (OLLAMA PATH)]");
            LogMessageLayout(sanitizedBody);
            LogUpstreamRequestMetadata("/api/chat->/v1/chat/completions", sanitizedBody);
            Logger.Log("========================================");

            using HttpResponseMessage upstreamRes = await SendUpstreamChatRequestAsync(sanitizedBody, streamRequestId, "/api/chat->/v1/chat/completions");
            LogUpstreamResponseHeaders(streamRequestId, upstreamRes);
            if (!upstreamRes.IsSuccessStatusCode)
            {
                await WriteUpstreamErrorResponseAsync(response, upstreamRes, streamRequestId);
                return true;
            }

            response.ContentType = "application/x-ndjson; charset=utf-8";
            response.StatusCode = (int) upstreamRes.StatusCode;
            response.SendChunked = true;

            LlamaStreamTransformer.OpenAiStreamTransformResult? streamResult = null;
            if (ollamaReq?["stream"]?.GetValue<bool>() == false)
            {
                await WriteNonStreamingOllamaResponseAsync(response, upstreamRes, streamRequestId);
            }
            else
            {
                streamResult = await WriteStreamingOllamaResponseAsync(response, upstreamRes, streamRequestId);
            }

            if (streamResult?.Status == LlamaStreamTransformer.OpenAiStreamCompletionStatus.LoopAborted)
            {
                Logger.Log($"[LlamaBridge][Ollama #{streamRequestId}] Stream aborted by loop guard.");
                response.Abort();
                return true;
            }

            if (streamResult?.ClientDisconnected == true)
            {
                LlamaAgentLoopDiagnostics.LogLifecycle(
                    streamRequestId, "ClientDisconnected",
                    $"observed=true,evidence=downstream_write_failure,completion={streamResult.Status}");
                response.Abort();
            }
            else
            {
                response.OutputStream.Close();
                LlamaAgentLoopDiagnostics.LogLifecycle(
                    streamRequestId, "ClientDisconnected",
                    "observed=false,evidence=no_downstream_write_failure;client_read_ack=unavailable");
                LlamaAgentLoopDiagnostics.LogLifecycle(
                    streamRequestId, "DownstreamCompleted",
                    "terminal_write_and_response_close=success,client_read_ack=unavailable");
            }

            Logger.Log(streamResult == null
                ? $"[LlamaBridge][Ollama #{streamRequestId}] Non-stream response ended."
                : $"[LlamaBridge][Ollama #{streamRequestId}] Stream ended: {streamResult.Status}.");
            return true;
        }

        private static async Task WriteNonStreamingOllamaResponseAsync(HttpListenerResponse response, HttpResponseMessage upstreamRes, string streamRequestId)
        {
            string resContent = await upstreamRes.Content.ReadAsStringAsync();
            LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "UpstreamResponseConsumed", $"mode=non_streaming,body_chars={resContent.Length}");
            JsonNode? openAiRes = JsonNode.Parse(resContent);
            Logger.Log($"[AGENT_LOOP #{streamRequestId}] NonStreamingResponse chars={resContent.Length},response_hash={LlamaAgentLoopDiagnostics.HashForTests(openAiRes)}.");
            JsonObject? choice = openAiRes?["choices"]?[0] as JsonObject;
            JsonObject? openAiMessage = choice?["message"] as JsonObject;
            LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "FirstToken", "source=non_streaming_response_body");
            string? finishReason = choice?["finish_reason"]?.ToString();
            if (finishReason != null)
            {
                LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "FinishReason", $"value={finishReason}");
            }
            if (openAiMessage?["tool_calls"] is JsonArray responseToolCalls && responseToolCalls.Count > 0)
            {
                LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "ToolCallStarted", $"source=non_streaming_response,count={responseToolCalls.Count}");
                LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "ToolCallCompleted", $"source=non_streaming_response,count={responseToolCalls.Count}");
            }
            LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "DoneReceived", "not_applicable_non_streaming_response");
            LlamaAgentLoopDiagnostics.BeginResponse(streamRequestId);
            if (openAiMessage != null)
            {
                var diagnosticChunk = new JsonObject
                {
                    ["choices"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["index"] = choice?["index"]?.DeepClone() ?? JsonValue.Create(0),
                            ["delta"] = openAiMessage.DeepClone(),
                            ["finish_reason"] = choice?["finish_reason"]?.DeepClone()
                        }
                    }
                };
                LlamaAgentLoopDiagnostics.ObserveResponseChunk(streamRequestId, diagnosticChunk);
            }
            LlamaAgentLoopDiagnostics.CompleteResponse(
                streamRequestId, doneReceived: openAiMessage != null, choice?["finish_reason"]?.ToString());
            var ollamaMessage = new JsonObject
            {
                ["role"] = "assistant",
                ["content"] = openAiMessage?["content"]?.ToString() ?? string.Empty
            };

            if (openAiMessage?["tool_calls"] is JsonArray openAiCalls)
            {
                var ollamaCalls = new JsonArray();
                foreach (JsonNode? call in openAiCalls)
                {
                    JsonObject? function = call?["function"] as JsonObject;
                    ollamaCalls.Add(new JsonObject
                    {
                        ["function"] = new JsonObject
                        {
                            ["name"] = function?["name"]?.ToString() ?? throw new JsonException("A tool call has no function name."),
                            ["arguments"] = ParseOllamaToolArguments(function?["arguments"])
                        }
                    });
                }
                ollamaMessage["tool_calls"] = ollamaCalls;
            }

            var ollamaRes = new JsonObject
            {
                ["model"] = _detectedModelName,
                ["message"] = ollamaMessage,
                ["done"] = true
            };
            if (choice?["finish_reason"] != null)
            {
                ollamaRes["done_reason"] = choice["finish_reason"]!.ToString();
            }

            byte[] buffer = Encoding.UTF8.GetBytes(ollamaRes.ToJsonString() + "\n");
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "DownstreamDoneWritten", "ollama_done=true");
            LlamaAgentLoopDiagnostics.LogLifecycle(
                streamRequestId, "ClientDisconnected",
                "observed=false,evidence=no_downstream_write_failure;client_read_ack=unavailable");
        }

        private static JsonObject ParseOllamaToolArguments(JsonNode? arguments)
        {
            if (arguments == null)
            {
                return new JsonObject();
            }
            if (arguments is JsonObject argumentsObject)
            {
                return (JsonObject) argumentsObject.DeepClone();
            }
            if (arguments is JsonValue value && value.TryGetValue(out string? jsonArguments) && jsonArguments != null)
            {
                return JsonNode.Parse(jsonArguments)?.AsObject()
                    ?? throw new JsonException("Tool-call arguments must be a JSON object.");
            }
            throw new JsonException("Tool-call arguments must be a JSON object.");
        }

        private static async Task<LlamaStreamTransformer.OpenAiStreamTransformResult> WriteStreamingOllamaResponseAsync(HttpListenerResponse response, HttpResponseMessage upstreamRes, string streamRequestId)
        {
            using Stream responseStream = await upstreamRes.Content.ReadAsStreamAsync();
            LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "UpstreamStreamStarted", "mode=ollama_ndjson");
            LlamaStreamTransformer.OpenAiStreamTransformResult result =
                await LlamaStreamTransformer.TransformOpenAiStreamToOllamaAsync(responseStream, response.OutputStream, _detectedModelName, streamRequestId);
            LlamaAgentLoopDiagnostics.LogLifecycle(
                streamRequestId, "BridgeStreamProcessingEnded",
                $"done_received={result.DoneReceived},finish_reason={result.FinalFinishReason ?? "<none>"},completion={result.Status}");
            return result;
        }

        private static async Task SendInvalidToolHistoryAsync(
            HttpListenerResponse response,
            string streamRequestId,
            ToolHistoryInspection history)
        {
            Logger.Log($"[LlamaBridge][#{streamRequestId}] Refusing invalid tool history before llama-server request.");
            response.StatusCode = (int)HttpStatusCode.BadRequest;
            response.ContentType = "application/json; charset=utf-8";
            byte[] body = Encoding.UTF8.GetBytes(new JsonObject
            {
                ["error"] = "Invalid assistant/tool message history.",
                ["details"] = new JsonArray(history.Issues.Select(issue => JsonValue.Create(issue)).ToArray())
            }.ToJsonString());
            await response.OutputStream.WriteAsync(body);
            response.OutputStream.Close();
        }

        private static async Task<bool> TryHandleApiTagsAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!IsEndpoint(request, "GET", "/api/tags"))
            {
                return false;
            }

            long fileSize = GetModelFileSize();
            string[] capabilities = _supportsVision
                ? new[] { "completion", "multimodal" }
                : new[] { "completion" };
            string[] tags = _modelTags.Count > 0 ? _modelTags.ToArray() : new[] { _quantizationLevel };

            var tagsData = new
            {
                models = new[]
                {
                    new {
                        name = _detectedModelName,
                        model = _detectedModelName,
                        modified_at = GetModelModifiedAt().ToString("o"),
                        size = fileSize,
                        digest = "proxy_identity_digest",
                        type = "model",
                        description = _modelDescription,
                        tags = tags,
                        capabilities = capabilities,
                        parameters = _modelNParams > 0 ? _modelNParams.ToString(CultureInfo.InvariantCulture) : string.Empty,
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

            long psFileSize = GetModelFileSize();
            string[] psCapabilities = _supportsVision
                ? new[] { "completion", "multimodal" }
                : new[] { "completion" };
            string[] psTags = _modelTags.Count > 0 ? _modelTags.ToArray() : new[] { _quantizationLevel };

            var psData = new
            {
                models = new[]
                {
                    new {
                        name = _detectedModelName,
                        model = _detectedModelName,
                        size = psFileSize,
                        digest = "proxy_identity_digest",
                        type = "model",
                        description = _modelDescription,
                        tags = psTags,
                        capabilities = psCapabilities,
                        parameters = _modelNParams > 0 ? _modelNParams.ToString(CultureInfo.InvariantCulture) : string.Empty,
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

        private static readonly (string method, string path)[] UpstreamPassthroughRoutes =
        [
            ("POST", "/v1/completions"),
            ("POST", "/v1/embeddings"),
            ("GET", "/props"),
        ];

        private static async Task<bool> TryHandleV1ModelsAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!IsEndpoint(request, "GET", "/v1/models"))
            {
                return false;
            }

            string modelId = _detectedModelName;
            long fileSize = GetModelFileSize();

            // created: LastWriteTimeUtc der GGUF-Datei (FileInfo), Fallback 0 wenn nicht vorhanden
            long createdUnixTs = 0;
            string modifiedAtIso = string.Empty;
            if (File.Exists(_detectedModelId))
            {
                try
                {
                    var lastWrite = File.GetLastWriteTimeUtc(_detectedModelId);
                    createdUnixTs = (long)(lastWrite - DateTime.UnixEpoch).TotalSeconds;
                    modifiedAtIso = lastWrite.ToString("o");
                }
                catch { }
            }

            string[] capabilities = _supportsVision
                ? new[] { "completion", "multimodal" }
                : new[] { "completion" };

            // Ollama-Format: models-Array
            var modelsArray = new JsonArray();
            modelsArray.Add(new JsonObject
            {
                ["name"] = modelId,
                ["model"] = modelId,
                ["modified_at"] = modifiedAtIso,
                ["size"] = fileSize,
                ["digest"] = "proxy_identity_digest",
                ["type"] = "model",
                ["description"] = _modelDescription,
                ["tags"] = BuildJsonArray(_modelTags.Count > 0 ? _modelTags.ToArray() : new[] { _quantizationLevel }),
                ["capabilities"] = BuildJsonArray(capabilities),
                ["parameters"] = _modelNParams > 0 ? _modelNParams.ToString(CultureInfo.InvariantCulture) : string.Empty,
                ["details"] = new JsonObject
                {
                    ["parent_model"] = _modelParentModel,
                    ["format"] = "gguf",
                    ["family"] = _modelFamily,
                    ["families"] = BuildJsonArray(new[] { _modelFamily }),
                    ["parameter_size"] = _parameterSize,
                    ["quantization_level"] = _quantizationLevel,
                    ["n_ctx"] = _detectedNumCtx,
                    ["context_length"] = _detectedNumCtx
                }
            });

            // OpenAI-Format: data-Array
            var metaObj = new JsonObject
            {
                ["vocab_type"] = true,
                ["n_vocab"] = _modelNVocab,
                ["n_ctx"] = _detectedNumCtx,
                ["n_ctx_train"] = _modelNCtxTrain,
                ["n_embd"] = _modelNEmbd,
                ["n_params"] = _modelNParams,
                ["size"] = fileSize,
                ["ftype"] = !string.Equals(_modelFtype, "unknown", StringComparison.OrdinalIgnoreCase)
                    ? _modelFtype
                    : _quantizationLevel
            };

            var dataArray = new JsonArray();
            dataArray.Add(new JsonObject
            {
                ["id"] = modelId,
                ["aliases"] = BuildJsonArray(new[] { modelId }),
                ["tags"] = new JsonArray(),
                ["object"] = "model",
                ["created"] = createdUnixTs,
                ["owned_by"] = "llamacpp",
                ["meta"] = metaObj
            });

            // Dual-Format: Ollama (models) + OpenAI (data) in einem Objekt
            var v1Data = new JsonObject
            {
                ["models"] = modelsArray,
                ["object"] = "list",
                ["data"] = dataArray
            };

            await SendJsonResponseAsync(response, v1Data);
            return true;
        }

        private static async Task<bool> TryHandleUpstreamPassthroughAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            string path = request.Url?.AbsolutePath ?? "";
            bool isWhitelisted = UpstreamPassthroughRoutes.Any(route => route.method == request.HttpMethod && route.path == path);
            if (!isWhitelisted)
            {
                return false;
            }

            Logger.Log($"[LlamaBridge] Passthrough: {request.HttpMethod} -> {path}");

            string? requestBody = null;
            if (request.HasEntityBody)
            {
                using var reader = new StreamReader(request.InputStream);
                requestBody = await reader.ReadToEndAsync();
            }

            try
            {
                using var upstreamReq = new HttpRequestMessage(new HttpMethod(request.HttpMethod), $"{_llamaServerBaseUrl}{path}")
                {
                    Content = requestBody is null ? null : new StringContent(requestBody, Encoding.UTF8, "application/json")
                };

                using HttpResponseMessage upstreamRes = await _httpClient.SendAsync(upstreamReq, HttpCompletionOption.ResponseHeadersRead);
                response.StatusCode = (int) upstreamRes.StatusCode;
                string contentType = upstreamRes.Content.Headers.ContentType?.ToString() ?? "application/json";
                response.ContentType = contentType.Contains("event-stream", StringComparison.OrdinalIgnoreCase) ? "text/event-stream" : contentType;
                response.SendChunked = true;

                using Stream upstreamStream = await upstreamRes.Content.ReadAsStreamAsync();
                await upstreamStream.CopyToAsync(response.OutputStream);
                response.OutputStream.Close();
                Logger.Log($"[LlamaBridge] Passthrough completed: {request.HttpMethod} -> {path} ({(int) upstreamRes.StatusCode})");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"[LlamaBridge-Exception] Passthrough failed for {request.HttpMethod} -> {path}: {ex.GetType().Name}: {ex.Message}");
                response.StatusCode = (int) HttpStatusCode.BadGateway;
                response.OutputStream.Close();
                return true;
            }
        }

        private static async Task HandleUnknownRouteAsync(HttpListenerResponse response, string path)
        {
            Logger.Log($"[LlamaBridge] Unknown path rejected (404): {path}");
            response.StatusCode = (int) HttpStatusCode.NotFound;
            await response.OutputStream.FlushAsync();
            response.OutputStream.Close();
        }

        private static JsonArray BuildJsonArray(string[] items)
        {
            var arr = new JsonArray();
            foreach (string item in items)
            {
                arr.Add(item);
            }
            return arr;
        }

        private static object CreateModelDetails()
        {
            return new
            {
                parent_model = _modelParentModel,
                format = "gguf",
                family = _modelFamily,
                families = new[] { _modelFamily },
                parameter_size = _parameterSize,
                quantization_level = _quantizationLevel
            };
        }

        private static long GetModelFileSize()
        {
            if (_modelFileSizeBytes > 0)
            {
                return _modelFileSizeBytes;
            }
            try { return File.Exists(_detectedModelId) ? new FileInfo(_detectedModelId).Length : 0; } catch { return 0; }
        }

        private static DateTime GetModelModifiedAt()
        {
            try { return File.Exists(_detectedModelId) ? File.GetLastWriteTimeUtc(_detectedModelId) : DateTime.UtcNow; } catch { return DateTime.UtcNow; }
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

        private static string SanitizeModelId(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                return "local-llama-model";
            }

            // Wenn die ID ein Dateipfad ist (enthält \ oder /), nimm nur den File-Name ohne Extension
            bool looksLikePath = modelId.Contains('\\') || modelId.Contains('/');
            if (looksLikePath)
            {
                string fileName = Path.GetFileName(modelId);
                return Path.GetFileNameWithoutExtension(fileName);
            }

            // Wenn die ID bereits eine .gguf-Extension hat, entferne sie
            if (modelId.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileNameWithoutExtension(modelId);
            }

            return modelId;
        }

        private static string FormatParameterSize(long nParams)
        {
            if (nParams <= 0)
            {
                return "unknown";
            }

            double billions = nParams / 1_000_000_000.0;
            if (billions < 1.0)
            {
                long millions = nParams / 1_000_000L;
                return $"{millions}M";
            }

            // Runden auf eine Dezimalstelle, aber ganze Zahlen ohne ".0"
            string formatted = billions.ToString("0.##", CultureInfo.InvariantCulture);
            return $"{formatted}B";
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