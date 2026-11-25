using Theseus.Core.Stopping;

namespace Theseus.Core.Questions;

/// <summary>
/// What the hook does about prompts (1.6): Claude Code's Notification of a permission prompt or a dialog records what is
/// asked; the owner typing, or the turn ending, clears it. A subagent's prompt stops the same tab, so it counts too.
/// </summary>
public static class HookPrompt
{
    /// <summary>The Notification types that mean the session is stopped at something only the owner can answer.</summary>
    public static IReadOnlyList<string> Stopping { get; } = ["permission_prompt", "elicitation_dialog"];

    /// <summary>Acts on one hook event.</summary>
    public static void OnEvent(StopHookInput input, PermissionPromptStore store, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(store);

        if (input.SessionId is not { } sessionId)
        {
            return;
        }

        switch (input.HookEventName)
        {
            case "Notification" when input.NotificationType is { } type && Stopping.Contains(type) && OneLine(input.Message) is { } text:
                store.Write(new PermissionPrompt(sessionId, text, now));
                break;
            case "UserPromptSubmit" or "Stop":
                store.Clear(sessionId);
                break;
        }
    }

    private static string? OneLine(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var line = string.Join(' ', message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return line.Length <= ProseQuestion.MaxLength ? line : $"{line[..(ProseQuestion.MaxLength - 1)]}…";
    }
}
