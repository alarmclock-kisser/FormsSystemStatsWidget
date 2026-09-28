using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using FormsSystemStatsWidget.Core;

namespace FormsSystemStatsWidget.Core;

/// <summary>
/// Configuration options for loop detection and interjection.
/// </summary>
public class LoopDetectionConfig
{
    /// <summary>
    /// Enable/disable loop detection.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Minimum number of consecutive identical actions before considering a loop.
    /// </summary>
    public int TriggerAfter { get; set; } = 3;

    /// <summary>
    /// Number of recent turns to consider for similarity detection.
    /// </summary>
    public int DetectionWindow { get; set; } = 5;

    /// <summary>
    /// Similarity threshold (0.0-1.0) for considering two actions as identical.
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.9;

    /// <summary>
    /// Enable/disable insertion of interjection message.
    /// </summary>
    public bool InterjectionEnabled { get; set; } = true;

    /// <summary>
    /// Message to insert when a loop is detected.
    /// </summary>
    public string InterjectionMessage { get; set; } = 
        "You appear to be repeating the same actions or tool calls. Stop looping, review the current tool results and continue with a different action or provide the final answer if the task is complete.";

    /// <summary>
    /// Maximum number of interjections allowed per request/agent turn.
    /// </summary>
    public int MaxInterjections { get; set; } = 2;

    /// <summary>
    /// Enable/disable hard abort when interjection is ignored and loop persists.
    /// </summary>
    public bool AbortEnabled { get; set; } = false;

    /// <summary>
    /// Abort after this many consecutive interjections without resolution.
    /// </summary>
    public int AbortAfterInterjections { get; set; } = 3;
}

/// <summary>
/// Service for detecting repetitive tool calls or assistant outputs (loops) and optionally inserting interjection messages.
/// </summary>
public class LoopDetectionService
{
    private const string InterjectionName = "loop_detection_interjection";
    private LoopDetectionConfig _config;

    public LoopDetectionService(LoopDetectionConfig config)
    {
        _config = config;
    }

    public LoopDetectionConfig GetConfig() => _config;

