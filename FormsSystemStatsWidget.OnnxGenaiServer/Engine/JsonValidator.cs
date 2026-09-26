using System.Text.Json;
using FormsSystemStatsWidget.OnnxGenaiServer.Engine;

namespace FormsSystemStatsWidget.OnnxGenaiServer.Engine;

/// <summary>
/// JSON-Validierung für Modell-Metadaten (R24).
/// Prüft config.json, tokenizer.json, generation_config.json, tokenizer_config.json,
/// special_tokens_map.json auf Malformed-JSON, fehlende Felder, Inkompatibilitäten.
/// </summary>
public static class JsonValidator
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Validiert alle JSON-Dateien eines Modell-Rootdirs und gibt ein ValidationResult zurück.
    /// </summary>
    public static ValidationResult ValidateAll(string modelDirectory)
    {
        var issues = new List<ValidationIssue>();

        // config.json
        var configPath = Path.Combine(modelDirectory, "config.json");
        if (File.Exists(configPath))
        {
            var result = ValidateConfigJson(configPath);
            issues.AddRange(result);
        }
        else
        {
            issues.Add(new ValidationIssue
            {
                Category = "JSON",
                Message = "config.json nicht gefunden",
                Severity = ValidationSeverity.Error,
                FileName = "config.json"
            });
        }

        // tokenizer.json
        var tokenizerPath = Path.Combine(modelDirectory, "tokenizer.json");
        if (File.Exists(tokenizerPath))
        {
            var result = ValidateTokenizerJson(tokenizerPath);
            issues.AddRange(result);
        }
        else
        {
            issues.Add(new ValidationIssue
            {
                Category = "JSON",
                Message = "tokenizer.json nicht gefunden",
                Severity = ValidationSeverity.Error,
                FileName = "tokenizer.json"
            });
        }

        // generation_config.json
        var genConfigPath = Path.Combine(modelDirectory, "generation_config.json");
        if (File.Exists(genConfigPath))
        {
            var result = ValidateGenerationConfigJson(genConfigPath);
            issues.AddRange(result);
        }

        // tokenizer_config.json
        var tokenizerConfigPath = Path.Combine(modelDirectory, "tokenizer_config.json");
        if (File.Exists(tokenizerConfigPath))
        {
            var result = ValidateTokenizerConfigJson(tokenizerConfigPath);
            issues.AddRange(result);
        }

        // special_tokens_map.json
        var specialTokensPath = Path.Combine(modelDirectory, "special_tokens_map.json");
        if (File.Exists(specialTokensPath))
        {
            var result = ValidateSpecialTokensJson(specialTokensPath);
            issues.AddRange(result);
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
    /// Validiert config.json: Architektur, hidden_size, vocab_size, layer_count, dtype, etc.
    /// </summary>
    private static List<ValidationIssue> ValidateConfigJson(string path)
    {
        var issues = new List<ValidationIssue>();

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Architektur
            if (!root.TryGetProperty("model_type", out var modelType))
            {
                issues.Add(new ValidationIssue
                {
                    Category = "JSON",
                    Message = "config.json: fehlendes Feld 'model_type'",
                    Severity = ValidationSeverity.Warning,
                    FileName = "config.json"
                });
            }

            // hidden_size
            if (!root.TryGetProperty("hidden_size", out var hiddenSize))
            {
                issues.Add(new ValidationIssue
                {
                    Category = "JSON",
                    Message = "config.json: fehlendes Feld 'hidden_size'",
                    Severity = ValidationSeverity.Warning,
                    FileName = "config.json"
                });
            }

            // vocab_size
            if (!root.TryGetProperty("vocab_size", out var vocabSize))
            {
                issues.Add(new ValidationIssue
                {
                    Category = "JSON",
                    Message = "config.json: fehlendes Feld 'vocab_size'",
                    Severity = ValidationSeverity.Warning,
                    FileName = "config.json"
                });
            }

            // num_hidden_layers
            if (!root.TryGetProperty("num_hidden_layers", out var numLayers))
            {
                issues.Add(new ValidationIssue
                {
                    Category = "JSON",
                    Message = "config.json: fehlendes Feld 'num_hidden_layers'",
                    Severity = ValidationSeverity.Warning,
                    FileName = "config.json"
                });
            }

            // num_attention_heads
            if (!root.TryGetProperty("num_attention_heads", out var numHeads))
            {
                issues.Add(new ValidationIssue
                {
                    Category = "JSON",
                    Message = "config.json: fehlendes Feld 'num_attention_heads'",
                    Severity = ValidationSeverity.Warning,
                    FileName = "config.json"
                });
            }

            // num_key_value_heads
            if (!root.TryGetProperty("num_key_value_heads", out var numKvHeads))
            {
                issues.Add(new ValidationIssue
                {
                    Category = "JSON",
                    Message = "config.json: fehlendes Feld 'num_key_value_heads'",
                    Severity = ValidationSeverity.Warning,
                    FileName = "config.json"
                });
            }

            // rms_norm_eps
            if (!root.TryGetProperty("rms_norm_eps", out var rmsEps))
            {
                issues.Add(new ValidationIssue
                {
                    Category = "JSON",
                    Message = "config.json: fehlendes Feld 'rms_norm_eps'",
                    Severity = ValidationSeverity.Warning,
                    FileName = "config.json"
                });
            }

            // max_position_embeddings
            if (!root.TryGetProperty("max_position_embeddings", out var maxPos))
            {
                issues.Add(new ValidationIssue
                {
                    Category = "JSON",
                    Message = "config.json: fehlendes Feld 'max_position_embeddings'",
                    Severity = ValidationSeverity.Warning,
                    FileName = "config.json"
                });
            }

            // dtype / hidden_size check
            if (hiddenSize.TryGetProperty("dtype", out var dtype) && dtype.GetString() is not null)
            {
                // dtype als String (z.B. "float16")
            }

            // Inkompatibilitäten prüfen
            if (hiddenSize.TryGetProperty("hidden_size", out var hs) && hs.GetInt32() > 0)
            {
                var hsVal = hs.GetInt32();
                if (numHeads.TryGetProperty("num_attention_heads", out var nh) && nh.GetInt32() > 0)
                {
                    var nhVal = nh.GetInt32();
                    if (numKvHeads.TryGetProperty("num_key_value_heads", out var nkv) && nkv.GetInt32() > 0)
                    {
                        var nkvVal = nkv.GetInt32();
                        // Qwen: num_key_value_heads muss hidden_size / (num_attention_heads / num_key_value_heads) sein
                        // oder: hidden_size muss durch num_attention_heads teilbar sein
                        if (hsVal % nhVal != 0)
                        {
                            issues.Add(new ValidationIssue
                            {
                                Category = "JSON",
                                Message = $"config.json: hidden_size ({hsVal}) nicht durch num_attention_heads ({nhVal}) teilbar",
                                Severity = ValidationSeverity.Error,
                                FileName = "config.json"
                            });
                        }
                        // num_key_value_heads muss <= num_attention_heads sein
                        if (nkvVal > nhVal)
                        {
                            issues.Add(new ValidationIssue
                            {
                                Category = "JSON",
                                Message = $"config.json: num_key_value_heads ({nkvVal}) > num_attention_heads ({nhVal})",
                                Severity = ValidationSeverity.Error,
                                FileName = "config.json"
                            });
                        }
                    }
                }
            }

            // KV-Dimension prüfen (Qwen: 4 heads * 256 dim = 1024 hidden_size für Qwen3.8-27B)
            if (hiddenSize.TryGetProperty("hidden_size", out var hs2) && numHeads.TryGetProperty("num_attention_heads", out var nh2) &&
                numKvHeads.TryGetProperty("num_key_value_heads", out var nkv2))
            {
                var kvDim = hs2.GetInt32() / nh2.GetInt32();
                if (kvDim <= 0)
                {
                    issues.Add(new ValidationIssue
                    {
                        Category = "JSON",
                        Message = $"config.json: berechnete KV-Dimension ({kvDim}) <= 0",
                        Severity = ValidationSeverity.Error,
                        FileName = "config.json"
                    });
                }
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue
            {
                Category = "JSON",
                Message = $"config.json: Malformed JSON — {ex.Message}",
                Severity = ValidationSeverity.Error,
                FileName = "config.json"
            });
        }

        return issues;
    }

    /// <summary>
    /// Validiert tokenizer.json: BPE-Vokabular, merges, special_tokens.
    /// </summary>
    private static List<ValidationIssue> ValidateTokenizerJson(string path)
    {
        var issues = new List<ValidationIssue>();

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // tokenizer_type
            if (!root.TryGetProperty("tokenizer_type", out var tokType))
            {
                issues.Add(new ValidationIssue
                {
                    Category = "JSON",
                    Message = "tokenizer.json: fehlendes Feld 'tokenizer_type'",
                    Severity = ValidationSeverity.Warning,
                    FileName = "tokenizer.json"
                });
            }

            // vocab
            if (!root.TryGetProperty("vocab", out var vocab))
            {
                issues.Add(new ValidationIssue
                {
                    Category = "JSON",
                    Message = "tokenizer.json: fehlendes Feld 'vocab'",
                    Severity = ValidationSeverity.Error,
                    FileName = "tokenizer.json"
                });
            }
            else if (vocab.ValueKind == JsonValueKind.Object)
            {
                var vocabSize = vocab.GetArrayLength();
                if (vocabSize == 0)
                {
                    issues.Add(new ValidationIssue
                    {
                        Category = "JSON",
                        Message = "tokenizer.json: 'vocab' ist leer",
                        Severity = ValidationSeverity.Error,
                        FileName = "tokenizer.json"
                    });
                }
            }

            // merges
            if (!root.TryGetProperty("merges", out var merges))
            {
                issues.Add(new ValidationIssue
                {
                    Category = "JSON",
                    Message = "tokenizer.json: fehlendes Feld 'merges'",
                    Severity = ValidationSeverity.Warning,
                    FileName = "tokenizer.json"
                });
            }

            // added_tokens
            if (root.TryGetProperty("added_tokens", out var addedTokens) && addedTokens.ValueKind == JsonValueKind.Array)
            {
                var addedCount = addedTokens.GetArrayLength();
                if (addedCount > 0)
                {
                    issues.Add(new ValidationIssue
                    {
                        Category = "JSON",
                        Message = $"tokenizer.json: {addedCount} added_tokens gefunden (muss mit config.json vocab_size konsistent sein)",
                        Severity = ValidationSeverity.Warning,
                        FileName = "tokenizer.json"
                    });
                }
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue
            {
                Category = "JSON",
                Message = $"tokenizer.json: Malformed JSON — {ex.Message}",
                Severity = ValidationSeverity.Error,
                FileName = "tokenizer.json"
            });
        }

        return issues;
    }

    /// <summary>
    /// Validiert generation_config.json: max_tokens, temperature, top_p, etc.
    /// </summary>
    private static List<ValidationIssue> ValidateGenerationConfigJson(string path)
    {
        var issues = new List<ValidationIssue>();

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // max_new_tokens
            if (!root.TryGetProperty("max_new_tokens", out var maxNew))
            {
                issues.Add(new ValidationIssue
                {
                    Category = "JSON",
                    Message = "generation_config.json: fehlendes Feld 'max_new_tokens'",
                    Severity = ValidationSeverity.Warning,
                    FileName = "generation_config.json"
                });
            }

            // temperature
            if (root.TryGetProperty("temperature", out var temp))
            {
                if (temp.GetDouble() < 0)
                {
                    issues.Add(new ValidationIssue
                    {
                        Category = "JSON",
                        Message = "generation_config.json: temperature < 0",
                        Severity = ValidationSeverity.Error,
                        FileName = "generation_config.json"
                    });
                }
            }

            // top_p
            if (root.TryGetProperty("top_p", out var topP))
            {
                var tp = topP.GetDouble();
                if (tp < 0 || tp > 1)
                {
                    issues.Add(new ValidationIssue
                    {
                        Category = "JSON",
                        Message = $"generation_config.json: top_p ({tp}) außerhalb [0, 1]",
                        Severity = ValidationSeverity.Error,
                        FileName = "generation_config.json"
                    });
                }
            }

            // top_k
            if (root.TryGetProperty("top_k", out var topK))
            {
                var tk = topK.GetInt32();
                if (tk < 0)
                {
                    issues.Add(new ValidationIssue
                    {
                        Category = "JSON",
                        Message = "generation_config.json: top_k < 0",
                        Severity = ValidationSeverity.Error,
                        FileName = "generation_config.json"
                    });
                }
            }

            // eos_token_id
            if (!root.TryGetProperty("eos_token_id", out var eos))
            {
                issues.Add(new ValidationIssue
                {
                    Category = "JSON",
                    Message = "generation_config.json: fehlendes Feld 'eos_token_id'",
                    Severity = ValidationSeverity.Warning,
                    FileName = "generation_config.json"
                });
            }

            // pad_token_id
            if (!root.TryGetProperty("pad_token_id", out var pad))
            {
                issues.Add(new ValidationIssue
                {
                    Category = "JSON",
                    Message = "generation_config.json: fehlendes Feld 'pad_token_id'",
                    Severity = ValidationSeverity.Warning,
                    FileName = "generation_config.json"
                });
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue
            {
                Category = "JSON",
                Message = $"generation_config.json: Malformed JSON — {ex.Message}",
                Severity = ValidationSeverity.Error,
                FileName = "generation_config.json"
            });
        }

        return issues;
    }

    /// <summary>
    /// Validiert tokenizer_config.json: model_type, tokenizer_class, etc.
    /// </summary>
    private static List<ValidationIssue> ValidateTokenizerConfigJson(string path)
    {
        var issues = new List<ValidationIssue>();

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // tokenizer_class
            if (!root.TryGetProperty("tokenizer_class", out var tokClass))
            {
                issues.Add(new ValidationIssue
                {
                    Category = "JSON",
                    Message = "tokenizer_config.json: fehlendes Feld 'tokenizer_class'",
                    Severity = ValidationSeverity.Warning,
                    FileName = "tokenizer_config.json"
                });
            }

            // model_max_length
            if (!root.TryGetProperty("model_max_length", out var maxLen))
            {
                issues.Add(new ValidationIssue
                {
                    Category = "JSON",
                    Message = "tokenizer_config.json: fehlendes Feld 'model_max_length'",
                    Severity = ValidationSeverity.Warning,
                    FileName = "tokenizer_config.json"
                });
            }

            // chat_template
            if (root.TryGetProperty("chat_template", out var chatTemplate))
            {
                if (chatTemplate.ValueKind == JsonValueKind.String)
                {
                    var template = chatTemplate.GetString();
                    if (string.IsNullOrWhiteSpace(template))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Category = "JSON",
                            Message = "tokenizer_config.json: 'chat_template' ist leer",
                            Severity = ValidationSeverity.Warning,
                            FileName = "tokenizer_config.json"
                        });
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue
            {
                Category = "JSON",
                Message = $"tokenizer_config.json: Malformed JSON — {ex.Message}",
                Severity = ValidationSeverity.Error,
                FileName = "tokenizer_config.json"
            });
        }

        return issues;
    }

    /// <summary>
    /// Validiert special_tokens_map.json: bos_token, eos_token, pad_token, unk_token.
    /// </summary>
    private static List<ValidationIssue> ValidateSpecialTokensJson(string path)
    {
        var issues = new List<ValidationIssue>();

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var requiredTokens = new[] { "bos_token", "eos_token", "pad_token", "unk_token" };
            foreach (var token in requiredTokens)
            {
                if (!root.TryGetProperty(token, out var val))
                {
                    issues.Add(new ValidationIssue
                    {
                        Category = "JSON",
                        Message = $"special_tokens_map.json: fehlendes Feld '{token}'",
                        Severity = ValidationSeverity.Warning,
                        FileName = "special_tokens_map.json"
                    });
                }
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue
            {
                Category = "JSON",
                Message = $"special_tokens_map.json: Malformed JSON — {ex.Message}",
                Severity = ValidationSeverity.Error,
                FileName = "special_tokens_map.json"
            });
        }

        return issues;
    }
}
