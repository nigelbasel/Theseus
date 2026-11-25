using Theseus.Core.Domain;

namespace Theseus.Core.Stopping;

/// <summary>
/// Renaming a card's handover file to match its title, offered in its menu: a card keeps the name it was created with
/// until the owner chooses this (handoff-names AC 5 and 6). Never while a session holds the path, and never for a file
/// another card also uses.
/// </summary>
public static class HandoffRename
{
    /// <summary>The name the file would take: the title written as a key, in the same folder.</summary>
    public static string Target(string currentFile, string title) =>
        Path.Combine(Path.GetDirectoryName(currentFile) ?? string.Empty, $"HANDOFF-{WorkstreamKey.FromTitle(title)}.md");

    /// <summary>The new path to offer, or null when no rename should be offered.</summary>
    public static string? Offered(string currentFile, string title, bool isBusy, bool isShared, bool fileExists, bool targetExists)
    {
        var target = Target(currentFile, title);

        return !isBusy && !isShared && fileExists && !targetExists
               && !string.Equals(Path.GetFileName(currentFile), Path.GetFileName(target), StringComparison.OrdinalIgnoreCase)
            ? target
            : null;
    }

    /// <summary>Moves the file, refusing to replace one already at the new name.</summary>
    public static void Apply(string currentFile, string target) => File.Move(currentFile, target, overwrite: false);
}
