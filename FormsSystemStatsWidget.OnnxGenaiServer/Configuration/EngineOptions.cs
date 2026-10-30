using System.Text.Json.Serialization;

namespace FormsSystemStatsWidget.OnnxGenaiServer.Configuration;

/// <summary>
/// Engine configuration options read from appsettings.json / ONNXGENAI_SERVER__* env vars.
/// </summary>
public sealed class EngineOptions
{
    /// <summary>
    /// Root directory containing model subdirectories.
    /// </summary>
    public string ModelRootDirectory { get; init; } = @"D:\Models\ONNX";

    /// <summary>
    /// Name of the model to load by default (subdirectory name under ModelRootDirectory).
    /// Empty = first discovered model.
    /// </summary>
    public string DefaultModel { get; init; } = string.Empty;

    /// <summary>
    /// ONNX Runtime Execution Provider. "Dml" (DirectML) or "Cuda".
    /// </summary>
    public string ExecutionProvider { get; init; } = "Dml";

    /// <summary>
    /// Context length (n_ctx) for KV-cache allocation.
    /// </summary>
    public int ContextLength { get; init; } = 4096;

    /// <summary>
    /// Maximum number of parallel generations (sessions).
    /// </summary>
    public int MaxConcurrentGenerations { get; init; } = 1;

    /// <summary>
    /// Default temperature for generation (overridden per request).
    /// </summary>
    public float Temperature { get; init; } = 0.8f;

    /// <summary>
    /// Default top-p for sampling (overridden per request).
    /// </summary>
    public float TopP { get; init; } = 0.9f;

    /// <summary>
    /// Default top-k for sampling (overridden per request).
    /// </summary>
    public int TopK { get; init; } = 40;

    /// <summary>
    /// Maximum tokens per generation (overridden per request).
    /// </summary>
    public int MaxTokens { get; init; } = 1024;

    /// <summary>
    /// Repeat penalty for generation.
    /// </summary>
    public float RepeatPenalty { get; init; } = 1.1f;

    /// <summary>
    /// System prompt prepended to every chat completion.
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// Model ID returned in API responses.
    /// </summary>
    public string ModelId { get; init; } = "fssw-onnx-genai";
}