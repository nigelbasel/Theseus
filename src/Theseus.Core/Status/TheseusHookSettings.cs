using System.Text.Json;

namespace Theseus.Core.Status;

/// <summary>
/// The <c>--settings</c> a session Theseus launches is given: a PreToolUse hook on Theseus's own tools,
/// so a call made while Theseus is down is answered with why, not a bare connection error; and a
/// Notification hook on the prompts that stop a session, so the board can say what is asked (1.6). Per
/// session, because Theseus reads the user's Claude Code settings and never writes them. A card of an
/// unattended project also has the hook on the question tool and on Stop, told so by its argument (2.4 W2).
/// </summary>
public static class TheseusHookSettings
{
    /// <summary>
    /// The settings JSON, for the hook executable at <paramref name="hookPath"/>. Serialised without the
    /// board's camel-case naming, because Claude Code's event names are case-sensitive: "preToolUse"
    /// would be ignored without a word.
    /// </summary>
    /// <param name="hookPath">The hook executable.</param>
    /// <param name="unattended">Whether the card's project is unattended.</param>
    public static string Json(string hookPath, bool unattended = false)
    {
        var hook = new[] { new { type = "command", command = $"\"{hookPath}\"" } };
        // A Notification matcher is matched against the notification's type.
        var notification = new[] { new { matcher = string.Join('|', Questions.HookPrompt.Stopping), hooks = hook } };
        var status = new { matcher = $"mcp__{Mcp.TheseusMcp.ServerName}__.*", hooks = hook };

        if (!unattended)
        {
            return JsonSerializer.Serialize(new { hooks = new { PreToolUse = new[] { status }, Notification = notification } });
        }

        var parked = new[] { new { type = "command", command = $"\"{hookPath}\" {Questions.UnattendedHook.Argument}" } };

        return JsonSerializer.Serialize(new
        {
            hooks = new
            {
                PreToolUse = new[] { status, new { matcher = Questions.UnattendedHook.QuestionTool, hooks = parked } },
                Notification = notification,
                Stop = new[] { new { hooks = parked } },
            },
        });
    }
}
