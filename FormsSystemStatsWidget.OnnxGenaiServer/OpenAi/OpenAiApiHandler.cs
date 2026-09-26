using System.Text;
using System.Text.Json;
using FormsSystemStatsWidget.OnnxGenaiServer.Engine;

namespace FormsSystemStatsWidget.OnnxGenaiServer.OpenAi;

public static class OpenAiApiHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task HandleChatCompletionsAsync(HttpContext ctx, OnnxGenaiEngine engine, CancellationToken ct)
    {
        if (!engine.IsReady)
        {
            await WriteJsonAsync(ctx, new ErrorResponse { Error = new ErrorBody { Message = "Engine not ready", Type = "server_error" } }, 503);
            return;
        }

        ChatCompletionRequest? request;
        try { request = await ctx.Request.ReadFromJsonAsync<ChatCompletionRequest>(ct); }
        catch (Exception ex) { await WriteJsonAsync(ctx, new ErrorResponse { Error = new ErrorBody { Message = $"Invalid JSON: {ex.Message}" } }, 400); return; }

        if (request is null || request.Messages is null || request.Messages.Count == 0)
        {
            await WriteJsonAsync(ctx, new ErrorResponse { Error = new ErrorBody { Message = "messages is required" } }, 400);
            return;
        }

        if (request.Messages.Any(message =>
            message is null
            || string.IsNullOrWhiteSpace(message.Role)
            || message.Content is null))
        {
            await WriteJsonAsync(ctx, new ErrorResponse { Error = new ErrorBody { Message = "Each message requires a role and string content" } }, 400);
            return;
        }

        var messages = request.Messages
            .Select(message => new PythonChatMessage(message.Role, message.Content, message.Name))
            .ToArray();
        var parameters = BuildParameters(
            request.Temperature, request.TopP, request.TypicalP, request.TopK,
            request.EffectiveMaxTokens, request.RepeatPenalty, request.MinP,
            request.PresencePenalty, request.FrequencyPenalty, request.Seed, engine);
        var modelId = request.Model ?? engine.LoadedModelId ?? "fssw-onnx-genai";
        var id = $"chatcmpl-{Guid.NewGuid():N}"[..29];
        var created = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;

        if (request.Stream)
        {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.Append("Cache-Control", "no-cache");
            var stream = ctx.Response.Body;
            var streamSb = new StringBuilder();
            var streamPromptTokens = 0;
            var streamCompletionTokens = 0;

            await foreach (var result in engine.GenerateChatAsync(messages, parameters, request.EnableThinking ?? false, ct))
            {
                if (result.FinishReason == "error") { await WriteSseErrorAsync(stream, "Generation failed"); return; }
                if (result.FinishReason == "ongoing")
                {
                    var delta = result.Text.Length > streamSb.Length ? result.Text[streamSb.Length..] : string.Empty;
                    streamSb.Append(delta);
                    streamPromptTokens = result.PromptTokens;
                    streamCompletionTokens = result.CompletionTokens;
                    var chunk = new ChatCompletionStreamChunk
                    {
                        Id = id, Created = created, Model = modelId,
                        Choices = [new ChatCompletionStreamChoice { Delta = new ChatMessageDelta { Role = "assistant", Content = delta } }]
                    };
                    await WriteSseAsync(stream, chunk);
                }
                else
                {
                    var finalChunk = new ChatCompletionStreamChunk
                    {
                        Id = id, Created = created, Model = modelId,
                        Choices = [new ChatCompletionStreamChoice { Delta = new ChatMessageDelta { }, FinishReason = result.FinishReason }]
                    };
                    await WriteSseAsync(stream, finalChunk);
                    await WriteStringAsync(stream, "data: [DONE]\r\n\r");
                }
            }
            return;
        }

        var nonStreamSb = new StringBuilder();
        var nonStreamPromptTokens = 0;
        var nonStreamCompletionTokens = 0;
        var finishReason = "stop";
        await foreach (var result in engine.GenerateChatAsync(messages, parameters, request.EnableThinking ?? false, ct))
        {
            if (result.FinishReason == "error")
            {
                await WriteJsonAsync(ctx, new ErrorResponse { Error = new ErrorBody { Message = "Generation failed", Type = "server_error" } }, 500);
                return;
            }
            nonStreamSb.Clear().Append(result.Text);
            nonStreamPromptTokens = result.PromptTokens;
            nonStreamCompletionTokens = result.CompletionTokens;
            finishReason = result.FinishReason;
        }

        var response = new ChatCompletionResponse
        {
            Id = id, Created = created, Model = modelId,
            Choices = [new ChatCompletionChoice { Message = new ChatMessage { Role = "assistant", Content = nonStreamSb.ToString() }, FinishReason = finishReason }],
            Usage = new Usage { PromptTokens = nonStreamPromptTokens, CompletionTokens = nonStreamCompletionTokens, TotalTokens = nonStreamPromptTokens + nonStreamCompletionTokens }
        };
        await WriteJsonAsync(ctx, response, 200);
    }

    public static async Task HandleCompletionsAsync(HttpContext ctx, OnnxGenaiEngine engine, CancellationToken ct)
    {
        if (!engine.IsReady)
        {
            await WriteJsonAsync(ctx, new ErrorResponse { Error = new ErrorBody { Message = "Engine not ready", Type = "server_error" } }, 503);
            return;
        }

        CompletionRequest? request;
        try { request = await ctx.Request.ReadFromJsonAsync<CompletionRequest>(ct); }
        catch (Exception ex) { await WriteJsonAsync(ctx, new ErrorResponse { Error = new ErrorBody { Message = $"Invalid JSON: {ex.Message}" } }, 400); return; }

        if (request is null || string.IsNullOrWhiteSpace(request.Prompt))
        {
            await WriteJsonAsync(ctx, new ErrorResponse { Error = new ErrorBody { Message = "prompt is required" } }, 400);
            return;
        }

        var parameters = BuildParameters(
            request.Temperature, request.TopP, request.TypicalP, null,
            request.MaxTokens, request.RepeatPenalty, request.MinP,
            request.PresencePenalty, request.FrequencyPenalty, request.Seed, engine);
        var modelId = request.Model ?? engine.LoadedModelId ?? "fssw-onnx-genai";
        var id = $"cmpl-{Guid.NewGuid():N}"[..29];
        var created = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;

        if (request.Stream)
        {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.Append("Cache-Control", "no-cache");
            var stream = ctx.Response.Body;
            var streamSb = new StringBuilder();
            var streamPromptTokens = 0;
            var streamCompletionTokens = 0;

            await foreach (var result in engine.GenerateAsync(request.Prompt, parameters, ct))
            {
                if (result.FinishReason == "error") { await WriteSseErrorAsync(stream, "Generation failed"); return; }
                if (result.FinishReason == "ongoing")
                {
                    var delta = result.Text.Length > streamSb.Length ? result.Text[streamSb.Length..] : string.Empty;
                    streamSb.Append(delta);
                    streamPromptTokens = result.PromptTokens;
                    streamCompletionTokens = result.CompletionTokens;
                    var chunk = new CompletionStreamChunk
                    {
                        Id = id, Created = created, Model = modelId,
                        Choices = [new CompletionStreamChoice { Text = delta }]
                    };
                    await WriteSseAsync(stream, chunk);
                }
                else
                {
                    var finalChunk = new CompletionStreamChunk
                    {
                        Id = id, Created = created, Model = modelId,
                        Choices = [new CompletionStreamChoice { Text = string.Empty, FinishReason = result.FinishReason }]
                    };
                    await WriteSseAsync(stream, finalChunk);
                    await WriteStringAsync(stream, "data: [DONE]\r\n\r");
                }
            }
            return;
        }

        var nonStreamSb = new StringBuilder();
        var nonStreamPromptTokens = 0;
        var nonStreamCompletionTokens = 0;
        var finishReason = "stop";
        await foreach (var result in engine.GenerateAsync(request.Prompt, parameters, ct))
        {
            if (result.FinishReason == "error")
            {
                await WriteJsonAsync(ctx, new ErrorResponse { Error = new ErrorBody { Message = "Generation failed", Type = "server_error" } }, 500);
                return;
            }
            nonStreamSb.Clear().Append(result.Text);
            nonStreamPromptTokens = result.PromptTokens;
            nonStreamCompletionTokens = result.CompletionTokens;
            finishReason = result.FinishReason;
        }

        var response = new CompletionResponse
        {
            Id = id, Created = created, Model = modelId,
            Choices = [new CompletionChoice { Text = nonStreamSb.ToString(), FinishReason = finishReason }],
            Usage = new Usage { PromptTokens = nonStreamPromptTokens, CompletionTokens = nonStreamCompletionTokens, TotalTokens = nonStreamPromptTokens + nonStreamCompletionTokens }
        };
        await WriteJsonAsync(ctx, response, 200);
    }

    public static async Task HandleEmbeddingsAsync(HttpContext ctx, OnnxGenaiEngine engine, CancellationToken ct)
    {
        if (!engine.IsReady)
        {
            await WriteJsonAsync(ctx, new ErrorResponse { Error = new ErrorBody { Message = "Engine not ready", Type = "server_error" } }, 503);
            return;
        }

        EmbeddingRequest? request;
        try { request = await ctx.Request.ReadFromJsonAsync<EmbeddingRequest>(ct); }
        catch (Exception ex) { await WriteJsonAsync(ctx, new ErrorResponse { Error = new ErrorBody { Message = $"Invalid JSON: {ex.Message}" } }, 400); return; }

        if (request is null)
        {
            await WriteJsonAsync(ctx, new ErrorResponse { Error = new ErrorBody { Message = "input is required" } }, 400);
            return;
        }

        var inputs = new List<string>();
        if (request.Input.ValueKind == JsonValueKind.String)
        {
            inputs.Add(request.Input.GetString()!);
        }
        else if (request.Input.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in request.Input.EnumerateArray())
            {
                inputs.Add(item.GetString() ?? string.Empty);
            }
        }

        if (inputs.Count == 0)
        {
            await WriteJsonAsync(ctx, new ErrorResponse { Error = new ErrorBody { Message = "input must be a string or array of strings" } }, 400);
            return;
        }

        var data = new List<EmbeddingData>();
        var totalTokens = 0;
        for (var i = 0; i < inputs.Count; i++)
        {
            var embedding = await engine.GetEmbeddingAsync(inputs[i], ct);
            if (embedding is null)
            {
                await WriteJsonAsync(ctx, new ErrorResponse { Error = new ErrorBody { Message = "Model does not support embeddings", Type = "server_error" } }, 501);
                return;
            }
            totalTokens += inputs[i].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            data.Add(new EmbeddingData { Index = i, Embedding = embedding });
        }

        var modelId = request.Model ?? engine.LoadedModelId ?? "fssw-onnx-genai";
        var response = new EmbeddingResponse
        {
            Data = data, Model = modelId,
            Usage = new EmbeddingUsage { PromptTokens = totalTokens, TotalTokens = totalTokens }
        };
        await WriteJsonAsync(ctx, response, 200);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static GenerationParameters BuildParameters(
        float? temperature, float? topP, float? typicalP, int? topK, int? maxTokens,
        float? repeatPenalty, float? minP, float? presencePenalty, float? frequencyPenalty,
        int? seed, OnnxGenaiEngine engine)
    {
        var opts = engine.Options;
        return new GenerationParameters
        {
            Temperature = temperature ?? opts.Temperature,
            TopP = topP ?? opts.TopP,
            TypicalP = typicalP ?? 1.0f,
            TopK = topK ?? opts.TopK,
            MaxNewTokens = maxTokens ?? opts.MaxTokens,
            RepeatPenalty = repeatPenalty ?? opts.RepeatPenalty,
            MinP = minP ?? 0.0f,
            PresencePenalty = presencePenalty ?? 0.0f,
            FrequencyPenalty = frequencyPenalty ?? 0.0f,
            Seed = seed
        };
    }

    private static async Task WriteJsonAsync<T>(HttpContext ctx, T data, int statusCode)
    {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(data, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ctx.Response.Body.WriteAsync(bytes);
    }

    private static async Task WriteSseAsync<T>(Stream stream, T chunk)
    {
        var json = JsonSerializer.Serialize(chunk, JsonOpts);
        await WriteStringAsync(stream, $"data: {json}\r\n\r");
    }

    private static async Task WriteSseErrorAsync(Stream stream, string message)
    {
        var err = new ErrorResponse { Error = new ErrorBody { Message = message, Type = "server_error" } };
        var json = JsonSerializer.Serialize(err, JsonOpts);
        await WriteStringAsync(stream, $"data: {json}\r\n\r");
        await WriteStringAsync(stream, "data: [DONE]\r\n\r");
    }

    private static async Task WriteStringAsync(Stream stream, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await stream.WriteAsync(bytes);
    }
}
