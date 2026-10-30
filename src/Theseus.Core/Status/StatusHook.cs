using System.Text.Json;
using Theseus.Core.Persistence;
using Theseus.Core.Stopping;

namespace Theseus.Core.Status;

/// <summary>What the hook says about Theseus's own state, on the two events where it can say it.</summary>
public static class StatusHook
{
    /// <summary>
    /// On PreToolUse for one of Theseus's tools while Theseus is down, the reply that denies the call
    /// and says why; null otherwise, which lets the call go ahead.
    /// </summary>
    public static string? PreToolUseReply(string? toolName, TheseusStatus? status, Func<int, bool> isRunning, TimeZoneInfo zone)
    {
        if (toolName is null || !toolName.StartsWith($"mcp__{Mcp.TheseusMcp.ServerName}__", StringComparison.Ordinal)
            || TheseusStatusNotice.WhyNotRunning(status, isRunning, zone) is not { } why)
        {
            return null;
        }

        return JsonSerializer.Serialize(new
        {
            hookSpecificOutput = new
            {
                hookEventName = "PreToolUse",
                permissionDecision = "deny",
                permissionDecisionReason = TheseusStatusNotice.ToolRefusal(why),
            },
        }, TheseusJson.Compact);
    }

    /// <summary>
    /// On UserPromptSubmit, the reply that tells a session Theseus launched what has changed since it
    /// last looked, once; null when nothing has, or for a session Theseus did not launch.
    /// </summary>
    public static string? PromptReply(string sessionId, StatusSeenStore seen, TheseusStatus? status, Func<int, bool> isRunning, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(seen);

        var last = seen.Read(sessionId);

        if (last is null)
        {
            return null;
        }

        var now = TheseusStatusNotice.Marker(status, isRunning);

        if (TheseusStatusNotice.ChangeSince(last, status, isRunning, zone) is not { } notice)
        {
            return null;
        }

        seen.Write(sessionId, now);

        return StopHookReply.For("UserPromptSubmit", notice);
    }

    /// <summary>The tool a PreToolUse event is about, from the hook's input.</summary>
    public static string? ToolName(string input)
    {
        try
        {
            using var document = JsonDocument.Parse(input);

            return document.RootElement.TryGetProperty("tool_name", out var name) ? name.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
