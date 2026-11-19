namespace Theseus.Core.Stopping;

/// <summary>
/// What the banner says when Close now did not close the session, by the reason it did not: a promise to try again
/// is made only where pressing again can help (close-step verify F3).
/// </summary>
public static class CloseNowText
{
    /// <summary>The notice for a Close now press that did not close the session.</summary>
    public static string NotClosed(string title, CloseVerdict verdict) => verdict == CloseVerdict.Retire
        ? $"{title} was not closed: its stop was handed to an earlier run of the session, so it has been retired."
        : $"{title} was not closed: it is not idle and quiet yet. Press Close now again once it is.";
}