    public void SetConfig(LoopDetectionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    /// <summary>
    /// Detects if the latest tool calls or assistant outputs form a loop.
    /// </summary>
    /// <param name="messages">The JSON array of messages to analyze.</param>
    /// <returns>A result containing loop detection info and optionally the interjection message.</returns>
    public LoopDetectionResult DetectLoop(JsonArray messages)
    {
        if (messages.Count == 0 || !_config.Enabled)
        {
            return new LoopDetectionResult();
        }

        List<AssistantAction> recentActions = GetRecentAssistantActions(messages, Math.Max(1, _config.DetectionWindow));
        int repeats = CountTrailingRepeats(recentActions, _config.SimilarityThreshold);
        int triggerAfter = Math.Max(2, _config.TriggerAfter);
        int interjectionCount = CountCurrentTurnInterjections(messages);
        bool detected = repeats >= triggerAfter;
        var result = new LoopDetectionResult
        {
            IsLoopDetected = detected,
            RepeatCount = repeats,
            InterjectionCount = interjectionCount
        };

        if (!detected)
        {
            return result;
        }

        if (_config.AbortEnabled && interjectionCount >= Math.Max(1, _config.AbortAfterInterjections))
        {
            result.ShouldAbort = true;
            return result;
        }

        if (_config.InterjectionEnabled && interjectionCount < Math.Max(1, _config.MaxInterjections))
        {
            result.InterjectionMessage = _config.InterjectionMessage;
        }

        return result;
    }

    /// <summary>Applies a detected interjection to the conversation for the next upstream generation.</summary>
    public LoopDetectionResult ApplyToConversation(JsonArray messages)
    {
        LoopDetectionResult result = DetectLoop(messages);
        if (result.InterjectionMessage is not null)
        {
            messages.Add(new JsonObject
            {
                ["role"] = "user",
                ["name"] = InterjectionName,
                ["content"] = result.InterjectionMessage
            });
            result.InterjectionCount++;
        }

        return result;
    }

    private static int CountCurrentTurnInterjections(JsonArray messages)
    {
        int count = 0;
        for (int index = messages.Count - 1; index >= 0; index--)
        {
            if (messages[index] is not JsonObject message)
            {
                continue;
            }
            string role = message["role"]?.ToString() ?? string.Empty;
            if (!string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (string.Equals(message["name"]?.ToString(), InterjectionName, StringComparison.Ordinal))
            {
                count++;
            }
            else
            {
                break;
            }
        }
        return count;
    }

    private static List<AssistantAction> GetRecentAssistantActions(JsonArray messages, int detectionWindow)
    {
        var actions = new List<AssistantAction>();
        for (int index = messages.Count - 1; index >= 0 && actions.Count < detectionWindow; index--)
        {
            if (messages[index] is not JsonObject message)
            {
                continue;
            }

            string role = message["role"]?.ToString() ?? string.Empty;
            if (string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(message["name"]?.ToString(), InterjectionName, StringComparison.Ordinal))
                {
                    break;
                }
                continue;
            }
            if (!string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AssistantAction? action = CreateAction(message);
            if (action is not null)
            {
                actions.Add(action);
            }
        }

        actions.Reverse();
        return actions;
    }

    private static AssistantAction? CreateAction(JsonObject message)
    {
        if (message["tool_calls"] is JsonArray toolCalls && toolCalls.Count > 0)
        {
            string[] signatures = toolCalls
                .OfType<JsonObject>()
                .Select(call =>
                {
                    JsonObject? function = call["function"] as JsonObject;
                    string name = function?["name"]?.ToString() ?? string.Empty;
                    string arguments = Canonicalize(function?["arguments"]);
                    return $"{name}:{arguments}";
                })
                .OrderBy(signature => signature, StringComparer.Ordinal)
                .ToArray();
            return signatures.Length == 0 ? null : new AssistantAction(true, string.Join("|", signatures));
        }

        string content = message["content"]?.ToString()?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(content) ? null : new AssistantAction(false, NormalizeText(content));
    }

    private static string Canonicalize(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            return "{" + string.Join(",", obj.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{JsonSerializer.Serialize(pair.Key)}:{Canonicalize(pair.Value)}")) + "}";
        }
        if (node is JsonArray array)
        {
            return "[" + string.Join(",", array.Select(Canonicalize)) + "]";
        }
        return node?.ToJsonString() ?? "null";
    }

    private static string NormalizeText(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    private static int CountTrailingRepeats(List<AssistantAction> actions, double threshold)
    {
        if (actions.Count == 0)
        {
            return 0;
        }

        AssistantAction latest = actions[^1];
        int count = 1;
        for (int index = actions.Count - 2; index >= 0; index--)
        {
            AssistantAction previous = actions[index];
            if (previous.IsToolCall != latest.IsToolCall)
            {
                break;
            }
            if (latest.IsToolCall)
            {
                if (!string.Equals(previous.Signature, latest.Signature, StringComparison.Ordinal))
                {
                    break;
                }
            }
            else if (!AreTextSimilar(previous.Signature, latest.Signature, threshold))
            {
                break;
            }
            count++;
        }
        return count;
    }

    private static bool AreTextSimilar(string left, string right, double threshold)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return true;
        }
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            return false;
        }

        HashSet<string> leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        HashSet<string> rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        int intersection = leftTokens.Intersect(rightTokens, StringComparer.Ordinal).Count();
        int union = leftTokens.Union(rightTokens, StringComparer.Ordinal).Count();
        return union > 0 && intersection / (double)union >= threshold;
    }

    private sealed record AssistantAction(bool IsToolCall, string Signature);
}

/// <summary>
/// Classifies the kind of repeating pattern observed in a stream.
/// </summary>
public enum StreamingLoopKind
{
    TextOutput,
    ToolCall
}

/// <summary>
/// Decision produced by <see cref="StreamingLoopDetector"/> for a single observed chunk.
/// </summary>
public sealed class StreamingLoopDecision
{
    public bool ShouldAbort { get; set; }
    public string? InterjectionMessage { get; set; }
    public int RepeatCount { get; set; }
    public StreamingLoopKind LoopKind { get; set; }
}

