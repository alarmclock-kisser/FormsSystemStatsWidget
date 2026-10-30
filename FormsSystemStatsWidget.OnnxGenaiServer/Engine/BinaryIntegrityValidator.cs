using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FormsSystemStatsWidget.OnnxGenaiServer.Engine;

namespace FormsSystemStatsWidget.OnnxGenaiServer.Engine;

/// <summary>
/// SHA-256 Binary-Integrity-Validierung (R23).
/// Berechnet und validiert Checksummen für Modell-Dateien.
/// </summary>
public static class BinaryIntegrityValidator
{
    /// <summary>
    /// Berechnet SHA-256 für alle binären Modell-Dateien (ONNX, .data, etc.).
    /// </summary>
    public static Dictionary<string, FileHash> ComputeHashes(string modelDirectory)
    {
        var hashes = new Dictionary<string, FileHash>();

        var files = Directory.EnumerateFiles(modelDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(f =>
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                return ext == ".onnx" || ext == ".data" || ext == ".bin" || ext == ".safetensors";
            })
            .ToList();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var fileInfo = new FileInfo(file);
            var sha256 = OnnxValidator.ComputeSha256(file);

            hashes[fileName] = new FileHash
            {
                FileName = fileName,
                SizeBytes = fileInfo.Length,
                Sha256 = sha256,
                ComputedAtUtc = DateTime.UtcNow
            };
        }

        return hashes;
    }

    /// <summary>
    /// Validiert SHA-256 Checksummen gegen ein optionales checksums.sha256-Manifest.
    /// </summary>
    public static ValidationResult ValidateAgainstManifest(string modelDirectory)
    {
        var issues = new List<ValidationIssue>();
        var manifestPath = Path.Combine(modelDirectory, "checksums.sha256");

        if (!File.Exists(manifestPath))
        {
            issues.Add(new ValidationIssue
            {
                Category = "Integrity",
                Message = "checksums.sha256-Manifest nicht gefunden (optional)",
                Severity = ValidationSeverity.Warning,
                FileName = "checksums.sha256"
            });
            return new ValidationResult
            {
                IsValid = true,
                Issues = issues,
                Warnings = issues,
                ValidatedAtUtc = DateTime.UtcNow
            };
        }

        try
        {
            var manifestJson = File.ReadAllText(manifestPath);
            using var doc = JsonDocument.Parse(manifestJson);
            var root = doc.RootElement;

            foreach (var prop in root.EnumerateObject())
            {
                var fileName = prop.Name;
                var expectedSha256 = prop.Value.TryGetProperty("sha256", out var sha) ? sha.GetString() : null;
                long? expectedSize = prop.Value.TryGetProperty("size", out var size) ? size.GetInt64() : (long?)null;

                if (expectedSha256 is null)
                {
                    issues.Add(new ValidationIssue
                    {
                        Category = "Integrity",
                        Message = $"checksums.sha256: fehlendes 'sha256' Feld für {fileName}",
                        Severity = ValidationSeverity.Warning,
                        FileName = fileName
                    });
                    continue;
                }

                var filePath = Path.Combine(modelDirectory, fileName);
                if (!File.Exists(filePath))
                {
                    issues.Add(new ValidationIssue
                    {
                        Category = "Integrity",
                        Message = $"checksums.sha256: Datei {fileName} im Manifest, aber nicht im Verzeichnis",
                        Severity = ValidationSeverity.Error,
                        FileName = fileName
                    });
                    continue;
                }

                var actualSha256 = OnnxValidator.ComputeSha256(filePath);
                if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue
                    {
                        Category = "Integrity",
                        Message = $"checksums.sha256: SHA-256-Mismatch für {fileName} (erwartet: {expectedSha256[..16]}..., gefunden: {actualSha256[..16]}...)",
                        Severity = ValidationSeverity.Error,
                        FileName = fileName
                    });
                }

                if (expectedSize is not null)
                {
                    var actualSize = new FileInfo(filePath).Length;
                    if (actualSize != expectedSize)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Category = "Integrity",
                            Message = $"checksums.sha256: Größen-Mismatch für {fileName} (erwartet: {expectedSize}, gefunden: {actualSize})",
                            Severity = ValidationSeverity.Error,
                            FileName = fileName
                        });
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue
            {
                Category = "Integrity",
                Message = $"checksums.sha256: Malformed JSON — {ex.Message}",
                Severity = ValidationSeverity.Error,
                FileName = "checksums.sha256"
            });
        }

        return new ValidationResult
        {
            IsValid = issues.All(i => i.Severity != ValidationSeverity.Error),
            Issues = issues,
            Warnings = issues.Where(i => i.Severity == ValidationSeverity.Warning).ToList(),
            ValidatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Erzeugt ein checksums.sha256-Manifest für alle binären Modell-Dateien.
    /// </summary>
    public static string GenerateManifest(string modelDirectory)
    {
        var hashes = ComputeHashes(modelDirectory);
        var manifest = new Dictionary<string, object>();

        foreach (var (fileName, hash) in hashes)
        {
            manifest[fileName] = new
            {
                size = hash.SizeBytes,
                sha256 = hash.Sha256
            };
        }

        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
    }
}

/// <summary>
/// SHA-256 Hash einer Datei.
/// </summary>
public sealed class FileHash
{
    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string Sha256 { get; init; } = string.Empty;
    public DateTime ComputedAtUtc { get; init; }
}
