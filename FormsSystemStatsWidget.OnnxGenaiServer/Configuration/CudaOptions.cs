using System.Text.Json.Serialization;

namespace FormsSystemStatsWidget.OnnxGenaiServer.Configuration;

/// <summary>
/// CUDA/GPU configuration options.
/// </summary>
public sealed class CudaOptions
{
    /// <summary>
    /// If true, CUDA/ExecutionProvider is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Device index for Stage 0 (typically where embedding + early layers run).
    /// </summary>
    public int Stage0Device { get; init; } = 0;

    /// <summary>
    /// Device index for Stage 1 (typically where later layers + lm_head run).
    /// </summary>
    public int Stage1Device { get; init; } = 1;

    /// <summary>
    /// If true, use CUDA execution provider with memory arena enabled.
    /// </summary>
    public bool UseMemoryArena { get; init; } = true;

    /// <summary>
    /// If true, enable CUDA graph recording for steady-state decode (experimental).
    /// </summary>
    public bool UseCudaGraphs { get; init; } = false;

    /// <summary>
    /// If true, allow CPU fallback if CUDA operations fail.
    /// </summary>
    public bool AllowCpuFallback { get; init; } = true;

    /// <summary>
    /// Enable verbose CUDA diagnostics logging.
    /// </summary>
    public bool EnableDiagnostics { get; init; } = false;
}