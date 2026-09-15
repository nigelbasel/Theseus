namespace Theseus.Core.Launching;

/// <summary>
/// Decides how a session is actually opened, given how it was asked to start.
///
/// Resuming reloads the conversation from its last compaction, and that is not small. Measured over
/// two weeks of this machine's transcripts on 2026-09-15, a fresh session's first reply came in at
/// 42k-71k tokens and a resumed one's at 124k-960k, most of them between 300k and 900k - and a
/// resumed context is carried again on every reply after. A fresh start trades all of that for
/// whatever the handoff says, which is cheap when the notes are good and a loss when they are not.
/// So it is a choice to offer on every start, not a rule to impose.
/// </summary>
public static class StartModeChoice
{
    /// <summary>How the session is launched.</summary>
    /// <param name="requested">How it was asked to start.</param>
    /// <param name="transcriptExists">Whether there is a conversation on disk to resume.</param>
    public static LaunchMode For(StartMode requested, bool transcriptExists) =>
        requested == StartMode.Resume && transcriptExists
            ? LaunchMode.Resume
            : LaunchMode.FirstRun;

    /// <summary>What a card's own start button says it will do.</summary>
    /// <param name="preferred">The board's default.</param>
    /// <param name="transcriptExists">Whether there is a conversation to resume at all.</param>
    /// <param name="resumeTooltip">What resuming would cost, as the card already describes it.</param>
    public static string Tooltip(StartMode preferred, bool transcriptExists, string resumeTooltip)
    {
        if (!transcriptExists)
        {
            return "Start a new session. There is no earlier conversation to resume.";
        }

        return preferred == StartMode.Fresh
            ? "Start a fresh session, told to read the handoff. Nothing from the last conversation is "
              + "reloaded, so it begins small. Use ▾ to resume that conversation instead."
            : $"{resumeTooltip} Use ▾ to start fresh from the handoff instead.";
    }

    /// <summary>What the menu's fresh-start entry says it will do.</summary>
    public const string FreshTooltip =
        "A new conversation, told to read the handoff. It knows only what the handoff says, and "
        + "starts without reloading anything the last conversation held.";
}
