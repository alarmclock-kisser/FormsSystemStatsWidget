using System.Text.Json;

namespace FormsSystemStatsWidget.OnnxGenaiServer.Engine;

/// <summary>
/// Modell-Partitionierung Mode A — PrePartitioned (R55).
/// Detectet stage0.onnx + stage1.onnx im Modell-Verzeichnis oder partitioned/,
/// liest Partition-Config aus config.json, bestimmt Device-Assignment.
/// </summary>
public static class ModelPartitioner
{
    /// <summary>
    /// Ergebnis der Partition-Discovery.
    /// </summary>
    public sealed class PartitionDiscoveryResult
    {
        public bool IsPartitioned { get; init; }
        public string? Stage0Model { get; init; }
        public string? Stage1Model { get; init; }
        public string? Stage0Data { get; init; }
        public string? Stage1Data { get; init; }
        public int? Stage0Device { get; init; }
        public int? Stage1Device { get; init; }
        public IReadOnlyList<string> BoundaryTensors { get; init; } = [];
        public IReadOnlyList<ValidationIssue> Issues { get; init; } = [];
    }

    /// <summary>
    /// Detectet PrePartitioned-Modell (stage0.onnx + stage1.onnx) im Modell-Verzeichnis.
    /// </summary>
    public static PartitionDiscoveryResult Discover(string modelDirectory, CudaOptions? cudaOptions = null, ILogger? logger = null)
    {
        var issues = new List<ValidationIssue>();
        var partitionDirectory = Path.Combine(modelDirectory, "partitioned");
        var stageDirectory = File.Exists(Path.Combine(partitionDirectory, "model.stage0.onnx"))
            || File.Exists(Path.Combine(partitionDirectory, "model.stage1.onnx"))
            ? partitionDirectory
            : modelDirectory;
        var stage0Model = FindStageFile(stageDirectory, "stage0", issues);
        var stage1Model = FindStageFile(stageDirectory, "stage1", issues);

        if (stage0Model is null || stage1Model is null)
        {
            // Nicht partitioniert — kein Fehler, einfach kein PrePartitioned-Modell
            return new PartitionDiscoveryResult
            {
                IsPartitioned = false,
                Issues = issues
            };
        }

        // .data-Dateien suchen
        var stage0Data = FindDataFile(stage0Model);
        var stage1Data = FindDataFile(stage1Model);

        // Boundary-Tensoren aus config.json lesen
        var boundaryTensors = ReadBoundaryTensors(modelDirectory, issues, logger);

        // Device-Assignment bestimmen
        var stage0Device = cudaOptions?.Stage0Device ?? 0;
        var stage1Device = cudaOptions?.Stage1Device ?? 1;

        // Wenn nur 1 GPU verfügbar, beide Stages auf Device 0
        if (cudaOptions is not null && cudaOptions.Enabled)
        {
            // Device-Assignment bleibt wie konfiguriert
            // (Validierung ob genug GPUs vorhanden happens in EnvironmentDiscovery)
        }

        logger?.LogInformation("Partition-Discovery: Stage0={S0} (Device {D0}), Stage1={S1} (Device {D1}), BoundaryTensors={BT}",
            Path.GetFileName(stage0Model), stage0Device, Path.GetFileName(stage1Model), stage1Device, boundaryTensors.Count);

        return new PartitionDiscoveryResult
        {
            IsPartitioned = true,
            Stage0Model = stage0Model,
            Stage1Model = stage1Model,
            Stage0Data = stage0Data,
            Stage1Data = stage1Data,
            Stage0Device = stage0Device,
            Stage1Device = stage1Device,
            BoundaryTensors = boundaryTensors,
            Issues = issues
        };
    }

    /// <summary>
    /// Sucht die Stage-ONNX-Datei (stage0.onnx, stage1.onnx, model.stage0.onnx, etc.).
    /// </summary>
    private static string? FindStageFile(string modelDirectory, string stageName, List<ValidationIssue> issues)
    {
        // Mögliche Dateinamen: stage0.onnx, model.stage0.onnx, stage0_model.onnx
        var candidates = new[]
        {
            $"{stageName}.onnx",
            $"model.{stageName}.onnx",
            $"{stageName}_model.onnx",
            $"model_{stageName}.onnx"
        };

        foreach (var candidate in candidates)
        {
            var path = Path.Combine(modelDirectory, candidate);
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Fallback: Suche nach *.onnx-Dateien die "stage" im Namen enthalten
        var onnxFiles = Directory.EnumerateFiles(modelDirectory, "*.onnx", SearchOption.TopDirectoryOnly)
            .Where(f => !f.EndsWith(".onnx.data", StringComparison.OrdinalIgnoreCase))
            .Where(f => f.Contains(stageName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (onnxFiles.Count > 0)
        {
            return onnxFiles[0];
        }

        issues.Add(new ValidationIssue
        {
            Category = "Partition",
            Message = $"{stageName}.onnx nicht gefunden in {modelDirectory}",
            Severity = ValidationSeverity.Warning,
            FileName = $"{stageName}.onnx"
        });

        return null;
    }

    /// <summary>
    /// Sucht die .data-Datei für eine ONNX-Datei.
    /// </summary>
    private static string? FindDataFile(string onnxPath)
    {
        var dataPath = onnxPath + ".data";
        return File.Exists(dataPath) ? dataPath : null;
    }

    /// <summary>
    /// Liest Boundary-Tensoren aus config.json (Feld "boundary_tensors" oder "partition_boundary").
    /// </summary>
    private static List<string> ReadBoundaryTensors(string modelDirectory, List<ValidationIssue> issues, ILogger? logger)
    {
        var boundaryTensors = new List<string>();
        var configPath = Path.Combine(modelDirectory, "config.json");

        if (!File.Exists(configPath))
        {
            issues.Add(new ValidationIssue
            {
                Category = "Partition",
                Message = "config.json nicht gefunden — Boundary-Tensoren unbekannt",
                Severity = ValidationSeverity.Warning,
                FileName = "config.json"
            });
            return boundaryTensors;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Verschiedene mögliche Feldnamen
            var possibleFields = new[] { "boundary_tensors", "partition_boundary", "boundary", "stage_boundary" };
            foreach (var field in possibleFields)
            {
                if (root.TryGetProperty(field, out var val) && val.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in val.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            var tensorName = item.GetString();
                            if (!string.IsNullOrWhiteSpace(tensorName))
                            {
                                boundaryTensors.Add(tensorName);
                            }
                        }
                    }
                    break;
                }
            }

            if (boundaryTensors.Count == 0)
            {
                issues.Add(new ValidationIssue
                {
                    Category = "Partition",
                    Message = "Keine Boundary-Tensoren in config.json gefunden (Felder: boundary_tensors, partition_boundary, boundary, stage_boundary)",
                    Severity = ValidationSeverity.Warning,
                    FileName = "config.json"
                });
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue
            {
                Category = "Partition",
                Message = $"config.json: Malformed JSON — {ex.Message}",
                Severity = ValidationSeverity.Error,
                FileName = "config.json"
            });
        }

        return boundaryTensors;
    }
}
