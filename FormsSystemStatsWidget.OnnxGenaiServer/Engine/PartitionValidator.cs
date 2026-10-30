namespace FormsSystemStatsWidget.OnnxGenaiServer.Engine;

/// <summary>
/// Partition-Validierung (R57).
/// Verifiziert stage0/stage1 Graph existiert, .data-Dateien existieren,
/// external references valid, boundary tensors in config.json definiert.
/// </summary>
public static class PartitionValidator
{
    /// <summary>
    /// Validiert eine PrePartitioned-Modell-Partition.
    /// </summary>
    public static ValidationResult ValidatePartition(
        string modelDirectory,
        ModelPartitioner.PartitionDiscoveryResult discovery,
        ILogger? logger = null)
    {
        var issues = new List<ValidationIssue>();

        if (!discovery.IsPartitioned)
        {
            issues.Add(new ValidationIssue
            {
                Category = "Partition",
                Message = "Modell ist nicht partitioniert (keine stage0/stage1 ONNX-Dateien)",
                Severity = ValidationSeverity.Warning,
                FileName = null
            });
            return new ValidationResult
            {
                IsValid = true, // Nicht partitioniert ist kein Fehler
                Issues = issues,
                Warnings = issues,
                ValidatedAtUtc = DateTime.UtcNow
            };
        }

        // 1. Stage0 ONNX-Datei validieren
        if (discovery.Stage0Model is not null)
        {
            var stage0Result = OnnxValidator.ValidateOnnx(discovery.Stage0Model);
            issues.AddRange(stage0Result.Issues.Where(i => i.Severity == ValidationSeverity.Error)
                .Select(i => new ValidationIssue
                {
                    Category = "Partition",
                    Message = $"Stage0: {i.Message}",
                    Severity = i.Severity,
                    FileName = i.FileName
                }));
        }
        else
        {
            issues.Add(new ValidationIssue
            {
                Category = "Partition",
                Message = "Stage0 ONNX-Datei fehlt",
                Severity = ValidationSeverity.Error,
                FileName = "stage0.onnx"
            });
        }

        // 2. Stage1 ONNX-Datei validieren
        if (discovery.Stage1Model is not null)
        {
            var stage1Result = OnnxValidator.ValidateOnnx(discovery.Stage1Model);
            issues.AddRange(stage1Result.Issues.Where(i => i.Severity == ValidationSeverity.Error)
                .Select(i => new ValidationIssue
                {
                    Category = "Partition",
                    Message = $"Stage1: {i.Message}",
                    Severity = i.Severity,
                    FileName = i.FileName
                }));
        }
        else
        {
            issues.Add(new ValidationIssue
            {
                Category = "Partition",
                Message = "Stage1 ONNX-Datei fehlt",
                Severity = ValidationSeverity.Error,
                FileName = "stage1.onnx"
            });
        }

        // 3. .data-Dateien prüfen (optional, aber warnen wenn ONNX external data nutzt)
        if (discovery.Stage0Model is not null && discovery.Stage0Data is null)
        {
            // Prüfen ob Stage0 external data nutzt
            var stage0HasExternalData = HasExternalData(discovery.Stage0Model);
            if (stage0HasExternalData)
            {
                issues.Add(new ValidationIssue
                {
                    Category = "Partition",
                    Message = "Stage0 nutzt external data, aber .data-Datei fehlt",
                    Severity = ValidationSeverity.Error,
                    FileName = "stage0.onnx.data"
                });
            }
        }

        if (discovery.Stage1Model is not null && discovery.Stage1Data is null)
        {
            var stage1HasExternalData = HasExternalData(discovery.Stage1Model);
            if (stage1HasExternalData)
            {
                issues.Add(new ValidationIssue
                {
                    Category = "Partition",
                    Message = "Stage1 nutzt external data, aber .data-Datei fehlt",
                    Severity = ValidationSeverity.Error,
                    FileName = "stage1.onnx.data"
                });
            }
        }

        // 4. Boundary-Tensoren prüfen
        if (discovery.BoundaryTensors.Count == 0)
        {
            issues.Add(new ValidationIssue
            {
                Category = "Partition",
                Message = "Keine Boundary-Tensoren definiert — Stage0→Stage1 Übergabe nicht möglich",
                Severity = ValidationSeverity.Error,
                FileName = "config.json"
            });
        }
        else
        {
            logger?.LogInformation("Boundary-Tensoren: {Count} ({Tensors})",
                discovery.BoundaryTensors.Count, string.Join(", ", discovery.BoundaryTensors));
        }

        // 5. Device-Assignment prüfen
        if (discovery.Stage0Device is int d0 && discovery.Stage1Device is int d1)
        {
            if (d0 == d1)
            {
                issues.Add(new ValidationIssue
                {
                    Category = "Partition",
                    Message = $"Stage0 und Stage1 auf derselben GPU (Device {d0}) — Multi-GPU nicht aktiv",
                    Severity = ValidationSeverity.Warning,
                    FileName = null
                });
            }
        }

        var result = new ValidationResult
        {
            IsValid = issues.All(i => i.Severity != ValidationSeverity.Error),
            Issues = issues,
            Warnings = issues.Where(i => i.Severity == ValidationSeverity.Warning).ToList(),
            ValidatedAtUtc = DateTime.UtcNow
        };

        logger?.LogInformation("Partition-Validierung: {Valid} ({Errors} Errors, {Warnings} Warnings)",
            result.IsValid, result.Errors.Count, result.Warnings.Count);

        return result;
    }

    /// <summary>
    /// Prüft ob eine ONNX-Datei external data nutzt (einfache Byte-Suche nach "external_data").
    /// </summary>
    private static bool HasExternalData(string onnxPath)
    {
        try
        {
            // Nur die ersten 1MB lesen (external_data-Referenzen sind am Anfang)
            using var fs = File.OpenRead(onnxPath);
            var buffer = new byte[Math.Min(1024 * 1024, (int)new FileInfo(onnxPath).Length)];
            var read = fs.Read(buffer, 0, buffer.Length);
            var text = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
            return text.Contains("external_data", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
