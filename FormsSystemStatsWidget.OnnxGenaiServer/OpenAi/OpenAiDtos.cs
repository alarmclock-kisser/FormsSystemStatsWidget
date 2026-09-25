using System.Text.Json;
using System.Text.Json.Serialization;

namespace FormsSystemStatsWidget.OnnxGenaiServer.OpenAi;

// ============================================================================
// OpenAI-kompatible DTOs (Request/Response) für /v1/chat/completions,
// /v1/completions, /v1/embeddings, /v1/models.
// ============================================================================

// --- Chat Completions ---

public sealed class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("messages")]
    public required List<ChatMessage> Messages { get; set; }

    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public float? TopP { get; set; }

    [JsonPropertyName("top_k")]
    public int? TopK { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("max_completion_tokens")]
    public int? MaxCompletionTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("stop")]
    public JsonElement? Stop { get; set; }

    [JsonPropertyName("presence_penalty")]
    public float? PresencePenalty { get; set; }

    [JsonPropertyName("frequency_penalty")]
    public float? FrequencyPenalty { get; set; }

    [JsonPropertyName("seed")]
    public int? Seed { get; set; }

    [JsonPropertyName("repeat_penalty")]
    public float? RepeatPenalty { get; set; }

    /// <summary> Effektive max_tokens (max_completion_tokens hat Vorrang). </summary>
    public int? EffectiveMaxTokens => MaxCompletionTokens ?? MaxTokens;
}

public sealed class ChatMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class ChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "chat.completion";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public required List<ChatCompletionChoice> Choices { get; set; }

    [JsonPropertyName("usage")]
    public Usage Usage { get; set; } = new();
}

public sealed class ChatCompletionChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public required ChatMessage Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public sealed class ChatCompletionStreamChunk
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "chat.completion.chunk";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public required List<ChatCompletionStreamChoice> Choices { get; set; }
}

public sealed class ChatCompletionStreamChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("delta")]
    public required ChatMessageDelta Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public sealed class ChatMessageDelta
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

// --- Completions (Legacy) ---

public sealed class CompletionRequest
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("prompt")]
    public required string Prompt { get; set; }

    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public float? TopP { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("stop")]
    public JsonElement? Stop { get; set; }

    [JsonPropertyName("n")]
    public int? N { get; set; }

    [JsonPropertyName("repeat_penalty")]
    public float? RepeatPenalty { get; set; }
}

public sealed class CompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "text_completion";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public required List<CompletionChoice> Choices { get; set; }

    [JsonPropertyName("usage")]
    public Usage Usage { get; set; } = new();
}

public sealed class CompletionChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("text")]
    public required string Text { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public sealed class CompletionStreamChunk
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "text_completion";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public required List<CompletionStreamChoice> Choices { get; set; }
}

public sealed class CompletionStreamChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("text")]
    public required string Text { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

// --- Embeddings ---

public sealed class EmbeddingRequest
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("input")]
    public required JsonElement Input { get; set; }

    [JsonPropertyName("dimensions")]
    public int? Dimensions { get; set; }
}

public sealed class EmbeddingResponse
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = "list";

    [JsonPropertyName("data")]
    public required List<EmbeddingData> Data { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("usage")]
    public EmbeddingUsage Usage { get; set; } = new();
}

public sealed class EmbeddingData
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = "embedding";

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("embedding")]
    public required float[] Embedding { get; set; }
}

public sealed class EmbeddingUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

// --- Models ---

public sealed class ModelsResponse
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = "list";

    [JsonPropertyName("data")]
    public required List<ModelInfo> Data { get; set; }
}

public sealed class ModelInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "model";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("owned_by")]
    public string OwnedBy { get; set; } = "fssw";
}

// --- Shared ---

public sealed class Usage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public sealed class ErrorResponse
{
    [JsonPropertyName("error")]
    public required ErrorBody Error { get; set; }
}

public sealed class ErrorBody
{
    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "invalid_request_error";

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}
