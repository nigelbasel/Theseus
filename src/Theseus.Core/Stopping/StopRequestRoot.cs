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
}
