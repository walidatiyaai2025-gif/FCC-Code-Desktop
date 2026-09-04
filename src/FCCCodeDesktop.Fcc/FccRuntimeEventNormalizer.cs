using System.Text.Json;
using System.Text.RegularExpressions;
using FCCCodeDesktop.Runtime;

namespace FCCCodeDesktop.Fcc;

internal static class FccRuntimeEventNormalizer
{
    private static readonly Regex SensitiveTextAssignment = new(
        @"(?im)\b(api[_-]?key|token|password|authorization|secret|credential)\b(\s*[:=]\s*)([^\r\n,;]+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public static List<NormalizedRuntimeEvent> Normalize(
        JsonElement root,
        string sourceType,
        string? frameSessionId,
        string? frameCorrelationId,
        int maximumTextCharacters)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumTextCharacters, 1);

        var events = new List<NormalizedRuntimeEvent>();

        if (IsSource(sourceType, "system/init") && !string.IsNullOrWhiteSpace(frameSessionId))
        {
            events.Add(
                new NormalizedRuntimeEvent(
                    AgentRuntimeEventKind.SessionIdentified,
                    null,
                    frameCorrelationId,
                    sourceType));
            return events;
        }

        if (IsSource(sourceType, "system/api_retry") || IsSource(sourceType, "retry"))
        {
            events.Add(
                new NormalizedRuntimeEvent(
                    AgentRuntimeEventKind.Retry,
                    ResolveText(root, maximumTextCharacters, "error", "message"),
                    frameCorrelationId,
                    sourceType));
            return events;
        }

        if (IsSource(sourceType, "assistant"))
        {
            NormalizeAssistantFrame(
                root,
                sourceType,
                frameCorrelationId,
                maximumTextCharacters,
                events);
            EnsureUnknownFallback(sourceType, frameCorrelationId, events);
            return events;
        }

        if (IsSource(sourceType, "user"))
        {
            NormalizeUserToolResults(
                root,
                sourceType,
                frameCorrelationId,
                maximumTextCharacters,
                events);
            EnsureUnknownFallback(sourceType, frameCorrelationId, events);
            return events;
        }

        if (IsSource(sourceType, "stream_event"))
        {
            NormalizeStreamEvent(
                root,
                sourceType,
                frameCorrelationId,
                maximumTextCharacters,
                events);
            EnsureUnknownFallback(sourceType, frameCorrelationId, events);
            return events;
        }

        if (IsSource(sourceType, "result") || IsSource(sourceType, "completion"))
        {
            AddUsageIfPresent(root, sourceType, frameCorrelationId, events);
            events.Add(
                new NormalizedRuntimeEvent(
                    AgentRuntimeEventKind.Completion,
                    ResolveText(root, maximumTextCharacters, "result", "text", "message"),
                    frameCorrelationId,
                    sourceType));
            return events;
        }

        if (IsSource(sourceType, "error") || IsSource(sourceType, "system/error"))
        {
            events.Add(
                new NormalizedRuntimeEvent(
                    AgentRuntimeEventKind.Error,
                    ResolveText(root, maximumTextCharacters, "error", "message", "text"),
                    frameCorrelationId,
                    sourceType));
            return events;
        }

        if (IsSource(sourceType, "status")
            || IsSource(sourceType, "system/status")
            || IsSource(sourceType, "runtime/status"))
        {
            events.Add(
                new NormalizedRuntimeEvent(
                    AgentRuntimeEventKind.RuntimeStatus,
                    ResolveText(root, maximumTextCharacters, "status", "message", "text"),
                    frameCorrelationId,
                    sourceType));
            return events;
        }

        if (IsSource(sourceType, "usage"))
        {
            events.Add(
                new NormalizedRuntimeEvent(
                    AgentRuntimeEventKind.Usage,
                    null,
                    frameCorrelationId,
                    sourceType));
            return events;
        }

        if (IsSource(sourceType, "tool_use")
            || IsSource(sourceType, "tool/start")
            || IsSource(sourceType, "tool/started"))
        {
            events.Add(
                new NormalizedRuntimeEvent(
                    AgentRuntimeEventKind.ToolStarted,
                    ResolveText(root, maximumTextCharacters, "name", "tool_name"),
                    ResolveCorrelation(root, frameCorrelationId, "id", "tool_use_id"),
                    sourceType));
            return events;
        }

        if (IsSource(sourceType, "tool_progress") || IsSource(sourceType, "tool/progress"))
        {
            events.Add(
                new NormalizedRuntimeEvent(
                    AgentRuntimeEventKind.ToolProgress,
                    ResolveText(root, maximumTextCharacters, "partial_json", "progress", "text"),
                    ResolveCorrelation(root, frameCorrelationId, "tool_use_id", "id"),
                    sourceType));
            return events;
        }

        if (IsSource(sourceType, "tool_result") || IsSource(sourceType, "tool/result"))
        {
            events.Add(
                new NormalizedRuntimeEvent(
                    AgentRuntimeEventKind.ToolResult,
                    ResolveText(root, maximumTextCharacters, "content", "result", "text"),
                    ResolveCorrelation(root, frameCorrelationId, "tool_use_id", "id"),
                    sourceType));
            return events;
        }

        EnsureUnknownFallback(sourceType, frameCorrelationId, events);
        return events;
    }

    private static void NormalizeAssistantFrame(
        JsonElement root,
        string sourceType,
        string? frameCorrelationId,
        int maximumTextCharacters,
        List<NormalizedRuntimeEvent> events)
    {
        var message = TryGetObject(root, "message");
        var contentOwner = message ?? root;
        if (TryGetArray(contentOwner, "content") is { } content)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (block.ValueKind != JsonValueKind.Object)
                {
                    events.Add(Unknown(sourceType + "/content/unknown", frameCorrelationId));
                    continue;
                }

                var blockType = TryGetString(block, "type")?.Trim();
                if (string.Equals(blockType, "text", StringComparison.OrdinalIgnoreCase))
                {
                    var text = ResolveText(block, maximumTextCharacters, "text");
                    if (!string.IsNullOrEmpty(text))
                    {
                        events.Add(
                            new NormalizedRuntimeEvent(
                                AgentRuntimeEventKind.AssistantTextDelta,
                                text,
                                frameCorrelationId,
                                sourceType + "/content/text"));
                    }
                    else
                    {
                        events.Add(Unknown(sourceType + "/content/text", frameCorrelationId));
                    }

                    continue;
                }

                if (string.Equals(blockType, "tool_use", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(blockType, "server_tool_use", StringComparison.OrdinalIgnoreCase))
                {
                    events.Add(
                        new NormalizedRuntimeEvent(
                            AgentRuntimeEventKind.ToolStarted,
                            ResolveText(block, maximumTextCharacters, "name"),
                            ResolveCorrelation(block, frameCorrelationId, "id"),
                            sourceType + "/content/" + blockType));
                    continue;
                }

                if (string.Equals(blockType, "tool_result", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(blockType, "server_tool_result", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(blockType, "advisor_tool_result", StringComparison.OrdinalIgnoreCase))
                {
                    events.Add(
                        new NormalizedRuntimeEvent(
                            AgentRuntimeEventKind.ToolResult,
                            ResolveContentText(block, maximumTextCharacters),
                            ResolveCorrelation(block, frameCorrelationId, "tool_use_id", "id"),
                            sourceType + "/content/" + blockType));
                    continue;
                }

                events.Add(
                    Unknown(
                        sourceType + "/content/" + (string.IsNullOrWhiteSpace(blockType) ? "unknown" : blockType),
                        ResolveCorrelation(block, frameCorrelationId, "id", "tool_use_id")));
            }
        }
        else
        {
            var text = ResolveText(contentOwner, maximumTextCharacters, "text");
            if (!string.IsNullOrEmpty(text))
            {
                events.Add(
                    new NormalizedRuntimeEvent(
                        AgentRuntimeEventKind.AssistantTextDelta,
                        text,
                        frameCorrelationId,
                        sourceType));
            }
        }

        AddUsageIfPresent(contentOwner, sourceType, frameCorrelationId, events);
        if (message is not null && !ReferenceEquals(contentOwner, root))
        {
            AddUsageIfPresent(root, sourceType, frameCorrelationId, events);
        }
    }

    private static void NormalizeUserToolResults(
        JsonElement root,
        string sourceType,
        string? frameCorrelationId,
        int maximumTextCharacters,
        List<NormalizedRuntimeEvent> events)
    {
        var message = TryGetObject(root, "message");
        var contentOwner = message ?? root;
        if (TryGetArray(contentOwner, "content") is not { } content)
        {
            return;
        }

        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var blockType = TryGetString(block, "type")?.Trim();
            if (!string.Equals(blockType, "tool_result", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(blockType, "server_tool_result", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(blockType, "advisor_tool_result", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            events.Add(
                new NormalizedRuntimeEvent(
                    AgentRuntimeEventKind.ToolResult,
                    ResolveContentText(block, maximumTextCharacters),
                    ResolveCorrelation(block, frameCorrelationId, "tool_use_id", "id"),
                    sourceType + "/content/" + blockType));
        }
    }

    private static void NormalizeStreamEvent(
        JsonElement root,
        string sourceType,
        string? frameCorrelationId,
        int maximumTextCharacters,
        List<NormalizedRuntimeEvent> events)
    {
        var upstreamEvent = TryGetObject(root, "event");
        if (upstreamEvent is null)
        {
            return;
        }

        var eventType = TryGetString(upstreamEvent.Value, "type")?.Trim();
        var eventSource = sourceType + "/" + (string.IsNullOrWhiteSpace(eventType) ? "unknown" : eventType);
        if (string.Equals(eventType, "content_block_delta", StringComparison.OrdinalIgnoreCase))
        {
            var delta = TryGetObject(upstreamEvent.Value, "delta");
            if (delta is null)
            {
                events.Add(Unknown(eventSource, frameCorrelationId));
                return;
            }

            var deltaType = TryGetString(delta.Value, "type")?.Trim();
            if (string.Equals(deltaType, "text_delta", StringComparison.OrdinalIgnoreCase))
            {
                var text = ResolveText(delta.Value, maximumTextCharacters, "text");
                events.Add(
                    string.IsNullOrEmpty(text)
                        ? Unknown(eventSource + "/text_delta", frameCorrelationId)
                        : new NormalizedRuntimeEvent(
                            AgentRuntimeEventKind.AssistantTextDelta,
                            text,
                            frameCorrelationId,
                            eventSource + "/text_delta"));
                return;
            }

            if (string.Equals(deltaType, "input_json_delta", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(
                    new NormalizedRuntimeEvent(
                        AgentRuntimeEventKind.ToolProgress,
                        ResolveText(delta.Value, maximumTextCharacters, "partial_json"),
                        ResolveCorrelation(upstreamEvent.Value, frameCorrelationId, "id", "tool_use_id"),
                        eventSource + "/input_json_delta"));
                return;
            }

            events.Add(Unknown(eventSource + "/" + (deltaType ?? "unknown"), frameCorrelationId));
            return;
        }

        if (string.Equals(eventType, "content_block_start", StringComparison.OrdinalIgnoreCase))
        {
            var block = TryGetObject(upstreamEvent.Value, "content_block");
            if (block is null)
            {
                events.Add(Unknown(eventSource, frameCorrelationId));
                return;
            }

            var blockType = TryGetString(block.Value, "type")?.Trim();
            if (string.Equals(blockType, "tool_use", StringComparison.OrdinalIgnoreCase)
                || string.Equals(blockType, "server_tool_use", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(
                    new NormalizedRuntimeEvent(
                        AgentRuntimeEventKind.ToolStarted,
                        ResolveText(block.Value, maximumTextCharacters, "name"),
                        ResolveCorrelation(block.Value, frameCorrelationId, "id"),
                        eventSource + "/" + blockType));
                return;
            }

            if (string.Equals(blockType, "text", StringComparison.OrdinalIgnoreCase))
            {
                var text = ResolveText(block.Value, maximumTextCharacters, "text");
                if (!string.IsNullOrEmpty(text))
                {
                    events.Add(
                        new NormalizedRuntimeEvent(
                            AgentRuntimeEventKind.AssistantTextDelta,
                            text,
                            frameCorrelationId,
                            eventSource + "/text"));
                }
                else
                {
                    events.Add(Unknown(eventSource + "/text", frameCorrelationId));
                }

                return;
            }

            events.Add(Unknown(eventSource + "/" + (blockType ?? "unknown"), frameCorrelationId));
            return;
        }

        if (string.Equals(eventType, "message_delta", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetObject(upstreamEvent.Value, "usage") is not null)
            {
                events.Add(
                    new NormalizedRuntimeEvent(
                        AgentRuntimeEventKind.Usage,
                        null,
                        frameCorrelationId,
                        eventSource));
            }
            else
            {
                events.Add(Unknown(eventSource, frameCorrelationId));
            }

            return;
        }

        events.Add(Unknown(eventSource, frameCorrelationId));
    }

    private static void AddUsageIfPresent(
        JsonElement root,
        string sourceType,
        string? frameCorrelationId,
        List<NormalizedRuntimeEvent> events)
    {
        if (TryGetObject(root, "usage") is null)
        {
            return;
        }

        events.Add(
            new NormalizedRuntimeEvent(
                AgentRuntimeEventKind.Usage,
                null,
                frameCorrelationId,
                sourceType + "/usage"));
    }

    private static string? ResolveContentText(JsonElement block, int maximumTextCharacters)
    {
        if (!block.TryGetProperty("content", out var content))
        {
            return ResolveText(block, maximumTextCharacters, "result", "text");
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return SanitizeAndBoundText(content.GetString(), maximumTextCharacters);
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object
                && string.Equals(TryGetString(item, "type"), "text", StringComparison.OrdinalIgnoreCase))
            {
                var text = SanitizeAndBoundText(TryGetString(item, "text"), maximumTextCharacters);
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static string? ResolveText(
        JsonElement root,
        int maximumTextCharacters,
        params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = TryGetString(root, propertyName);
            if (!string.IsNullOrEmpty(value))
            {
                return SanitizeAndBoundText(value, maximumTextCharacters);
            }
        }

        return null;
    }

    private static string? SanitizeAndBoundText(string? value, int maximumTextCharacters)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var sanitized = SensitiveTextAssignment.Replace(value, "$1$2[REDACTED]");
        return sanitized.Length <= maximumTextCharacters
            ? sanitized
            : sanitized[..maximumTextCharacters];
    }

    private static string? ResolveCorrelation(
        JsonElement root,
        string? fallback,
        params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = TryGetString(root, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.IsNullOrWhiteSpace(fallback) ? null : fallback.Trim();
    }

    private static JsonElement? TryGetObject(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return value;
    }

    private static JsonElement? TryGetArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return value;
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static void EnsureUnknownFallback(
        string sourceType,
        string? frameCorrelationId,
        List<NormalizedRuntimeEvent> events)
    {
        if (events.Count == 0)
        {
            events.Add(Unknown(sourceType, frameCorrelationId));
        }
    }

    private static NormalizedRuntimeEvent Unknown(string sourceType, string? correlationId) =>
        new(AgentRuntimeEventKind.Unknown, null, correlationId, sourceType);

    private static bool IsSource(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    internal sealed record NormalizedRuntimeEvent(
        AgentRuntimeEventKind Kind,
        string? Text,
        string? CorrelationId,
        string SourceType);
}
