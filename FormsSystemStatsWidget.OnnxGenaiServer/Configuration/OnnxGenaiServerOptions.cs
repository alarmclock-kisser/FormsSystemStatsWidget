namespace FormsSystemStatsWidget.OnnxGenaiServer;

/// <summary>
/// Konfiguration des ONNX GenAI Servers. Wird aus appsettings.json /
/// Umgebungsvariablen (ONNXGENAI_SERVER__*) gelesen.
/// </summary>
public sealed class OnnxGenaiServerOptions
{
    public const string SectionName = "OnnxGenaiServer";

    /// <summary>
    /// Kestrel-Listen-URLs, z. B. "http://localhost:8080".
    /// </summary>
    public string[] ListenUrls { get; set; } = [];

    /// <summary>
    /// Root-Ordner für alle ONNX-Modelle. Jede Subdirectory (1 Ebene, nicht rekursiv) ist
    /// ein Modell-Rootdir, das direkt .onnx/.onnx.data + die erforderlichen .json-Files enthält.
    /// Beispiel: D:\Models\ONNX\gemma-4-eb4-it-ONNX, D:\Models\ONNX\Qwen3.8-27B-onnx-int4
    /// </summary>
    public string ModelRootDirectory { get; set; } = @"D:\Models\ONNX";

    /// <summary>
    /// Name des Modells (Subdir-Name im ModelRootDirectory), das beim Start geladen wird.
    /// Leer = erstes gefundenes Modell.
    /// </summary>
    public string DefaultModel { get; set; } = string.Empty;

    /// <summary>
    /// ONNX package layout: Auto (prefer discovered partitions), Main, or Partitioned.
    /// </summary>
    public string ModelLayout { get; set; } = "Auto";

    /// <summary>
    /// ONNX Runtime Execution Provider. Für CUDA-int4-Quantisierung: "Dml" (DirectML) oder "Cuda".
    /// </summary>
    public string ExecutionProvider { get; set; } = "Dml";

    /// <summary>
    /// Context cap (n_ctx): prompt + max_tokens beyond this are rejected
    /// with finish_reason "length" before any GPU work (0 = unlimited).
    /// </summary>
    public int ContextLength { get; set; } = 4096;

    /// <summary>
    /// Standard-Generierungsparameter (werden pro Request überschrieben).
    /// </summary>
    public float Temperature { get; set; } = 0.8f;
    public float TopP { get; set; } = 0.9f;
    public int TopK { get; set; } = 40;
    public int MaxTokens { get; set; } = 1024;
    public float RepeatPenalty { get; set; } = 1.1f;
    public float MinP { get; set; } = 0.0f;
    public float TypicalP { get; set; } = 1.0f;
    public float PresencePenalty { get; set; } = 0.0f;
    public float FrequencyPenalty { get; set; } = 0.0f;

    /// <summary>
    /// Default seed (null = random per request). Per-request "seed" wins.
    /// </summary>
    public int? DefaultSeed { get; set; }

    /// <summary>
    /// Stop sequences, semicolon-separated (in addition to per-request "stop").
    /// </summary>
    public string StopSequences { get; set; } = string.Empty;

    /// <summary>
    /// Repetition-penalty window in tokens (0 = full history, like before).
    /// </summary>
    public int RepeatLastN { get; set; } = 0;

    /// <summary>
    /// Default for the model-specific "enable_thinking" template flag.
    /// Per-request "enable_thinking" wins.
    /// </summary>
    public bool EnableThinking { get; set; }

    // ---- Inference execution (load-time, needs model reload on change) ----

    /// <summary>CUDA device for stage 0 (partitioned) / the single stage.</summary>
    public int Stage0Device { get; set; } = 0;

    /// <summary>CUDA device for stage 1 (partitioned models).</summary>
    public int Stage1Device { get; set; } = 1;

    /// <summary>Append CPUExecutionProvider after CUDA (slower, but won't hard-fail).</summary>
    public bool AllowCpuFallback { get; set; }

    public int IntraOpThreads { get; set; } = 0;
    public int InterOpThreads { get; set; } = 0;

    /// <summary>ORT_SEQUENTIAL or ORT_PARALLEL.</summary>
    public string ExecutionMode { get; set; } = "sequential";

    /// <summary>disable_all, basic, extended or all (ORT_-prefixed spellings accepted).</summary>
    public string GraphOptimization { get; set; } = "all";

    public bool EnableMemPattern { get; set; } = true;
    public bool EnableCpuMemArena { get; set; } = true;
    public bool EnableProfiling { get; set; }
    public bool DisablePrepacking { get; set; }

    /// <summary>CUDA arena memory limit in MB (0 = unlimited).</summary>
    public long GpuMemLimitMb { get; set; } = 0;

    /// <summary>kNextPowerOfTwo or kSameAsRequested.</summary>
    public string ArenaExtendStrategy { get; set; } = "kNextPowerOfTwo";

    /// <summary>EXHAUSTIVE, HEURISTIC or DEFAULT.</summary>
    public string CudnnConvAlgoSearch { get; set; } = "EXHAUSTIVE";

    public bool CopyInDefaultStream { get; set; } = true;

    /// <summary>CUDA graphs for steady-state decode (experimental, needs reload).</summary>
    public bool UseCudaGraphs { get; set; }

    public bool UseTf32 { get; set; } = true;

    /// <summary>
    /// Max. Anzahl paralleler Generationen (Sessions).
    /// </summary>
    public int MaxConcurrentGenerations { get; set; } = 1;

    /// <summary>
    /// System-Prompt, der standardmäßig vorangestellt wird (wie AdditionalCopilotSystemPrompt im Widget).
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Modell-Name, der in OpenAI-Antworten als "model" zurückgegeben wird.
    /// </summary>
    public string ModelId { get; set; } = "fssw-onnx-genai";

    /// <summary>
    /// URL des Python-ONNX-Servers, an den die C#-Engine Inference-Anfragen weiterleitet.
    /// Standard: http://localhost:8080 (gleicher Port wie der C#-Server).
    /// </summary>
    public string? PythonServerUrl { get; set; }

    // Phase 3b: Python-Process-Supervision + IPC

    /// <summary>
    /// Pfad zum Python-Executable (z. B. "python", "C:\Python311\python.exe", oder venv-Pfad).
    /// </summary>
    public string? PythonExecutable { get; set; }

    /// <summary>
    /// Python-Modul-Pfad für die Engine (z. B. "onnx_engine.server").
    /// </summary>
    public string? PythonEngineModule { get; set; }

    /// <summary>
    /// Port für die Python-Engine (IPC via HTTP).
    /// </summary>
    public int? PythonEnginePort { get; set; }

    public bool SingleEngineOnly { get; set; } = true;
    public bool IdleAutoShutdownEnabled { get; set; } = true;
    public int IdleAutoShutdownSeconds { get; set; } = 120;
    public int GracefulShutdownTimeoutSeconds { get; set; } = 5;
    public int ForceKillTimeoutSeconds { get; set; } = 3;
    public int RegistryLockTimeoutSeconds { get; set; } = 10;
    public int HealthCheckIntervalSeconds { get; set; } = 15;
    public int StartupTimeoutSeconds { get; set; } = 30;
    public int MaxPythonRestarts { get; set; } = 3;
    public int PythonRestartIntervalSeconds { get; set; } = 5;
    public string? EngineProcessRegistryPath { get; set; }
}
