namespace Theseus.Core.Stopping;

/// <summary>
/// Where stop requests are kept.
///
/// Named here rather than passed in because two programs have to agree on it without either
/// configuring the other: the board writes requests, and a hook running inside somebody else's
/// session reads them, with nothing between them but this path.
/// </summary>
public static class StopRequestRoot
{
    /// <summary>The directory both the board and the hook use.</summary>
    public static string Default { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Theseus", "stop-requests");

    /// <summary>
    /// Where text for a running session's next turn waits, as a stop request does (1.1e AC 27): a session told its spec
    /// was accepted. Apart from stop requests, so nothing that ends or closes a session after a stop sees these.
    /// </summary>
    public static string Tells { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Theseus", "tell-requests");
}
