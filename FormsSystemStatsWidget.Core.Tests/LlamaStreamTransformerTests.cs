using System.Text;
using System.Text.Json.Nodes;
using FormsSystemStatsWidget.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FormsSystemStatsWidget.Core.Tests;

[TestClass]
public sealed class LlamaStreamTransformerTests
{
    private const string ModelName = "test-model";

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_NormalText_PreservesStopAndDone()
    {
        string stream = CreateSse(
            "{\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"Hello\"},\"finish_reason\":null}]}",
            "{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}",
            "[DONE]");

        (string output, LlamaStreamTransformer.OpenAiStreamTransformResult result) = await TransformAsync(stream);
        JsonObject contentChunk = ParseChunks(output).First(chunk => chunk["choices"]?[0]?["delta"]?["content"] != null);

        Assert.AreEqual("Hello", contentChunk["choices"]?[0]?["delta"]?["content"]?.ToString());
        Assert.AreEqual("stop", result.FinalFinishReason);
        Assert.IsTrue(result.DoneReceived);
        Assert.AreEqual(5, result.GeneratedContentLength);
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.CompletedByLlm, result.Status);
        StringAssert.Contains(output, "[DONE]");
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_ToolCallWithoutText_PreservesFragmentedArgumentsAndFinish()
    {
        string stream = CreateSse(
            "{\"choices\":[{\"index\":0,\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"call-1\",\"type\":\"function\",\"function\":{\"name\":\"read_file\",\"arguments\":\"{\\\"path\\\":\"}}]},\"finish_reason\":null}]}",
            "{\"choices\":[{\"index\":0,\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\"\\\"file.cs\\\"}\"}}]},\"finish_reason\":null}]}",
            "{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"tool_calls\"}]}",
            "[DONE]");

        (string output, LlamaStreamTransformer.OpenAiStreamTransformResult result) = await TransformAsync(stream, getGenerationStatsText: true);
        JsonObject[] chunks = ParseChunks(output).ToArray();
        JsonObject[] toolChunks = chunks.Where(chunk => chunk["choices"]?[0]?["delta"]?["tool_calls"] is JsonArray).ToArray();
        string arguments = string.Concat(toolChunks.Select(chunk =>
            chunk["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["arguments"]?.ToString()));

        Assert.AreEqual(2, toolChunks.Length);
        Assert.AreEqual("call-1", toolChunks[0]["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["id"]?.ToString());
        Assert.AreEqual("function", toolChunks[0]["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["type"]?.ToString());
        Assert.AreEqual("read_file", toolChunks[0]["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["name"]?.ToString());
        Assert.AreEqual("{\"path\":\"file.cs\"}", arguments);
        Assert.AreEqual("tool_calls", result.FinalFinishReason);
        Assert.AreEqual(1, result.ToolCallCount);
        Assert.AreEqual(0, result.GeneratedContentLength);
        Assert.IsTrue(result.DoneReceived);
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.CompletedByToolCall, result.Status);
        Assert.IsFalse(output.Contains("Generation Stats", StringComparison.Ordinal));
        StringAssert.Contains(output, "[DONE]");
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_ContentAlongsideNativeToolCall_PreservesBothDeltas()
    {
        string stream = CreateSse(
            "{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"{\\\"name\\\":\\\"not_a_tool\\\"}\",\"tool_calls\":[{\"index\":0,\"id\":\"call-native\",\"type\":\"function\",\"function\":{\"name\":\"native_tool\",\"arguments\":\"{}\"}}]},\"finish_reason\":null}]}",
            "{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"tool_calls\"}]}",
            "[DONE]");

        (string output, LlamaStreamTransformer.OpenAiStreamTransformResult result) = await TransformAsync(stream);
        JsonObject chunk = ParseChunks(output).First(item => item["choices"]?[0]?["delta"]?["tool_calls"] is JsonArray);

        Assert.AreEqual("{\"name\":\"not_a_tool\"}", chunk["choices"]?[0]?["delta"]?["content"]?.ToString());
        Assert.AreEqual("native_tool", chunk["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["name"]?.ToString());
        Assert.AreEqual(1, result.ToolCallCount);
        Assert.AreEqual("tool_calls", result.FinalFinishReason);
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_SequentialToolResponses_AllowFinalAssistantAnswer()
    {
        (string firstOutput, LlamaStreamTransformer.OpenAiStreamTransformResult firstResult) =
            await TransformAsync(CreateToolCallResponse("call-1", "read_file"));
        (string secondOutput, LlamaStreamTransformer.OpenAiStreamTransformResult secondResult) =
            await TransformAsync(CreateToolCallResponse("call-2", "run_command"));
        (string finalOutput, LlamaStreamTransformer.OpenAiStreamTransformResult finalResult) =
            await TransformAsync(CreateSse(
                "{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Finished\"},\"finish_reason\":null}]}",
                "{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}",
                "[DONE]"));

        Assert.AreEqual("tool_calls", firstResult.FinalFinishReason);
        Assert.AreEqual("read_file", ParseChunks(firstOutput).First()["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["name"]?.ToString());
        Assert.AreEqual("tool_calls", secondResult.FinalFinishReason);
        Assert.AreEqual("run_command", ParseChunks(secondOutput).First()["choices"]?[0]?["delta"]?["tool_calls"]?[0]?["function"]?["name"]?.ToString());
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.CompletedByLlm, finalResult.Status);
        Assert.AreEqual("Finished", ParseChunks(finalOutput).First()["choices"]?[0]?["delta"]?["content"]?.ToString());
    }

    [TestMethod]
    public async Task TransformOpenAiStreamToOllamaAsync_ToolCall_ProducesOllamaToolResultChunk()
    {
        string stream = CreateToolCallResponse("call-ollama", "read_file");
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(stream));
        using var output = new MemoryStream();

        LlamaStreamTransformer.OpenAiStreamTransformResult result =
            await LlamaStreamTransformer.TransformOpenAiStreamToOllamaAsync(input, output, ModelName);
        JsonObject[] chunks = Encoding.UTF8.GetString(output.ToArray())
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonNode.Parse(line)!.AsObject())
            .ToArray();
        JsonObject finalChunk = chunks[^1];

        Assert.AreEqual(1, chunks.Length);
        Assert.IsTrue(finalChunk["done"]!.GetValue<bool>());
        Assert.AreEqual("tool_calls", finalChunk["done_reason"]?.ToString());
        Assert.AreEqual("read_file", finalChunk["message"]?["tool_calls"]?[0]?["function"]?["name"]?.ToString());
        Assert.AreEqual("file.cs", finalChunk["message"]?["tool_calls"]?[0]?["function"]?["arguments"]?["path"]?.ToString());
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.CompletedByToolCall, result.Status);
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_MissingDone_ReportsIncompleteUpstreamStream()
    {
        (string _, LlamaStreamTransformer.OpenAiStreamTransformResult result) = await TransformAsync(
            CreateSse("{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"partial\"},\"finish_reason\":\"stop\"}] }"));

        Assert.IsFalse(result.DoneReceived);
        Assert.AreEqual("stop", result.FinalFinishReason);
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.UpstreamError, result.Status);
    }

    [TestMethod]
    public async Task TransformOpenAiStreamAsync_ClientDisconnect_ReportsDisconnectWithoutSuccessfulCompletion()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(CreateSse(
            "{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hello\"},\"finish_reason\":null}]}",
            "[DONE]")));
        using var output = new DisconnectingStream();

        LlamaStreamTransformer.OpenAiStreamTransformResult result =
            await LlamaStreamTransformer.TransformOpenAiStreamWithDiagnosticsAsync(input, output, ModelName);

        Assert.IsTrue(result.ClientDisconnected);
        Assert.IsFalse(result.DoneReceived);
        Assert.AreEqual(LlamaStreamTransformer.OpenAiStreamCompletionStatus.ClientDisconnected, result.Status);
    }

    [TestMethod]
    public void SanitizeIncomingRequest_PreservesLlamaToolHistoryAndSupportedFields()
    {
        var request = new JsonObject
        {
            ["parallel_tool_calls"] = false,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = null,
                    ["reasoning_content"] = "prior reasoning",
                    ["tool_calls"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "call-1",
                            ["type"] = "function",
                            ["function"] = new JsonObject { ["name"] = "read_file", ["arguments"] = "{}" }
                        }
                    }
                },
                new JsonObject { ["role"] = "tool", ["tool_call_id"] = "call-1", ["content"] = "result" }
            }
        };

        JsonObject sanitized = JsonNode.Parse(LlamaStreamTransformer.SanitizeIncomingRequest(request.ToJsonString(), "llama"))!.AsObject();
        JsonArray messages = sanitized["messages"]!.AsArray();

        Assert.IsFalse(sanitized["parallel_tool_calls"]!.GetValue<bool>());
        Assert.AreEqual("prior reasoning", messages[0]?["reasoning_content"]?.ToString());
        Assert.AreEqual("call-1", messages[0]?["tool_calls"]?[0]?["id"]?.ToString());
        Assert.AreEqual("tool", messages[1]?["role"]?.ToString());
        Assert.AreEqual("call-1", messages[1]?["tool_call_id"]?.ToString());
    }

    private static string CreateToolCallResponse(string id, string name)
    {
        var firstDelta = new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["index"] = 0,
                    ["delta"] = new JsonObject
                    {
                        ["tool_calls"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["index"] = 0,
                                ["id"] = id,
                                ["type"] = "function",
                                ["function"] = new JsonObject { ["name"] = name, ["arguments"] = "{\"path\":" }
                            }
                        }
                    },
                    ["finish_reason"] = null
                }
            }
        };
        var secondDelta = new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["index"] = 0,
                    ["delta"] = new JsonObject
                    {
                        ["tool_calls"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["index"] = 0,
                                ["function"] = new JsonObject { ["arguments"] = "\"file.cs\"}" }
                            }
                        }
                    },
                    ["finish_reason"] = null
                }
            }
        };
        return CreateSse(
            firstDelta.ToJsonString(),
            secondDelta.ToJsonString(),
            "{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"tool_calls\"}]}",
            "[DONE]");
    }

    private static async Task<(string Output, LlamaStreamTransformer.OpenAiStreamTransformResult Result)> TransformAsync(string stream, bool getGenerationStatsText = false)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(stream));
        using var output = new MemoryStream();
        LlamaStreamTransformer.OpenAiStreamTransformResult result =
            await LlamaStreamTransformer.TransformOpenAiStreamWithDiagnosticsAsync(input, output, ModelName, getGenerationStatsText);
        return (Encoding.UTF8.GetString(output.ToArray()), result);
    }

    private static string CreateSse(params string[] dataFrames) =>
        string.Join("\r\n\r\n", dataFrames.Select(frame => frame == "[DONE]" ? "data: [DONE]" : "data: " + frame)) + "\r\n\r\n";

    private static IEnumerable<JsonObject> ParseChunks(string output) => output
        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
        .Where(line => line.StartsWith("data: ", StringComparison.Ordinal) && line != "data: [DONE]")
        .Select(line => JsonNode.Parse(line["data: ".Length..])!.AsObject());

    private sealed class DisconnectingStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => throw new IOException("Client disconnected.");
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.FromException(new IOException("Client disconnected."));
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("Client disconnected.");
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.FromException(new IOException("Client disconnected."));
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException(new IOException("Client disconnected."));
    }
}
