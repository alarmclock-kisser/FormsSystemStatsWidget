using System.Text.Json.Serialization;

namespace FormsSystemStatsWidget.OnnxGenaiServer.Configuration;

/// <summary>
/// Runtime configuration options.
/// </summary>
public sealed class RuntimeOptions
{
    /// <summary>
    /// If true, model is not loaded immediately at engine startup.
    /// First inference request triggers lazy loading.
    /// </summary>
    public bool LazyLoad { get; init; } = true;

    /// <summary>
    /// Ordered list of execution providers to try (first available wins).
    /// </summary>
    public List<string> ExecutionProviders { get; init; } = new() { "CUDAExecutionProvider", "CPUExecutionProvider" };

    /// <summary>
    /// If true, allow falling back to CPU if requested provider fails.
    /// </summary>
    public bool AllowCpuFallback { get; init; } = true;

    /// <summary>
    /// Enable deterministic mode for testing (may impact performance).
    /// </summary>
    public bool Deterministic { get; init; } = false;

    /// <summary>
    /// Maximum sequence length including generated tokens.
    /// </summary>
    public int MaxSequenceLength { get; init; } = 8192;
}