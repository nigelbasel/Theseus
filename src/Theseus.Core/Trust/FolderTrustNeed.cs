namespace Theseus.Core.Trust;

/// <summary>A folder Theseus starts sessions in that Claude Code will ask about, and the record that would trust it.</summary>
/// <param name="Folder">The folder as a card names it.</param>
/// <param name="Key">The <c>projects</c> key Claude Code looks up: the repository's main root, or the folder itself.</param>
public sealed record FolderTrustNeed(string Folder, string Key);
