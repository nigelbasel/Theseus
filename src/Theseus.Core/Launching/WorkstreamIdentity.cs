using Theseus.Core.Domain;

namespace Theseus.Core.Launching;

/// <summary>
/// Tells a session which workstream it is.
///
/// A session was given a standing preamble and a prompt and nothing else: not its own name, not
/// which notes on disk were its own, not that other workstreams keep theirs in the same places. It
/// had no way to know, so an ambient instruction like "read any handoff files and continue" was
/// answered with whatever lay nearest — and two sessions ended up revising one document, which they
/// only avoided ruining because one of them noticed the file moving and asked.
///
/// Scope cannot be inferred from a working directory several workstreams share. It has to be said.
///
/// The wording is editable in Settings. Empty there means the built-in text below, so a board that
/// never changed it picks up any later improvement to it rather than being frozen on an old copy.
/// </summary>
public static class WorkstreamIdentity
{
    /// <summary>Replaced with the workstream's title, or its key when it has none.</summary>
    public const string TitlePlaceholder = "{title}";

    /// <summary>Replaced with the workstream's key.</summary>
    public const string KeyPlaceholder = "{key}";

    /// <summary>
    /// Replaced with the session's plan file. A line naming it is left out when there is no plan file,
    /// because a session sent to edit a file nobody named would go looking for one.
    /// </summary>
    public const string PlanPlaceholder = "{plan}";

    /// <summary>The built-in text. <c>{handoff}</c> is the workstream's handover notes.</summary>
    public static string DefaultTemplate { get; } = string.Join(Environment.NewLine,
    [
        "You are the \"{title}\" workstream ({key}), one of several on this board.",
        "Your handover notes are {handoff}. Whenever you are asked to stop, wrap up, hand"
        + " over or leave things for the next session - however it is worded, and whether or not"
        + " the file is named - bring that document up to date and write it there, rather than"
        + " starting notes of your own somewhere else. Say what you did, what is unfinished, and"
        + " what someone resuming should do next. It is the only thing your next session is"
        + " given, so anything not in it is lost.",
        "Other workstreams keep their own notes beside yours and their work in the same"
        + " directories. Do not take up work belonging to another one: if what you find is"
        + " theirs, say so rather than doing it.",
        "Your card on the board is {plan}. If you raise a Jira issue, or start work on"
        + " one the board does not already show, add its key to the \"jiraKeys\" list in that"
        + " file - creating the list if it is not there - and leave the rest of the file"
        + " alone. That is the only way an issue raised mid-session reaches the board.",
    ]);

    /// <summary>The lines that open every session, before anything else it is told.</summary>
    /// <param name="planPath">
    /// The session's own plan file. Given so that a Jira issue raised during the session can be
    /// recorded somewhere the board reads: it is in neither the opening prompt, written before the
    /// issue existed, nor on the card, where nobody has typed it.
    /// </param>
    /// <param name="template">The wording from Settings. Empty means <see cref="DefaultTemplate"/>.</param>
    public static string For(Workstream workstream, string handoffPath, string? planPath = null, string? template = null)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        var title = string.IsNullOrWhiteSpace(workstream.Title) ? workstream.Key : workstream.Title.Trim();
        var wording = string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template;
        var hasPlan = !string.IsNullOrWhiteSpace(planPath);

        var lines = wording
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => hasPlan || !line.Contains(PlanPlaceholder, StringComparison.Ordinal))
            .Select(line => line
                .Replace(TitlePlaceholder, title, StringComparison.Ordinal)
                .Replace(KeyPlaceholder, workstream.Key, StringComparison.Ordinal)
                .Replace(SessionOpening.HandoffPlaceholder, handoffPath, StringComparison.Ordinal)
                .Replace(PlanPlaceholder, planPath ?? string.Empty, StringComparison.Ordinal));

        return string.Join(Environment.NewLine, lines).Trim();
    }
}
