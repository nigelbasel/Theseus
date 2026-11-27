using System.Text.Json.Nodes;

namespace Theseus.Core.Launching;

/// <summary>
/// Adds Claude Code's <c>autoMemoryDirectory</c> to a session's <c>--settings</c>, so a card that starts in its workstream
/// folder keeps the memory of its records folder (objective 1.3f): Claude Code keys auto-memory by the start folder.
/// </summary>
public static class SessionMemorySettings
{
    /// <summary>The settings with the memory folder added; unchanged when there is no folder to add.</summary>
    public static string? With(string? settings, string? memoryDirectory)
    {
        if (string.IsNullOrWhiteSpace(memoryDirectory))
        {
            return settings;
        }

        var json = string.IsNullOrWhiteSpace(settings) ? new JsonObject() : JsonNode.Parse(settings) as JsonObject ?? new JsonObject();
        json["autoMemoryDirectory"] = memoryDirectory;

        return json.ToJsonString();
    }
}
