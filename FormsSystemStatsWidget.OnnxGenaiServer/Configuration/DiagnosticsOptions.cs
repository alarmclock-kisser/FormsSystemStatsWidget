using System.Text.Json.Serialization;

namespace FormsSystemStatsWidget.OnnxGenaiServer.Configuration;

/// <summary>
/// Diagnostics and logging configuration.
/// </summary>
public sealed class DiagnosticsOptions
{
    /// <summary>
    /// Log level (Default, Information, Warning, Error, Critical).
    /// </summary>
    public string LogLevel { get; init; } = "Information";

    /// <summary>
    /// If true, enable performance logging (tokens/s, ms/token, stage times).
    /// </summary>
    public bool PerformanceLogging { get; init; } = true;

    /// <summary>
    /// If true, enable GPU memory monitoring.
    /// </summary>
    public bool GpuMonitoring { get; init; } = true;

    /// <summary>
    /// If true, include model information in diagnostics output.
    /// </summary>
    public bool ModelInfo { get; init; } = true;

    /// <summary>
    /// If true, include CUDA/ORT version information.
    /// </summary>
    public bool ProviderInfo { get; init; } = true;

    /// <summary>
    /// If true, include GPU hardware information.
    /// </summary>
    public bool GpuInfo { get; init; } = true;

    /// <summary>
    /// If true, log startup diagnostics at Information level.
    /// </summary>
    public bool StartupDiagnostics { get; init; } = true;

    /// <summary>
    /// Custom log format identifier to include in log messages.
    /// </summary>
    public string LogPrefix { get; init; } = "ONNXGenAI";

    /// <summary>
    /// Maximum number of diagnostics entries to retain in memory.
    /// </summary>
    public int MaxHistoryEntries { get; init; } = 1000;
}