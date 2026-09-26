using System.Text.Json.Serialization;

namespace FormsSystemStatsWidget.OnnxGenaiServer.Configuration;

/// <summary>
/// Default generation parameter values.
/// </summary>
public sealed class GenerationDefaults
{
    /// <summary>
    /// Default temperature (0.0 to 1.0, higher = more random).
    /// </summary>
    public float Temperature { get; init; } = 0.8f;

    /// <summary>
    /// Default top-p (nucleus sampling) (0.0 to 1.0).
    /// </summary>
    public float TopP { get; init; } = 0.9f;

    /// <summary>
    /// Default top-k (0 means disabled).
    /// </summary>
    public int TopK { get; init; } = 40;

    /// <summary>
    /// Default maximum tokens to generate.
    /// </summary>
    public int MaxTokens { get; init; } = 1024;

    /// <summary>
    /// Default repeat penalty (1.0 = disabled, >1.0 discourages repetition).
    /// </summary>
    public float RepeatPenalty { get; init; } = 1.1f;

    /// <summary>
    /// Default presence penalty.
    /// </summary>
    public float PresencePenalty { get; init; } = 0.0f;

    /// <summary>
    /// Default frequency penalty.
    /// </summary>
    public float FrequencyPenalty { get; init; } = 0.0f;

    /// <summary>
    /// Default seed for deterministic generation (0 = random).
    /// </summary>
    public int Seed { get; init; } = 0;

    /// <summary>
    /// Default max completion tokens (overrides MaxTokens if set).
    /// </summary>
    public int? MaxCompletionTokens { get; init; }
}