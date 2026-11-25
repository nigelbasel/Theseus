using System.Text.Json;

namespace Theseus.Core.Status;

/// <summary>
/// The <c>--settings</c> a session Theseus launches is given: a PreToolUse hook on Theseus's own tools,
/// so a call made while Theseus is down is answered with why, not a bare connection error; and a
/// Notification hook on the prompts that stop a session, so the board can say what is asked (1.6). Per
/// session, because Theseus reads the user's Claude Code settings and never writes them.
/// </summary>
public static class TheseusHookSettings
{
    /// <summary>
    /// The settings JSON, for the hook executable at <paramref name="hookPath"/>. Serialised without the
    /// board's camel-case naming, because Claude Code's event names are case-sensitive: "preToolUse"
    /// would be ignored without a word.
    /// </summary>
    public static string Json(string hookPath)
    {
        var hook = new[] { new { type = "command", command = $"\"{hookPath}\"" } };

        return JsonSerializer.Serialize(new
        {
            hooks = new
            {
                PreToolUse = new[] { new { matcher = $"mcp__{Mcp.TheseusMcp.ServerName}__.*", hooks = hook } },
                // A Notification matcher is matched against the notification's type.
                Notification = new[] { new { matcher = string.Join('|', Questions.HookPrompt.Stopping), hooks = hook } },
            },
        });
    }
}
