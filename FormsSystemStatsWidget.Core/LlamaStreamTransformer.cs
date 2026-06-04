using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FormsSystemStatsWidget.Core
{
    public static partial class LlamaStreamTransformer
    {
        private static readonly Regex FunctionToolCallRegex = FunctionRegex();
        private static readonly Regex ParameterRegex = ParamsRegex();
        private static readonly Regex QwenToolCallRegex = QwenToolRegex(); // NEUER PARSER

        public static string SanitizeIncomingRequest(string jsonInput, string modelFamily = "llama")
        {
            try
            {
                var node = JsonNode.Parse(jsonInput);
                bool flattenToolHistory = string.Equals(modelFamily, "qwen", StringComparison.OrdinalIgnoreCase);

                if (node is JsonObject root)
                {
                    root.Remove("parallel_tool_calls");
                    root.Remove("store");

                    if (root["messages"] is JsonArray messages)
                    {
                        foreach (var msg in messages.OfType<JsonObject>())
                        {
                            msg.Remove("audio");
                            msg.Remove("refusal");
                            msg.Remove("reasoning_content");

                            NormalizeToolHistoryMessage(msg, flattenToolHistory);
                        }
                    }
                }

                return node?.ToJsonString() ?? jsonInput;
            }
            catch (Exception ex)
            {
                Logger.Log($"[Sanitizer-Error] {ex.Message}");
                return jsonInput;
            }
        }

        private static void NormalizeToolHistoryMessage(JsonObject message, bool flattenToolHistory)
        {
            if (!flattenToolHistory)
            {
                return;
            }

            string role = message["role"]?.ToString() ?? string.Empty;

            if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) && message["tool_calls"] is JsonArray toolCalls)
            {
                string existingContent = message["content"]?.ToString() ?? string.Empty;
                string toolSummary = BuildAssistantToolCallSummary(toolCalls);

                if (!string.IsNullOrWhiteSpace(toolSummary))
                {
                    message["content"] = string.IsNullOrWhiteSpace(existingContent)
                        ? toolSummary
                        : $"{existingContent}\n{toolSummary}";
                }
                message.Remove("tool_calls");
                return;
            }

            if (!string.Equals(role, "tool", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string toolCallId = message["tool_call_id"]?.ToString() ?? string.Empty;
            string toolContent = message["content"]?.ToString() ?? string.Empty;
            string toolPrefix = string.IsNullOrWhiteSpace(toolCallId)
                ? "[Tool Result]"
                : $"[Tool Result {toolCallId}]";

            message["role"] = "user";

            string baseContent = string.IsNullOrWhiteSpace(toolContent)
                ? toolPrefix
                : $"{toolPrefix}:\n{toolContent}";

            message["content"] = baseContent + "\n\n(Tool execution finished. Proceed with the task or output the next tool call.)";

            message.Remove("tool_call_id");
            message.Remove("name");
        }

        private static string BuildAssistantToolCallSummary(JsonNode? toolCallsNode)
        {
            if (toolCallsNode is not JsonArray toolCalls || toolCalls.Count == 0)
            {
                return string.Empty;
            }

            var summaries = toolCalls
                .OfType<JsonNode>()
                .Select(toolCall => toolCall as JsonObject)
                .Where(toolCall => toolCall != null)
                .Select(toolCall =>
                {
                    var function = toolCall!["function"] as JsonObject;
                    string functionName = function?["name"]?.ToString() ?? "unknown_tool";
                    string arguments = function?["arguments"]?.ToString() ?? "{}";

                    // =========================================================
                    // DER ANTI-HALLUZINATIONS-FIX: 
                    // Wir füttern Qwen exakt mit dem Format, das es nativ nutzt.
                    // Keine Fake-Phrasen mehr, die das Modell verwirren!
                    // =========================================================
                    return $"<tool_call>\n{{\"name\": \"{functionName}\", \"arguments\": {arguments}}}\n</tool_call>";
                })
                .ToArray();

            return summaries.Length == 0 ? string.Empty : string.Join("\n", summaries);
        }

        private static int FindToolCallStartIndex(string content)
        {
            int jsonIndex = content.IndexOf("{\"command\"", StringComparison.Ordinal);
            int xmlIndex = content.IndexOf("<function=", StringComparison.OrdinalIgnoreCase);
            int qwenIndex = content.IndexOf("<tool_call>", StringComparison.OrdinalIgnoreCase);

            var validIndices = new[] { jsonIndex, xmlIndex, qwenIndex }.Where(i => i >= 0).ToArray();
            return validIndices.Length > 0 ? validIndices.Min() : -1;
        }

        private static bool TryCreateToolCallsArray(string toolBuffer, out JsonArray? toolCallsArray)
        {
            toolCallsArray = null;

            if (TryParseJsonCommandToolCall(toolBuffer, out JsonObject? jsonToolCall))
            {
                toolCallsArray = CreateToolCallsArray(jsonToolCall);
                return true;
            }

            if (TryParseTaggedToolCall(toolBuffer, out JsonObject? taggedToolCall))
            {
                toolCallsArray = CreateToolCallsArray(taggedToolCall);
                return true;
            }

            if (TryParseQwenToolCall(toolBuffer, out JsonObject? qwenToolCall))
            {
                toolCallsArray = CreateToolCallsArray(qwenToolCall);
                return true;
            }

            return false;
        }

        private static bool TryParseJsonCommandToolCall(string toolBuffer, out JsonObject? toolCall)
        {
            toolCall = null;
            int firstBrace = toolBuffer.IndexOf('{');
            if (firstBrace < 0)
            {
                return false;
            }

            string jsonCandidate = toolBuffer[firstBrace..];
            int openBraces = 0, closeBraces = 0;

            foreach (char character in jsonCandidate)
            {
                if (character == '{')
                {
                    openBraces++;
                }

                if (character == '}')
                {
                    closeBraces++;
                }
            }

            if (openBraces == 0 || openBraces != closeBraces)
            {
                return false;
            }

            JsonObject? toolObject = JsonNode.Parse(jsonCandidate)?.AsObject();
            string functionName = toolObject?["command"]?.ToString() ?? string.Empty;
            functionName = Regex.Match(functionName, @"[a-zA-Z0-9_]+").Value;

            if (string.IsNullOrWhiteSpace(functionName))
            {
                return false;
            }

            toolObject?.Remove("command");
            toolCall = new JsonObject
            {
                ["name"] = functionName,
                ["arguments"] = toolObject?.ToJsonString() ?? "{}"
            };

            return true;
        }

        private static bool TryParseTaggedToolCall(string toolBuffer, out JsonObject? toolCall)
        {
            toolCall = null;
            Match functionMatch = FunctionToolCallRegex.Match(toolBuffer);
            if (!functionMatch.Success)
            {
                return false;
            }

            string functionName = functionMatch.Groups["name"].Value.Trim();
            functionName = Regex.Match(functionName, @"[a-zA-Z0-9_]+").Value;

            if (string.IsNullOrWhiteSpace(functionName))
            {
                return false;
            }

            string parameterBody = functionMatch.Groups["body"].Value;
            var arguments = new JsonObject();

            foreach (Match parameterMatch in ParameterRegex.Matches(parameterBody))
            {
                string parameterName = parameterMatch.Groups["name"].Value.Trim();
                string parameterValue = parameterMatch.Groups["value"].Value.Trim();

                if (string.IsNullOrWhiteSpace(parameterName))
                {
                    continue;
                }

                arguments[parameterName] = ConvertParameterValue(parameterValue);
            }

            toolCall = new JsonObject { ["name"] = functionName, ["arguments"] = arguments.ToJsonString() };
            return true;
        }

        private static bool TryParseQwenToolCall(string toolBuffer, out JsonObject? toolCall)
        {
            toolCall = null;
            var match = QwenToolCallRegex.Match(toolBuffer);
            if (!match.Success)
            {
                return false;
            }

            try
            {
                var jsonObj = JsonNode.Parse(match.Groups["json"].Value)?.AsObject();
                if (jsonObj != null && jsonObj.ContainsKey("name"))
                {
                    toolCall = new JsonObject
                    {
                        ["name"] = jsonObj["name"]?.ToString() ?? "",
                        ["arguments"] = jsonObj["arguments"]?.ToJsonString() ?? "{}"
                    };
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static JsonNode? ConvertParameterValue(string rawValue)
        {
            string normalizedValue = rawValue.Trim();
            if (normalizedValue.Length >= 2 && ((normalizedValue[0] == '"' && normalizedValue[^1] == '"') || (normalizedValue[0] == '\'' && normalizedValue[^1] == '\'')))
            {
                normalizedValue = normalizedValue[1..^1];
            }

            if (bool.TryParse(normalizedValue, out bool boolValue))
            {
                return JsonValue.Create(boolValue);
            }

            if (int.TryParse(normalizedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
            {
                return JsonValue.Create(intValue);
            }

            if (long.TryParse(normalizedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue))
            {
                return JsonValue.Create(longValue);
            }

            if (double.TryParse(normalizedValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double doubleValue))
            {
                return JsonValue.Create(doubleValue);
            }

            return JsonValue.Create(normalizedValue);
        }

        private static JsonArray CreateToolCallsArray(JsonObject parsedToolCall)
        {
            return new JsonArray
            {
                new JsonObject
                {
                    ["index"] = 0,
                    ["id"] = "call_" + Guid.NewGuid().ToString("N"),
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = parsedToolCall["name"]?.ToString() ?? "unknown_tool",
                        ["arguments"] = parsedToolCall["arguments"]?.ToString() ?? "{}"
                    }
                }
            };
        }

        public static async Task TransformOpenAiStreamAsync(Stream upstreamStream, Stream downstreamStream, string detectedModelName)
        {
            using var streamReader = new StreamReader(upstreamStream);
            using var writer = new StreamWriter(downstreamStream, new UTF8Encoding(false)) { AutoFlush = true };

            bool isReceivingReasoning = false; // State-Tracker für den Gedankengang
            bool inToolCall = false;
            bool toolCallTriggered = false;
            string toolBuffer = "";
            string responseTextBuffer = "";
            string detectBuffer = ""; // Puffer für Tool-Detection über Chunk-Grenzen hinweg
            string? line;

            while ((line = await streamReader.ReadLineAsync()) != null)
            {
                Logger.Log($"[RAW CHUNK] {line}");

                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                {
                    if (!toolCallTriggered)
                    {
                        await writer.WriteLineAsync(line);
                    }

                    continue;
                }

                var dataStr = line["data: ".Length..].Trim();
                if (dataStr == "[DONE]")
                {
                    try { await writer.WriteLineAsync(line); } catch { }
                    break;
                }

                try
                {
                    var chunk = JsonNode.Parse(dataStr);
                    if (chunk?["choices"] is not JsonArray choicesArray || choicesArray.Count == 0)
                    {
                        if (!toolCallTriggered)
                        {
                            await writer.WriteLineAsync("data: " + chunk?.ToJsonString());
                        }

                        continue;
                    }

                    var choice = choicesArray[0]?.AsObject();
                    var delta = choice?["delta"]?.AsObject();

                    if (toolCallTriggered)
                    {
                        continue;
                    }

                    if (delta != null)
                    {
                        // PRIO 2: Reasoning-Content nativ belassen! Kein <think> Wrapping mehr!
                        // Copilot (VS2026) rendert das `reasoning_content` JSON-Feld nativ als ausklappbaren Spoiler.

                        if (delta.ContainsKey("content"))
                        {
                            var content = delta["content"]?.ToString();

                            if (!string.IsNullOrEmpty(content))
                            {
                                responseTextBuffer += content;

                                if (inToolCall)
                                {
                                    toolBuffer += content;
                                    if (TryCreateToolCallsArray(toolBuffer, out JsonArray? toolCallsArray))
                                    {
                                        inToolCall = false;
                                        toolCallTriggered = true;

                                        delta.Remove("content");
                                        delta.Remove("reasoning_content");
                                        delta["tool_calls"] = toolCallsArray;
                                        if (choice != null) choice["finish_reason"] = null;

                                        await writer.WriteLineAsync("data: " + chunk?.ToJsonString());

                                        var finishChunk = new JsonObject
                                        {
                                            ["choices"] = new JsonArray {
                                                new JsonObject {
                                                    ["delta"] = new JsonObject(),
                                                    ["finish_reason"] = "tool_calls",
                                                    ["index"] = 0
                                                }
                                            }
                                        };
                                        await writer.WriteLineAsync("data: " + finishChunk.ToJsonString());
                                        toolBuffer = "";
                                    }
                                    continue;
                                }

                                detectBuffer += content;
                                int toolIdx = FindToolCallStartIndex(detectBuffer);

                                if (toolIdx >= 0)
                                {
                                    inToolCall = true;
                                    toolBuffer = detectBuffer.Substring(toolIdx);

                                    string textBeforeToolCall = detectBuffer.Substring(0, toolIdx);
                                    if (!string.IsNullOrEmpty(textBeforeToolCall))
                                    {
                                        delta["content"] = textBeforeToolCall;
                                        await writer.WriteLineAsync("data: " + chunk?.ToJsonString());
                                    }
                                    detectBuffer = "";
                                    continue;
                                }

                                // Prefix-Check für partiell eintreffende Tool-Calls (Chunk-übergreifend)
                                bool isPartial = false;
                                string[] triggers = { "<tool_call>", "<function=", "{\"command\"" };
                                foreach (var trigger in triggers)
                                {
                                    for (int i = 1; i <= trigger.Length; i++)
                                    {
                                        if (detectBuffer.EndsWith(trigger.Substring(0, i), StringComparison.OrdinalIgnoreCase))
                                        {
                                            isPartial = true;
                                            break;
                                        }
                                    }
                                    if (isPartial) break;
                                }

                                if (!isPartial)
                                {
                                    delta["content"] = detectBuffer;
                                    await writer.WriteLineAsync("data: " + chunk?.ToJsonString());
                                    detectBuffer = "";
                                }
                                continue; // Write handled manually to hold buffers
                            }
                        }

                        // Flush chunks that have no content but might have reasoning_content
                        if (!delta.ContainsKey("content") || string.IsNullOrEmpty(delta["content"]?.ToString()))
                        {
                            await writer.WriteLineAsync("data: " + chunk?.ToJsonString());
                        }
                    }
                }
                catch
                {
                    try { await writer.WriteLineAsync(line); } catch { }
                }
            }

            // Flush remaining partial tool detection buffer just in case the model stopped mid-word
            if (!string.IsNullOrEmpty(detectBuffer) && !toolCallTriggered && !inToolCall)
            {
                var finalChunk = new JsonObject
                {
                    ["choices"] = new JsonArray {
                        new JsonObject {
                            ["delta"] = new JsonObject { ["content"] = detectBuffer },
                            ["index"] = 0
                        }
                    }
                };
                try { await writer.WriteLineAsync("data: " + finalChunk.ToJsonString()); } catch { }
            }

            if (!string.IsNullOrWhiteSpace(responseTextBuffer))
            {
                string cleanLog = responseTextBuffer.Replace("\r", "").Replace("\n", " ").Trim();
                if (cleanLog.Length > 200)
                {
                    cleanLog = cleanLog.Substring(0, 200) + "...";
                }

                Logger.Log($"[LLM Output Summary] Generated text: {cleanLog}");
            }
        }

        [GeneratedRegex(@"<function=(?<name>[^\s>]+)>\s*(?<body>.*?)\s*</function>(?:\s*</tool_call>)?", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline, "de-DE")]
        private static partial Regex FunctionRegex();

        [GeneratedRegex(@"<parameter=(?<name>[^\s>]+)>\s*(?<value>.*?)\s*</parameter>", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline, "de-DE")]
        private static partial Regex ParamsRegex();

        // DER NEUE QWEN PARSER
        [GeneratedRegex(@"<tool_call>\s*(?<json>\{.*?\})\s*</tool_call>", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline, "de-DE")]
        private static partial Regex QwenToolRegex();
    }
}