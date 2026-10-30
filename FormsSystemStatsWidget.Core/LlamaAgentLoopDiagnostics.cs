using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FormsSystemStatsWidget.Core;

internal sealed record ToolHistoryInspection(bool IsValid, IReadOnlyList<string> Issues);
internal sealed record AgentToolCallSnapshot(
    string Name,
    int ArgumentsLength,
    string ArgumentsHash,
    long? CreatedRequest,
    long? ResultReceivedRequest,
    long? ReappearedRequest);

internal static class LlamaAgentLoopDiagnostics
{
    private sealed class ToolCallState
    {
        public string Name { get; set; } = string.Empty;
        public int ArgumentsLength { get; set; }
        public string ArgumentsHash { get; set; } = HashText(string.Empty);
        public long? CreatedRequest { get; set; }
        public long? ResultReceivedRequest { get; set; }
        public long? ReappearedRequest { get; set; }
    }

    private sealed class StreamedToolCall
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public string? Index { get; set; }
        public StringBuilder Name { get; } = new();
        public StringBuilder Arguments { get; } = new();
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<string, ToolCallState> ToolCalls = new(StringComparer.Ordinal);
    private static readonly Queue<string> ToolCallOrder = new();
    private static readonly Dictionary<string, Dictionary<string, StreamedToolCall>> ActiveResponses = new(StringComparer.Ordinal);
    private const int MaximumRememberedToolCalls = 4096;

    internal static void LogLifecycle(string requestId, string state, string? details = null)
    {
        string timestamp = DateTimeOffset.UtcNow.ToString("O");
        Logger.Log(details == null
            ? $"[REQUEST_LIFECYCLE {timestamp} #{requestId}] {state}"
            : $"[REQUEST_LIFECYCLE {timestamp} #{requestId}] {state} {details}");
    }

    internal static string HashRequestBody(string requestBody)
    {
        JsonObject? request = ParseObject(requestBody);
        return request == null ? HashText(requestBody) : HashNode(request);
    }

    internal static string HashRequestMessages(string requestBody)
    {
        return HashNode(ParseObject(requestBody)?["messages"]);
    }

