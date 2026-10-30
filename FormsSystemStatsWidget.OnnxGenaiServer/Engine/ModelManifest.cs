namespace FormsSystemStatsWidget.OnnxGenaiServer.Engine;

/// <summary>
/// Normalisiertes internes Modell-Manifest (R20).
/// </summary>
public sealed class ModelManifest
{
    public string ModelId { get; init; } = string.Empty;
    public string ModelDirectory { get; init; } = string.Empty;

    public IReadOnlyList<ModelArtifact> Artifacts { get; init; } = [];

    public ModelArchitectureInfo Architecture { get; init; } = new();

    public TokenizerInfo Tokenizer { get; init; } = new();

    public PartitionInfo? Partition { get; init; }

    public ValidationResult Validation { get; init; } = new();
}

/// <summary>
/// Ein einzelnes Modell-Artefakt (Datei) im Modell-Verzeichnis (R21).
/// </summary>
public sealed class ModelArtifact
{
    public string FileName { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string? Sha256 { get; init; }
    public bool IsOnnxGraph { get; init; }
    public bool IsExternalData { get; init; }
    public bool IsJson { get; init; }
    public bool IsTokenizer { get; init; }
    public bool IsChatTemplate { get; init; }
    public bool IsRequired { get; init; }
}

/// <summary>
/// Architektur-Informationen, die aus config.json / Modell-Metadaten ermittelt werden (R10, R24).
/// </summary>
public sealed class ModelArchitectureInfo
{
    public string? Architecture { get; init; }
    public int? HiddenSize { get; init; }
    public int? VocabSize { get; init; }
    public int? NumLayers { get; init; }
    public int? NumAttentionHeads { get; init; }
    public int? NumKeyValueHeads { get; init; }
    public int? RmsNormEps { get; init; }
    public string? DType { get; init; }
    public int? MaxPositionEmbeddings { get; init; }
    public bool IsMoe { get; init; }
    public string? RawJson { get; init; }
}

/// <summary>
/// Tokenizer-Informationen (R26).
/// </summary>
public sealed class TokenizerInfo
{
    public string? TokenizerFile { get; init; }
    public string? TokenizerConfigFile { get; init; }
    public string? SpecialTokensFile { get; init; }
    public string? MergesFile { get; init; }
    public string? VocabFile { get; init; }
    public int? VocabSize { get; init; }
    public string? ModelType { get; init; }
    public bool IsDetected { get; init; }
}

/// <summary>
/// Partition-Informationen für PrePartitioned-Modelle (R55, R57).
/// </summary>
public sealed class PartitionInfo
{
    public string Mode { get; init; } = "PrePartitioned";
    public string? Stage0Model { get; init; }
    public string? Stage1Model { get; init; }
    public string? Stage0Data { get; init; }
    public string? Stage1Data { get; init; }
    public int? Stage0Device { get; init; }
    public int? Stage1Device { get; init; }
    public IReadOnlyList<string> BoundaryTensors { get; init; } = [];
}

/// <summary>
/// Aggregiertes Validierungsergebnis (R21-R24, R23).
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = [];
    public IReadOnlyList<ValidationIssue> Warnings { get; init; } = [];
    public DateTime? ValidatedAtUtc { get; init; }

    public IReadOnlyList<ValidationIssue> Errors => Issues.Where(i => i.Severity == ValidationSeverity.Error).ToList();
}

public sealed class ValidationIssue
{
    public string Category { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public ValidationSeverity Severity { get; init; }
    public string? FileName { get; init; }
}

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}