/// <summary>
/// Detects repeating natural-language output patterns in a stream.
/// Tool-call deltas are fragments; repeated complete tool actions are handled by
/// <see cref="LoopDetectionService"/> across conversation turns.
/// </summary>
public class StreamingLoopDetector
{
    private readonly LoopDetectionConfig _config;
    private readonly Queue<string> _contentSignatures = new();
    private int _contentRepeatCount;
    private int _interjectionCount;
    private bool _hasSeenToolCallInCurrentStream;

    public StreamingLoopDetector(LoopDetectionConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Observes a single upstream chunk. Returns a decision when a loop is detected,
    /// or null when no loop has been reached yet.
    /// </summary>
    public StreamingLoopDecision? ObserveChunk(JsonNode? chunk)
    {
        if (!_config.Enabled || chunk is not JsonObject)
        {
            return null;
        }

        if (chunk["choices"] is not JsonArray choices)
        {
            return null;
        }

        foreach (JsonNode? choice in choices)
        {
            if (choice is not JsonObject choiceObj)
            {
                continue;
            }
            if (choiceObj["delta"] is not JsonObject delta)
            {
                continue;
            }

            if (delta["tool_calls"] is JsonArray toolCalls && toolCalls.Count > 0)
            {
                _hasSeenToolCallInCurrentStream = true;
                continue;
            }

            if (_hasSeenToolCallInCurrentStream)
            {
                continue;
            }

            string? content = delta["content"]?.ToString();
            if (!string.IsNullOrWhiteSpace(content) && delta["reasoning_content"] == null)
            {
                string signature = NormalizeText(content);
                if (!string.IsNullOrEmpty(signature))
                {
                    ObserveSignature(signature, out StreamingLoopDecision? decision);
                    if (decision is not null)
                    {
                        return decision;
                    }
                }
            }
        }

        return null;
    }

    private void ObserveSignature(string signature, out StreamingLoopDecision? decision)
    {
        decision = null;
        if (_contentSignatures.Count > 0 && _contentSignatures.Peek() == signature)
        {
            _contentRepeatCount++;
        }
        else
        {
            _contentRepeatCount = 1;
        }

        if (_contentSignatures.Count == Math.Max(1, _config.DetectionWindow))
        {
            _ = _contentSignatures.Dequeue();
        }
        _contentSignatures.Enqueue(signature);

        int triggerAfter = Math.Max(2, _config.TriggerAfter);
        if (_contentRepeatCount < triggerAfter)
        {
            return;
        }

        if (_config.AbortEnabled && _interjectionCount >= Math.Max(1, _config.AbortAfterInterjections))
        {
            decision = new StreamingLoopDecision
            {
                ShouldAbort = true,
                RepeatCount = _contentRepeatCount,
                LoopKind = StreamingLoopKind.TextOutput
            };
            return;
        }

        if (_config.InterjectionEnabled && _interjectionCount < Math.Max(1, _config.MaxInterjections))
        {
            _interjectionCount++;
            decision = new StreamingLoopDecision
            {
                InterjectionMessage = _config.InterjectionMessage,
                RepeatCount = _contentRepeatCount,
                LoopKind = StreamingLoopKind.TextOutput
            };
        }
    }

    private static string NormalizeText(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}

/// <summary>
/// Result of loop detection operation.
/// </summary>
public class LoopDetectionResult
{
    /// <summary>
    /// Indicates whether a loop was detected.
    /// </summary>
    public bool IsLoopDetected { get; set; }

    /// <summary>
    /// The interjection message to insert if a loop is detected.
    /// </summary>
    public string? InterjectionMessage { get; set; }

    /// <summary>
    /// Number of interjections already inserted in this request.
    /// </summary>
    public int InterjectionCount { get; set; }

    /// <summary>
    /// Indicates whether the system should abort further processing.
    /// </summary>
    public bool ShouldAbort { get; set; }

    public int RepeatCount { get; set; }

    /// <summary>
    /// Timestamp of detection (UTC).
    /// </summary>
    public DateTime DetectionTime { get; set; } = DateTime.UtcNow;
}