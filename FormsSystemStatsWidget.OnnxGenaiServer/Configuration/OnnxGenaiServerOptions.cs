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
    public string[] ListenUrls { get; set; } = ["http://localhost:8080"];

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
    /// ONNX Runtime Execution Provider. Für CUDA-int4-Quantisierung: "Dml" (DirectML) oder "Cuda".
    /// </summary>
    public string ExecutionProvider { get; set; } = "Dml";

    /// <summary>
    /// Kontextlänge (n_ctx) für die KV-Cache-Allokation.
    /// </summary>
    public int ContextLength { get; set; } = 4096;

    /// <summary>
    /// Max. Anzahl paralleler Generationen (Sessions).
    /// </summary>
    public int MaxConcurrentGenerations { get; set; } = 1;

    /// <summary>
    /// Standard-Generierungsparameter (werden pro Request überschrieben).
    /// </summary>
    public float Temperature { get; set; } = 0.8f;
    public float TopP { get; set; } = 0.9f;
    public int TopK { get; set; } = 40;
    public int MaxTokens { get; set; } = 1024;
    public float RepeatPenalty { get; set; } = 1.1f;

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
}
