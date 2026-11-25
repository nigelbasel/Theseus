namespace Theseus.Core.Launching;

/// <summary>
/// How long a launcher keeps a failed session's tab open for its reason to be read. A held tab that nobody reads would
/// otherwise keep its launcher running for ever.
/// </summary>
public static class LauncherHold
{
    /// <summary>The longest a tab is held.</summary>
    public static readonly TimeSpan Limit = TimeSpan.FromMinutes(15);
}
