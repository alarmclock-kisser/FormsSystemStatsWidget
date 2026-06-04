using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FormsSystemStatsWidget.Core
{
    public static class LlamaStreamTransformer
    {
        public static string SanitizeIncomingRequest(string jsonInput)
        {
            try
            {
                var node = JsonNode.Parse(jsonInput);

                if (node is JsonObject root)
                {
                    // llama.cpp kennt manche OpenAI/Ollama Felder nicht

                    root.Remove("parallel_tool_calls");
                    root.Remove("store");

                    if (root["messages"] is JsonArray messages)
                    {
                        foreach (var msg in messages.OfType<JsonObject>())
                        {
                            msg.Remove("audio");
                            msg.Remove("refusal");

                            // Optional:
                            // reasoning_content entfernen falls Copilot das zurückschickt
                            msg.Remove("reasoning_content");
                        }
                    }
                }

                Logger.Log($"[Sanitized Request] {node?.ToJsonString(new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = false,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }) ?? jsonInput}");

                return node?.ToJsonString() ?? jsonInput;
            }
            catch (Exception ex)
            {
                Logger.Log($"[Sanitizer-Error] {ex.Message}");
                return jsonInput;
            }
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
                    string toolCallId = toolCall["id"]?.ToString() ?? string.Empty;

                    return string.IsNullOrWhiteSpace(toolCallId)
                        ? $"[Assistant Tool Call]: {functionName}({arguments})"
                        : $"[Assistant Tool Call {toolCallId}]: {functionName}({arguments})";
                })
                .ToArray();

            return summaries.Length == 0 ? string.Empty : string.Join("\n", summaries);
        }

        public static async Task TransformOpenAiStreamAsync(Stream upstreamStream, Stream downstreamStream, string detectedModelName)
        {
            using var streamReader = new StreamReader(upstreamStream);
            using var writer = new StreamWriter(downstreamStream, new UTF8Encoding(false));

            bool inThinking = false;
            bool inToolCall = false;
            string toolBuffer = "";
            string toolCallId = "call_" + Guid.NewGuid().ToString("N");

            string? line;
            while ((line = await streamReader.ReadLineAsync()) != null)
            {
                Logger.Log($"[RAW CHUNK] {line}");

                if (string.IsNullOrWhiteSpace(line))
                {
                    await writer.WriteLineAsync(line);
                    await writer.FlushAsync();
                    continue;
                }

                if (!line.StartsWith("data: "))
                {
                    await writer.WriteLineAsync(line);
                    await writer.FlushAsync();
                    continue;
                }

                var dataStr = line["data: ".Length..].Trim();
                if (dataStr == "[DONE]")
                {
                    await writer.WriteLineAsync(line);
                    await writer.FlushAsync();
                    break;
                }

                try
                {
                    var chunk = JsonNode.Parse(dataStr);
                    var choice = chunk?["choices"]?[0]?.AsObject();
                    var delta = choice?["delta"]?.AsObject();

                    if (delta != null && delta.ContainsKey("content"))
                    {
                        var content = delta["content"]?.ToString();

                        if (!string.IsNullOrEmpty(content))
                        {
                            if (content.Contains("<think>"))
                            {
                                inThinking = true;
                                content = content.Replace("<think>", "");
                            }

                            if (inThinking)
                            {
                                if (content.Contains("</think>"))
                                {
                                    inThinking = false;
                                    var parts = content.Split(new[] { "</think>" }, StringSplitOptions.None);
                                    if (!string.IsNullOrEmpty(parts[0]))
                                    {
                                        delta.Remove("content");
                                        delta["reasoning_content"] = parts[0];
                                        await writer.WriteLineAsync("data: " + chunk?.ToJsonString());
                                        await writer.FlushAsync();
                                    }
                                    content = parts.Length > 1 ? parts[1] : "";
                                }
                                else
                                {
                                    delta.Remove("content");
                                    delta["reasoning_content"] = content;
                                    await writer.WriteLineAsync("data: " + chunk?.ToJsonString());
                                    await writer.FlushAsync();
                                    continue;
                                }
                            }

                            if (string.IsNullOrEmpty(content))
                            {
                                continue;
                            }

                            // JSON Buffering
                            if (content.Contains("{\"command\"") || inToolCall)
                            {
                                int firstBraceInChunk = content.IndexOf('{');

                                // Falls vor dem JSON noch normaler Text steht,
                                // diesen sofort an Copilot durchreichen.
                                if (!inToolCall &&
                                    firstBraceInChunk > 0)
                                {
                                    string textBeforeJson = content[..firstBraceInChunk];

                                    delta["content"] = textBeforeJson;

                                    await writer.WriteLineAsync(
                                        "data: " + chunk?.ToJsonString());

                                    await writer.FlushAsync();
                                }

                                inToolCall = true;

                                string jsonPart =
                                    firstBraceInChunk >= 0
                                        ? content[firstBraceInChunk..]
                                        : content;

                                toolBuffer += jsonPart;

                                int firstBrace = toolBuffer.IndexOf('{');

                                if (firstBrace >= 0)
                                {
                                    string jsonCandidate =
                                        toolBuffer[firstBrace..];

                                    int openBraces = 0;
                                    int closeBraces = 0;

                                    foreach (char c in jsonCandidate)
                                    {
                                        switch (c)
                                        {
                                            case '{':
                                                openBraces++;
                                                break;

                                            case '}':
                                                closeBraces++;
                                                break;
                                        }
                                    }

                                    if (openBraces > 0 &&
                                        openBraces == closeBraces)
                                    {
                                        inToolCall = false;

                                        try
                                        {
                                            Logger.Log($"[JSON CANDIDATE] {jsonCandidate}");

                                            var toolObj =
                                                JsonNode.Parse(jsonCandidate)
                                                    ?.AsObject();

                                            string functionName =
                                                toolObj?["command"]
                                                    ?.ToString() ?? "";

                                            functionName =
                                                Regex.Match(
                                                    functionName,
                                                    @"[a-zA-Z0-9_]+")
                                                .Value;

                                            Logger.Log($"[SEND TOOL CALL] {functionName}");
                                            Logger.Log(toolObj?.ToJsonString() ?? "ERR getting toolObj: toolObj was null.");

                                            if (!string.IsNullOrWhiteSpace(functionName))
                                            {
                                                toolObj?.Remove("command");

                                                var toolCallsArray =
                                                    new JsonArray
                                                    {
                            new JsonObject
                            {
                                ["index"] = 0,
                                ["id"] =
                                    "call_" +
                                    Guid.NewGuid()
                                        .ToString("N"),

                                ["type"] = "function",

                                ["function"] =
                                    new JsonObject
                                    {
                                        ["name"] =
                                            functionName,

                                        ["arguments"] =
                                            toolObj?.ToJsonString()
                                            ?? "{}"
                                    }
                            }
                                                    };

                                                delta.Remove("content");
                                                delta.Remove("reasoning_content");

                                                delta["tool_calls"] = toolCallsArray;

                                                if (choice != null)
                                                {
                                                    choice["finish_reason"] = null;
                                                }

                                                Logger.Log(
                                                    "[TOOL CALL CHUNK]\n" +
                                                    chunk?.ToJsonString());

                                                await writer.WriteLineAsync(
                                                    "data: " + chunk?.ToJsonString());

                                                await writer.FlushAsync();

                                                var finishChunk =
                                                    new JsonObject
                                                    {
                                                        ["choices"] = new JsonArray
                                                        {
            new JsonObject
            {
                ["delta"] = new JsonObject(),
                ["finish_reason"] = "tool_calls",
                ["index"] = 0
            }
                                                        }
                                                    };

                                                Logger.Log(
                                                    "[TOOL FINISH CHUNK]\n" +
                                                    finishChunk.ToJsonString());

                                                await writer.WriteLineAsync(
                                                    "data: " + finishChunk.ToJsonString());

                                                await writer.FlushAsync();

                                                toolBuffer = "";

                                                continue;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Log($"[ToolParseError] {ex}");

                                            delta["content"] = toolBuffer;

                                            Logger.Log("[TOOL CALL CHUNK WRITTEN]");

                                            await writer.WriteLineAsync(
                                                "data: " + chunk?.ToJsonString());

                                            await writer.FlushAsync();

                                            toolBuffer = "";

                                            continue;
                                        }

                                        toolBuffer = "";
                                    }
                                }

                                continue;
                            }
                        }
                    }

                    // Normale Textausgabe (außerhalb von Tool-Calls)
                    await writer.WriteLineAsync("data: " + chunk?.ToJsonString());
                    await writer.FlushAsync();
                }
                catch (Exception ex)
                {
                    Logger.Log($"[Transformer-Error] {ex.Message}");
                    await writer.WriteLineAsync(line);
                    await writer.FlushAsync();
                }
            }
        }
    }
}