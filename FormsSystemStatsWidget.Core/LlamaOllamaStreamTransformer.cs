using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace FormsSystemStatsWidget.Core
{
    public static partial class LlamaStreamTransformer
    {
        private sealed class OllamaToolCallAccumulator
        {
            public StringBuilder Name { get; } = new();
            public StringBuilder Arguments { get; } = new();
        }

        public static async Task<OpenAiStreamTransformResult> TransformOpenAiStreamToOllamaAsync(Stream upstreamStream, Stream downstreamStream, string detectedModelName, string? streamRequestId = null)
        {
            const int traceLimit = 12;
            if (streamRequestId != null)
            {
                LlamaAgentLoopDiagnostics.BeginResponse(streamRequestId);
            }
            using var streamReader = new StreamReader(upstreamStream);
            using var responseHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var llamaServerTrace = new Queue<string>();
            var ollamaTrace = new Queue<string>();
            var toolCalls = new SortedDictionary<int, OllamaToolCallAccumulator>();
            int nextToolCallIndex = 0;
            int generatedContentLength = 0;
            string? finalFinishReason = null;
            bool doneReceived = false;
            bool clientDisconnected = false;
            bool upstreamError = false;
            bool transformationError = false;
            bool firstTokenLogged = false;
            bool toolCallStartedLogged = false;
            bool toolCallCompletedLogged = false;
            bool finishReasonLogged = false;
            bool upstreamIdentifiersLogged = false;

            void AddTrace(Queue<string> trace, string entry)
            {
                if (trace.Count == traceLimit)
                {
                    _ = trace.Dequeue();
                }
                trace.Enqueue(entry);
            }

            async Task EmitOllamaChunkAsync(JsonObject chunk)
            {
                try
                {
                    byte[] outputBytes = Encoding.UTF8.GetBytes(chunk.ToJsonString() + "\r\n");
                    await downstreamStream.WriteAsync(outputBytes);
                    await downstreamStream.FlushAsync();
                    responseHash.AppendData(outputBytes);
                }
                catch (IOException)
                {
                    clientDisconnected = true;
                    throw new DownstreamDisconnectedException();
                }
                catch (System.Net.HttpListenerException)
                {
                    clientDisconnected = true;
                    throw new DownstreamDisconnectedException();
                }
                catch (ObjectDisposedException)
                {
                    clientDisconnected = true;
                    throw new DownstreamDisconnectedException();
                }

                JsonObject? message = chunk["message"] as JsonObject;
                string content = message?["content"]?.ToString() ?? string.Empty;
                generatedContentLength += content.Length;
                string names = message?["tool_calls"] is JsonArray outputCalls
                    ? string.Join(";", outputCalls.Select(call => SafeTraceValue(call?["function"]?["name"]?.ToString())))
                    : string.Empty;
                string argumentsLength = message?["tool_calls"] is JsonArray calls
                    ? string.Join(";", calls.Select(call => (call?["function"]?["arguments"]?.ToJsonString() ?? "{}").Length))
                    : string.Empty;
                AddTrace(ollamaTrace, $"contentLength={content.Length},toolNames=[{names}],argumentsLengths=[{argumentsLength}],done={chunk["done"]?.ToString() ?? "false"},done_reason={chunk["done_reason"]?.ToString() ?? "<null>"}");
            }

            JsonObject CreateFinalChunk()
            {
                var message = new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = string.Empty
                };

                if (toolCalls.Count > 0)
                {
                    var ollamaCalls = new JsonArray();
                    foreach (KeyValuePair<int, OllamaToolCallAccumulator> entry in toolCalls)
                    {
                        string argumentsText = entry.Value.Arguments.ToString();
                        JsonObject arguments;
                        if (string.IsNullOrWhiteSpace(argumentsText))
                        {
                            arguments = new JsonObject();
                        }
                        else
                        {
                            JsonNode? parsedArguments = JsonNode.Parse(argumentsText);
                            arguments = parsedArguments as JsonObject
                                ?? throw new JsonException("Tool-call arguments must be a JSON object.");
                        }

                        if (entry.Value.Name.Length == 0)
                        {
                            throw new JsonException("A streamed tool call did not include a function name.");
                        }

                        ollamaCalls.Add(new JsonObject
                        {
                            ["function"] = new JsonObject
                            {
                                ["name"] = entry.Value.Name.ToString(),
                                ["arguments"] = arguments
                            }
                        });
                    }
                    message["tool_calls"] = ollamaCalls;
                }

                var finalChunk = new JsonObject
                {
                    ["model"] = detectedModelName,
                    ["message"] = message,
                    ["done"] = true
                };
                if (finalFinishReason != null)
                {
                    finalChunk["done_reason"] = finalFinishReason;
                }
                return finalChunk;
            }

            try
            {
                while (true)
                {
                    string? line;
                    try
                    {
                        line = await streamReader.ReadLineAsync();
                    }
                    catch (IOException ex)
                    {
                        upstreamError = true;
                        Logger.Log($"[Ollama SSE] Upstream read failed ({ex.GetType().Name}).");
                        break;
                    }
                    catch (ObjectDisposedException ex)
                    {
                        upstreamError = true;
                        Logger.Log($"[Ollama SSE] Upstream stream closed unexpectedly ({ex.GetType().Name}).");
                        break;
                    }
                    catch (System.Net.Http.HttpRequestException ex)
                    {
                        upstreamError = true;
                        Logger.Log($"[Ollama SSE] Upstream request failed ({ex.GetType().Name}).");
                        break;
                    }
                    catch (OperationCanceledException ex)
                    {
                        upstreamError = true;
                        Logger.Log($"[Ollama SSE] Upstream read was canceled ({ex.GetType().Name}).");
                        break;
                    }

                    if (line == null)
                    {
                        break;
                    }
                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ", System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string data = line["data: ".Length..].Trim();
                    AddTrace(llamaServerTrace, SummarizeOpenAiSseData(data));
                    if (data == "[DONE]")
                    {
                        doneReceived = true;
                        if (streamRequestId != null)
                        {
                            LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "DoneReceived");
                        }
                        try
                        {
                            await EmitOllamaChunkAsync(CreateFinalChunk());
                            if (streamRequestId != null)
                            {
                                LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "DownstreamDoneWritten", "ollama_done=true");
                                if (toolCalls.Count > 0)
                                {
                                    toolCallCompletedLogged = true;
                                    LlamaAgentLoopDiagnostics.LogLifecycle(
                                        streamRequestId, "ToolCallCompleted",
                                        $"finish_reason={finalFinishReason ?? "<none>"},tool_call_count={toolCalls.Count}");
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            transformationError = true;
                            Logger.Log($"[Ollama SSE] Tool-call arguments could not be assembled (length={toolCalls.Values.Sum(call => call.Arguments.Length)}, {ex.GetType().Name}).");
                        }
                        break;
                    }

                    JsonObject chunk;
                    try
                    {
                        chunk = JsonNode.Parse(data)?.AsObject()
                            ?? throw new JsonException("The upstream SSE data was not a JSON object.");
                    }
                    catch (JsonException ex)
                    {
                        transformationError = true;
                        Logger.Log($"[Ollama SSE] Invalid upstream JSON ({ex.GetType().Name}, {data.Length} chars).");
                        break;
                    }

                    if (streamRequestId != null)
                    {
                        LlamaAgentLoopDiagnostics.ObserveResponseChunk(streamRequestId, chunk);
                    }
                    string? upstreamResponseId = chunk["id"]?.ToString();
                    string? upstreamTaskId = chunk["task_id"]?.ToString();
                    string? upstreamRequestId = chunk["request_id"]?.ToString();
                    if (streamRequestId != null &&
                        !upstreamIdentifiersLogged &&
                        (upstreamResponseId != null || upstreamTaskId != null || upstreamRequestId != null))
                    {
                        upstreamIdentifiersLogged = true;
                        LlamaAgentLoopDiagnostics.LogLifecycle(
                            streamRequestId, "UpstreamSseIdentifiers",
                            $"id={SafeTraceValue(upstreamResponseId)},task_id={SafeTraceValue(upstreamTaskId)},request_id={SafeTraceValue(upstreamRequestId)}");
                    }

                    if (chunk["choices"] is not JsonArray choices)
                    {
                        continue;
                    }

                    foreach (JsonNode? choice in choices)
                    {
                        if (choice?["finish_reason"] != null)
                        {
                            finalFinishReason = choice["finish_reason"]!.ToString();
                            if (streamRequestId != null && !finishReasonLogged)
                            {
                                finishReasonLogged = true;
                                LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "FinishReason", $"value={finalFinishReason}");
                            }
                        }

                        if (choice?["delta"] is not JsonObject delta)
                        {
                            continue;
                        }

                        string content = delta["content"]?.ToString() ?? string.Empty;
                        if (streamRequestId != null &&
                            !firstTokenLogged &&
                            (content.Length > 0 || delta["reasoning_content"]?.ToString().Length > 0 ||
                             delta["tool_calls"] is JsonArray { Count: > 0 }))
                        {
                            firstTokenLogged = true;
                            LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "FirstToken");
                        }
                        if (content.Length > 0)
                        {
                            await EmitOllamaChunkAsync(new JsonObject
                            {
                                ["model"] = detectedModelName,
                                ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = content },
                                ["done"] = false
                            });
                        }

                        if (delta["tool_calls"] is not JsonArray deltaToolCalls)
                        {
                            continue;
                        }
                        if (streamRequestId != null && deltaToolCalls.Count > 0 && !toolCallStartedLogged)
                        {
                            toolCallStartedLogged = true;
                            string calls = string.Join(";", deltaToolCalls.Select(call =>
                                $"index={call?["index"]?.ToString() ?? "<none>"},id={call?["id"]?.ToString() ?? "<none>"},type={call?["type"]?.ToString() ?? "<none>"},name={call?["function"]?["name"]?.ToString() ?? "<fragment>"},arguments_length={call?["function"]?["arguments"]?.ToString().Length ?? 0}"));
                            LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "ToolCallStarted", calls);
                        }

                        foreach (JsonNode? toolCall in deltaToolCalls)
                        {
                            string? indexText = toolCall?["index"]?.ToString();
                            int index = int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedIndex)
                                ? parsedIndex
                                : nextToolCallIndex;
                            nextToolCallIndex = System.Math.Max(nextToolCallIndex, index + 1);
                            if (!toolCalls.TryGetValue(index, out OllamaToolCallAccumulator? accumulator))
                            {
                                accumulator = new OllamaToolCallAccumulator();
                                toolCalls.Add(index, accumulator);
                            }

                            JsonObject? function = toolCall?["function"] as JsonObject;
                            string? name = function?["name"]?.ToString();
                            if (!string.IsNullOrEmpty(name))
                            {
                                _ = accumulator.Name.Append(name);
                            }

                            JsonNode? arguments = function?["arguments"];
                            if (arguments != null)
                            {
                                string fragment = arguments is JsonValue value && value.TryGetValue(out string? stringValue)
                                    ? stringValue ?? string.Empty
                                    : arguments.ToJsonString();
                                _ = accumulator.Arguments.Append(fragment);
                            }
                        }
                    }
                }
            }
            catch (DownstreamDisconnectedException)
            {
                clientDisconnected = true;
            }
            catch (InvalidOperationException ex)
            {
                transformationError = true;
                Logger.Log($"[Ollama SSE] Transformation failed ({ex.GetType().Name}).");
            }

            OpenAiStreamCompletionStatus status = clientDisconnected
                ? OpenAiStreamCompletionStatus.ClientDisconnected
                : transformationError
                    ? OpenAiStreamCompletionStatus.TransformationError
                    : upstreamError || !doneReceived
                        ? OpenAiStreamCompletionStatus.UpstreamError
                        : string.Equals(finalFinishReason, "tool_calls", System.StringComparison.Ordinal)
                            ? OpenAiStreamCompletionStatus.CompletedByToolCall
                            : finalFinishReason != null
                                ? OpenAiStreamCompletionStatus.CompletedByLlm
                                : OpenAiStreamCompletionStatus.CompletedByDone;

            if (streamRequestId != null)
            {
                if (!firstTokenLogged)
                {
                    LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "FirstToken", "received=false");
                }
                if (!toolCallStartedLogged)
                {
                    LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "ToolCallStarted", "received=false");
                }
                if (!toolCallCompletedLogged)
                {
                    LlamaAgentLoopDiagnostics.LogLifecycle(
                        streamRequestId, "ToolCallCompleted",
                        toolCallStartedLogged ? "completed=false" : "applicable=false");
                }
                if (!finishReasonLogged)
                {
                    LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "FinishReason", "received=false");
                }
                if (!doneReceived)
                {
                    LlamaAgentLoopDiagnostics.LogLifecycle(streamRequestId, "DoneReceived", "received=false");
                }
                LlamaAgentLoopDiagnostics.CompleteResponse(streamRequestId, doneReceived && !clientDisconnected, finalFinishReason);
            }
            string responseHashValue = Convert.ToHexString(responseHash.GetHashAndReset());
            Logger.Log($"[Ollama SSE][LLAMA SERVER #{streamRequestId ?? "untracked"}] {string.Join(" || ", llamaServerTrace)}");
            Logger.Log($"[Ollama SSE][OLLAMA CLIENT #{streamRequestId ?? "untracked"}] {string.Join(" || ", ollamaTrace)}");
            Logger.Log($"[Ollama SSE][Summary #{streamRequestId ?? "untracked"}] DoneReceived={doneReceived}, FinalFinishReason={finalFinishReason ?? "<none>"}, ToolCallCount={toolCalls.Count}, GeneratedContentLength={generatedContentLength}, ResponseHash={responseHashValue}, ClientDisconnected={clientDisconnected}, Completion={status}.");

            return new OpenAiStreamTransformResult(
                doneReceived,
                finalFinishReason,
                toolCalls.Count,
                generatedContentLength,
                clientDisconnected,
                status);
        }
    }
}
