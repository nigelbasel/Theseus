using System.Globalization;

namespace Theseus.Core.Stopping;

/// <summary>
/// A card's own handover file, given when it stops sharing one: named for its key, beside the shared file, and
/// starting with a line that says where its notes were, so nothing is lost (handoff-names AC 2, Nigel 2026-11-19).
/// </summary>
public static class HandoffPointer
{
    /// <summary>The card's own handover file.</summary>
    public static string OwnFile(string sharedFile, string key) =>
        Path.Combine(Path.GetDirectoryName(sharedFile) ?? string.Empty, $"HANDOFF-{key}.md");

    /// <summary>The line the new file starts with.</summary>
    public static string Text(string sharedFile, DateOnly date) =>
        $"Until {date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} this card shared its handover notes with another "
        + $"card, in {sharedFile}. Its notes are kept here from now on.";
}
