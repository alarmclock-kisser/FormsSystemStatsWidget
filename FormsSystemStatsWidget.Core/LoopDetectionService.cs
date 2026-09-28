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
    public bool InterjectionEnabled { get; set; } = false;

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
    private LoopDetectionConfig _config;
    private readonly object _gate = new();
    private readonly Dictionary<string, List<string>> _recentToolCalls = new();
    private readonly Dictionary<string, int> _callCounts = new();
    private readonly List<string> _interjectionHistory = new();

    public LoopDetectionService(LoopDetectionConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Detects if the latest tool calls or assistant outputs form a loop.
    /// </summary>
    /// <param name="messages">The JSON array of messages to analyze.</param>
    /// <returns>A result containing loop detection info and optionally the interjection message.</returns>
    public LoopDetectionResult DetectLoop(JsonArray messages)
    {
        if (messages == null || messages.Count == 0)
        {
            return new LoopDetectionResult();
        }

        // Get the current configuration
        var config = GetConfig();

        // Loop checks are diagnostic only; interjections and aborts are intentionally not applied.
        if (!config.Enabled)
        {
            return new LoopDetectionResult();
        }

        // Convert messages to string representations for comparison
        var messageStrings = new List<string>(messages.Count);
        foreach (JsonNode? message in messages)
        {
            messageStrings.Add(message?.ToJsonString() ?? "<null>");
        }

        // Check if we have enough history to detect a loop
        int windowSize = Math.Min(config.DetectionWindow, messages.Count);
        if (windowSize < config.TriggerAfter)
        {
            return new LoopDetectionResult();
        }

        // Compare the last N messages to detect similarity
        var recentMessages = messageStrings[^windowSize..];
        string currentMessage = recentMessages[^1];

        // Count how many times this pattern has occurred consecutively
        int consecutiveCount = 1;
        for (int i = recentMessages.Count - 2; i >= 0; i--)
        {
            if (!IsSimilar(recentMessages[i], currentMessage, config.SimilarityThreshold))
            {
                break;
            }
            consecutiveCount++;
        }

        return consecutiveCount >= config.TriggerAfter
            ? new LoopDetectionResult { IsLoopDetected = true }
            : new LoopDetectionResult();
    }

    /// <summary>
    /// Checks if two messages are similar based on a similarity threshold.
    /// </summary>
    /// <param name="message1">First message to compare.</param>
    /// <param name="message2">Second message to compare.</param>
    /// <param name="threshold">Similarity threshold between 0.0 and 1.0.</param>
    /// <returns>True if messages are similar.</returns>
    private bool IsSimilar(string message1, string message2, double threshold)
    {
        if (string.IsNullOrEmpty(message1) || string.IsNullOrEmpty(message2))
        {
            return string.IsNullOrEmpty(message1) && string.IsNullOrEmpty(message2);
        }

        // Simple string similarity check - could be enhanced with more sophisticated methods
        double lengthSimilarity = 1.0 - Math.Abs(message1.Length - message2.Length) / (double)Math.Max(message1.Length, message2.Length);

        // Character similarity (basic)
        int minLength = Math.Min(message1.Length, message2.Length);
        int matchCount = 0;
        for (int i = 0; i < minLength; i++)
        {
            if (message1[i] == message2[i])
            {
                matchCount++;
            }
        }

        double charSimilarity = (double)matchCount / minLength;

        // Combine similarities (weighted average)
        double similarity = (lengthSimilarity + charSimilarity) / 2.0;

        return similarity >= threshold;
    }

    /// <summary>
    /// Checks if a specific tool call pattern constitutes a loop based on recent history.
    /// </summary>
    /// <param name="toolCallId">The ID of the tool call to check.</param>
    /// <param name="toolName">The name of the tool being called.</param>
    /// <returns>True if a loop is detected.</returns>
    public bool IsLoopDetected(string toolCallId, string toolName)
    {
        // Implementation would go here - for now placeholder
        return false;
    }

    /// <summary>
    /// Retrieves the current configuration.
    /// </summary>
    /// <returns>The configured <see cref="LoopDetectionConfig"/>.</returns>
    public LoopDetectionConfig GetConfig()
    {
        return _config;
    }

    /// <summary>
    /// Replaces the current configuration with the provided one.
    /// </summary>
    public void SetConfig(LoopDetectionConfig config)
    {
        _config = config;
    }
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

    /// <summary>
    /// Timestamp of detection (UTC).
    /// </summary>
    public DateTime DetectionTime { get; set; } = DateTime.UtcNow;
}