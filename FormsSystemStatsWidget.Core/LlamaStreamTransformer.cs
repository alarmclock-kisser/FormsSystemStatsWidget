using System;
using System.Collections.Generic;
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
        private static readonly Regex FunctionToolCallRegex = new(@"<function=(?<name>[^\s>]+)>\s*(?<body>.*?)\s*</function>(?:\s*</tool_call>)?", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex ParameterRegex = new(@"<parameter=(?<name>[^\s>]+)>\s*(?<value>.*?)\s*</parameter>", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex QwenToolCallRegex = new(@"<tool_call>\s*(?<json>\{.*?\})\s*</tool_call>", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex KeywordSearchRegex = new(@"\b[\p{L}\p{Nd}_]{3,}\b", RegexOptions.Compiled);
        private static readonly Regex CodeFenceBlockRegex = new(@"```(?<lang>[^\r\n`]*)\r?\n(?<code>[\s\S]*?)```", RegexOptions.Compiled);
        private static readonly HashSet<string> IgnoredKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "using", "namespace", "class", "public", "private", "protected", "internal", "static", "void", "string", "int", "bool",
            "this", "that", "with", "from", "were", "have", "just", "wenn", "dann", "oder", "aber", "nicht", "eine", "einer", "einem"
        };

        public static string SanitizeIncomingRequest(string jsonInput, string modelFamily = "llama", int numCtx = 4096, double temperature = 0.3, double repetitionPenalty = 1.25, double userDefinedTopP = 0.95, double userDefinedMinP = 0.1, int userDefinedTopK = 40)
        {
            try
            {
                JsonNode? node = JsonNode.Parse(jsonInput);
                bool flattenToolHistory = string.Equals(modelFamily, "qwen", StringComparison.OrdinalIgnoreCase);

                if (node is not JsonObject root)
                {
                    Logger.Log($"[Sanitized Request] {node?.ToJsonString() ?? jsonInput}");
                    return node?.ToJsonString() ?? jsonInput;
                }

                root["temperature"] = temperature;
                root["repetition_penalty"] = repetitionPenalty;
                root["top_p"] = userDefinedTopP;
                root["min_p"] = userDefinedMinP;
                root["top_k"] = userDefinedTopK;
                root.Remove("store");

                if (root["messages"] is not JsonArray messages)
                {
                    return root.ToJsonString();
                }

                foreach (JsonObject message in messages.OfType<JsonObject>())
                {
                    message.Remove("audio");
                    message.Remove("refusal");
                    message.Remove("reasoning_content");
                    NormalizeToolHistoryMessage(message, flattenToolHistory);
                }

                double promptSafetyRatio = Math.Clamp(SmartPromptOptimizationSettings.PromptSafetyRatio, 0.10, 1.00);
                int maxPromptTokens = (int) (numCtx * promptSafetyRatio);
                int hardCharLimit = maxPromptTokens * 3;

                if (SmartPromptOptimizationSettings.IsEnabled)
                {
                    OptimizeMessagesForSmartContext(messages, hardCharLimit);
                }

                Logger.Log($"[Sanitizer] Enforcing max prompt length: {maxPromptTokens} tokens (~{hardCharLimit} chars). Original length: {jsonInput.Length} chars.");

                bool hasSystem = messages.Count > 0 && string.Equals(messages[0]?["role"]?.ToString(), "system", StringComparison.OrdinalIgnoreCase);
                int deleteIndex = hasSystem ? 1 : 0;

                while (GetTotalContentLength(messages) > hardCharLimit && messages.Count > (hasSystem ? 2 : 1))
                {
                    messages.RemoveAt(deleteIndex);
                }

                if (GetTotalContentLength(messages) > hardCharLimit && messages.Count > 0)
                {
                    JsonObject? lastMessage = messages.Last() as JsonObject;
                    string content = lastMessage?["content"]?.ToString() ?? string.Empty;

                    if (content.Length > hardCharLimit)
                    {
                        int safeStart = Math.Max(0, content.Length - hardCharLimit + Math.Max(0, SmartPromptOptimizationSettings.TailKeepBonusChars));
                        string truncatedContent = string.Concat($"// [Context automatically rolled by proxy to fit dynamic limit of {numCtx} ctx]\r\n", content.AsSpan(safeStart));
                        if (lastMessage != null)
                        {
                            lastMessage["content"] = truncatedContent;
                        }
                    }
                }

                return root.ToJsonString();
            }
            catch (Exception ex)
            {
                Logger.Log($"[Sanitizer-Error] {ex.Message}");
                return jsonInput;
            }
        }

        private static void OptimizeMessagesForSmartContext(JsonArray messages, int hardCharLimit)
        {
            if (messages.Count == 0)
            {
                return;
            }

            double smartBudgetRatio = Math.Clamp(SmartPromptOptimizationSettings.SmartBudgetRatio, 0.10, 1.00);
            int smartTargetLimit = (int) (hardCharLimit * smartBudgetRatio);
            if (GetTotalContentLength(messages) <= smartTargetLimit)
            {
                return;
            }

            string focusContent = GetFocusContent(messages);
            HashSet<string> focusKeywords = ExtractKeywords(focusContent);

            CompressLargeMessages(messages);
            TrimMessagesByRelevance(messages, smartTargetLimit, focusKeywords);
        }

        private static int GetTotalContentLength(JsonArray messages)
        {
            return messages.OfType<JsonObject>().Sum(message => message["content"]?.ToString().Length ?? 0);
        }

        private static void CompressLargeMessages(JsonArray messages)
        {
            foreach (JsonObject message in messages.OfType<JsonObject>())
            {
                string content = message["content"]?.ToString() ?? string.Empty;
                if (content.Length <= SmartPromptOptimizationSettings.LargeMessageThresholdChars)
                {
                    continue;
                }

                string compactContent = CompressMessageContent(content);
                if (compactContent.Length < content.Length)
                {
                    message["content"] = compactContent;
                }
            }
        }

        private static string CompressMessageContent(string content)
        {
            int largeMessageThresholdChars = Math.Max(256, SmartPromptOptimizationSettings.LargeMessageThresholdChars);
            int skeletonMaxLines = Math.Max(5, SmartPromptOptimizationSettings.SkeletonMaxLines);

            string reducedContent = ReduceCodeFenceBlocks(content);
            if (reducedContent.Length > largeMessageThresholdChars && LooksLikeCode(reducedContent))
            {
                reducedContent = BuildCodeSkeleton(reducedContent, skeletonMaxLines);
            }

            return reducedContent.Length > largeMessageThresholdChars
                ? ShrinkWithHeadTail(reducedContent, largeMessageThresholdChars)
                : reducedContent;
        }

        private static string ReduceCodeFenceBlocks(string content)
        {
            return CodeFenceBlockRegex.Replace(content, static match =>
            {
                int largeMessageThresholdChars = Math.Max(256, SmartPromptOptimizationSettings.LargeMessageThresholdChars);
                int skeletonMaxLines = Math.Max(5, SmartPromptOptimizationSettings.SkeletonMaxLines);
                string language = (match.Groups["lang"].Value ?? string.Empty).Trim();
                string code = match.Groups["code"].Value;
                if (code.Length < largeMessageThresholdChars / 2)
                {
                    return match.Value;
                }

                string skeleton = BuildCodeSkeleton(code, skeletonMaxLines);
                return $"```{language}\n{skeleton}\n```";
            });
        }

        private static string BuildCodeSkeleton(string code, int maxLines)
        {
            string[] lines = code.Replace("\r", string.Empty).Split('\n');
            if (lines.Length <= maxLines)
            {
                return code.Trim();
            }

            List<string> signatureLines = [];
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (IsStructuralCodeLine(line))
                {
                    signatureLines.Add(line);
                }

                if (signatureLines.Count >= maxLines)
                {
                    break;
                }
            }

            if (signatureLines.Count == 0)
            {
                return ShrinkWithHeadTail(code, Math.Max(256, SmartPromptOptimizationSettings.LargeMessageThresholdChars));
            }

            return string.Join("\n", signatureLines) + "\n// [smart-context: code body omitted]";
        }

        private static bool IsStructuralCodeLine(string line)
        {
            if (line.StartsWith("using ", StringComparison.Ordinal) ||
                line.StartsWith("namespace ", StringComparison.Ordinal) ||
                line.StartsWith("class ", StringComparison.Ordinal) ||
                line.StartsWith("struct ", StringComparison.Ordinal) ||
                line.StartsWith("interface ", StringComparison.Ordinal) ||
                line.StartsWith("enum ", StringComparison.Ordinal) ||
                line.StartsWith("record ", StringComparison.Ordinal) ||
                line.StartsWith("public ", StringComparison.Ordinal) ||
                line.StartsWith("internal ", StringComparison.Ordinal) ||
                line.StartsWith("protected ", StringComparison.Ordinal) ||
                line.StartsWith("private ", StringComparison.Ordinal))
            {
                return true;
            }

            return line.StartsWith("[", StringComparison.Ordinal) ||
                   line == "{" ||
                   line == "}" ||
                   (line.Contains('(') && (line.EndsWith("{", StringComparison.Ordinal) || line.EndsWith(");", StringComparison.Ordinal) || line.Contains("=>", StringComparison.Ordinal)));
        }

        private static bool LooksLikeCode(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            int markerHits = 0;
            string sample = content.Length > 2000 ? content[..2000] : content;

            if (sample.Contains("{", StringComparison.Ordinal))
            {
                markerHits++;
            }
            if (sample.Contains("}", StringComparison.Ordinal))
            {
                markerHits++;
            }
            if (sample.Contains(";", StringComparison.Ordinal))
            {
                markerHits++;
            }
            if (sample.Contains("class ", StringComparison.OrdinalIgnoreCase) || sample.Contains("namespace ", StringComparison.OrdinalIgnoreCase))
            {
                markerHits++;
            }

            return markerHits >= 3;
        }

        private static void TrimMessagesByRelevance(JsonArray messages, int targetCharLimit, HashSet<string> focusKeywords)
        {
            int totalLength = GetTotalContentLength(messages);
            if (totalLength <= targetCharLimit)
            {
                return;
            }

            bool hasSystem = messages.Count > 0 && string.Equals(messages[0]?["role"]?.ToString(), "system", StringComparison.OrdinalIgnoreCase);
            int latestUserIndex = FindLatestRoleIndex(messages, "user");

            while (totalLength > targetCharLimit && messages.Count > (hasSystem ? 2 : 1))
            {
                var candidate = Enumerable.Range(0, messages.Count)
                    .Where(index => CanRemoveMessage(messages, index, hasSystem, latestUserIndex))
                    .Select(index => new
                    {
                        Index = index,
                        Score = ScoreMessage(messages[index] as JsonObject, index, messages.Count, focusKeywords),
                        Length = messages[index]?["content"]?.ToString().Length ?? 0
                    })
                    .OrderBy(entry => entry.Score)
                    .ThenByDescending(entry => entry.Length)
                    .FirstOrDefault();

                if (candidate == null)
                {
                    break;
                }

                messages.RemoveAt(candidate.Index);
                totalLength = GetTotalContentLength(messages);
            }
        }

        private static bool CanRemoveMessage(JsonArray messages, int index, bool hasSystem, int latestUserIndex)
        {
            if (hasSystem && index == 0)
            {
                return false;
            }

            if (index == messages.Count - 1 || index == latestUserIndex)
            {
                return false;
            }

            return true;
        }

        private static double ScoreMessage(JsonObject? message, int index, int totalCount, HashSet<string> focusKeywords)
        {
            if (message == null)
            {
                return double.MinValue;
            }

            string role = message["role"]?.ToString() ?? string.Empty;
            string content = message["content"]?.ToString() ?? string.Empty;

            double roleWeight = string.Equals(role, "user", StringComparison.OrdinalIgnoreCase)
                ? 3.0
                : string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase)
                    ? 2.0
                    : string.Equals(role, "system", StringComparison.OrdinalIgnoreCase)
                        ? 4.0
                        : 1.0;

            double recencyWeight = totalCount <= 1 ? 0 : (double) index / (totalCount - 1);
            int keywordHits = CountKeywordHits(content, focusKeywords);
            double lengthPenalty = Math.Min(content.Length / 5000d, 1d);

            return roleWeight + (recencyWeight * 2d) + (keywordHits * 0.75d) - lengthPenalty;
        }

        private static int CountKeywordHits(string content, HashSet<string> focusKeywords)
        {
            if (string.IsNullOrWhiteSpace(content) || focusKeywords.Count == 0)
            {
                return 0;
            }

            string normalized = content.ToLowerInvariant();
            return focusKeywords.Count(keyword => normalized.Contains(keyword, StringComparison.Ordinal));
        }

        private static int FindLatestRoleIndex(JsonArray messages, string role)
        {
            for (int index = messages.Count - 1; index >= 0; index--)
            {
                string currentRole = messages[index]?["role"]?.ToString() ?? string.Empty;
                if (string.Equals(currentRole, role, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static HashSet<string> ExtractKeywords(string text)
        {
            var frequencies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in KeywordSearchRegex.Matches(text ?? string.Empty))
            {
                string keyword = match.Value.ToLowerInvariant();
                if (IgnoredKeywords.Contains(keyword))
                {
                    continue;
                }

                frequencies[keyword] = frequencies.TryGetValue(keyword, out int count) ? count + 1 : 1;
            }

            return frequencies
                .OrderByDescending(entry => entry.Value)
                .ThenByDescending(entry => entry.Key.Length)
                .Take(Math.Max(1, SmartPromptOptimizationSettings.FocusKeywordLimit))
                .Select(entry => entry.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static string GetFocusContent(JsonArray messages)
        {
            int latestUserIndex = FindLatestRoleIndex(messages, "user");
            if (latestUserIndex >= 0)
            {
                return messages[latestUserIndex]?["content"]?.ToString() ?? string.Empty;
            }

            return messages.Count > 0 ? messages[^1]?["content"]?.ToString() ?? string.Empty : string.Empty;
        }

        private static string ShrinkWithHeadTail(string content, int maxChars)
        {
            if (string.IsNullOrEmpty(content) || content.Length <= maxChars || maxChars < 80)
            {
                return content;
            }

            int headLength = maxChars / 2;
            int tailLength = Math.Max(0, maxChars - headLength - 64);
            string head = content[..headLength];
            string tail = content[^tailLength..];
            return $"{head}\n\n// [smart-context: middle omitted]\n\n{tail}";
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
                if (jsonToolCall != null)
                {
                    toolCallsArray = CreateToolCallsArray(jsonToolCall);
                }

                return true;
            }

            if (TryParseTaggedToolCall(toolBuffer, out JsonObject? taggedToolCall))
            {
                if (taggedToolCall != null)
                {
                    toolCallsArray = CreateToolCallsArray(taggedToolCall);
                }

                return true;
            }

            if (TryParseQwenToolCall(toolBuffer, out JsonObject? qwenToolCall))
            {
                if (qwenToolCall != null)
                {
                    toolCallsArray = CreateToolCallsArray(qwenToolCall);
                }

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

            return double.TryParse(normalizedValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double doubleValue)
                ? JsonValue.Create(doubleValue)
                : JsonValue.Create(normalizedValue);
        }

        private static JsonArray CreateToolCallsArray(JsonObject parsedToolCall)
        {
            return
            [
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
            ];
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
                        try
                        {
                            await writer.WriteLineAsync(line);
                            await writer.FlushAsync();
                        }
                        catch (IOException) { break; }
                        catch (System.Net.HttpListenerException) { break; }
                        catch { break; }
                    }
                    continue;
                }

                var dataStr = line["data: ".Length..].Trim();
                if (dataStr == "[DONE]")
                {
                    try
                    {
                        await writer.WriteLineAsync(line);
                        await writer.FlushAsync();
                    }
                    catch { }
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
                            await writer.FlushAsync();
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
                        // ====================================================================
                        // REASONING INTERCEPTOR (Robuste Markdown Version für VS Copilot)
                        // Wandelt reasoning_content in visuelle Zitate (Blockquotes) um.
                        // ====================================================================
                        bool hasReasoning = delta.ContainsKey("reasoning_content") && delta["reasoning_content"] != null && !string.IsNullOrEmpty(delta["reasoning_content"]?.ToString());
                        bool hasContent = delta.ContainsKey("content") && delta["content"] != null && !string.IsNullOrEmpty(delta["content"]?.ToString());

                        if (hasReasoning)
                        {
                            string rContent = delta["reasoning_content"]!.ToString();
                            delta.Remove("reasoning_content");

                            // Verwandelt Zeilenumbrüche im Gedankengang in Zitat-Umbrüche
                            rContent = rContent.Replace("\n", "\n> ");

                            if (!isReceivingReasoning)
                            {
                                rContent = "\n\n> 🧠 **Gedankengang:**\n> " + rContent;
                                isReceivingReasoning = true;
                            }

                            delta["content"] = rContent;
                            hasContent = true;
                        }
                        else if (isReceivingReasoning && hasContent)
                        {
                            string nContent = delta["content"]!.ToString();

                            // Beendet das Zitat durch eine doppelte Leerzeile vor der echten Antwort
                            delta["content"] = "\n\n" + nContent;
                            isReceivingReasoning = false;
                        }
                        else if (isReceivingReasoning && choice != null && choice.ContainsKey("finish_reason") && choice["finish_reason"]?.ToString() != null)
                        {
                            // Beendet das Zitat sauber, falls der Stream abrupt endet
                            delta["content"] = (delta["content"]?.ToString() ?? "") + "\n\n";
                            isReceivingReasoning = false;
                        }
                        // ====================================================================

                        if (hasContent)
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
                                        choice?["finish_reason"] = null;

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
                                        await writer.FlushAsync();
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
                                        await writer.FlushAsync();
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
                                    if (isPartial)
                                    {
                                        break;
                                    }
                                }

                                if (!isPartial)
                                {
                                    delta["content"] = detectBuffer;
                                    await writer.WriteLineAsync("data: " + chunk?.ToJsonString());
                                    await writer.FlushAsync();
                                    detectBuffer = "";
                                }
                                continue;
                            }
                        }

                        // Flush chunks that have no content but might have reasoning_content
                        if (!delta.ContainsKey("content") || string.IsNullOrEmpty(delta["content"]?.ToString()))
                        {
                            await writer.WriteLineAsync("data: " + chunk?.ToJsonString());
                            await writer.FlushAsync();
                        }
                    }
                }
                catch (IOException)
                {
                    Logger.Log("[Disconnect] Copilot hat die Anfrage abgebrochen (Timeout/Stop). Beende llama-server Generierung...");
                    break; // Bricht die Schleife ab -> Stream wird disposed -> llama-server stoppt sofort!
                }
                catch (System.Net.HttpListenerException)
                {
                    Logger.Log("[Disconnect] HTTP Verbindung getrennt. Beende llama-server Generierung...");
                    break; // Bricht die Schleife ab -> Stream wird disposed -> llama-server stoppt sofort!
                }
                catch (Exception ex)
                {
                    Logger.Log($"[Transformer-Error] {ex.Message}");
                    try
                    {
                        await writer.WriteLineAsync(line);
                        await writer.FlushAsync();
                    }
                    catch
                    {
                        break; // Wenn der Fallback auch fehlschlägt: Raus hier und Ressourcen freigeben!
                    }
                }
            } // ENDE DER WHILE-SCHLEIFE

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
                try
                {
                    await writer.WriteLineAsync("data: " + finalChunk.ToJsonString());
                    await writer.FlushAsync();
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(responseTextBuffer))
            {
                string cleanLog = responseTextBuffer.Replace("\r", "").Replace("\n", " ").Trim();
                if (cleanLog.Length > 200)
                {
                    cleanLog = string.Concat(cleanLog.AsSpan(0, 200), "...");
                }

                Logger.Log($"[LLM Output Summary] Generated text: {cleanLog}");
            }
        }
    }
}