    internal static ToolHistoryInspection ValidateToolHistory(JsonArray messages)
    {
        var calls = new Dictionary<string, string>(StringComparer.Ordinal);
        var returned = new HashSet<string>(StringComparer.Ordinal);
        var issues = new List<string>();

        for (int messageIndex = 0; messageIndex < messages.Count; messageIndex++)
        {
            if (messages[messageIndex] is not JsonObject message)
            {
                continue;
            }

            string role = message["role"]?.ToString() ?? string.Empty;
            if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) &&
                message["tool_calls"] is JsonArray toolCalls)
            {
                foreach (JsonNode? toolCall in toolCalls)
                {
                    string id = toolCall?["id"]?.ToString() ?? string.Empty;
                    string name = toolCall?["function"]?["name"]?.ToString() ?? "<missing>";
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        issues.Add($"assistant tool call at message {messageIndex} has no id");
                        continue;
                    }

                    if (!calls.TryAdd(id, name))
                    {
                        issues.Add($"assistant tool call id {id} is repeated at message {messageIndex}");
                    }
                }
            }
            else if (string.Equals(role, "tool", StringComparison.OrdinalIgnoreCase))
            {
                string id = message["tool_call_id"]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id))
                {
                    issues.Add($"tool result at message {messageIndex} has no tool_call_id");
                }
                else if (!calls.ContainsKey(id))
                {
                    issues.Add($"tool result id {id} at message {messageIndex} has no preceding assistant tool call");
                }
                else if (!returned.Add(id))
                {
                    issues.Add($"tool result id {id} is duplicated at message {messageIndex}");
                }
            }
        }

        return new ToolHistoryInspection(issues.Count == 0, issues);
    }

    internal static ToolHistoryInspection BeginRequest(
        string route,
        string requestId,
        string originalBody,
        string sanitizedBody,
        string modelFamily,
        bool requireToolCallIds = true,
        string? inboundRequestBody = null)
    {
        string inboundBody = inboundRequestBody ?? originalBody;
        JsonObject? original = ParseObject(originalBody);
        JsonObject? sanitized = ParseObject(sanitizedBody);
        JsonArray originalMessages = original?["messages"] as JsonArray ?? [];
        JsonArray sanitizedMessages = sanitized?["messages"] as JsonArray ?? [];
        ToolHistoryInspection originalInspection = requireToolCallIds
            ? ValidateToolHistory(originalMessages)
            : new ToolHistoryInspection(true, Array.Empty<string>());
        ToolHistoryInspection sanitizedInspection = !requireToolCallIds ||
            string.Equals(modelFamily, "qwen", StringComparison.OrdinalIgnoreCase)
            ? new ToolHistoryInspection(true, Array.Empty<string>())
            : ValidateToolHistory(sanitizedMessages);

        var issues = originalInspection.Issues
            .Select(issue => $"incoming: {issue}")
            .Concat(sanitizedInspection.Issues.Select(issue => $"sanitized: {issue}"))
            .ToArray();

        string validationStatus = !requireToolCallIds
            ? "SKIPPED_OLLAMA_IDLESS"
            : issues.Length == 0 ? "PASS" : "FAIL";
        LogRequestSummary(route, requestId, original, sanitized, originalBody, sanitizedBody, validationStatus, inboundBody);
        if (issues.Length > 0)
        {
            Logger.Log($"[AGENT_LOOP #{requestId}] INVALID_TOOL_HISTORY: {string.Join(" | ", issues.Select(SafeLogValue))}");
            return new ToolHistoryInspection(false, issues);
        }

        TrackIncomingHistory(requestId, originalMessages);
        return new ToolHistoryInspection(true, Array.Empty<string>());
    }

    internal static void BeginResponse(string requestId)
    {
        lock (Gate)
        {
            ActiveResponses[requestId] = new Dictionary<string, StreamedToolCall>(StringComparer.Ordinal);
        }
    }

    internal static void ObserveResponseChunk(string requestId, JsonNode? chunk)
    {
        if (chunk?["choices"] is not JsonArray choices)
        {
            return;
        }

        lock (Gate)
        {
            if (!ActiveResponses.TryGetValue(requestId, out Dictionary<string, StreamedToolCall>? calls))
            {
                return;
            }

            foreach (JsonNode? choice in choices)
            {
                if (choice?["delta"]?["tool_calls"] is not JsonArray deltas)
                {
                    continue;
                }

                string choiceIndex = choice?["index"]?.ToString() ?? "0";
                foreach (JsonNode? delta in deltas)
                {
                    string? index = delta?["index"]?.ToString();
                    string? id = delta?["id"]?.ToString();
                    string toolKey = index ?? id ?? $"anonymous:{calls.Count}";
                    string key = $"{choiceIndex}:{toolKey}";
                    if (!calls.TryGetValue(key, out StreamedToolCall? call))
                    {
                        call = new StreamedToolCall();
                        calls.Add(key, call);
                    }
                    call.Index = index;

                    if (!string.IsNullOrEmpty(id))
                    {
                        call.Id = id;
                    }

                    string? type = delta?["type"]?.ToString();
                    if (!string.IsNullOrEmpty(type))
                    {
                        call.Type = type;
                    }

                    string? name = delta?["function"]?["name"]?.ToString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        _ = call.Name.Append(name);
                    }

                    JsonNode? arguments = delta?["function"]?["arguments"];
                    if (arguments != null)
                    {
                        _ = call.Arguments.Append(arguments.ToString());
                    }
                }
            }
        }
    }

    internal static void CompleteResponse(string requestId, bool doneReceived, string? finishReason)
    {
        lock (Gate)
        {
            if (!ActiveResponses.Remove(requestId, out Dictionary<string, StreamedToolCall>? calls))
            {
                return;
            }

            bool completeToolCallResponse = doneReceived &&
                string.Equals(finishReason, "tool_calls", StringComparison.Ordinal);
            foreach (StreamedToolCall call in calls.Values)
            {
                string id = call.Id ?? "<missing>";
                string name = call.Name.ToString();
                string arguments = call.Arguments.ToString();
                string argumentsHash = HashText(arguments);
                Logger.Log(
                    $"[AGENT_LOOP #{requestId}] ToolCall index={call.Index ?? "<none>"},id={SafeLogValue(id)},name={SafeLogValue(name)},arguments_length={arguments.Length},arguments_hash={argumentsHash},state={(completeToolCallResponse ? "complete" : "incomplete")}.");

                if (call.Id is null)
                {
                    continue;
                }

                if (ToolCalls.TryGetValue(call.Id, out ToolCallState? previous))
                {
                    Logger.Log(
                        $"[AGENT_LOOP] ToolCall id={SafeLogValue(call.Id)} name={SafeLogValue(name)} created=#{previous.CreatedRequest?.ToString() ?? "history"} result_received=#{previous.ResultReceivedRequest?.ToString() ?? "<none>"} reappeared=#{requestId}.");
                    previous.Name = name;
                    previous.ArgumentsLength = arguments.Length;
                    previous.ArgumentsHash = argumentsHash;
                    previous.ReappearedRequest = ParseRequestNumber(requestId);
                }
                else
                {
                    ToolCalls[call.Id] = new ToolCallState
                    {
                        Name = name,
                        ArgumentsLength = arguments.Length,
                        ArgumentsHash = argumentsHash,
                        CreatedRequest = ParseRequestNumber(requestId)
                    };
                    ToolCallOrder.Enqueue(call.Id);
                }
                TrimRememberedToolCalls();
            }
        }
    }

    internal static string HashForTests(JsonNode? node) => HashNode(node);

    internal static AgentToolCallSnapshot? GetToolCallSnapshot(string id)
    {
        lock (Gate)
        {
            return ToolCalls.TryGetValue(id, out ToolCallState? state)
                ? new AgentToolCallSnapshot(
                    state.Name, state.ArgumentsLength, state.ArgumentsHash,
                    state.CreatedRequest, state.ResultReceivedRequest, state.ReappearedRequest)
                : null;
        }
    }

    private static void TrackIncomingHistory(string requestId, JsonArray messages)
    {
        lock (Gate)
        {
            var priorCallMetadata = new Dictionary<string, (string Name, int ArgumentLength, string ArgumentHash)>(StringComparer.Ordinal);
            foreach (JsonNode? node in messages)
            {
                if (node is not JsonObject message)
                {
                    continue;
                }

                string role = message["role"]?.ToString() ?? string.Empty;
                if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) &&
                    message["tool_calls"] is JsonArray calls)
                {
                    foreach (JsonNode? call in calls)
                    {
                        string? id = call?["id"]?.ToString();
                        if (string.IsNullOrWhiteSpace(id))
                        {
                            continue;
                        }

                        string name = call?["function"]?["name"]?.ToString() ?? "<missing>";
                        string arguments = call?["function"]?["arguments"]?.ToString() ?? string.Empty;
                        priorCallMetadata[id] = (name, arguments.Length, HashText(arguments));
                        if (!ToolCalls.ContainsKey(id))
                        {
                            ToolCalls[id] = new ToolCallState
                            {
                                Name = name,
                                ArgumentsLength = arguments.Length,
                                ArgumentsHash = HashText(arguments)
                            };
                            ToolCallOrder.Enqueue(id);
                        }

                        ToolCallState callState = ToolCalls[id];
                        Logger.Log(
                            $"[AGENT_LOOP #{requestId}] ToolCallHistory id={SafeLogValue(id)},name={SafeLogValue(name)},arguments_length={arguments.Length},arguments_hash={HashText(arguments)},created=#{callState.CreatedRequest?.ToString() ?? "history"},result_received=#{callState.ResultReceivedRequest?.ToString() ?? "<none>"}.");
                    }
                }
                else if (string.Equals(role, "tool", StringComparison.OrdinalIgnoreCase))
                {
                    string id = message["tool_call_id"]?.ToString() ?? "<missing>";
                    ToolCalls.TryGetValue(id, out ToolCallState? state);
                    bool matches = state != null || priorCallMetadata.ContainsKey(id);
                    (string Name, int ArgumentLength, string ArgumentHash) metadata = default;
                    if (!matches && id == "<missing>")
                    {
                        string toolName = message["name"]?.ToString() ?? string.Empty;
                        KeyValuePair<string, ToolCallState>? pendingCall = ToolCalls
                            .Where(pair => string.Equals(pair.Value.Name, toolName, StringComparison.Ordinal) &&
                                           pair.Value.ResultReceivedRequest is null)
                            .Select(pair => (KeyValuePair<string, ToolCallState>?)pair)
                            .FirstOrDefault();
                        if (pendingCall is not null)
                        {
                            id = pendingCall.Value.Key;
                            state = pendingCall.Value.Value;
                            matches = true;
                        }
                    }

                    if (priorCallMetadata.TryGetValue(id, out var knownMetadata))
                    {
                        metadata = knownMetadata;
                    }
                    if (state == null && priorCallMetadata.TryGetValue(id, out metadata))
                    {
                        state = new ToolCallState
                        {
                            Name = metadata.Name,
                            ArgumentsLength = metadata.ArgumentLength,
                            ArgumentsHash = metadata.ArgumentHash
                        };
                        ToolCalls[id] = state;
                        ToolCallOrder.Enqueue(id);
                    }

                    if (state != null && state.ResultReceivedRequest is null)
                    {
                        state.ResultReceivedRequest = ParseRequestNumber(requestId);
                    }

                    Logger.Log(
                        $"[AGENT_LOOP #{requestId}] ToolResult id={SafeLogValue(id)},matches_known_call={matches},result_hash={HashNode(message)},result_received_first=#{state?.ResultReceivedRequest?.ToString() ?? requestId}.");
                }
            }
            TrimRememberedToolCalls();
        }
    }

    private static void LogRequestSummary(
        string route,
        string requestId,
        JsonObject? original,
        JsonObject? sanitized,
        string originalBody,
        string sanitizedBody,
        string validationStatus,
        string inboundRequestBody)
    {
        JsonArray originalMessages = original?["messages"] as JsonArray ?? [];
        JsonArray messages = sanitized?["messages"] as JsonArray ?? [];
        JsonArray tools = original?["tools"] as JsonArray ?? [];
        JsonObject? last = messages.LastOrDefault() as JsonObject;
        JsonObject? lastToolResult = messages.LastOrDefault(message =>
            string.Equals(message?["role"]?.ToString(), "tool", StringComparison.OrdinalIgnoreCase)) as JsonObject;
        JsonObject? latestAssistantToolCall = messages.LastOrDefault(message =>
            string.Equals(message?["role"]?.ToString(), "assistant", StringComparison.OrdinalIgnoreCase) &&
            message?["tool_calls"] is JsonArray calls && calls.Count > 0) as JsonObject;
        JsonArray? latestCalls = latestAssistantToolCall?["tool_calls"] as JsonArray;
        JsonArray? lastCalls = (messages.LastOrDefault() as JsonObject)?["tool_calls"] as JsonArray;
        JsonNode? originalSystem = originalMessages.FirstOrDefault(message =>
            string.Equals(message?["role"]?.ToString(), "system", StringComparison.OrdinalIgnoreCase));
        JsonNode? sanitizedSystem = messages.FirstOrDefault(message =>
            string.Equals(message?["role"]?.ToString(), "system", StringComparison.OrdinalIgnoreCase));

        int CountRole(string role) => messages.Count(message =>
            string.Equals(message?["role"]?.ToString(), role, StringComparison.OrdinalIgnoreCase));
        string ids = string.Join(",", messages
            .SelectMany(message => (message?["tool_calls"] as JsonArray ?? [])
                .Select(call => SafeLogValue(call?["id"]?.ToString() ?? "<missing>"))));
        string results = string.Join(",", messages
            .Where(message => string.Equals(message?["role"]?.ToString(), "tool", StringComparison.OrdinalIgnoreCase))
            .Select(message => SafeLogValue(message?["tool_call_id"]?.ToString() ?? "<missing>")));
        string latestCallId = latestCalls?.LastOrDefault()?["id"]?.ToString() ?? string.Empty;
        string lastResultId = lastToolResult?["tool_call_id"]?.ToString() ?? string.Empty;
        bool? latestResultMatches = lastToolResult == null
            ? null
            : string.IsNullOrEmpty(lastResultId)
                ? null
                : latestCalls?.Any(call => string.Equals(call?["id"]?.ToString(), lastResultId, StringComparison.Ordinal)) == true;
        string names = string.Join(",", messages
            .SelectMany(message => (message?["tool_calls"] as JsonArray ?? [])
                .Select(call => call?["function"]?["name"]?.ToString())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => SafeLogValue(name!)))
            .Distinct(StringComparer.Ordinal));

        Logger.Log(
            $"[AGENT_LOOP #{requestId}] route={SafeLogValue(route)},inbound_chars={inboundRequestBody.Length},inbound_request_hash={HashRequestBody(inboundRequestBody)},normalized_request_hash={HashRequestBody(originalBody)},request_chars={originalBody.Length},sanitized_request_hash={HashRequestBody(sanitizedBody)},messages_hash={HashNode(original?["messages"])},tools_hash={HashNode(original?["tools"])},sanitized_messages_hash={HashNode(sanitized?["messages"])},original_system_hash={HashNode(originalSystem)},sanitized_system_hash={HashNode(sanitizedSystem)},system_prompt_changed={!string.Equals(HashNode(originalSystem), HashNode(sanitizedSystem), StringComparison.Ordinal)},native_tool_definitions={tools.Count},legacy_tool_rules_injected={SmartPromptOptimizationSettings.InjectStrictToolCallingRules && tools.Count == 0},last_message_hash={HashNode(last)},last_tool_result_hash={HashNode(lastToolResult)},latest_assistant_toolcall_hash={HashNode(latestAssistantToolCall)},sanitized_chars={sanitizedBody.Length},original_message_count={originalMessages.Count},message_count={messages.Count},assistant_count={CountRole("assistant")},tool_count={CountRole("tool")},user_count={CountRole("user")},last_role={SafeLogValue(last?["role"]?.ToString() ?? "<none>")},last_assistant_toolcall_count={latestCalls?.Count ?? 0},last_message_toolcall_count={lastCalls?.Count ?? 0},previous_assistant_tool_call_ids=[{ids}],incoming_tool_result_ids=[{results}],latest_tool_result_matches_latest_call={latestResultMatches?.ToString() ?? "<none>"},last_tool_result_call_id={SafeLogValue(lastResultId)},tool_names=[{names}],tool_choice={SafeLogValue(SummarizeToolChoice(original?["tool_choice"]))},parallel_tool_calls={SafeLogValue(original?["parallel_tool_calls"]?.ToString() ?? "<absent>")},stream={SafeLogValue(original?["stream"]?.ToString() ?? "<absent>")},max_tokens={SafeLogValue(original?["max_tokens"]?.ToString() ?? "<absent>")},max_completion_tokens={SafeLogValue(original?["max_completion_tokens"]?.ToString() ?? "<absent>")},n_predict={SafeLogValue(original?["n_predict"]?.ToString() ?? "<absent>")},validation={validationStatus}.");
    }

    private static string SummarizeToolChoice(JsonNode? toolChoice)
    {
        if (toolChoice is JsonObject choice)
        {
            return $"type={choice["type"]?.ToString() ?? "<absent>"},name={choice["function"]?["name"]?.ToString() ?? "<absent>"}";
        }

        return toolChoice?.ToString() ?? "<absent>";
    }

    private static JsonObject? ParseObject(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string HashNode(JsonNode? node)
    {
        if (node == null)
        {
            return HashText("<null>");
        }

        JsonNode canonical = Canonicalize(node);
        return HashText(canonical.ToJsonString());
    }

    private static JsonNode Canonicalize(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var canonical = new JsonObject();
            foreach (KeyValuePair<string, JsonNode?> pair in obj.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                canonical[pair.Key] = pair.Value == null ? null : Canonicalize(pair.Value);
            }
            return canonical;
        }

        if (node is JsonArray array)
        {
            var canonical = new JsonArray();
            foreach (JsonNode? item in array)
            {
                canonical.Add(item == null ? null : Canonicalize(item));
            }
            return canonical;
        }

        return node.DeepClone();
    }

    private static string HashText(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string SafeLogValue(string value)
    {
        string oneLine = value.Replace('\r', ' ').Replace('\n', ' ');
        return oneLine.Length <= 120 ? oneLine : string.Concat(oneLine.AsSpan(0, 120), "...");
    }

    private static long? ParseRequestNumber(string requestId) =>
        long.TryParse(requestId, out long requestNumber) ? requestNumber : null;

    private static void TrimRememberedToolCalls()
    {
        while (ToolCalls.Count > MaximumRememberedToolCalls && ToolCallOrder.TryDequeue(out string? oldest))
        {
            _ = ToolCalls.Remove(oldest);
        }
    }
}
