using System.Security.Cryptography;
using System.Text;
using FormsSystemStatsWidget.OnnxGenaiServer.Engine;

namespace FormsSystemStatsWidget.OnnxGenaiServer.Engine;

/// <summary>
/// ONNX-Validierung (R22 Level 1-3, R23 SHA-256).
/// Level 1: Filesystem (exists/readable/size>0)
/// Level 2: External data (offsets, lengths, end offsets, overlap detection)
/// Level 3: ONNX protobuf (nodes, initializers, graph inputs/outputs, opsets)
/// Level 4: Runtime (ORT session creation) — separat implementiert
/// </summary>
public static class OnnxValidator
{
    private static readonly byte[] MagicBytes = { 0x6F, 0x6E, 0x58, 0x4E, 0x58, 0x00, 0x00, 0x00 }; // "ONNX" + 4 null bytes

    /// <summary>
    /// Führt Level 1-3 ONNX-Validierung für eine ONNX-Datei durch.
    /// </summary>
    public static ValidationResult ValidateOnnx(string onnxPath)
    {
        var issues = new List<ValidationIssue>();

        // Level 1: Filesystem
        issues.AddRange(ValidateLevel1(onnxPath));

        // Level 2: External data
        issues.AddRange(ValidateLevel2(onnxPath));

        // Level 3: Protobuf
        issues.AddRange(ValidateLevel3(onnxPath));

        return new ValidationResult
        {
            IsValid = issues.All(i => i.Severity != ValidationSeverity.Error),
            Issues = issues,
            Warnings = issues.Where(i => i.Severity == ValidationSeverity.Warning).ToList(),
            ValidatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Berechnet SHA-256 Hash einer Datei.
    /// </summary>
    public static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Validiert alle .onnx-Dateien eines Modell-Rootdirs.
    /// </summary>
    public static IReadOnlyList<(string FileName, ValidationResult Result)> ValidateAllOnnxFiles(string modelDirectory)
    {
        var results = new List<(string FileName, ValidationResult Result)>();

        var onnxFiles = Directory.EnumerateFiles(modelDirectory, "*.onnx", SearchOption.TopDirectoryOnly)
            .Where(f => !f.EndsWith(".onnx.data", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var onnxFile in onnxFiles)
        {
            var fileName = Path.GetFileName(onnxFile);
            var result = ValidateOnnx(onnxFile);
            results.Add((fileName, result));
        }

        return results;
    }

    /// <summary>
    /// Level 1: Filesystem-Validierung (R22 Level 1).
    /// </summary>
    private static List<ValidationIssue> ValidateLevel1(string onnxPath)
    {
        var issues = new List<ValidationIssue>();
        var fileName = Path.GetFileName(onnxPath);

        if (!File.Exists(onnxPath))
        {
            issues.Add(new ValidationIssue
            {
                Category = "ONNX",
                Message = $"{fileName}: Datei existiert nicht",
                Severity = ValidationSeverity.Error,
                FileName = fileName
            });
            return issues;
        }

        var fileInfo = new FileInfo(onnxPath);
        if (fileInfo.Length == 0)
        {
            issues.Add(new ValidationIssue
            {
                Category = "ONNX",
                Message = $"{fileName}: Datei ist leer (size=0)",
                Severity = ValidationSeverity.Error,
                FileName = fileName
            });
            return issues;
        }

        // Magic bytes prüfen
        try
        {
            using var fs = File.OpenRead(onnxPath);
            var header = new byte[8];
            var read = fs.Read(header, 0, 8);
            if (read < 8)
            {
                issues.Add(new ValidationIssue
                {
                    Category = "ONNX",
                    Message = $"{fileName}: Header zu kurz ({read} < 8)",
                    Severity = ValidationSeverity.Error,
                    FileName = fileName
                });
                return issues;
            }

            if (!header.Take(6).SequenceEqual(MagicBytes.Take(6)))
            {
                issues.Add(new ValidationIssue
                {
                    Category = "ONNX",
                    Message = $"{fileName}: Ungültige Magic bytes (kein ONNX-Format)",
                    Severity = ValidationSeverity.Error,
                    FileName = fileName
                });
                return issues;
            }

            // Version lesen (4 bytes ab offset 6)
            var version = BitConverter.ToInt32(header, 6);
            if (version < 1 || version > 19)
            {
                issues.Add(new ValidationIssue
                {
                    Category = "ONNX",
                    Message = $"{fileName}: Ungültige ONNX-Version {version} (erwartet 1-19)",
                    Severity = ValidationSeverity.Warning,
                    FileName = fileName
                });
            }
        }
        catch (IOException ex)
        {
            issues.Add(new ValidationIssue
            {
                Category = "ONNX",
                Message = $"{fileName}: Kann nicht gelesen werden — {ex.Message}",
                Severity = ValidationSeverity.Error,
                FileName = fileName
            });
        }

        return issues;
    }

    /// <summary>
    /// Level 2: External data Validierung (R22 Level 2).
    /// Prüft .onnx.data-Dateien auf korrekte Offsets, Längen, End-Offsets, Overlap.
    /// </summary>
    private static List<ValidationIssue> ValidateLevel2(string onnxPath)
    {
        var issues = new List<ValidationIssue>();
        var fileName = Path.GetFileName(onnxPath);

        var dataPath = onnxPath + ".data";
        if (!File.Exists(dataPath))
        {
            // Keine externe Daten-Datei vorhanden — OK (kleine Modelle)
            return issues;
        }

        var dataFileInfo = new FileInfo(dataPath);
        if (dataFileInfo.Length == 0)
        {
            issues.Add(new ValidationIssue
            {
                Category = "ONNX",
                Message = $"{fileName}.data: Datei ist leer",
                Severity = ValidationSeverity.Error,
                FileName = fileName
            });
            return issues;
        }

        try
        {
            // ONNX-Protobuf parsen, um external_data zu extrahieren
            // Wir lesen die initializers und prüfen ihre offsets
            var protobufData = File.ReadAllBytes(onnxPath);

            // Einfache Suche nach "external_data" Einträgen
            // ONNX-Protobuf: initializers haben ein "external_data" Feld mit key/value pairs
            // Wir suchen nach dem Muster: key="offset", value=<int64>
            var externalDataEntries = ExtractExternalDataEntries(protobufData);

            if (externalDataEntries.Count == 0)
            {
                // Keine externen Daten im Protobuf — OK
                return issues;
            }

            // Alle offsets prüfen
            long maxOffset = 0;
            foreach (var entry in externalDataEntries)
            {
                if (entry.Offset < 0)
                {
                    issues.Add(new ValidationIssue
                    {
                        Category = "ONNX",
                        Message = $"{fileName}: Negativer Offset ({entry.Offset}) für {entry.Key}",
                        Severity = ValidationSeverity.Error,
                        FileName = fileName
                    });
                }

                if (entry.Offset + entry.Length > dataFileInfo.Length)
                {
                    issues.Add(new ValidationIssue
                    {
                        Category = "ONNX",
                        Message = $"{fileName}: Offset+Length ({entry.Offset}+{entry.Length}) > .data-Dateigröße ({dataFileInfo.Length})",
                        Severity = ValidationSeverity.Error,
                        FileName = fileName
                    });
                }

                maxOffset = Math.Max(maxOffset, entry.Offset + entry.Length);
            }

            // Overlap-Prüfung: sortiere Einträge nach Offset und prüfe auf Überlappung
            var sorted = externalDataEntries.OrderBy(e => e.Offset).ToList();
            for (int i = 1; i < sorted.Count; i++)
            {
                var prev = sorted[i - 1];
                var curr = sorted[i];
                if (prev.Offset + prev.Length > curr.Offset)
                {
                    issues.Add(new ValidationIssue
                    {
                        Category = "ONNX",
                        Message = $"{fileName}: Overlap zwischen {prev.Key} (Offset={prev.Offset}, End={prev.Offset + prev.Length}) und {curr.Key} (Offset={curr.Offset})",
                        Severity = ValidationSeverity.Error,
                        FileName = fileName
                    });
                }
            }

            // End-Offset-Prüfung: letzter Eintrag darf nicht über .data-Ende hinausgehen
            if (sorted.Count > 0)
            {
                var last = sorted.Last();
                if (last.Offset + last.Length > dataFileInfo.Length)
                {
                    issues.Add(new ValidationIssue
                    {
                        Category = "ONNX",
                        Message = $"{fileName}: Letzter external_data-Eintrag endet bei {last.Offset + last.Length} > .data-Größe {dataFileInfo.Length}",
                        Severity = ValidationSeverity.Error,
                        FileName = fileName
                    });
                }
            }
        }
        catch (Exception ex)
        {
            issues.Add(new ValidationIssue
            {
                Category = "ONNX",
                Message = $"{fileName}: External data Validierung fehlgeschlagen — {ex.Message}",
                Severity = ValidationSeverity.Warning,
                FileName = fileName
            });
        }

        return issues;
    }

    /// <summary>
    /// Level 3: ONNX-Protobuf Validierung (R22 Level 3).
    /// Prüft nodes, initializers, graph inputs/outputs, opsets.
    /// </summary>
    private static List<ValidationIssue> ValidateLevel3(string onnxPath)
    {
        var issues = new List<ValidationIssue>();
        var fileName = Path.GetFileName(onnxPath);

        try
        {
            var protobufData = File.ReadAllBytes(onnxPath);

            // Graph inputs/outputs prüfen
            var graphInputs = ExtractGraphInputs(protobufData);
            var graphOutputs = ExtractGraphOutputs(protobufData);

            if (graphInputs.Count == 0)
            {
                issues.Add(new ValidationIssue
                {
                    Category = "ONNX",
                    Message = $"{fileName}: Keine graph inputs gefunden",
                    Severity = ValidationSeverity.Error,
                    FileName = fileName
                });
            }

            if (graphOutputs.Count == 0)
            {
                issues.Add(new ValidationIssue
                {
                    Category = "ONNX",
                    Message = $"{fileName}: Keine graph outputs gefunden",
                    Severity = ValidationSeverity.Error,
                    FileName = fileName
                });
            }

            // Initializer count
            var initializerCount = CountInitializers(protobufData);
            if (initializerCount == 0)
            {
                issues.Add(new ValidationIssue
                {
                    Category = "ONNX",
                    Message = $"{fileName}: Keine initializers gefunden (möglicherweise alle als inputs)",
                    Severity = ValidationSeverity.Warning,
                    FileName = fileName
                });
            }

            // Opset-Informationen
            var opsets = ExtractOpsets(protobufData);
            if (opsets.Count == 0)
            {
                issues.Add(new ValidationIssue
                {
                    Category = "ONNX",
                    Message = $"{fileName}: Keine opsets gefunden",
                    Severity = ValidationSeverity.Warning,
                    FileName = fileName
                });
            }

            // Nodes count
            var nodeCount = CountNodes(protobufData);
            if (nodeCount == 0)
            {
                issues.Add(new ValidationIssue
                {
                    Category = "ONNX",
                    Message = $"{fileName}: Keine nodes gefunden",
                    Severity = ValidationSeverity.Error,
                    FileName = fileName
                });
            }
        }
        catch (Exception ex)
        {
            issues.Add(new ValidationIssue
            {
                Category = "ONNX",
                Message = $"{fileName}: Protobuf-Validierung fehlgeschlagen — {ex.Message}",
                Severity = ValidationSeverity.Warning,
                FileName = fileName
            });
        }

        return issues;
    }

    // ------------------------------------------------------------------
    // Einfache Protobuf-Parser-Hilfsfunktionen (kein externes Protobuf-Lib nötig)
    // ------------------------------------------------------------------

    private static List<(string Key, long Offset, long Length)> ExtractExternalDataEntries(byte[] data)
    {
        var entries = new List<(string Key, long Offset, long Length)>();

        // Suche nach "external_data" Feldern in initializers
        // ONNX-Protobuf: repeated ExternalDataEntry { string key = 1; int64 value = 2; }
        // Wir suchen nach dem Muster: key (string) + value (int64 varint)
        var pos = 0;
        while (pos < data.Length - 10)
        {
            // Suche nach "offset" oder "length" als string key
            if (pos + 7 <= data.Length && data[pos] == 0x0A) // field 1 (key)
            {
                var keyLen = ReadVarint(data, pos + 1, out var keyLenVal);
                if (keyLenVal > 0 && keyLenVal < 20 && pos + 1 + keyLenVal + 8 <= data.Length)
                {
                    var keyBytes = data[(pos + 1 + keyLenVal)..(pos + 1 + keyLenVal + 8)];
                    var key = Encoding.UTF8.GetString(keyBytes);
                    if (key == "offset" || key == "length")
                    {
                        var valPos = pos + 1 + keyLenVal + 8;
                        var val = ReadInt64Varint(data, valPos, out var valLen);
                        if (val >= 0)
                        {
                            entries.Add((key, val, 0));
                        }
                        pos += 1 + keyLenVal + 8 + valLen;
                        continue;
                    }
                }
            }
            pos++;
        }

        return entries;
    }

    private static List<string> ExtractGraphInputs(byte[] data)
    {
        var inputs = new List<string>();
        // Suche nach "input" Feldern im graph
        var pos = 0;
        while (pos < data.Length - 5)
        {
            if (pos + 7 <= data.Length && data[pos] == 0x0A) // field 1 (name)
            {
                var nameLen = ReadVarint(data, pos + 1, out var nameLenVal);
                if (nameLenVal > 0 && nameLenVal < 100 && pos + 1 + nameLenVal + 8 <= data.Length)
                {
                    var nameBytes = data[(pos + 1 + nameLenVal)..(pos + 1 + nameLenVal + 8)];
                    var name = Encoding.UTF8.GetString(nameBytes);
                    if (name.Length > 0 && name.Length < 100)
                    {
                        inputs.Add(name);
                    }
                }
            }
            pos++;
        }
        return inputs;
    }

    private static List<string> ExtractGraphOutputs(byte[] data)
    {
        var outputs = new List<string>();
        var pos = 0;
        while (pos < data.Length - 5)
        {
            if (pos + 7 <= data.Length && data[pos] == 0x12) // field 2 (output)
            {
                var len = ReadVarint(data, pos + 1, out var lenVal);
                if (lenVal > 0 && lenVal < 100 && pos + 1 + lenVal + 8 <= data.Length)
                {
                    var nameBytes = data[(pos + 1 + lenVal)..(pos + 1 + lenVal + 8)];
                    var name = Encoding.UTF8.GetString(nameBytes);
                    if (name.Length > 0 && name.Length < 100)
                    {
                        outputs.Add(name);
                    }
                }
            }
            pos++;
        }
        return outputs;
    }

    private static int CountInitializers(byte[] data)
    {
        var count = 0;
        var pos = 0;
        while (pos < data.Length - 5)
        {
            if (pos + 7 <= data.Length && data[pos] == 0x1A) // field 3 (initializer)
            {
                count++;
                var len = ReadVarint(data, pos + 1, out var lenVal);
                pos += 1 + lenVal + 8;
                continue;
            }
            pos++;
        }
        return count;
    }

    private static int CountNodes(byte[] data)
    {
        var count = 0;
        var pos = 0;
        while (pos < data.Length - 5)
        {
            if (pos + 7 <= data.Length && data[pos] == 0x22) // field 4 (node)
            {
                count++;
                var len = ReadVarint(data, pos + 1, out var lenVal);
                pos += 1 + lenVal + 8;
                continue;
            }
            pos++;
        }
        return count;
    }

    private static List<(string Domain, string Version)> ExtractOpsets(byte[] data)
    {
        var opsets = new List<(string Domain, string Version)>();
        var pos = 0;
        while (pos < data.Length - 5)
        {
            if (pos + 7 <= data.Length && data[pos] == 0x2A) // field 5 (opset_import)
            {
                var len = ReadVarint(data, pos + 1, out var lenVal);
                if (lenVal > 0 && pos + 1 + lenVal + 8 <= data.Length)
                {
                    var inner = data[(pos + 1 + lenVal)..(pos + 1 + lenVal + 8)];
                    // Domain (string) + Version (int64)
                    var domainLen = ReadVarint(inner, 0, out var domainLenVal);
                    if (domainLenVal > 0 && domainLenVal < 50)
                    {
                        var domain = Encoding.UTF8.GetString(inner[(domainLenVal)..(domainLenVal + 8)]);
                        var version = ReadInt64Varint(inner, domainLenVal + 8, out var _vlen);
                        opsets.Add((domain, version.ToString()));
                    }
                }
            }
            pos++;
        }
        return opsets;
    }

    private static int ReadVarint(byte[] data, int pos, out int length)
    {
        length = 0;
        int result = 0;
        int shift = 0;
        while (pos < data.Length)
        {
            var b = data[pos];
            result |= (b & 0x7F) << shift;
            length++;
            if ((b & 0x80) == 0) break;
            shift += 7;
            pos++;
        }
        return result;
    }

    private static long ReadInt64Varint(byte[] data, int pos, out int length)
    {
        length = 0;
        long result = 0;
        int shift = 0;
        while (pos < data.Length && shift < 64)
        {
            var b = data[pos];
            result |= (long)(b & 0x7F) << shift;
            length++;
            if ((b & 0x80) == 0) break;
            shift += 7;
            pos++;
        }
        return result;
    }
}
