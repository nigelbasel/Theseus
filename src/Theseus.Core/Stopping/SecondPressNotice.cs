namespace Theseus.Core.Stopping;

/// <summary>
/// What the banner says when Stop is pressed on sessions whose last /exit did not land: each by name, in the words its
/// card uses, rather than a promise that it will close (close-step AC 10; verify F4).
/// </summary>
public static class SecondPressNotice
{
    /// <summary>Whether a session already stopping is stuck: its last /exit did not land.</summary>
    public static bool IsStuck(StopState? state) => state?.Stage == StopStage.ExitFailed;

    /// <summary>The notice.</summary>
    /// <param name="stuck">Each stuck session's title and state.</param>
    /// <param name="described">What the press did for every other session.</param>
    /// <param name="othersDescribed">Whether the press covered any session that was not stuck.</param>
    /// <param name="zone">The zone the times are shown in.</param>
    public static string For(
        IReadOnlyList<(string Title, StopState State)> stuck,
        string described,
        bool othersDescribed,
        TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(stuck);

        if (stuck.Count == 0)
        {
            return described;
        }

        var said = string.Join(" ", stuck.Select(session => $"{session.Title}: {StopStageText.For(session.State, zone)}"));

        return othersDescribed ? $"{said} {described}" : said;
    }
}
