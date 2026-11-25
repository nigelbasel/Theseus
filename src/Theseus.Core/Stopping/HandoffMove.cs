namespace Theseus.Core.Stopping;

/// <summary>One card given a handover file of its own, away from a file it shared.</summary>
/// <param name="Key">The card that moves.</param>
/// <param name="Title">Its title.</param>
/// <param name="OwnerTitle">The title of the card the shared file is named after, which keeps it.</param>
/// <param name="SharedFile">The file it shared.</param>
/// <param name="OwnFile">Its own file from now on.</param>
/// <param name="PointerText">The line to write into <paramref name="OwnFile"/>, or null when that file already exists.</param>
public sealed record HandoffMove(string Key, string Title, string OwnerTitle, string SharedFile, string OwnFile, string? PointerText)
{
    /// <summary>What the status bar says, naming both cards and both files (handoff-names AC 4).</summary>
    public string Notice =>
        $"{Title} shared {SharedFile} with {OwnerTitle}, which it is named after; {Title}'s notes now go to {OwnFile}.";
}
