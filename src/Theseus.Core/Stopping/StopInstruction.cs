using System.Text;

namespace Theseus.Core.Stopping;

/// <summary>
/// Builds the instruction handed to the courier session that carries a stop request to the others.
///
/// Theseus cannot message a session itself, so it starts one short-lived session whose only job is
/// to pass the request on. Targets are named explicitly rather than described, so the courier can
/// neither address a session that was not asked for nor tell itself to stop.
/// </summary>
public static class StopInstruction
{
    /// <summary>The placeholder replaced with each session's own handoff path.</summary>
    public const string HandoffPlaceholder = "{handoff}";

    /// <summary>What a stop request says when nothing else is configured.</summary>
    public const string DefaultStopPrompt =
        "Please stop what you are doing now. Write a handoff to " + HandoffPlaceholder
        + " covering what you did, what is unfinished, and what someone resuming should do next. "
        + "Then exit.";

    /// <summary>Builds the courier's instruction, or null when there is nobody to stop.</summary>
    public static string? Compose(string? stopPrompt, IReadOnlyList<StopTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        var usable = targets
            .Where(target => !string.IsNullOrWhiteSpace(target.SessionName))
            .ToList();

        if (usable.Count == 0)
        {
            return null;
        }

        var body = string.IsNullOrWhiteSpace(stopPrompt) ? DefaultStopPrompt : stopPrompt.Trim();
        var instruction = new StringBuilder();

        instruction.AppendLine(
            "Use SendMessage to deliver one message to each session named below, addressing each by "
            + "its exact name. Send nothing to any other session, and do not carry out the work "
            + "yourself. When every message has been sent, reply with SENT and nothing else.");
        instruction.AppendLine();

        foreach (var target in usable)
        {
            instruction.AppendLine($"To \"{target.SessionName}\":");
            instruction.AppendLine(body.Replace(HandoffPlaceholder, target.HandoffPath, StringComparison.Ordinal));
            instruction.AppendLine();
        }

        return instruction.ToString().TrimEnd();
    }
}
