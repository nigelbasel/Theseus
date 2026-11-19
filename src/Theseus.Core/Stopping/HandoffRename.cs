using Theseus.Core.Domain;

namespace Theseus.Core.Stopping;

/// <summary>
/// Renaming a card's handover file to match its title, offered in its menu: a card keeps the name it was created with
/// (<c>HANDOFF-git-22.md</c> for a card now titled Theseus) until the owner chooses this (handoff-names AC 5,
/// Nigel 2026-11-19). Only while no session runs, since a running one holds the old path.
/// </summary>
public static class HandoffRename
{
    /// <summary>The name the file would take: the title written as a key, in the same folder.</summary>
    public static string Target(string currentFile, string title) =>
        Path.Combine(Path.GetDirectoryName(currentFile) ?? string.Empty, $"HANDOFF-{WorkstreamKey.FromTitle(title)}.md");

    /// <summary>The new path to offer, or null when no rename should be offered.</summary>
    public static string? Offered(string currentFile, string title, bool isLive, bool fileExists, bool targetExists)
    {
        var target = Target(currentFile, title);

        return !isLive && fileExists && !targetExists
               && !string.Equals(Path.GetFileName(currentFile), Path.GetFileName(target), StringComparison.OrdinalIgnoreCase)
            ? target
            : null;
    }
}
