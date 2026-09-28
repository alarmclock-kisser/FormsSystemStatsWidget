using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FormsSystemStatsWidget.Core;

public sealed class LlamaChatTemplateProfile
{
    private readonly JsonObject _capabilities;

    private LlamaChatTemplateProfile(string template, JsonObject capabilities)
    {
        Template = template;
        _capabilities = capabilities;
    }

    public string Template { get; }
    public int TemplateLength => Template.Length;
    public bool? SupportsReasoningEffort => ReadCapability("supports_reasoning_effort");
    public bool? SupportsParallelToolCalls => ReadCapability("supports_parallel_tool_calls");
    public bool? SupportsTools => ReadCapability("supports_tools");
    public bool? SupportsToolCalls => ReadCapability("supports_tool_calls");

    public static LlamaChatTemplateProfile? FromProps(JsonNode? props)
    {
        string template = props?["chat_template"]?.ToString() ?? string.Empty;
        JsonObject capabilities = props?["chat_template_caps"] is JsonObject caps
            ? caps.DeepClone().AsObject()
            : new JsonObject();

        return template.Length == 0 && capabilities.Count == 0
            ? null
            : new LlamaChatTemplateProfile(template, capabilities);
    }

    public bool ApplyCompatibility(JsonObject request)
    {
        bool changed = false;
        string? reasoningEffort = request["reasoning_effort"]?.ToString();
        if (SupportsReasoningEffort == false && reasoningEffort is not null)
        {
            _ = request.Remove("reasoning_effort");
            changed = true;

            if (!string.IsNullOrWhiteSpace(reasoningEffort) &&
                Template.Contains("enable_thinking", System.StringComparison.Ordinal) &&
                request["chat_template_kwargs"] is null)
            {
                request["chat_template_kwargs"] = new JsonObject();
            }

            if (!string.IsNullOrWhiteSpace(reasoningEffort) &&
                Template.Contains("enable_thinking", System.StringComparison.Ordinal) &&
                request["chat_template_kwargs"] is JsonObject kwargs &&
                kwargs["enable_thinking"] is null)
            {
                kwargs["enable_thinking"] = !IsDisabledReasoningEffort(reasoningEffort);
            }
        }

        if (request["tools"] is JsonArray tools && tools.Count > 0 &&
            (SupportsTools == false || SupportsToolCalls == false))
        {
            changed |= ConvertToolsToLegacyPrompt(request, tools);
        }

        if (SupportsParallelToolCalls == false &&
            bool.TryParse(request["parallel_tool_calls"]?.ToString(), out bool parallelToolCalls) &&
            parallelToolCalls)
        {
            request["parallel_tool_calls"] = false;
            changed = true;
        }

        return changed;
    }

    public static IReadOnlyList<string> BuildCompatibilityRetryCandidates(string requestBody)
    {
        JsonObject original = JsonNode.Parse(requestBody) as JsonObject
            ?? throw new JsonException("Request body must be a JSON object.");
        var candidates = new List<string>();
        var candidate = original.DeepClone().AsObject();
        if (RemoveFields(candidate, "reasoning_effort", "reasoning_budget"))
        {
            candidates.Add(candidate.ToJsonString());
        }

        if (RemoveFields(candidate, "chat_template_kwargs"))
        {
            candidates.Add(candidate.ToJsonString());
        }

        if (RemoveFields(candidate, "tool_choice", "parallel_tool_calls"))
        {
            candidates.Add(candidate.ToJsonString());
        }

        return candidates.Distinct(System.StringComparer.Ordinal).ToArray();
    }

    private static bool ConvertToolsToLegacyPrompt(JsonObject request, JsonArray tools)
    {
        if (request["messages"] is not JsonArray messages)
        {
            return false;
        }

        const string marker = "[Bridge legacy tool definitions]";
        string toolPrompt = $"{marker}\nThis chat template does not support native function calls. Available tools are listed as JSON below. When a tool is needed, emit one JSON object exactly shaped as {{\"name\":\"tool_name\",\"arguments\":{{...}}}}. Use only a declared tool name, valid JSON arguments, and no Markdown fence.\n{tools.ToJsonString()}";
        JsonObject? systemMessage = messages
            .OfType<JsonObject>()
            .FirstOrDefault(message => string.Equals(message["role"]?.ToString(), "system", System.StringComparison.OrdinalIgnoreCase));

        if (systemMessage is null)
        {
            messages.Insert(0, new JsonObject { ["role"] = "system", ["content"] = toolPrompt });
        }
        else if (systemMessage["content"] is JsonValue systemContent && systemContent.TryGetValue<string>(out string? existingContent))
        {
            string currentContent = existingContent ?? string.Empty;
            if (!currentContent.Contains(marker, System.StringComparison.Ordinal))
            {
                systemMessage["content"] = string.IsNullOrWhiteSpace(currentContent)
                    ? toolPrompt
                    : string.Concat(currentContent, "\n\n", toolPrompt);
            }
        }
        else
        {
            int systemIndex = messages.IndexOf(systemMessage);
            messages.Insert(systemIndex + 1, new JsonObject { ["role"] = "system", ["content"] = toolPrompt });
        }

        _ = request.Remove("tools");
        _ = request.Remove("tool_choice");
        _ = request.Remove("parallel_tool_calls");
        return true;
    }

    private bool? ReadCapability(string name)
    {
        return bool.TryParse(_capabilities[name]?.ToString(), out bool value) ? value : null;
    }

    private static bool IsDisabledReasoningEffort(string effort)
    {
        return effort.Equals("none", System.StringComparison.OrdinalIgnoreCase) ||
               effort.Equals("off", System.StringComparison.OrdinalIgnoreCase) ||
               effort.Equals("disabled", System.StringComparison.OrdinalIgnoreCase) ||
               effort.Equals("false", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool RemoveFields(JsonObject value, params string[] fields)
    {
        bool changed = false;
        foreach (string field in fields)
        {
            changed |= value.Remove(field);
        }
        return changed;
    }
}
