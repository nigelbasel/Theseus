using System.Text.Json.Nodes;

namespace Theseus.Core.Mcp;

/// <summary>Where the session time server listens, and how a launched session is told to reach it.</summary>
public static class TheseusMcp
{
    /// <summary>The port it listens on, on localhost only.</summary>
    public const int DefaultPort = 47813;

    /// <summary>The name sessions see its tools under: <c>mcp__theseus__start_work</c>.</summary>
    public const string ServerName = "theseus";

    /// <summary>Every tool the server offers.</summary>
    public static readonly IReadOnlyList<string> ToolNames =
        ["start_work", "stop_work", "time_for", "whoami", "locks", "lock_acquire", "lock_release"];

    /// <summary>
    /// The <c>--allowedTools</c> value that lets a launched session use exactly these tools without being
    /// asked each time (Nigel, 2026-09-17 for the time tools, 2026-10-06 for the lock tools). Named one by
    /// one rather than with a wildcard, so a tool added to the server later is not approved without anyone
    /// deciding it should be. None of them writes to Jira, and none can reorder, cancel or take another
    /// session's lock; those stay on the board.
    /// </summary>
    public static string AllowedTools => string.Join(",", ToolNames.Select(tool => $"mcp__{ServerName}__{tool}"));

    /// <summary>
    /// The endpoint for one session. The session is identified by its path rather than by anything it
    /// says, so a session cannot record work against a workstream that is not its own.
    /// </summary>
    public static string UrlFor(int port, string sessionId) =>
        $"http://localhost:{port}/mcp/{Uri.EscapeDataString(sessionId)}";

    /// <summary>The <c>--mcp-config</c> value that points one session at the server.</summary>
    public static string ConfigJson(int port, string sessionId) =>
        new JsonObject
        {
            ["mcpServers"] = new JsonObject
            {
                [ServerName] = new JsonObject
                {
                    ["type"] = "http",
                    ["url"] = UrlFor(port, sessionId),
                },
            },
        }.ToJsonString();
}
